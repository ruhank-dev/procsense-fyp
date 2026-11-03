using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace ProcSense.EditorTools
{
    /// <summary>
    /// 3 Nov 2026 (owner's lighting reference): low-angle sunlight enters the corridor through the vision glass of the doors on the west wall and lands in patches on the floor and the opposite wall, with long frame and bench
    /// shadows. Two baked directionals (a 20 degree sun for the floor streaks and a grazing 7 degree one for the opposite-wall patches) share one yaw; the door leaves are not lightmap static, so static ShadowsOnly
    /// blockers that fill each leaf except its glass opening make the glass the only way in (the glass itself casts nothing, so the light passes through it). Rooms west of the corridor are open sky for the bake, with the
    /// patient bay's own walls blocking the doors beside it. Indoor lamps drop to neutral 4000-4300 K, sconces light up and down in cones, and the realtime fills are lowered so the sun is clearly the brightest source.
    /// Everything is baked: no runtime cost on Quest. Idempotent; run <see cref="GoldenHourLighting.Bake"/> afterwards.
    /// </summary>
    public static class CorridorSun
    {
        static readonly Color Sun = new Color(1f, .955f, .89f), Lamp = new Color(1f, .945f, .875f);

        static Light MakeSun(Transform parent, string name, float pitch, float yaw, float intensity, float angle)
        {
            var go = new GameObject(name, typeof(Light)); go.transform.SetParent(parent, false); go.transform.rotation = Quaternion.Euler(pitch, yaw, 0);
            var l = go.GetComponent<Light>(); l.type = LightType.Directional; l.lightmapBakeType = LightmapBakeType.Mixed; l.color = Sun; l.intensity = intensity; l.shadows = LightShadows.Soft; l.shadowAngle = angle; l.shadowStrength = .95f; l.shadowBias = .02f; l.shadowNormalBias = .4f;
            return l;
        }

        static void Blocker(Transform parent, Bounds b)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube); Object.DestroyImmediate(go.GetComponent<Collider>()); go.name = "Sun blocker"; go.transform.SetParent(parent, false);
            go.transform.position = b.center; go.transform.localScale = b.size;
            var r = go.GetComponent<MeshRenderer>(); r.shadowCastingMode = ShadowCastingMode.ShadowsOnly; r.receiveShadows = false; r.lightProbeUsage = LightProbeUsage.Off; r.reflectionProbeUsage = ReflectionProbeUsage.Off;
            GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.ContributeGI);
        }

        /// <summary>The patient bay and the theatre get the same wall lamps as the corridor: each baked point sconce light becomes an upward and a downward soft cone beside its oval fixture (no hot spot on the wall).</summary>
        public static void RoomSconces()
        {
            foreach (var name in new[] { "PatientBay", "OT" })
            {
                var scene = EditorSceneManager.OpenScene("Assets/Scenes/" + name + ".unity", OpenSceneMode.Single); int done = 0;
                if (name == "PatientBay") { RemovePatientBayLamp(scene); continue; }
                var fixtures = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Transform>(true)).Where(t => t.name == "wall_sconce" && PrefabUtility.IsOutermostPrefabInstanceRoot(t.gameObject)).ToArray();
                foreach (var l in Object.FindObjectsByType<Light>(FindObjectsInactive.Include).Where(l => l.gameObject.scene == scene && l.name == "Baked sconce light").ToArray())
                {
                    var fx = fixtures.OrderBy(f => (f.position - l.transform.position).sqrMagnitude).FirstOrDefault(); if (fx == null) continue;
                    var wallDir = l.transform.position - fx.position; wallDir.y = 0; wallDir.Normalize(); var p = fx.position + wallDir * .06f + Vector3.up * (l.transform.position.y - fx.position.y);
                    l.intensity = 0f; l.enabled = false; string id = Mathf.RoundToInt(fx.position.x * 100) + "_" + Mathf.RoundToInt(fx.position.z * 100);
                    for (int k = 0; k < 2; k++)
                    {
                        var dir = k == 0 ? Vector3.up : Vector3.down; var nm = (k == 0 ? "Sconce up " : "Sconce down ") + id;
                        var old = l.transform.parent.Find(nm); if (old != null) Object.DestroyImmediate(old.gameObject);
                        var go = new GameObject(nm, typeof(Light)); go.transform.SetParent(l.transform.parent, true); go.transform.position = p + dir * .12f; go.transform.rotation = Quaternion.LookRotation(dir + wallDir * .35f);
                        var s = go.GetComponent<Light>(); s.type = LightType.Spot; s.lightmapBakeType = LightmapBakeType.Baked; s.color = Lamp; s.intensity = 1.0f; s.range = 3.6f; s.spotAngle = 112f; s.innerSpotAngle = 30f; s.shadows = LightShadows.Soft;
                    }
                    done++;
                }
                EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); Debug.Log($"[CorridorSun] {name}: {done} sconces converted to up/down cones");
            }
        }

        /// <summary>Owner request: the patient room has no wall lamp. Removes the sconce fixture, its baked lights and the up/down cones, and adds a local exposure trim volume (the room read too bright).</summary>
        static void RemovePatientBayLamp(UnityEngine.SceneManagement.Scene scene)
        {
            int removed = 0;
            foreach (var t in scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Transform>(true)).Where(t => t != null && (t.name == "wall_sconce" || t.name == "Baked sconce light" || t.name.StartsWith("Sconce up ") || t.name.StartsWith("Sconce down "))).ToArray())
            { if (t != null) { Object.DestroyImmediate(t.gameObject); removed++; } }
            var lampsRoot = scene.GetRootGameObjects().FirstOrDefault(g => g.name == "Wall lamps"); if (lampsRoot != null && lampsRoot.transform.childCount == 0) Object.DestroyImmediate(lampsRoot);
            const string path = "Assets/Settings/HospitalLighting/PatientBay exposure trim.asset";
            var prof = AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
            if (prof == null) { prof = ScriptableObject.CreateInstance<VolumeProfile>(); AssetDatabase.CreateAsset(prof, path); }
            if (!prof.TryGet(out ColorAdjustments ca)) ca = prof.Add<ColorAdjustments>(true);
            ca.active = true; ca.postExposure.overrideState = true; ca.postExposure.value = -.55f; ca.contrast.overrideState = true; ca.contrast.value = 10f;
            EditorUtility.SetDirty(prof); AssetDatabase.SaveAssets();
            var old = scene.GetRootGameObjects().FirstOrDefault(g => g.name == "Patient room exposure trim"); if (old != null) Object.DestroyImmediate(old);
            var go = new GameObject("Patient room exposure trim"); UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(go, scene); go.transform.position = new Vector3(0f, 1.4f, 0f);
            var box = go.AddComponent<BoxCollider>(); box.isTrigger = true; box.size = new Vector3(4.9f, 2.8f, 3.9f);
            var vol = go.AddComponent<Volume>(); vol.isGlobal = false; vol.sharedProfile = prof; vol.priority = 10f; vol.blendDistance = .35f; vol.weight = 1f;
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
            Debug.Log($"[CorridorSun] PatientBay: {removed} lamp objects removed, local exposure trim volume added (-0.55 EV, contrast +10, on top of the shared -0.4 override)");
        }

        static Material FindMat(string name) => AssetDatabase.FindAssets(name + " t:Material").Select(AssetDatabase.GUIDToAssetPath).Select(AssetDatabase.LoadAssetAtPath<Material>).FirstOrDefault(m => m != null && m.name == name);

        /// <summary>
        /// The west wall is one single-sided quad. This rebuilds it as rectangles around real openings behind the vision glass of the doors that admit sun (same UV mapping, so the plaster does not move),
        /// so the glass looks into a real room and the sun passes through a real opening, not through a faked one.
        /// </summary>
        static void CutWall(MeshRenderer wall, System.Collections.Generic.List<Bounds> holes)
        {
            var tr = wall.transform; var mf = wall.GetComponent<MeshFilter>(); const float pad = .03f;
            float LZ(float wz) => (wz - tr.position.z) / tr.localScale.z;
            float LY(float wy) => (wy - tr.position.y) / tr.localScale.y;
            var verts = new System.Collections.Generic.List<Vector3>(); var uvs = new System.Collections.Generic.List<Vector2>(); var tris = new System.Collections.Generic.List<int>();
            void Rect(float z0, float z1, float y0, float y1)
            {
                if (z1 - z0 < .0005f || y1 - y0 < .0005f) return; int k = verts.Count;
                verts.AddRange(new[] { new Vector3(.5f, y0, z0), new Vector3(.5f, y1, z0), new Vector3(.5f, y1, z1), new Vector3(.5f, y0, z1) });
                uvs.AddRange(new[] { new Vector2(z0 + .5f, y0 + .5f), new Vector2(z0 + .5f, y1 + .5f), new Vector2(z1 + .5f, y1 + .5f), new Vector2(z1 + .5f, y0 + .5f) });
                tris.AddRange(new[] { k, k + 1, k + 2, k, k + 2, k + 3 });
            }
            float cursor = -.5f;
            foreach (var h in holes.OrderBy(b => b.center.z))
            {
                float z0 = LZ(h.min.z - pad), z1 = LZ(h.max.z + pad), y0 = LY(h.min.y - pad), y1 = LY(h.max.y + pad);
                Rect(cursor, z0, -.5f, .5f); Rect(z0, z1, -.5f, y0); Rect(z0, z1, y1, .5f); cursor = z1;
            }
            Rect(cursor, .5f, -.5f, .5f);
            string path = "Assets/Models/Environment/Corridor west wall " + Mathf.RoundToInt(tr.position.z * 10) + ".asset";
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path); if (mesh == null) { mesh = new Mesh { name = "Corridor west wall" }; AssetDatabase.CreateAsset(mesh, path); }
            mesh.Clear(); mesh.SetVertices(verts); mesh.SetUVs(0, uvs); mesh.SetTriangles(tris, 0); mesh.SetNormals(verts.Select(_ => Vector3.right).ToList()); mesh.RecalculateBounds();
            Unwrapping.GenerateSecondaryUVSet(mesh); EditorUtility.SetDirty(mesh); mf.sharedMesh = mesh;
        }

        static GameObject Cube(Transform parent, string name, Vector3 center, Vector3 size, Material mat, bool casts = true)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube); Object.DestroyImmediate(go.GetComponent<Collider>()); go.name = name; go.transform.SetParent(parent, false);
            go.transform.position = center; go.transform.localScale = size; var r = go.GetComponent<MeshRenderer>(); if (mat != null) r.sharedMaterial = mat;
            r.shadowCastingMode = casts ? ShadowCastingMode.On : ShadowCastingMode.Off; r.lightProbeUsage = LightProbeUsage.Off;
            GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.ContributeGI | StaticEditorFlags.ReflectionProbeStatic);
            return go;
        }

        /// <summary>
        /// One real room behind each door that admits sun: floor, ceiling, partitions and a west wall with a tall window (frame, two mullions and a transom, so their shadows fall inside the sunlit patch). The sun enters that window,
        /// crosses the room and leaves through the door glass; the room is lit by it and by a baked 4000 K ceiling light, so it reads brighter than the corridor when seen through the glass.
        /// </summary>
        static int BuildRooms(Transform rig, System.Collections.Generic.List<Bounds> lit, Vector3 dir)
        {
            var host = new GameObject("Sunlit rooms behind the doors"); host.transform.SetParent(rig, false);
            var wallM = RoomWallMat(); var floorM = FindMat("PatientBay grey vinyl"); var ceilM = FindMat("Corridor ceiling tile"); var glassM = FindMat("Door vision glass"); var trimM = FindMat("Satin ivory trim");
            float prevHi = 2.3f; int n = 0;
            foreach (var g in lit.OrderBy(b => b.center.z))
            {
                float shift = 2.6f * Mathf.Abs(dir.z / dir.x); float zc = g.center.z, zlo = zc - 1.5f, zhi = zc + 1.35f + shift;     // the sun drifts along the corridor while it crosses the room, so the room (and its window) extends that far toward the sun's origin float zmid = (zlo + zhi) / 2f, depth = zhi - zlo;
                float zmid = (zlo + zhi) / 2f, depth = zhi - zlo;
                const float xw = -.15f, xe = 2.45f, top = 3.05f, wy0 = 1.2f, wy1 = 2.85f; float wz0 = zlo + .2f, wz1 = zhi - .04f;
                var room = new GameObject("Room z" + Mathf.RoundToInt(zc * 10)); room.transform.SetParent(host.transform, false);
                float cx = (xw - .05f + xe) / 2f, wx = xe - (xw - .05f) + .1f;
                Cube(room.transform, "Floor", new Vector3(cx, -.05f, zmid), new Vector3(wx, .1f, depth + .2f), floorM);
                Cube(room.transform, "Ceiling", new Vector3(cx, top + .05f, zmid), new Vector3(wx, .1f, depth + .2f), ceilM);
                Cube(room.transform, "South partition", new Vector3(cx, top / 2f, zlo - .04f), new Vector3(wx, top, .08f), wallM, false);
                Cube(room.transform, "North partition", new Vector3(cx, top / 2f, zhi + .04f), new Vector3(wx, top, .08f), wallM, false);
                Cube(room.transform, "West wall below window", new Vector3(xw, wy0 / 2f, zmid), new Vector3(.1f, wy0, depth), wallM);
                Cube(room.transform, "West wall above window", new Vector3(xw, (wy1 + top) / 2f, zmid), new Vector3(.1f, top - wy1, depth), wallM);
                if (wz0 - zlo > .01f) Cube(room.transform, "West wall left of window", new Vector3(xw, (wy0 + wy1) / 2f, (zlo + wz0) / 2f), new Vector3(.1f, wy1 - wy0, wz0 - zlo), wallM);
                if (zhi - wz1 > .01f) Cube(room.transform, "West wall right of window", new Vector3(xw, (wy0 + wy1) / 2f, (wz1 + zhi) / 2f), new Vector3(.1f, wy1 - wy0, zhi - wz1), wallM);
                for (int k = 0; k < 4; k++)                                                                                         // frame: two mullions between three panes, plus the outer sides
                {
                    float z = Mathf.Lerp(wz0, wz1, k / 3f); Cube(room.transform, "Window mullion " + k, new Vector3(xw, (wy0 + wy1) / 2f, z), new Vector3(.07f, wy1 - wy0, .06f), trimM ?? wallM);
                }
                Cube(room.transform, "Window transom", new Vector3(xw, 2.05f, (wz0 + wz1) / 2f), new Vector3(.07f, .05f, wz1 - wz0), trimM ?? wallM);
                Cube(room.transform, "Window glass", new Vector3(xw, (wy0 + wy1) / 2f, (wz0 + wz1) / 2f), new Vector3(.01f, wy1 - wy0, wz1 - wz0), glassM, false);
                var lg = new GameObject("Room ceiling light"); lg.transform.SetParent(room.transform, false); lg.transform.position = new Vector3(1.1f, 2.85f, zmid);
                var l = lg.AddComponent<Light>(); l.type = LightType.Point; l.lightmapBakeType = LightmapBakeType.Baked; l.color = Lamp; l.intensity = .22f; l.range = 4.2f; l.shadows = LightShadows.Soft;
                n++;
            }
            return n;
        }

        /// <summary>Door vision glass becomes the same premultiplied Fresnel glass as the corridor glass wall: clear, glossy, with a faint cool tint, so the sunlit room shows through and the surroundings reflect at grazing angles.</summary>
        static Material RoomWallMat()
        {
            const string rp = "Assets/Materials/Environment/Sunlit room interior.mat"; var m = AssetDatabase.LoadAssetAtPath<Material>(rp);
            if (m == null) { var src = FindMat("Warm painted plaster"); m = new Material(src); AssetDatabase.CreateAsset(m, rp); }
            m.SetColor("_BaseColor", new Color(.36f, .35f, .33f)); EditorUtility.SetDirty(m); return m;
        }

        static void GlassLook()
        {
            var src = FindMat("Corridor glass wall"); var dst = FindMat("Door vision glass"); if (src == null || dst == null) return;
            dst.shader = src.shader; dst.CopyPropertiesFromMaterial(src); foreach (var k in src.shaderKeywords) dst.EnableKeyword(k);
            dst.SetColor("_BaseColor", new Color(.7f, .8f, .84f, .14f)); dst.SetFloat("_Smoothness", .96f); dst.renderQueue = src.renderQueue; EditorUtility.SetDirty(dst);
        }

        /// <summary>
        /// Soft sunbeams in the style of the patient bay shafts (same texture and material as WallLighting.ShaftMat: two feathered edges and a pale warm core). Each beam starts at a door glass and follows the real sun direction to the surface it lights
        /// (the floor or the opposite wall, the same place the baked patch lands), fading out toward that end. Desktop only through DesktopOnly.
        /// </summary>
        static Texture2D SoftDot()
        {
            const string tp = "Assets/Textures/Environment/soft_dot.png";
            var tex = new Texture2D(32, 32, TextureFormat.RGBA32, false);
            for (int y = 0; y < 32; y++) for (int x = 0; x < 32; x++) { float u = (x + .5f) / 16f - 1f, v = (y + .5f) / 16f - 1f; float a = Mathf.Pow(Mathf.Clamp01(1f - Mathf.Sqrt(u * u + v * v)), 1.6f); tex.SetPixel(x, y, new Color(1, 1, 1, a)); }
            System.IO.Directory.CreateDirectory("Assets/Textures/Environment"); System.IO.File.WriteAllBytes(tp, tex.EncodeToPNG()); AssetDatabase.ImportAsset(tp, ImportAssetOptions.ForceSynchronousImport);
            var ti = (TextureImporter)AssetImporter.GetAtPath(tp); ti.alphaIsTransparency = true; ti.wrapMode = TextureWrapMode.Clamp; ti.mipmapEnabled = false; ti.sRGBTexture = true; ti.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(tp);
        }

        /// <summary>
        /// Soft desktop sunbeams. Per window opening: several overlapping prisms of different widths, lengths, strengths and slightly different directions, all lying inside the window rectangle and the real sun direction, so the
        /// density noise in the shader (stretched along the sun) divides the air into irregular shafts. Plus a restrained dust-mote particle system inside the same sunlit volume (slow drift, turbulence, fade in and out).
        /// Everything sits under one DesktopOnly host: Quest never draws it. It starts switched off; <see cref="ShaftsOn"/> enables it.
        /// </summary>
        internal static void ShaftsFor(Transform rig, System.Collections.Generic.List<Bounds> lit, Vector3 dir, float maxX = 6.4f, string tag = "")
        {
            var host = new GameObject("Door sun shafts"); host.transform.SetParent(rig, false); host.AddComponent<ProcSense.VR.Views.DesktopOnly>();
            System.IO.Directory.CreateDirectory("Assets/Materials/Environment");
            string mp = "Assets/Materials/Environment/Sun shaft soft" + tag + ".mat", dp = "Assets/Materials/Environment/Sun dust mote" + tag + ".mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(mp); if (mat == null) { mat = new Material(Shader.Find("ProcSense/Sun Shaft")); AssetDatabase.CreateAsset(mat, mp); }
            mat.shader = Shader.Find("ProcSense/Sun Shaft"); mat.SetFloat("_Intensity", .035f); mat.SetFloat("_EdgePower", 3.2f); mat.SetFloat("_NoiseAmount", .85f); mat.SetVector("_ShaftDir", new Vector4(dir.x, dir.y, dir.z, 0)); EditorUtility.SetDirty(mat);
            var dust = AssetDatabase.LoadAssetAtPath<Material>(dp); if (dust == null) { dust = new Material(Shader.Find("ProcSense/Sun Dust Mote")); AssetDatabase.CreateAsset(dust, dp); }
            dust.shader = Shader.Find("ProcSense/Sun Dust Mote"); dust.SetTexture("_MainTex", SoftDot()); dust.SetFloat("_Gain", 1.3f); EditorUtility.SetDirty(dust);
            foreach (var b in lit)
            {
                var rng = new System.Random(Mathf.RoundToInt(b.center.z * 100)); float R(float lo, float hi) => lo + (float)rng.NextDouble() * (hi - lo);
                float x = b.center.x + .05f; var c = b.center; float tFull = Mathf.Min(c.y / -dir.y, (maxX - x) / dir.x);
                var verts = new System.Collections.Generic.List<Vector3>(); var uvs = new System.Collections.Generic.List<Vector2>(); var norms = new System.Collections.Generic.List<Vector3>(); var cols = new System.Collections.Generic.List<Color>(); var tris = new System.Collections.Generic.List<int>();
                int shafts = 6; var strong = new System.Collections.Generic.List<(Vector3 c, Vector3 d, float w, float h, float t)>();
                for (int k = 0; k < shafts; k++)
                {
                    float w = R(.14f, .38f) * b.size.z, z0 = Mathf.Lerp(b.min.z, b.max.z - w, R(0f, 1f)), z1 = z0 + w;
                    float h = R(.45f, 1f) * b.size.y, y0 = Mathf.Lerp(b.min.y, b.max.y - h, R(0f, 1f)), y1 = y0 + h;
                    var d = Quaternion.Euler(R(-1.2f, 1.2f), R(-1.6f, 1.6f), 0) * dir; float t = tFull * R(.7f, 1f); float strength = k < 2 ? R(.8f, 1f) : R(.25f, .65f); if (k < 2) strong.Add((new Vector3(x, (y0 + y1) / 2f, (z0 + z1) / 2f), d, w, h, t));
                    var p = new[] { new Vector3(x, y0, z0), new Vector3(x, y0, z1), new Vector3(x, y1, z1), new Vector3(x, y1, z0) };
                    for (int i = 0; i < 4; i++)
                    {
                        var a0 = p[i]; var a1 = p[(i + 1) % 4]; int q = verts.Count; var n = Vector3.Cross(a1 - a0, d).normalized;
                        verts.AddRange(new[] { a0, a1, a0 + d * t, a1 + d * t }); uvs.AddRange(new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 1), new Vector2(1, 1) }); norms.AddRange(new[] { n, n, n, n });
                        for (int v = 0; v < 4; v++) cols.Add(new Color(strength, 0, 0, 1)); tris.AddRange(new[] { q, q + 2, q + 1, q + 1, q + 2, q + 3 });
                    }
                }
                var m = new Mesh { name = "sun shafts z" + Mathf.RoundToInt(c.z * 10) }; m.SetVertices(verts); m.SetUVs(0, uvs); m.SetNormals(norms); m.SetColors(cols); m.SetTriangles(tris, 0); m.RecalculateBounds();
                var go = new GameObject("Shafts z" + Mathf.RoundToInt(c.z * 10)); go.transform.SetParent(host.transform, false); go.AddComponent<MeshFilter>().sharedMesh = m;
                var r = go.AddComponent<MeshRenderer>(); r.sharedMaterial = mat; r.shadowCastingMode = ShadowCastingMode.Off; r.receiveShadows = false; r.lightProbeUsage = LightProbeUsage.Off; r.reflectionProbeUsage = ReflectionProbeUsage.Off;

                int si = 0; foreach (var sp in strong)
                {
                var pgo = new GameObject("Dust z" + Mathf.RoundToInt(c.z * 10) + "_" + si); pgo.transform.SetParent(host.transform, false); pgo.transform.position = sp.c + sp.d * (sp.t * .45f); pgo.transform.rotation = Quaternion.LookRotation(sp.d);
                var ps = pgo.AddComponent<ParticleSystem>(); ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                var main = ps.main; main.loop = true; main.prewarm = true; main.simulationSpace = ParticleSystemSimulationSpace.World; main.maxParticles = 150; main.startLifetime = new ParticleSystem.MinMaxCurve(9f, 18f);
                main.startSpeed = 0f; main.startSize = new ParticleSystem.MinMaxCurve(.005f, .02f); main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, .96f, .88f, .1f), new Color(1f, .97f, .9f, .55f)); main.gravityModifier = 0f;
                var em = ps.emission; em.rateOverTime = 150f / 13f;
                var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Box; sh.scale = new Vector3(sp.w * .8f, sp.h * .8f, sp.t * .7f);
                var vel = ps.velocityOverLifetime; vel.enabled = true; vel.space = ParticleSystemSimulationSpace.World; vel.x = new ParticleSystem.MinMaxCurve(-.012f, .012f); vel.y = new ParticleSystem.MinMaxCurve(-.01f, .016f); vel.z = new ParticleSystem.MinMaxCurve(-.012f, .012f);
                var noise = ps.noise; noise.enabled = true; noise.strength = .045f; noise.frequency = .35f; noise.scrollSpeed = .06f; noise.damping = true; noise.quality = ParticleSystemNoiseQuality.Low;
                var col = ps.colorOverLifetime; col.enabled = true; var g = new Gradient(); g.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) }, new[] { new GradientAlphaKey(0, 0), new GradientAlphaKey(1, .22f), new GradientAlphaKey(1, .75f), new GradientAlphaKey(0, 1) }); col.color = g;
                var pr = pgo.GetComponent<ParticleSystemRenderer>(); pr.sharedMaterial = dust; pr.renderMode = ParticleSystemRenderMode.Billboard; pr.shadowCastingMode = ShadowCastingMode.Off; pr.receiveShadows = false; pr.sortingFudge = 2f;
                ps.Play(true);
                    si++;
                }
            }
            host.SetActive(false);                                                                                              // off until the surface lighting is validated; CorridorSun.ShaftsOn switches it on
        }

        /// <summary>Rebuilds only the sunshafts and dust (no bake needed: they are non-static and take no part in lighting) from the saved sun and glass, then switches them on.</summary>
        public static void RebuildShafts()
        {
            var sc = EditorSceneManager.OpenScene("Assets/Scenes/Corridor.unity", OpenSceneMode.Single);
            var rig = sc.GetRootGameObjects().First(g => g.name == "Corridor sun rig"); var old = rig.transform.Find("Door sun shafts"); if (old != null) Object.DestroyImmediate(old.gameObject);
            var sun = rig.GetComponentsInChildren<Light>().First(l => l.name == "Corridor sun"); var dir = sun.transform.forward;
            var lit = Object.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Include).Where(r => r.gameObject.scene == sc && r.name.StartsWith("EQ_door_leaf_") && r.name.EndsWith("M_DoorGlass") && r.bounds.center.x < 3f && r.bounds.center.z > 3f).Select(r => r.bounds).ToList();
            var interior = RoomWallMat(); var rooms = rig.transform.Find("Sunlit rooms behind the doors");                  // the sunlit rooms behind the glass get darker walls, so the door glass stops clipping
            if (rooms != null) { foreach (var r in rooms.GetComponentsInChildren<MeshRenderer>(true)) if (r.sharedMaterial != null && r.sharedMaterial.name == "Warm painted plaster") r.sharedMaterial = interior; foreach (var l in rooms.GetComponentsInChildren<Light>(true)) l.intensity = .22f; }
            ShaftsFor(rig.transform, lit, dir); rig.transform.Find("Door sun shafts").gameObject.SetActive(true);
            EditorSceneManager.MarkSceneDirty(sc); EditorSceneManager.SaveScene(sc); AssetDatabase.SaveAssets(); Debug.Log($"[CorridorSun] shafts rebuilt for {lit.Count} windows, dir {dir}");
        }

        /// <summary>Switches the soft sunshafts on (or off with -noshafts in the command line) and saves the corridor scene.</summary>
        public static void ShaftsOn()
        {
            var sc = EditorSceneManager.OpenScene("Assets/Scenes/Corridor.unity", OpenSceneMode.Single);
            var host = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include).FirstOrDefault(t => t.name == "Door sun shafts");
            if (host != null) host.gameObject.SetActive(!System.Environment.GetCommandLineArgs().Contains("-noshafts"));
            EditorSceneManager.MarkSceneDirty(sc); EditorSceneManager.SaveScene(sc); Debug.Log("[CorridorSun] shafts active = " + (host != null && host.gameObject.activeSelf));
        }

        public static void Apply() => Run(11f, 138f, 1.9f, 0f);

        public static void Run(float pitch, float yaw, float sunI, float lowI)
        {
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/Corridor.unity", OpenSceneMode.Single);
            var oldRig = scene.GetRootGameObjects().FirstOrDefault(g => g.name == "Corridor sun rig"); if (oldRig != null) Object.DestroyImmediate(oldRig);
            var rig = new GameObject("Corridor sun rig"); UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(rig, scene);
            MakeSun(rig.transform, "Corridor sun", pitch, yaw, sunI, .8f);

            // 1. door leaves on the west wall: fill everything but the glass opening with static shadow-only blockers
            var all = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<MeshRenderer>(true)).ToArray(); int doors = 0;
            foreach (var glass in all.Where(r => r.name.StartsWith("EQ_door_leaf_") && r.name.EndsWith("M_DoorGlass") && r.bounds.center.x < 3f))
            {
                var body = all.Where(r => r.transform.parent == glass.transform.parent && r.name.StartsWith("EQ_door_leaf_") && !r.name.EndsWith("M_DoorGlass") && !r.name.EndsWith("M_SteelBrushed") && Mathf.Abs(r.bounds.center.z - glass.bounds.center.z) < .6f)
                    .OrderBy(r => Mathf.Abs(r.bounds.center.z - glass.bounds.center.z)).FirstOrDefault();
                if (body == null) { Debug.LogWarning("[CorridorSun] no leaf body for " + glass.transform.parent.name); continue; }
                var s = body.bounds; var g = glass.bounds; float x = s.center.x, th = .08f;
                float zMin = s.min.z, zMax = s.max.z, yMin = 0f, yMax = 2.25f, gz0 = g.min.z, gz1 = g.max.z, gy0 = g.min.y, gy1 = g.max.y;
                void Box(float z0, float z1, float y0, float y1) { if (z1 - z0 > .005f && y1 - y0 > .005f) Blocker(rig.transform, new Bounds(new Vector3(x, (y0 + y1) / 2, (z0 + z1) / 2), new Vector3(th, y1 - y0, z1 - z0))); }
                Box(zMin - .03f, gz0, yMin, yMax); Box(gz1, zMax + .03f, yMin, yMax); Box(gz0, gz1, yMin, gy0); Box(gz0, gz1, gy1, yMax); doors++;
            }

            // The 2 cm oak wainscot panel on the west wall runs in front of every door up to 1.12 m. It sat exactly in the sun's low rays (the ones that reach the floor), so only a sliver of the glass lit the floor. A 2 cm panel casts no meaningful shadow, so it stops casting.
            foreach (var trim in all.Where(r => r.name == "Lower panel" && r.bounds.center.x < 3f)) trim.shadowCastingMode = ShadowCastingMode.Off;

            // 1b. the west wall gets real openings behind the glass of the doors that admit sun, and a real sunlit room behind each
            var lit = all.Where(r => r.name.StartsWith("EQ_door_leaf_") && r.name.EndsWith("M_DoorGlass") && r.bounds.center.x < 3f && r.bounds.center.z > 3f).Select(r => r.bounds).ToList();
            foreach (var wall in all.Where(r => r.name.StartsWith("Wall left doorway") && r.bounds.center.x < 3f && r.bounds.size.z > 5f))
            {
                wall.shadowCastingMode = ShadowCastingMode.TwoSided; var wb = wall.bounds;
                CutWall(wall, lit.Where(h => h.center.z > wb.min.z && h.center.z < wb.max.z).ToList());
            }
            // the diagnostic doors carry a full-leaf opaque "portal" cube behind the glass (TransportSetup): it blocked every sun ray and made the glass read as a flat grey panel. It is switched off for the doors that admit sun.
            foreach (var arch in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include).Where(t => t.gameObject.scene == scene && t.name.EndsWith(" architecture") && t.position.x < 3.2f && lit.Any(h => Mathf.Abs(h.center.z - t.position.z) < .7f)))
            { var bd = arch.Find("Opaque diagnostic threshold"); if (bd != null) bd.gameObject.SetActive(false); }
            int beams = BuildRooms(rig.transform, lit, Quaternion.Euler(pitch, yaw, 0) * Vector3.forward);
            GlassLook();
            ShaftsFor(rig.transform, lit, Quaternion.Euler(pitch, yaw, 0) * Vector3.forward);

            // 2. lamps: 4000-4300 K neutral panels, sconces as an upward and a downward cone, realtime fills lowered
            int fills = 0, panels = 0, sconces = 0;
            foreach (var l in Object.FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None).Where(l => l.gameObject.scene == scene && l.transform.root != rig.transform).ToArray())
            {
                if (l == null || l.name.StartsWith("Sconce ")) continue;                                                   // spots made on an earlier run are destroyed and rebuilt below
                if (l.name.StartsWith("Baked ceiling panel")) { l.color = Lamp; l.intensity = .38f; panels++; }
                else if (l.name.StartsWith("Ceiling fill")) { l.color = Lamp; l.intensity = .04f; fills++; }
                else if (l.name == "Room light") l.intensity = .1f;
                else if (l.name == "Soft clinical key") l.intensity = .08f;
                else if (l.name == "Baked sconce light")
                {
                    var wallDir = l.transform.position.x < 4.5f ? Vector3.right : Vector3.left; var p = l.transform.position - wallDir * .10f;
                    l.intensity = 0f; l.enabled = false;
                    string id = Mathf.RoundToInt(l.transform.position.x * 100) + "_" + Mathf.RoundToInt(l.transform.position.z * 100);
                    for (int k = 0; k < 2; k++)
                    {
                        var dir = k == 0 ? Vector3.up : Vector3.down; var nm = (k == 0 ? "Sconce up " : "Sconce down ") + id;
                        var old = l.transform.parent.Find(nm); if (old != null) Object.DestroyImmediate(old.gameObject);
                        var go = new GameObject(nm, typeof(Light)); go.transform.SetParent(l.transform.parent, true); go.transform.position = p + dir * .12f; go.transform.rotation = Quaternion.LookRotation(dir + wallDir * .35f);
                        var s = go.GetComponent<Light>(); s.type = LightType.Spot; s.lightmapBakeType = LightmapBakeType.Baked; s.color = Lamp; s.intensity = 1.6f; s.range = 3.6f; s.spotAngle = 112f; s.innerSpotAngle = 30f; s.shadows = LightShadows.Soft;
                    }
                    sconces++;
                }
            }
            var diffuser = AssetDatabase.FindAssets("PatientBay ceiling diffuser t:Material").Select(AssetDatabase.GUIDToAssetPath).Select(AssetDatabase.LoadAssetAtPath<Material>).FirstOrDefault();
            if (diffuser != null) { diffuser.SetColor("_EmissionColor", new Color(.2f, .195f, .19f)); diffuser.SetColor("_BaseColor", new Color(.86f, .86f, .85f)); EditorUtility.SetDirty(diffuser); }
            RenderSettings.ambientSkyColor = new Color(.06f, .075f, .10f); RenderSettings.ambientEquatorColor = new Color(.035f, .04f, .055f); RenderSettings.ambientGroundColor = new Color(.02f, .02f, .024f);
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);

            // 3. exposure: a small negative post exposure on the default volume profile
            var prof = AssetDatabase.LoadAssetAtPath<VolumeProfile>("Assets/DefaultVolumeProfile.asset");
            if (prof != null && prof.TryGet(out ColorAdjustments ca)) { ca.active = true; ca.postExposure.overrideState = true; ca.postExposure.value = -.8f; ca.contrast.overrideState = true; ca.contrast.value = 12f; EditorUtility.SetDirty(prof); AssetDatabase.SaveAssets(); }
            if (prof != null && prof.TryGet(out Bloom bl)) { bl.active = true; bl.threshold.overrideState = true; bl.threshold.value = 1.05f; bl.intensity.overrideState = true; bl.intensity.value = .12f; EditorUtility.SetDirty(prof); AssetDatabase.SaveAssets(); }
            Debug.Log($"[CorridorSun] sun {pitch}/{yaw} I={sunI}, {doors} door openings, {beams} sunlit rooms, {panels} panels, {fills} fills, {sconces} sconces; exposure -0.6, contrast +12, bloom 0.12 ({(prof ? "profile ok" : "NO PROFILE")})");
        }
    }
}
