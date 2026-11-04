using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace ProcSense.EditorTools
{
    /// <summary>
    /// Final-look props (sofa, palms, vine, wall TV, three framed prints): imports the Blender FBXs from Logs/ReferenceProps, builds their materials, and places them in PatientBay under
    /// "Reference room props". Idempotent: the root is rebuilt each run. Static meshes contribute to the baked lighting; nothing here is a Rigidbody.
    /// </summary>
    public static class FinalLookProps
    {
        const string Models = "Assets/Models/Environment/ReferenceProps";
        const string Mats = "Assets/Materials/Hospital/ReferenceProps";
        const string Tex = "Assets/Textures/Hospital/ReferenceProps";
        const string Root = "Reference room props";
        static readonly string[] Ids = { "ref_sofa", "ref_palm", "ref_vine", "ref_wall_tv", "ref_print1", "ref_print2", "ref_print3" };

        static Color Hex(string v) { ColorUtility.TryParseHtmlString("#" + v, out var c); return c; }
        static void Folder(string p) { if (!AssetDatabase.IsValidFolder(p)) { var parent = Path.GetDirectoryName(p).Replace('\\', '/'); Folder(parent); AssetDatabase.CreateFolder(parent, Path.GetFileName(p)); } }

        static Material Mat(string name, string hex, float smooth, float metal = 0f)
        {
            var path = Mats + "/" + name + ".mat"; var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null) { m = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name }; AssetDatabase.CreateAsset(m, path); }
            m.SetColor("_BaseColor", Hex(hex)); m.SetFloat("_Smoothness", smooth); m.SetFloat("_Metallic", metal); m.enableInstancing = true;
            EditorUtility.SetDirty(m); return m;
        }

        static Material PrintMat(int n)
        {
            var dst = Tex + $"/print_{n}.png";
            File.Copy(Path.GetFullPath($"Logs/ReferenceProps/prints/print_{n}.png"), Path.GetFullPath(dst), true);
            AssetDatabase.ImportAsset(dst, ImportAssetOptions.ForceSynchronousImport);
            var ti = (TextureImporter)AssetImporter.GetAtPath(dst); ti.mipmapEnabled = true; ti.maxTextureSize = 1024; ti.wrapMode = TextureWrapMode.Clamp;
            ti.SetPlatformTextureSettings(new TextureImporterPlatformSettings { name = "Android", overridden = true, maxTextureSize = 1024, format = TextureImporterFormat.ASTC_6x6 }); ti.SaveAndReimport();
            var m = Mat($"Wall print {n}", "FFFFFF", .55f); m.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(dst)); return m;
        }

        public static void Import()
        {
            Folder(Models); Folder(Mats); Folder(Tex);
            var mats = new System.Collections.Generic.Dictionary<string, Material>
            {
                ["M_SofaFabric"] = Mat("Sofa teal fabric", "2B8079", .12f), ["M_SofaLeg"] = Mat("Sofa steel leg", "8C9094", .45f, .85f),
                ["M_PlanterGrey"] = Mat("Planter brushed grey", "7E8184", .42f, .85f), ["M_Soil"] = Mat("Planter soil", "17120D", .1f),
                ["M_Leaf"] = Mat("Palm leaf dark", "1F5C21", .5f), ["M_PalmFrond"] = Mat("Palm leaf dark", "FFFFFF", .35f), ["M_PothosLeaf"] = Mat("Pothos leaf", "FFFFFF", .35f), ["M_LeafLight"] = Mat("Palm leaf light", "387724", .5f), ["M_Stem"] = Mat("Plant stem", "55442A", .35f),
                ["M_Terracotta"] = Mat("Terracotta pot", "9E4E2B", .2f), ["M_TvBlack"] = Mat("Wall TV housing", "0E0F11", .35f), ["M_TvScreen"] = Mat("Wall TV screen off", "050608", .8f),
                ["M_FrameOak"] = Mat("Print frame oak", "B88F5C", .4f), ["M_PrintMat"] = Mat("Print mount board", "EFEDE6", .2f),
                ["M_Print1"] = PrintMat(1), ["M_Print2"] = PrintMat(2), ["M_Print3"] = PrintMat(3),
            };
            foreach (var id in Ids)
            {
                var path = Models + "/" + id + ".fbx";
                File.Copy(Path.GetFullPath("Logs/ReferenceProps/" + id + ".fbx"), Path.GetFullPath(path), true);
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
                var imp = (ModelImporter)AssetImporter.GetAtPath(path);
                imp.globalScale = 1; imp.useFileScale = true; imp.importAnimation = false; imp.importCameras = false; imp.importLights = false; imp.isReadable = false;
                imp.generateSecondaryUV = true; imp.materialImportMode = ModelImporterMaterialImportMode.None; imp.SaveAndReimport();
                var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                foreach (var r in go.GetComponentsInChildren<MeshRenderer>(true))
                {
                    var key = mats.Keys.FirstOrDefault(k => r.name.EndsWith(k, StringComparison.Ordinal));
                    if (key == null) Debug.LogWarning("[FinalLookProps] no material for " + r.name);
                }
            }
            AssetDatabase.SaveAssets();
            Debug.Log("[FinalLookProps] imported " + Ids.Length);
        }

        static GameObject Spawn(string id, Transform parent, Vector3 pos, float yaw, Vector3? scale = null)
        {
            var src = AssetDatabase.LoadAssetAtPath<GameObject>(Models + "/" + id + ".fbx");
            var go = (GameObject)PrefabUtility.InstantiatePrefab(src); go.transform.SetParent(parent, false);
            go.name = id; go.transform.position = pos; go.transform.rotation = Quaternion.Euler(0, yaw, 0); if (scale.HasValue) go.transform.localScale = scale.Value;
            foreach (var r in go.GetComponentsInChildren<MeshRenderer>(true))
            {
                var path = Mats + "/";
                var key = r.name.Substring(r.name.LastIndexOf("M_", StringComparison.Ordinal));
                r.sharedMaterial = KeyToMaterial(key);
                r.receiveGI = ReceiveGI.Lightmaps; r.shadowCastingMode = ShadowCastingMode.On;
                GameObjectUtility.SetStaticEditorFlags(r.gameObject, StaticEditorFlags.ContributeGI);
            }
            return go;
        }

        static Material KeyToMaterial(string key)
        {
            var map = new System.Collections.Generic.Dictionary<string, string>
            {
                ["M_SofaFabric"] = "Sofa teal fabric", ["M_SofaLeg"] = "Sofa steel leg", ["M_PlanterGrey"] = "Planter brushed grey", ["M_Soil"] = "Planter soil", ["M_Leaf"] = "Palm leaf dark",
                ["M_LeafLight"] = "Palm leaf light", ["M_PalmFrond"] = "Palm leaf dark", ["M_PothosLeaf"] = "Pothos leaf", ["M_Stem"] = "Plant stem", ["M_Terracotta"] = "Terracotta pot", ["M_TvBlack"] = "Wall TV housing", ["M_TvScreen"] = "Wall TV screen off",
                ["M_FrameOak"] = "Print frame oak", ["M_PrintMat"] = "Print mount board", ["M_Print1"] = "Wall print 1", ["M_Print2"] = "Wall print 2", ["M_Print3"] = "Wall print 3",
            };
            return AssetDatabase.LoadAssetAtPath<Material>(Mats + "/" + map[key] + ".mat");
        }

        static void Collider(GameObject go, Bounds worldBounds, bool capsule = false)
        {
            var c = new GameObject("Prop collision"); c.transform.SetParent(go.transform, false); c.transform.position = worldBounds.center;
            if (capsule) { var cc = c.AddComponent<CapsuleCollider>(); cc.radius = Mathf.Min(worldBounds.extents.x, worldBounds.extents.z); cc.height = worldBounds.size.y; }
            else { var bc = c.AddComponent<BoxCollider>(); bc.size = worldBounds.size; }
        }

        static Bounds Bnd(GameObject go) { var b = new Bounds(); bool first = true; foreach (var r in go.GetComponentsInChildren<Renderer>()) { if (first) { b = r.bounds; first = false; } else b.Encapsulate(r.bounds); } return b; }

        public static void Place()
        {
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/PatientBay.unity", OpenSceneMode.Single);
            var old = scene.GetRootGameObjects().FirstOrDefault(g => g.name == Root); if (old != null) UnityEngine.Object.DestroyImmediate(old);
            var root = new GameObject(Root).transform;
            var all = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Transform>(true)).ToArray();
            // make room along the window wall: the old teal visitor chair is replaced by the sofa; the pedal bin moves to the front wall, out of the walking lane
            var chair = all.FirstOrDefault(t => t.name == "visitor_chair" && t.parent != null && t.parent.name == "Clinical bay details"); if (chair != null) chair.gameObject.SetActive(false);
            var bin = all.FirstOrDefault(t => t.name == "pedal_bin" && t.parent != null && t.parent.name == "Hospital room dressing");
            if (bin != null) { var b = Bnd(bin.gameObject); bin.position += new Vector3(-.10f - b.center.x, 0, -1.72f - b.center.z); }   // against the front wall, clear of the walking lane between the tablet table and the wall fixtures (z -1.3 .. -.65)
            // sofa against the window wall, front toward +X (model front is -Z in Unity after import)
            var sofa = Spawn("ref_sofa", root, new Vector3(-2.03f, 0, -.28f), 90f);
            Collider(sofa, Bnd(sofa));
            var palmA = Spawn("ref_palm", root, new Vector3(-1.85f, 0, 1.72f), 20f); Collider(palmA, new Bounds(new Vector3(-1.85f, .26f, 1.72f), new Vector3(.42f, .52f, .42f)), true);
            var palmB = Spawn("ref_palm", root, new Vector3(2.12f, 0, 1.70f), 140f, new Vector3(1.08f, 1.08f, 1.08f)); Collider(palmB, new Bounds(new Vector3(2.12f, .26f, 1.70f), new Vector3(.42f, .52f, .42f)), true);
            Spawn("ref_vine", root, new Vector3(-2.29f, 1.30f, .62f), 0f);
            // thin wall TV above the workstation, screen toward the patient (+Z), so the reference camera sees it nearly edge-on
            Spawn("ref_wall_tv", root, new Vector3(.98f, 1.88f, -1.905f), 0f, new Vector3(.86f, .86f, .86f));
            // three framed prints above the headwall
            float[] xs = { -.98f, 0f, .98f };
            for (int i = 0; i < 3; i++) Spawn($"ref_print{i + 1}", root, new Vector3(xs[i], 2.40f, 1.939f), 180f, new Vector3(1.12f, 1.12f, 1.12f));
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); AssetDatabase.SaveAssets();
            Debug.Log("[FinalLookProps] placed; sofa bounds " + Bnd(sofa));
        }
    }
}
