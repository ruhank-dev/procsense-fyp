using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace ProcSense.EditorTools
{
    /// <summary>
    /// Read-only review tool for the PatientBay bed: dumps the bed-related hierarchy, bounds, colliders and triangle
    /// counts, and renders fixed review views under Logs/BedReview. It never saves a scene or changes an asset.
    /// Run in batch mode: -executeMethod ProcSense.EditorTools.HospitalBedReview.Run -quit (tag via -bedTag NAME).
    /// </summary>
    public static class HospitalBedReview
    {
        const string BayPath = "Assets/Scenes/PatientBay.unity";
        const int Width = 1600, Height = 900;

        static string Out => Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Logs", "BedReview"));

        static string Tag()
        {
            var args = System.Environment.GetCommandLineArgs();
            var i = System.Array.IndexOf(args, "-bedTag");
            return i >= 0 && i + 1 < args.Length ? args[i + 1] : "before";
        }

        public static void Run()
        {
            Directory.CreateDirectory(Out);
            EditorSceneManager.OpenScene(BayPath, OpenSceneMode.Single);
            var tag = Tag();
            Dump(Path.Combine(Out, tag + "-hierarchy.txt"));
            PatientClearance(Path.Combine(Out, tag + "-clearance.txt"));
            DumpWalls(Path.Combine(Out, tag + "-walls.txt"));
            ExportHeadProxy(Path.Combine(Out, "patient_head_proxy.obj"));
            TeleportProbe(Path.Combine(Out, tag + "-teleport.txt"));
            CaptureViews(tag);
        }

        /// <summary>
        /// Bakes the visible patient skin and garments, reports their tight world bounds and foot/head extents, then ray-tests
        /// every vertex that lies over the mattress against the mattress top: negative clearance means the body clips the bed.
        /// </summary>
        static void PatientClearance(string file)
        {
            var sb = new StringBuilder();
            var mattress = GameObject.Find("Soft mattress");
            var mf = mattress != null ? mattress.GetComponent<MeshFilter>() : null;
            if (mf == null) { File.WriteAllText(file, "no mattress"); return; }
            var holder = new GameObject("ClearanceProbe") { hideFlags = HideFlags.HideAndDontSave };
            var mc = holder.AddComponent<MeshCollider>();
            mc.sharedMesh = mf.sharedMesh;
            holder.transform.SetPositionAndRotation(mattress.transform.position, mattress.transform.rotation);
            holder.transform.localScale = mattress.transform.lossyScale;
            var mb = mf.sharedMesh.bounds;
            sb.AppendLine($"mattress mesh bounds (world) c={mattress.transform.TransformPoint(mb.center):F3} size={mb.size:F3} tris={mf.sharedMesh.triangles.Length / 3}");
            var grid = new System.Collections.Generic.Dictionary<(int, int), float>();
            var skins = Object.FindObjectsByType<SkinnedMeshRenderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)
                .Where(s => s.enabled && s.sharedMesh != null && s.sharedMesh.name.StartsWith("patient_")).ToArray();
            foreach (var s in skins)
            {
                var baked = new Mesh();
                s.BakeMesh(baked, true);
                var verts = baked.vertices;
                float minClear = float.MaxValue, minZ = float.MaxValue, maxZ = float.MinValue, minX = float.MaxValue, maxX = float.MinValue;
                int hits = 0, below = 0, below2 = 0;
                foreach (var v in verts)
                {
                    var w = s.transform.TransformPoint(v);
                    minZ = Mathf.Min(minZ, w.z); maxZ = Mathf.Max(maxZ, w.z);
                    minX = Mathf.Min(minX, w.x); maxX = Mathf.Max(maxX, w.x);
                    if (mc.Raycast(new Ray(new Vector3(w.x, 3f, w.z), Vector3.down), out var hit, 6f))
                    {
                        if (w.y > hit.point.y + 0.30f) continue; // a raised hand or head, not resting on the bed
                        hits++;
                        var clear = w.y - hit.point.y;
                        var key = (Mathf.RoundToInt(w.x / 0.02f), Mathf.RoundToInt(w.z / 0.02f));
                        grid[key] = grid.TryGetValue(key, out var old) ? Mathf.Min(old, clear) : clear;
                        minClear = Mathf.Min(minClear, clear);
                        if (clear < 0f) below++;
                        if (clear < -0.002f) below2++;
                    }
                }
                sb.AppendLine($"{s.name}: world z {minZ:F3}..{maxZ:F3} x {minX:F3}..{maxX:F3}  resting-verts={hits} minClearance={(hits > 0 ? minClear : 0):F4}m below-surface={below} below-by->2mm={below2}");
                Object.DestroyImmediate(baked);
            }
            File.WriteAllText(Path.Combine(Out, Path.GetFileNameWithoutExtension(file).Replace("-clearance", "") + "-footprint.csv"),
                string.Join("\n", grid.Select(kv => $"{kv.Key.Item1 * 0.02f:F2},{kv.Key.Item2 * 0.02f:F2},{kv.Value:F4}")));
            // Head-on-pillow clearance: same ray test against the pillow surface.
            var pillowRoot = GameObject.Find("patient_pillow");
            var pf = pillowRoot != null ? pillowRoot.GetComponentInChildren<MeshFilter>(true) : null;
            if (pf != null)
            {
                var pc = new GameObject("PillowProbe") { hideFlags = HideFlags.HideAndDontSave };
                var pcol = pc.AddComponent<MeshCollider>();
                pcol.sharedMesh = pf.sharedMesh;
                pc.transform.SetPositionAndRotation(pf.transform.position, pf.transform.rotation);
                pc.transform.localScale = pf.transform.lossyScale;
                var wb = pf.GetComponent<Renderer>().bounds;
                sb.AppendLine($"pillow mesh={pf.sharedMesh.name} tris={pf.sharedMesh.triangles.Length / 3} world bounds c={wb.center:F3} size={wb.size:F3} min={wb.min:F3} max={wb.max:F3}");
                foreach (var s in skins.Where(k => k.sharedMesh.name == "patient_body_idle"))
                {
                    var baked = new Mesh();
                    s.BakeMesh(baked, true);
                    float minClear = float.MaxValue; int hits = 0, below = 0;
                    foreach (var v in baked.vertices)
                    {
                        var w = s.transform.TransformPoint(v);
                        if (pcol.Raycast(new Ray(new Vector3(w.x, 3f, w.z), Vector3.down), out var hit, 6f) && w.y < hit.point.y + 0.10f)
                        {
                            hits++;
                            minClear = Mathf.Min(minClear, w.y - hit.point.y);
                            if (w.y < hit.point.y) below++;
                        }
                    }
                    sb.AppendLine($"head-over-pillow: verts-within-10cm={hits} minClearance={(hits > 0 ? minClear : 0):F4}m below-surface={below}");
                    Object.DestroyImmediate(baked);
                }
                Object.DestroyImmediate(pc);
            }
            Object.DestroyImmediate(holder);
            File.WriteAllText(file, sb.ToString());
            Debug.Log("BedReview clearance:\n" + sb);
        }

        static string Path_(Transform t)
        {
            var s = t.name;
            for (var p = t.parent; p != null; p = p.parent) s = p.name + "/" + s;
            return s;
        }

        static bool Related(Transform t) =>
            System.Text.RegularExpressions.Regex.IsMatch(t.name, "bed|mattress|pillow|blanket|cover|rail|patient|iv_|drip", System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        static void Dump(string file)
        {
            var sb = new StringBuilder();
            foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                         .Where(Related).OrderBy(Path_))
            {
                sb.AppendLine($"{Path_(t)}  active={t.gameObject.activeInHierarchy} pos={t.position:F3} euler={t.eulerAngles:F1} scale={t.lossyScale:F3}");
                foreach (var r in t.GetComponents<Renderer>())
                {
                    var mf = r.GetComponent<MeshFilter>();
                    var mesh = mf != null ? mf.sharedMesh : (r as SkinnedMeshRenderer)?.sharedMesh;
                    var tris = mesh != null ? (int)Enumerable.Range(0, mesh.subMeshCount).Sum(i => mesh.GetIndexCount(i) / 3) : 0;
                    sb.AppendLine($"    R {r.GetType().Name} enabled={r.enabled} mesh={(mesh ? mesh.name : "-")} tris={tris} boundsC={r.bounds.center:F3} boundsS={r.bounds.size:F3} " +
                                  $"static={GameObjectUtility.GetStaticEditorFlags(t.gameObject)} mats=[{string.Join(", ", r.sharedMaterials.Select(m => m ? m.name : "null"))}]");
                }
                foreach (var c in t.GetComponents<Collider>())
                    sb.AppendLine($"    C {c.GetType().Name} enabled={c.enabled} trigger={c.isTrigger} boundsC={c.bounds.center:F3} boundsS={c.bounds.size:F3}");
                foreach (var c in t.GetComponents<MonoBehaviour>().Where(m => m != null))
                    sb.AppendLine($"    S {c.GetType().Name}");
            }
            File.WriteAllText(file, sb.ToString());
            Debug.Log("BedReview hierarchy: " + file);
        }

        /// <summary>Lists every scene renderer near the bed head (z above 1.8 m) so the head-board bumpers can be checked against the wall.</summary>
        static void DumpWalls(string file)
        {
            var bed = GameObject.Find("hospital_bed");
            var sb = new StringBuilder();
            foreach (var r in Object.FindObjectsByType<Renderer>(FindObjectsInactive.Exclude))
            {
                var b = r.bounds;
                if (b.max.z < 1.80f || b.min.z > 2.40f || b.max.x < -0.80f || b.min.x > 0.80f || b.max.y < 0.30f || b.min.y > 1.60f) continue;
                if (bed != null && r.transform.IsChildOf(bed.transform)) { sb.AppendLine($"[BED] {Path_(r.transform)} min={b.min:F3} max={b.max:F3}"); continue; }
                sb.AppendLine($"{Path_(r.transform)} static={GameObjectUtility.GetStaticEditorFlags(r.gameObject)} min={b.min:F3} max={b.max:F3}");
            }
            File.WriteAllText(file, sb.ToString());
        }

        /// <summary>
        /// Writes the baked patient head, neck, shoulders and hair (world metres, Unity axes) as an OBJ so an offline pillow
        /// simulation can collide with the real surface. Only triangles above z 1.45 m are kept.
        /// </summary>
        static void ExportHeadProxy(string file)
        {
            var sb = new StringBuilder();
            var offset = 0;
            foreach (var s in Object.FindObjectsByType<SkinnedMeshRenderer>(FindObjectsInactive.Exclude))
            {
                var n = s.sharedMesh != null ? s.sharedMesh.name : "";
                if (!s.enabled || !(n == "patient_body_idle" || n == "patient_gown_idle" || n.EndsWith("short02"))) continue;
                var baked = new Mesh();
                s.BakeMesh(baked, true);
                var v = baked.vertices;
                var tris = baked.triangles;
                var map = new System.Collections.Generic.Dictionary<int, int>();
                var faces = new StringBuilder();
                for (var t = 0; t < tris.Length; t += 3)
                {
                    var w0 = s.transform.TransformPoint(v[tris[t]]);
                    var w1 = s.transform.TransformPoint(v[tris[t + 1]]);
                    var w2 = s.transform.TransformPoint(v[tris[t + 2]]);
                    if (w0.z < 1.45f || w1.z < 1.45f || w2.z < 1.45f) continue;
                    var ids = new int[3];
                    for (var k = 0; k < 3; k++)
                    {
                        var vi = tris[t + k];
                        if (!map.TryGetValue(vi, out var id))
                        {
                            var w = s.transform.TransformPoint(v[vi]);
                            sb.AppendLine($"v {w.x:F5} {w.y:F5} {w.z:F5}");
                            id = ++offset; map[vi] = id;
                        }
                        ids[k] = id;
                    }
                    faces.AppendLine($"f {ids[0]} {ids[1]} {ids[2]}");
                }
                sb.Append(faces);
                Object.DestroyImmediate(baked);
            }
            File.WriteAllText(file, sb.ToString());
        }

        /// <summary>What SafeTeleportProvider.Clear sees at the bedside pad: the floor ray and every solid collider inside the rig capsule.</summary>
        static void TeleportProbe(string file)
        {
            var sb = new StringBuilder();
            var pad = GameObject.Find("TELEPORT_Bedside");
            var p = pad != null ? pad.transform.position : new Vector3(0.95f, 0.02f, 0.5f);
            sb.AppendLine($"pad position {p:F3}");
            Physics.SyncTransforms();
            if (Physics.Raycast(p + Vector3.up * 0.2f, Vector3.down, out var hit, 0.4f, ~0, QueryTriggerInteraction.Ignore))
                sb.AppendLine($"floor ray hit {hit.collider.name} at y={hit.point.y:F3} (scene {hit.collider.gameObject.scene.name})");
            else sb.AppendLine("floor ray: NO HIT");
            foreach (var radius in new[] { 0.15f, 0.2f, 0.3f })
            {
                var hits = Physics.OverlapCapsule(p + Vector3.up * (radius + 0.04f), p + Vector3.up * (1.6f - radius + 0.04f), radius, ~0, QueryTriggerInteraction.Ignore);
                sb.AppendLine($"capsule r={radius}: {hits.Length} solid colliders: " + string.Join(" | ", hits.Select(h => Path_(h.transform) + " [" + h.GetType().Name + "]")));
            }
            File.WriteAllText(file, sb.ToString());
        }

        static Bounds BedBounds()
        {
            if (System.Environment.GetCommandLineArgs().Contains("-bedFixedCam"))
                return new Bounds(new Vector3(0f, 0.62f, 0.998f), new Vector3(1.328f, 1.24f, 1.975f));   // the original bed's bounds: identical cameras
            var bed = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .FirstOrDefault(t => t.name == "hospital_bed");
            var b = new Bounds(new Vector3(-0.4f, 0.6f, 1f), new Vector3(1f, 1f, 2f));
            if (bed == null) return b;
            var rs = bed.GetComponentsInChildren<Renderer>().Where(r => r.enabled).ToArray();
            if (rs.Length == 0) return b;
            b = rs[0].bounds;
            foreach (var r in rs) b.Encapsulate(r.bounds);
            return b;
        }

        static void CaptureViews(string tag)
        {
            var b = BedBounds();
            var c = b.center;
            var dist = Mathf.Max(b.size.x, b.size.z) * 1.35f;
            var views = new (string name, Vector3 pos, Vector3 look, float fov)[]
            {
                ("az000", c + Quaternion.Euler(0, 0, 0) * new Vector3(0, 1.1f, -dist), c, 55),
                ("az045", c + Quaternion.Euler(0, 45, 0) * new Vector3(0, 1.1f, -dist), c, 55),
                ("az090", c + Quaternion.Euler(0, 90, 0) * new Vector3(0, 1.1f, -dist), c, 55),
                ("az135", c + Quaternion.Euler(0, 135, 0) * new Vector3(0, 1.1f, -dist), c, 55),
                ("az180", c + Quaternion.Euler(0, 180, 0) * new Vector3(0, 1.1f, -dist), c, 55),
                ("az225", c + Quaternion.Euler(0, 225, 0) * new Vector3(0, 1.1f, -dist), c, 55),
                ("az270", c + Quaternion.Euler(0, 270, 0) * new Vector3(0, 1.1f, -dist), c, 55),
                ("az315", c + Quaternion.Euler(0, 315, 0) * new Vector3(0, 1.1f, -dist), c, 55),
                ("top", c + new Vector3(0.01f, 3.0f, 0), c, 60),
                ("player", new Vector3(0.95f, 1.65f, 0.5f), new Vector3(0f, 0.7f, 1.2f), 70),
                ("closePillow", new Vector3(0.95f, 1.35f, 1.55f), new Vector3(0f, 0.78f, 1.78f), 55),
                ("closeRail", new Vector3(0.55f, 1.25f, 1.0f), new Vector3(-0.55f, 0.72f, 1.58f), 60),
                ("closeFoot", new Vector3(1.05f, 0.95f, -0.75f), new Vector3(0.2f, 0.6f, 0.15f), 60),
                ("closeSheet", new Vector3(-0.2f, 1.1f, 0.15f), new Vector3(0.25f, 0.66f, 0.1f), 55),
                ("pillowSide", new Vector3(1.10f, 0.84f, 1.76f), new Vector3(0f, 0.80f, 1.76f), 38),
                ("pillowSideLow", new Vector3(1.10f, 0.74f, 1.62f), new Vector3(0f, 0.78f, 1.80f), 40),
                ("bumper", new Vector3(0.85f, 0.75f, 1.45f), new Vector3(0.40f, 0.55f, 1.98f), 50),
                ("headEndBack", new Vector3(0.0f, 1.55f, 1.15f), new Vector3(0f, 0.9f, 1.95f), 55),
            };
            foreach (var v in views) Render(Path.Combine(Out, $"{tag}-{v.name}.png"), v.pos, v.look, v.fov);
            // patient hidden: pure bed views (cover, pillow, frame) for material and construction review
            var hidden = Object.FindObjectsByType<SkinnedMeshRenderer>(FindObjectsInactive.Exclude).Where(r => r.enabled).ToArray();
            var props = Object.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Exclude)
                .Where(r => r.enabled && new[] { "wristband", "tablet_table", "sensor_equipment", "TELEPORT_", "TeleportTargets", "Physical bedside monitoring" }
                    .Any(k => Path_(r.transform).IndexOf(k, System.StringComparison.OrdinalIgnoreCase) >= 0)).ToArray();   // props hidden only in the bare inspection views
            foreach (var r in hidden) r.enabled = false;
            foreach (var r in props) r.enabled = false;
            var bare = new (string name, Vector3 pos, Vector3 look, float fov)[]
            {
                ("bare34", new Vector3(1.55f, 1.55f, -0.9f), new Vector3(0f, 0.65f, 1.0f), 50),
                ("barePillow", new Vector3(0.95f, 1.25f, 1.45f), new Vector3(0f, 0.76f, 1.75f), 45),
                ("bareCover", new Vector3(0.75f, 1.05f, 0.1f), new Vector3(0.05f, 0.68f, 0.55f), 55),
                ("lowFoot", new Vector3(1.05f, 0.42f, -1.0f), new Vector3(0.05f, 0.40f, 0.55f), 55),
                ("lowSide", new Vector3(1.55f, 0.42f, 0.35f), new Vector3(0f, 0.42f, 0.95f), 55),
            };
            foreach (var v in bare) Render(Path.Combine(Out, $"{tag}-{v.name}.png"), v.pos, v.look, v.fov);
            foreach (var r in hidden) r.enabled = true;
            foreach (var r in props) r.enabled = true;
            File.WriteAllText(Path.Combine(Out, tag + "-bounds.txt"), $"bed bounds center={b.center:F3} size={b.size:F3} min={b.min:F3} max={b.max:F3}");
        }

        public static void Render(string file, Vector3 pos, Vector3 look, float fov)
        {
            var go = new GameObject("BedReviewCamera") { hideFlags = HideFlags.HideAndDontSave };
            var cam = go.AddComponent<Camera>();
            cam.transform.position = pos;
            cam.transform.LookAt(look);
            cam.fieldOfView = fov;
            cam.nearClipPlane = 0.05f;
            cam.farClipPlane = 40f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.12f, 0.12f, 0.14f);
            // Hide the roof and far walls from this review camera only by pulling near clip is not possible; keep the real room.
            var rt = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB) { antiAliasing = 4 };
            cam.targetTexture = rt;
            cam.Render();
            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(Width, Height, TextureFormat.RGB24, false, false);
            tex.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
            tex.Apply();
            File.WriteAllBytes(file, tex.EncodeToPNG());
            RenderTexture.active = prev;
            cam.targetTexture = null;
            Object.DestroyImmediate(rt);
            Object.DestroyImmediate(tex);
            Object.DestroyImmediate(go);
        }
    }
}
