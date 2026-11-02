using System.Collections.Generic;
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
    /// 3 Nov 2026 (owner: realistic perforated stainless waiting benches in groups, stretchers, side tables, fuller corridor): imports the Blender models from Tools/Environment/build_waiting_furniture.py as prefabs,
    /// builds the perforated-steel material (alpha-cut holes, polished rims), removes the old teal corridor benches and places benches in facing groups along both walls with side tables and a parked stretcher.
    /// Placement tests every candidate against all renderers (doors get a 0.9 m clear zone) and keeps a 2.2 m walking lane. Runtime geometry; needs the final bake for lightmaps. Idempotent.
    /// </summary>
    public static class WaitingFurniture
    {
        const string Models = "Assets/Models/Environment/RoomDressing", Prefabs = "Assets/Prefabs/Environment/RoomDressing", Root = "Waiting furniture";
        static readonly string[] Ids = { "perforated_bench", "patient_stretcher", "round_side_table" };

        static readonly Dictionary<string, string> Map = new Dictionary<string, string> {
            ["M_Ivory"] = "Clean ivory housing", ["M_PlasticWhite"] = "Satin ivory trim", ["M_PlasticGrey"] = "Recesses and tablet bezel", ["M_SteelBrushed"] = "Brushed stainless hardware",
            ["M_RubberBlack"] = "Castor rubber", ["M_Chrome"] = "PatientBay chrome", ["M_AlarmRed"] = "PatientBay alarm red", ["M_VinylTeal"] = "Teal clinical upholstery", ["M_FootCap"] = "Teal clinical upholstery",
            ["M_Oak"] = "Pale oak laminate", ["M_PerfSteel"] = "Perforated steel"
        };

        static Material Find(string name)
        {
            foreach (var g in AssetDatabase.FindAssets(name + " t:Material")) { var p = AssetDatabase.GUIDToAssetPath(g); if (Path.GetFileNameWithoutExtension(p) == name) return AssetDatabase.LoadAssetAtPath<Material>(p); }
            return null;
        }

        static Texture2D Tex(string n, bool srgb, bool normal)
        {
            var p = "Assets/Textures/Hospital/PBR/" + n + ".png"; AssetDatabase.ImportAsset(p, ImportAssetOptions.ForceSynchronousImport);
            var ti = (TextureImporter)AssetImporter.GetAtPath(p); ti.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default; ti.sRGBTexture = srgb; ti.alphaIsTransparency = srgb;
            ti.mipmapEnabled = true; ti.anisoLevel = 8; ti.filterMode = FilterMode.Trilinear; ti.maxTextureSize = 1024; ti.wrapMode = TextureWrapMode.Repeat;
            ti.SetPlatformTextureSettings(new TextureImporterPlatformSettings { name = "Android", overridden = true, maxTextureSize = 512, format = TextureImporterFormat.ASTC_6x6 });
            ti.SaveAndReimport(); return AssetDatabase.LoadAssetAtPath<Texture2D>(p);
        }

        static Material PerfSteel()
        {
            const string path = "Assets/Materials/Hospital/Decor/Perforated steel.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null) { m = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(m, path); }
            m.SetTexture("_BaseMap", Tex("perf_steel_albedo", true, false)); m.SetColor("_BaseColor", Color.white);
            m.SetTexture("_BumpMap", Tex("perf_steel_normal", false, true)); m.SetFloat("_BumpScale", 1f); m.EnableKeyword("_NORMALMAP");
            m.SetTexture("_MetallicGlossMap", Tex("perf_steel_mask", false, false)); m.SetFloat("_Metallic", 1f); m.SetFloat("_Smoothness", 1f); m.EnableKeyword("_METALLICSPECGLOSSMAP");
            m.SetTexture("_OcclusionMap", Tex("perf_steel_ao", false, false)); m.SetFloat("_OcclusionStrength", .8f); m.EnableKeyword("_OCCLUSIONMAP");
            m.SetFloat("_AlphaClip", 1f); m.SetFloat("_Cutoff", .5f); m.SetFloat("_Cull", 0f); m.EnableKeyword("_ALPHATEST_ON"); m.renderQueue = (int)RenderQueue.AlphaTest;
            EditorUtility.SetDirty(m); return m;
        }

        public static void Import()
        {
            var perf = PerfSteel();
            foreach (var id in Ids)
            {
                var src = "Logs/RoomDressing/" + id + ".fbx"; if (!File.Exists(src)) { Debug.LogError("[WaitingFurniture] missing " + src); continue; }
                var fbx = Models + "/" + id + ".fbx"; File.Copy(src, fbx, true); AssetDatabase.ImportAsset(fbx, ImportAssetOptions.ForceSynchronousImport);
                var mi = (ModelImporter)AssetImporter.GetAtPath(fbx); mi.globalScale = 1; mi.useFileScale = true; mi.importAnimation = false; mi.importCameras = false; mi.importLights = false; mi.isReadable = false;
                mi.generateSecondaryUV = true; mi.materialImportMode = ModelImporterMaterialImportMode.None; mi.SaveAndReimport();
                var wrapper = new GameObject(id);
                try
                {
                    var model = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(fbx)); model.transform.SetParent(wrapper.transform, false);
                    var front = model.GetComponentsInChildren<Transform>().Single(t => t.name == "FRONT_MARKER");
                    if (model.transform.InverseTransformPoint(front.position).z > 0) model.transform.localRotation = Quaternion.Euler(0, 180, 0);
                    foreach (var r in model.GetComponentsInChildren<MeshRenderer>())
                    {
                        string key = Map.Keys.FirstOrDefault(k => r.name.EndsWith(k)); var m = key == "M_PerfSteel" ? perf : key != null ? Find(Map[key]) : null;
                        if (m != null) r.sharedMaterial = m; else Debug.LogWarning("[WaitingFurniture] no material for " + r.name);
                        r.receiveGI = ReceiveGI.Lightmaps; GameObjectUtility.SetStaticEditorFlags(r.gameObject, StaticEditorFlags.ContributeGI | StaticEditorFlags.BatchingStatic);
                    }
                    PrefabUtility.SaveAsPrefabAsset(wrapper, Prefabs + "/" + id + ".prefab");
                }
                finally { Object.DestroyImmediate(wrapper); }
            }
            AssetDatabase.SaveAssets(); Debug.Log("[WaitingFurniture] imported");
        }

        struct Spot { public Vector3 face, along, normal; public float from, to; }

        public static void Place()
        {
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/Corridor.unity", OpenSceneMode.Single);
            var old = scene.GetRootGameObjects().FirstOrDefault(g => g.name == Root); if (old != null) Object.DestroyImmediate(old);
            int removed = 0;
            foreach (var t in scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Transform>(true)).Where(t => t != null && t.name.StartsWith("waiting_bench") && t.parent != null && t.GetComponent<MeshRenderer>() == null).ToArray())
            { if (t == null) continue; Object.DestroyImmediate(t.gameObject); removed++; }                       // a nested bench may already have gone with its parent
            var root = new GameObject(Root); SceneManager.MoveGameObjectToScene(root, scene);

            var block = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Renderer>(false)).Where(r => r.enabled && r.gameObject.activeInHierarchy && !(r is ParticleSystemRenderer)
                && !(r.name.StartsWith("Wall") || r.name.StartsWith("Floor") || r.name.StartsWith("Ceiling") || r.name.StartsWith("EQ_city") || r.name.StartsWith("City") || r.name.StartsWith("Skyline") || r.name == "Contact shadows"
                || r.name.StartsWith("Lower") || r.name.StartsWith("Panel cap") || r.name.StartsWith("Skirting") || r.name.StartsWith("Cap rail") || r.name.ToLower().Contains("contact shadow") || r.transform.root.name == "Wall decoration"
                || r.transform.root.name == "Corridor people" || r.transform.root.name == "Wall lamps" || r.transform.root.name == "Sun shafts" || r.name.StartsWith("Person_"))).ToList();
            bool IsDoor(Renderer r) => r.name.Contains("door") || r.name.Contains("Door");
            var placed = new List<Bounds>();

            bool Free(Bounds b)
            {
                var wide = b; wide.Expand(.06f); var doorZone = b; doorZone.Expand(.9f);
                foreach (var r in block) if (IsDoor(r) ? r.bounds.Intersects(doorZone) : r.bounds.Intersects(wide)) return false;
                return !placed.Any(p => p.Intersects(wide));
            }

            var walls = new[] { new Spot { face = new Vector3(2.55f, 0, 0), along = Vector3.forward, normal = Vector3.right, from = -6.2f, to = 12.8f },
                                new Spot { face = new Vector3(6.45f, 0, 0), along = Vector3.forward, normal = Vector3.left, from = -6.2f, to = 12.8f } };
            int benches = 0, tables = 0;
            foreach (var w in walls)
            {
                float z = w.from;
                while (z < w.to)
                {
                    var c = w.face + w.normal * .42f + w.along * (z + .85f) + Vector3.up * .46f;
                    var size = new Vector3(Mathf.Abs(w.normal.x) * .75f + Mathf.Abs(w.along.x) * 1.7f, .92f, Mathf.Abs(w.normal.z) * .75f + Mathf.Abs(w.along.z) * 1.7f); var bb = new Bounds(c, size);
                    if (!Free(bb)) { z += .4f; continue; }
                    var go = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Prefabs + "/perforated_bench.prefab"), root.transform);
                    go.transform.SetPositionAndRotation(w.face + w.normal * .42f + w.along * (z + .85f), Quaternion.LookRotation(-w.normal) * Quaternion.Euler(0, 90, 0) * Quaternion.Euler(0, -90, 0)); go.name = "perforated_bench";
                    // the model's long axis is local X; LookRotation(-normal) puts the front toward the room and X along the wall
                    placed.Add(bb); benches++; Debug.Log("[WaitingFurniture] bench at " + go.transform.position.ToString("F2") + " facing " + (-go.transform.forward).ToString("F2"));
                    var tc = w.face + w.normal * .45f + w.along * (z + 1.7f + .42f);
                    var tb = new Bounds(tc + Vector3.up * .3f, new Vector3(.6f, .6f, .6f));
                    if (Free(tb)) { var t = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Prefabs + "/round_side_table.prefab"), root.transform); t.transform.position = tc; placed.Add(tb); tables++; z += 1.7f + .8f; }
                    else z += 1.7f + .25f;
                    z += 3.2f;                                                                                       // group spacing: a pair (bench, table, bench) then open wall
                    var c2 = w.face + w.normal * .42f + w.along * (z - 3.2f + .85f);                                // (second bench of the pair is attempted next loop pass at the table's far side)
                }
            }
            // a parked stretcher along the right wall where free
            foreach (var z in new[] { 11.2f, 9.6f, 7.9f, 4.6f, 1.2f, -3.0f })
            {
                var pos = new Vector3(6.45f - .45f, 0, z); var bb = new Bounds(pos + Vector3.up * .5f, new Vector3(.8f, 1.0f, 2.2f));
                if (!Free(bb)) continue;
                var s = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Prefabs + "/patient_stretcher.prefab"), root.transform); s.transform.SetPositionAndRotation(pos, Quaternion.LookRotation(Vector3.forward)); placed.Add(bb); break;
            }
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); AssetDatabase.SaveAssets();
            Debug.Log($"[WaitingFurniture] placed {benches} benches, {tables} tables, removed {removed} old benches");
        }
    }
}
