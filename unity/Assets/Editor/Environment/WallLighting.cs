using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace ProcSense.EditorTools
{
    /// <summary>
    /// 3 Nov 2026 (owner: oval wall lamps switched on, rays of sunlight through the windows): oval wall sconces with an emissive opal diffuser and a baked warm point light, spaced along every wall where space allows;
    /// and soft light shafts along the real sun direction behind each window and glass wall (additive, very low alpha, desktop only). The sun itself is the baked Mixed sun in golden.json; the shafts only make its
    /// beam visible and always follow its direction. Idempotent.
    /// </summary>
    public static class WallLighting
    {
        const string Models = "Assets/Models/Environment/RoomDressing", Prefabs = "Assets/Prefabs/Environment/RoomDressing", Mats = "Assets/Materials/Hospital/Decor", Tex = "Assets/Textures/Hospital/Decor";

        static void Folder(string p) { if (!AssetDatabase.IsValidFolder(p)) { var parent = Path.GetDirectoryName(p).Replace('\\', '/'); Folder(parent); AssetDatabase.CreateFolder(parent, Path.GetFileName(p)); } }

        static Material Find(string name)
        {
            foreach (var g in AssetDatabase.FindAssets(name + " t:Material")) { var p = AssetDatabase.GUIDToAssetPath(g); if (Path.GetFileNameWithoutExtension(p) == name) return AssetDatabase.LoadAssetAtPath<Material>(p); }
            return null;
        }

        static Material SconceMat()
        {
            Folder(Mats); const string path = Mats + "/Sconce diffuser.mat"; var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null) { m = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(m, path); }
            m.SetColor("_BaseColor", new Color(1f, .96f, .88f)); m.SetFloat("_Smoothness", .55f); m.SetFloat("_Metallic", 0f);
            m.EnableKeyword("_EMISSION"); m.SetColor("_EmissionColor", new Color(1f, .92f, .80f) * 2.0f); m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.BakedEmissive;
            EditorUtility.SetDirty(m); return m;
        }

        public static void ImportSconce()
        {
            var src = "Logs/RoomDressing/wall_sconce.fbx"; var fbx = Models + "/wall_sconce.fbx"; File.Copy(src, fbx, true); AssetDatabase.ImportAsset(fbx, ImportAssetOptions.ForceSynchronousImport);
            var mi = (ModelImporter)AssetImporter.GetAtPath(fbx); mi.globalScale = 1; mi.useFileScale = true; mi.importAnimation = false; mi.importCameras = false; mi.importLights = false; mi.generateSecondaryUV = true;
            mi.materialImportMode = ModelImporterMaterialImportMode.None; mi.SaveAndReimport();
            var wrapper = new GameObject("wall_sconce");
            try
            {
                var model = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(fbx)); model.transform.SetParent(wrapper.transform, false);
                var front = model.GetComponentsInChildren<Transform>().Single(t => t.name == "FRONT_MARKER"); if (model.transform.InverseTransformPoint(front.position).z > 0) model.transform.localRotation = Quaternion.Euler(0, 180, 0);
                foreach (var r in model.GetComponentsInChildren<MeshRenderer>())
                {
                    r.sharedMaterial = r.name.EndsWith("M_Diffuser") ? SconceMat() : Find("Brushed stainless hardware"); r.receiveGI = ReceiveGI.Lightmaps;
                    GameObjectUtility.SetStaticEditorFlags(r.gameObject, StaticEditorFlags.ContributeGI | StaticEditorFlags.BatchingStatic);
                }
                PrefabUtility.SaveAsPrefabAsset(wrapper, Prefabs + "/wall_sconce.prefab");
            }
            finally { Object.DestroyImmediate(wrapper); }
            AssetDatabase.SaveAssets(); Debug.Log("[WallLighting] sconce imported");
        }

        [System.Serializable] class Cfg { public SunC sun; }
        [System.Serializable] class SunC { public float pitch, yaw; }

        static Vector3 SunDirection()
        {
            var cfg = JsonUtility.FromJson<Cfg>(File.ReadAllText("Logs/Lighting/golden.json")); return Quaternion.Euler(cfg.sun.pitch, cfg.sun.yaw, 0f) * Vector3.forward;
        }

        public static void Apply()
        {
            ImportSconce();
            var sconce = AssetDatabase.LoadAssetAtPath<GameObject>(Prefabs + "/wall_sconce.prefab"); var dir = SunDirection(); var shaftMat = ShaftMat();
            foreach (var sceneName in new[] { "PatientBay", "Corridor", "OT" })
            {
                var scene = EditorSceneManager.OpenScene("Assets/Scenes/" + sceneName + ".unity", OpenSceneMode.Single);
                foreach (var n in new[] { "Wall lamps", "Sun shafts" }) { var o = scene.GetRootGameObjects().FirstOrDefault(g => g.name == n); if (o != null) Object.DestroyImmediate(o); }
                var lamps = new GameObject("Wall lamps"); SceneManager.MoveGameObjectToScene(lamps, scene);
                var obstacles = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Renderer>(false)).Where(r => r.enabled && r.gameObject.activeInHierarchy && !(r is ParticleSystemRenderer)
                    && !(r.name.StartsWith("Wall") || r.name.StartsWith("Floor") || r.name.StartsWith("Ceiling") || r.name.StartsWith("EQ_city") || r.name.StartsWith("City") || r.name.StartsWith("Skyline") || r.name.StartsWith("Lower") || r.name.StartsWith("Panel cap")
                    || r.name.StartsWith("Skirting") || r.name.StartsWith("Cap rail") || r.name.Contains("contact") || r.name.Contains("Contact"))).Select(r => r.bounds).ToList();
                int count = 0; float spacing = sceneName == "Corridor" ? 3.4f : 2.1f, y = sceneName == "Corridor" ? 2.35f : 2.0f;
                foreach (var w in WallDecor.WallsFor(sceneName))
                    for (float t = w.from + 1.1f; t < w.to - .6f; t += spacing)
                    {
                        var c = w.face + w.along * t + Vector3.up * y + w.normal * .05f; var size = new Vector3(Mathf.Abs(w.along.x) * .3f + Mathf.Abs(w.normal.x) * .14f, .5f, Mathf.Abs(w.along.z) * .3f + Mathf.Abs(w.normal.z) * .14f);
                        var bb = new Bounds(c, size); bb.Expand(.06f); if (obstacles.Any(o => o.Intersects(bb))) continue;
                        var go = (GameObject)PrefabUtility.InstantiatePrefab(sconce, lamps.transform); go.transform.SetPositionAndRotation(w.face + w.along * t + Vector3.up * y + w.normal * .004f, Quaternion.LookRotation(-w.normal));
                        var lg = new GameObject("Baked sconce light"); lg.transform.SetParent(lamps.transform, false); lg.transform.position = c + w.normal * .14f; var l = lg.AddComponent<Light>();
                        l.type = LightType.Point; l.lightmapBakeType = LightmapBakeType.Baked; l.range = 3.0f; l.intensity = .75f; l.color = new Color(1f, .93f, .84f); l.shadows = LightShadows.Soft;
                        obstacles.Add(bb); count++;
                    }
                var shafts = new GameObject("Sun shafts"); SceneManager.MoveGameObjectToScene(shafts, scene); shafts.AddComponent<ProcSense.VR.Views.DesktopOnly>(); int ribbons = 0;
                if (sceneName != "OT")
                    foreach (var pane in scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<MeshRenderer>(true)).Where(r => (r.name == "Window glass pane" || r.name == "Glass pane") && r.gameObject.activeInHierarchy).ToArray())
                    {
                        var b = pane.bounds; bool alongX = b.size.x > b.size.z; var along = alongX ? Vector3.right : Vector3.forward; var normalAxis = alongX ? Vector3.forward : Vector3.right;
                        if (Mathf.Abs(Vector3.Dot(dir, normalAxis)) < .05f) continue;
                        float length = Mathf.Max(b.size.x, b.size.z); int n = Mathf.Clamp(Mathf.RoundToInt(length / .9f), 2, 5);
                        for (int i = 0; i < n; i++)
                        {
                            float u = (i + .5f) / n - .5f; var start = new Vector3(b.center.x, b.max.y - .25f, b.center.z) + along * u * length * .9f; float tt = start.y / -dir.y; var end = start + dir * tt;
                            ribbons += Ribbon(shafts.transform, shaftMat, start, end, Mathf.Min(.5f, length / n * .6f));
                        }
                    }
                EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
                Debug.Log($"[WallLighting] {sceneName}: {count} sconces, {ribbons} shaft ribbons");
            }
            AssetDatabase.SaveAssets(); Debug.Log("[WallLighting] applied");
        }

        static int Ribbon(Transform parent, Material mat, Vector3 start, Vector3 end, float width)
        {
            var d = (end - start).normalized; var side = Vector3.Cross(Vector3.up, d).normalized; var tilt = Vector3.Cross(d, side).normalized; int made = 0;
            foreach (var axis in new[] { side, tilt })
            {
                var m = new Mesh { name = "shaft" }; var w = axis * width * .5f; var we = axis * width * .5f * 1.8f;
                m.vertices = new[] { start - w, start + w, end - we, end + we }; m.uv = new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 1), new Vector2(1, 1) }; m.triangles = new[] { 0, 2, 1, 1, 2, 3, 0, 1, 2, 1, 3, 2 }; m.RecalculateNormals(); m.RecalculateBounds();
                var go = new GameObject("Shaft"); go.transform.SetParent(parent, false); go.AddComponent<MeshFilter>().sharedMesh = m;
                var r = go.AddComponent<MeshRenderer>(); r.sharedMaterial = mat; r.shadowCastingMode = ShadowCastingMode.Off; r.receiveShadows = false; r.lightProbeUsage = LightProbeUsage.Off; r.reflectionProbeUsage = ReflectionProbeUsage.Off; made++;
            }
            return made;
        }

        static Material ShaftMat()
        {
            Folder(Tex); Folder(Mats);
            var tp = Tex + "/sunshaft.png"; var tex = new Texture2D(64, 256, TextureFormat.RGBA32, false);
            for (int y = 0; y < 256; y++) for (int x = 0; x < 64; x++)
            {
                float u = (x + .5f) / 64f * 2f - 1f, v = (y + .5f) / 256f; float across = Mathf.Exp(-u * u * 3.2f); float along = Mathf.SmoothStep(0, 1, v / .06f) * Mathf.Pow(1f - v, 1.6f);
                tex.SetPixel(x, y, new Color(1, 1, 1, across * along));
            }
            File.WriteAllBytes(tp, tex.EncodeToPNG()); AssetDatabase.ImportAsset(tp, ImportAssetOptions.ForceSynchronousImport);
            var ti = (TextureImporter)AssetImporter.GetAtPath(tp); ti.alphaIsTransparency = true; ti.wrapMode = TextureWrapMode.Clamp; ti.mipmapEnabled = true; ti.sRGBTexture = true; ti.SaveAndReimport();
            const string path = Mats + "/Sun shaft.mat"; var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null) { m = new Material(Shader.Find("Universal Render Pipeline/Unlit")); AssetDatabase.CreateAsset(m, path); }
            m.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(tp)); m.SetColor("_BaseColor", new Color(1f, .93f, .78f, .11f));
            m.SetFloat("_Surface", 1f); m.SetFloat("_Blend", 2f); m.SetFloat("_SrcBlend", 5f); m.SetFloat("_DstBlend", 1f); m.SetFloat("_SrcBlendAlpha", 0f); m.SetFloat("_DstBlendAlpha", 1f); m.SetFloat("_ZWrite", 0f); m.SetFloat("_Cull", 0f);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT"); m.SetOverrideTag("RenderType", "Transparent"); m.renderQueue = (int)RenderQueue.Transparent + 5; m.SetShaderPassEnabled("ShadowCaster", false);
            EditorUtility.SetDirty(m); return m;
        }
    }
}
