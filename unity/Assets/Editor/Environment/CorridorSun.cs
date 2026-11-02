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
            var l = go.GetComponent<Light>(); l.type = LightType.Directional; l.lightmapBakeType = LightmapBakeType.Baked; l.color = Sun; l.intensity = intensity; l.shadows = LightShadows.Soft; l.shadowAngle = angle;
            return l;
        }

        static void Blocker(Transform parent, Bounds b)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube); Object.DestroyImmediate(go.GetComponent<Collider>()); go.name = "Sun blocker"; go.transform.SetParent(parent, false);
            go.transform.position = b.center; go.transform.localScale = b.size;
            var r = go.GetComponent<MeshRenderer>(); r.shadowCastingMode = ShadowCastingMode.ShadowsOnly; r.receiveShadows = false; r.lightProbeUsage = LightProbeUsage.Off; r.reflectionProbeUsage = ReflectionProbeUsage.Off;
            GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.ContributeGI);
        }

        /// <summary>Lists the static renderers whose bounds sit between each west-wall door glass and the sky along the sun direction (all open scenes), to find what blocks the sun.</summary>
        public static void Diagnose()
        {
            var corridor = EditorSceneManager.OpenScene("Assets/Scenes/Corridor.unity", OpenSceneMode.Single);
            foreach (var n in new[] { "PatientBay", "OT" }) EditorSceneManager.OpenScene("Assets/Scenes/" + n + ".unity", OpenSceneMode.Additive);
            var rends = Object.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None).Where(r => GameObjectUtility.GetStaticEditorFlags(r.gameObject).HasFlag(StaticEditorFlags.ContributeGI) && r.shadowCastingMode != ShadowCastingMode.Off).ToArray();
            foreach (var pitch in new[] { 20f, 7f })
            {
                var dir = -(Quaternion.Euler(pitch, 108f, 0) * Vector3.forward);
                foreach (var glass in rends.Concat(Object.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)).Where(r => r.name.EndsWith("M_DoorGlass") && r.bounds.center.x < 3f).Distinct())
                {
                    var o = glass.bounds.center; var ray = new Ray(o, dir); var hits = rends.Where(r => !r.name.Contains("door") && !r.name.StartsWith("Sun blocker") && r.bounds.IntersectRay(ray, out float d) && d > .15f && d < 80f).Select(r => { r.bounds.IntersectRay(ray, out float d); return (r, d); }).OrderBy(t => t.d).Take(4);
                    Debug.Log($"[CorridorSun] pitch {pitch} glass z={o.z:F1} y={o.y:F1}: " + string.Join("; ", hits.Select(h => $"{h.r.name}<{h.r.transform.root.name}>@{h.d:F1}")));
                }
            }
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
                        var s = go.GetComponent<Light>(); s.type = LightType.Spot; s.lightmapBakeType = LightmapBakeType.Baked; s.color = Lamp; s.intensity = 1.6f; s.range = 3.6f; s.spotAngle = 112f; s.innerSpotAngle = 30f; s.shadows = LightShadows.Soft;
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

        static Material BeamMaterial(string matName, float alpha)
        {
            const string tp = "Assets/Textures/Environment/doorbeam.png"; string mp = "Assets/Materials/Environment/" + matName + ".mat";
            var tex = new Texture2D(32, 128, TextureFormat.RGBA32, false);
            for (int y = 0; y < 128; y++) for (int x = 0; x < 32; x++)
            {
                float u = (x + .5f) / 32f * 2f - 1f, v = (y + .5f) / 128f; float across = Mathf.SmoothStep(0, 1, 1f - Mathf.Abs(u) * Mathf.Abs(u)); float along = Mathf.SmoothStep(0, 1, v / .08f) * Mathf.Lerp(1f, .55f, v);
                tex.SetPixel(x, y, new Color(1, 1, 1, across * along));
            }
            System.IO.Directory.CreateDirectory("Assets/Textures/Environment"); System.IO.File.WriteAllBytes(tp, tex.EncodeToPNG()); AssetDatabase.ImportAsset(tp, ImportAssetOptions.ForceSynchronousImport);
            var ti = (TextureImporter)AssetImporter.GetAtPath(tp); ti.alphaIsTransparency = true; ti.wrapMode = TextureWrapMode.Clamp; ti.mipmapEnabled = true; ti.sRGBTexture = true; ti.SaveAndReimport();
            var m = AssetDatabase.LoadAssetAtPath<Material>(mp);
            if (m == null) { System.IO.Directory.CreateDirectory("Assets/Materials/Environment"); m = new Material(Shader.Find("Universal Render Pipeline/Unlit")); AssetDatabase.CreateAsset(m, mp); }
            m.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(tp)); m.SetColor("_BaseColor", new Color(1f, .95f, .82f, alpha));
            m.SetFloat("_Surface", 1f); m.SetFloat("_Blend", 2f); m.SetFloat("_SrcBlend", 5f); m.SetFloat("_DstBlend", 1f); m.SetFloat("_SrcBlendAlpha", 0f); m.SetFloat("_DstBlendAlpha", 1f); m.SetFloat("_ZWrite", 0f); m.SetFloat("_Cull", 0f);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT"); m.SetOverrideTag("RenderType", "Transparent"); m.renderQueue = (int)RenderQueue.Transparent + 6; m.SetShaderPassEnabled("ShadowCaster", false);
            EditorUtility.SetDirty(m); return m;
        }

        /// <summary>One visible light beam per unobstructed west door glass: the pane rectangle swept along the sun direction until it meets the floor or the east wall (desktop only, additive, no shadows).</summary>
        static int Beams(Transform rig, MeshRenderer[] glasses, Vector3 dir, string hostName, string matName, float alpha)
        {
            var host = new GameObject(hostName); host.transform.SetParent(rig, false); host.AddComponent<ProcSense.VR.Views.DesktopOnly>(); var mat = BeamMaterial(matName, alpha); int n = 0;
            foreach (var g in glasses.Where(r => r.bounds.center.x < 3f && r.bounds.center.z > 3f))
            {
                var b = g.bounds; float x = b.center.x + .05f; var c = new Vector3(x, b.center.y, b.center.z);
                float tFloor = c.y / -dir.y, tWall = (6.42f - x) / dir.x, t = Mathf.Min(tFloor, tWall);
                var p = new[] { new Vector3(x, b.min.y, b.min.z), new Vector3(x, b.min.y, b.max.z), new Vector3(x, b.max.y, b.max.z), new Vector3(x, b.max.y, b.min.z) };
                var m = new Mesh { name = "door beam" }; var verts = new System.Collections.Generic.List<Vector3>(); var uvs = new System.Collections.Generic.List<Vector2>(); var tris = new System.Collections.Generic.List<int>();
                for (int i = 0; i < 4; i++)
                {
                    var a = p[i]; var bb = p[(i + 1) % 4]; int k = verts.Count;
                    verts.AddRange(new[] { a, bb, a + dir * t, bb + dir * t }); uvs.AddRange(new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 1), new Vector2(1, 1) });
                    tris.AddRange(new[] { k, k + 2, k + 1, k + 1, k + 2, k + 3, k, k + 1, k + 2, k + 1, k + 3, k + 2 });
                }
                m.SetVertices(verts); m.SetUVs(0, uvs); m.SetTriangles(tris, 0); m.RecalculateNormals(); m.RecalculateBounds();
                var go = new GameObject(hostName + " z" + Mathf.RoundToInt(b.center.z * 10)); go.transform.SetParent(host.transform, false); go.AddComponent<MeshFilter>().sharedMesh = m;
                var r = go.AddComponent<MeshRenderer>(); r.sharedMaterial = mat; r.shadowCastingMode = ShadowCastingMode.Off; r.receiveShadows = false; r.lightProbeUsage = LightProbeUsage.Off; r.reflectionProbeUsage = ReflectionProbeUsage.Off; n++;
            }
            return n;
        }

        public static void Apply() => Run(14f, 110f, 5.2f, 2.4f);

        public static void Run(float pitch, float yaw, float sunI, float lowI)
        {
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/Corridor.unity", OpenSceneMode.Single);
            var oldRig = scene.GetRootGameObjects().FirstOrDefault(g => g.name == "Corridor sun rig"); if (oldRig != null) Object.DestroyImmediate(oldRig);
            var rig = new GameObject("Corridor sun rig"); UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(rig, scene);
            MakeSun(rig.transform, "Corridor sun", pitch, yaw, sunI, 1.2f); MakeSun(rig.transform, "Corridor low sun", 6f, yaw + 4f, lowI, 1.0f);

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

            var doorGlass = all.Where(r => r.name.StartsWith("EQ_door_leaf_") && r.name.EndsWith("M_DoorGlass")).ToArray();
            int beams = Beams(rig.transform, doorGlass, Quaternion.Euler(pitch, yaw, 0) * Vector3.forward, "Door sun beams", "Door sun beam", .045f)
                      + Beams(rig.transform, doorGlass, Quaternion.Euler(6f, yaw + 4f, 0) * Vector3.forward, "Door low sun beams", "Door low sun beam", .03f);

            // 1b. the west wall is one solid slab with the diagnostic doors mounted proud of it: it stops casting in the bake and shadow-only blockers rebuild it with a hole behind each glass pane
            foreach (var wall in all.Where(r => r.name.StartsWith("Wall left doorway") && r.bounds.center.x < 3f))
            {
                wall.shadowCastingMode = ShadowCastingMode.Off; var wb = wall.bounds;
                var holes = all.Where(r => r.name.StartsWith("EQ_door_leaf_") && r.name.EndsWith("M_DoorGlass") && r.bounds.center.x < 3f && r.bounds.center.z > wb.min.z && r.bounds.center.z < wb.max.z).OrderBy(r => r.bounds.center.z).Select(r => r.bounds).ToList();
                float cursor = wb.min.z; const float pad = .04f;
                void WBox(float z0, float z1, float y0, float y1) { if (z1 - z0 > .005f && y1 - y0 > .005f) Blocker(rig.transform, new Bounds(new Vector3(wb.center.x, (y0 + y1) / 2, (z0 + z1) / 2), new Vector3(wb.size.x, y1 - y0, z1 - z0))); }
                foreach (var h in holes)
                {
                    WBox(cursor, h.min.z - pad, wb.min.y, wb.max.y); WBox(h.min.z - pad, h.max.z + pad, wb.min.y, h.min.y - pad); WBox(h.min.z - pad, h.max.z + pad, h.max.y + pad, wb.max.y); cursor = h.max.z + pad;
                }
                WBox(cursor, wb.max.z, wb.min.y, wb.max.y);
            }

            // 2. lamps: 4000-4300 K neutral panels, sconces as an upward and a downward cone, realtime fills lowered
            int fills = 0, panels = 0, sconces = 0;
            foreach (var l in Object.FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None).Where(l => l.gameObject.scene == scene && l.transform.root != rig.transform).ToArray())
            {
                if (l == null || l.name.StartsWith("Sconce ")) continue;                                                   // spots made on an earlier run are destroyed and rebuilt below
                if (l.name.StartsWith("Baked ceiling panel")) { l.color = Lamp; l.intensity = .85f; panels++; }
                else if (l.name.StartsWith("Ceiling fill")) { l.color = Lamp; l.intensity = .1f; fills++; }
                else if (l.name == "Room light") l.intensity = .22f;
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
            if (diffuser != null) { diffuser.SetColor("_EmissionColor", new Color(.30f, .29f, .28f)); diffuser.SetColor("_BaseColor", new Color(.86f, .86f, .85f)); EditorUtility.SetDirty(diffuser); }
            RenderSettings.ambientSkyColor = new Color(.21f, .24f, .29f); RenderSettings.ambientEquatorColor = new Color(.13f, .145f, .17f); RenderSettings.ambientGroundColor = new Color(.07f, .068f, .06f);
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);

            // 3. exposure: a small negative post exposure on the default volume profile
            var prof = AssetDatabase.LoadAssetAtPath<VolumeProfile>("Assets/DefaultVolumeProfile.asset");
            if (prof != null && prof.TryGet(out ColorAdjustments ca)) { ca.active = true; ca.postExposure.overrideState = true; ca.postExposure.value = -.4f; EditorUtility.SetDirty(prof); AssetDatabase.SaveAssets(); }
            Debug.Log($"[CorridorSun] sun {pitch}/{yaw} I={sunI}, {doors} door openings, {beams} beams, {panels} panels, {fills} fills, {sconces} sconces; exposure -0.4 ({(prof ? "profile ok" : "NO PROFILE")})");
        }
    }
}
