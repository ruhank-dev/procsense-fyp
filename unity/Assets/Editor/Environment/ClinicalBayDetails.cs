using System;
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
    public static class ClinicalBayDetails
    {
        public const string RootName = "Clinical bay details";
        private const string Models = "Assets/Models/Environment/ClinicalDetails";
        private const string Prefabs = "Assets/Prefabs/Environment/ClinicalDetails";
        private const string Materials = "Assets/Materials/Hospital/ClinicalDetails";
        private const string Textures = "Assets/Textures/Hospital/ClinicalDetails";
        private static readonly string[] Ids = { "clinical_sink", "glove_dispenser", "towel_dispenser", "sharps_container", "charting_counter", "visitor_chair", "wall_clock", "wall_bumper" };

        private static void Guard()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || Lightmapping.isRunning || Enumerable.Range(0, SceneManager.sceneCount).Any(i => SceneManager.GetSceneAt(i).isDirty))
                throw new InvalidOperationException("Stop Play/baking and preserve unsaved work first");
        }

        public static object Import()
        {
            Guard();
            foreach (string id in Ids)
            {
                if (!File.Exists("Logs/ClinicalDetails/" + id + ".fbx")) throw new FileNotFoundException("Generate clinical detail props first", id);
                if (AssetDatabase.LoadAssetAtPath<GameObject>(Prefabs + "/" + id + ".prefab") != null) throw new InvalidOperationException("Preserve existing prefab " + id);
            }
            Folder(Models); Folder(Prefabs); Folder(Materials); Folder(Textures);
            var map = new Dictionary<string, Material> {
                ["M_Ivory"] = Existing("Bedside/Clean ivory housing"), ["M_Sage"] = Existing("Doors/Sage clinical door"),
                ["M_PlasticWhite"] = Existing("Satin ivory trim"), ["M_PlasticGrey"] = Existing("Bedside/Recesses and tablet bezel"),
                ["M_SteelBrushed"] = Existing("Doors/Brushed stainless hardware"), ["M_RubberBlack"] = Existing("Bedside/Castor rubber"),
                ["M_ScreenOff"] = Existing("RoomDressing/Inactive display glass"), ["M_Upholstery"] = Existing("RoomDressing/Teal clinical upholstery"),
                ["M_Amber"] = Existing("RoomDressing/Amber waste lid")
            };
            foreach (string id in Ids)
            {
                string path = Models + "/" + id + ".fbx";
                File.Copy("Logs/ClinicalDetails/" + id + ".fbx", path, true);
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
                var importer = (ModelImporter)AssetImporter.GetAtPath(path);
                importer.globalScale = 1; importer.useFileScale = true; importer.importAnimation = false;
                importer.importCameras = false; importer.importLights = false; importer.isReadable = false;
                importer.generateSecondaryUV = true; importer.materialImportMode = ModelImporterMaterialImportMode.None; importer.SaveAndReimport();
                var wrapper = new GameObject(id);
                try
                {
                    var model = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(path));
                    model.transform.SetParent(wrapper.transform, false);
                    if (model.transform.InverseTransformPoint(model.GetComponentsInChildren<Transform>().Single(t => t.name == "FRONT_MARKER").position).z > 0) model.transform.localRotation = Quaternion.Euler(0, 180, 0);
                    foreach (var renderer in model.GetComponentsInChildren<MeshRenderer>())
                    {
                        renderer.sharedMaterial = map.Single(p => renderer.name.EndsWith(p.Key, StringComparison.Ordinal)).Value;
                        renderer.receiveGI = ReceiveGI.Lightmaps;
                        GameObjectUtility.SetStaticEditorFlags(renderer.gameObject, StaticEditorFlags.ContributeGI | StaticEditorFlags.BatchingStatic);
                    }
                    PrefabUtility.SaveAsPrefabAsset(wrapper, Prefabs + "/" + id + ".prefab");
                }
                finally { UnityEngine.Object.DestroyImmediate(wrapper); }
            }
            AssetDatabase.SaveAssets();
            return new { imported = Ids.Length };
        }

        public static object ApplyCorridor()
        {
            Guard();
            if (AssetDatabase.LoadAssetAtPath<GameObject>(Prefabs + "/wall_clock.prefab") == null) throw new InvalidOperationException("Import first");
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/Corridor.unity", OpenSceneMode.Single);
            if (scene.GetRootGameObjects().Any(g => g.name == RootName)) throw new InvalidOperationException("Already detailed");
            var root = new GameObject(RootName).transform;
            Spawn("wall_clock", root, new Vector3(0, 2.08f, 4.923f));
            Spawn("wall_bumper", root, new Vector3(1.878f, .77f, -3.20f), 90);
            Spawn("wall_bumper", root, new Vector3(-1.878f, .77f, 3.65f), -90);
            var floor = scene.GetRootGameObjects().Single(g => g.name == "Corridor").transform.Find("Floor").GetComponent<Renderer>();
            var material = Clone(floor.sharedMaterial, "Corridor muted wood flooring");
            material.SetColor("_BaseColor", new Color(.82f, .89f, .88f)); material.SetFloat("_Smoothness", .17f);
            floor.sharedMaterial = material; EditorUtility.SetDirty(material);
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); AssetDatabase.SaveAssets();
            return new { room = scene.name, props = root.childCount, readyToBake = true };
        }

        public static object Apply()
        {
            Guard();
            foreach (string id in Ids) if (AssetDatabase.LoadAssetAtPath<GameObject>(Prefabs + "/" + id + ".prefab") == null) throw new InvalidOperationException("Import incomplete: " + id);
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/PatientBay.unity", OpenSceneMode.Single);
            if (scene.GetRootGameObjects().Any(g => g.name == RootName)) throw new InvalidOperationException("Already detailed; preserve authored work");
            var bay = scene.GetRootGameObjects().Single(g => g.name == "PatientBay").transform;
            var dressing = scene.GetRootGameObjects().Single(g => g.name == HospitalRoomDressing.RootName).transform;
            var root = new GameObject(RootName).transform;
            var sink = Spawn("clinical_sink", root, new Vector3(-1.68f, 0, -1.645f), 180);
            Solid(sink, new Vector3(0, .40f, 0), new Vector3(.82f, .73f, .48f));
            foreach (float x in new[] { -.38f, .38f }) Solid(sink, new Vector3(x, .885f, 0), new Vector3(.14f, .045f, .56f));
            foreach (float z in new[] { -.225f, .225f }) Solid(sink, new Vector3(0, .885f, z), new Vector3(.64f, .045f, .11f));
            foreach (float x in new[] { -.27f, .27f }) Solid(sink, new Vector3(x, .82f, 0), new Vector3(.035f, .14f, .35f));
            foreach (float z in new[] { -.16f, .16f }) Solid(sink, new Vector3(0, .82f, z), new Vector3(.54f, .14f, .035f));
            Spawn("glove_dispenser", root, new Vector3(-.85f, 1.48f, -1.9125f), 180);
            Spawn("towel_dispenser", root, new Vector3(-2.20f, 1.56f, -1.885f), 180);
            Spawn("sharps_container", root, new Vector3(2.335f, 1.25f, 1.16f), 90);
            var counter = Spawn("charting_counter", root, new Vector3(.98f, 0, -1.70f), 180);
            Solid(counter, new Vector3(0, .98f, 0), new Vector3(1.02f, .04f, .46f));
            var chair = Spawn("visitor_chair", root, new Vector3(-2.08f, 0, -.12f), -90);
            Solid(chair, new Vector3(0, .48f, 0), new Vector3(.50f, .065f, .47f));
            Solid(chair, new Vector3(0, .77f, .22f), new Vector3(.48f, .33f, .065f));
            foreach (float x in new[] { -.23f, .23f }) foreach (float z in new[] { -.21f, .21f })
                Solid(chair, new Vector3(x, .25f, z), new Vector3(.035f, .45f, .035f));
            Spawn("wall_clock", root, new Vector3(1.69f, 2.12f, 1.923f));
            Spawn("wall_bumper", root, new Vector3(2.378f, .77f, .72f), 90);
            dressing.Find("handrub").position = new Vector3(-1.45f, 1.35f, -1.9175f);
            dressing.Find("pedal_bin").position = new Vector3(-2.10f, 0, -.92f);
            Finishes(scene, bay);
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); AssetDatabase.SaveAssets();
            return new { room = scene.name, props = root.childCount, readyToBake = true };
        }

        private static void Finishes(Scene scene, Transform bay)
        {
            var laminate = Clone(Existing("Satin ivory trim"), "Bay sage washable panels");
            laminate.SetColor("_BaseColor", new Color(.62f, .72f, .68f)); laminate.SetFloat("_Smoothness", .23f);
            var finishes = scene.GetRootGameObjects().Single(g => g.name == "Hospital finishes");
            foreach (var renderer in finishes.GetComponentsInChildren<MeshRenderer>().Where(r => r.name == "Lower oak wall panel")) renderer.sharedMaterial = laminate;
            var flooring = Clone(bay.Find("Floor").GetComponent<Renderer>().sharedMaterial, "Bay muted wood flooring");
            flooring.SetColor("_BaseColor", new Color(.82f, .89f, .88f)); flooring.SetFloat("_Smoothness", .17f);
            bay.Find("Floor").GetComponent<Renderer>().sharedMaterial = flooring;
            var weave = Weave();
            var originals = new Dictionary<Material, Material>();
            foreach (var renderer in scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Renderer>(true)))
            {
                bool fabric = renderer.name == "Draped bed blanket" || renderer.name == "Draped patient gown" || renderer.sharedMaterial != null && (renderer.sharedMaterial.name == "Sage curtain fabric" || renderer.sharedMaterial.name == "Sage privacy fabric");
                if (!fabric || renderer.sharedMaterial == null) continue;
                var source = renderer.sharedMaterial;
                if (!originals.TryGetValue(source, out var material))
                {
                    material = Clone(source, "Bay woven " + source.name); originals.Add(source, material);
                    material.SetTexture("_BumpMap", weave); material.SetTextureScale("_BumpMap", new Vector2(8, 8));
                    material.SetFloat("_BumpScale", .11f); material.SetFloat("_Smoothness", .07f); material.EnableKeyword("_NORMALMAP");
                }
                renderer.sharedMaterial = material;
            }
            foreach (var material in originals.Values.Concat(new[] { laminate, flooring })) EditorUtility.SetDirty(material);
        }

        private static Texture2D Weave()
        {
            string path = Textures + "/Woven micro normal.png";
            var texture = new Texture2D(256, 256, TextureFormat.RGB24, false, true);
            var pixels = new Color[256 * 256];
            for (int y = 0; y < 256; y++) for (int x = 0; x < 256; x++)
            {
                float dx = .17f * Mathf.Sin(x * Mathf.PI / 2) * (.75f + .25f * Mathf.Cos(y * Mathf.PI / 2));
                float dy = .17f * Mathf.Sin(y * Mathf.PI / 2) * (.75f + .25f * Mathf.Cos(x * Mathf.PI / 2));
                var n = new Vector3(dx, dy, 1).normalized;
                pixels[y * 256 + x] = new Color(n.x * .5f + .5f, n.y * .5f + .5f, n.z * .5f + .5f);
            }
            texture.SetPixels(pixels); texture.Apply(); File.WriteAllBytes(path, texture.EncodeToPNG()); UnityEngine.Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path); importer.textureType = TextureImporterType.NormalMap;
            importer.mipmapEnabled = true; importer.wrapMode = TextureWrapMode.Repeat; importer.maxTextureSize = 256; importer.anisoLevel = 2;
            importer.SetPlatformTextureSettings(new TextureImporterPlatformSettings { name = "Android", overridden = true, maxTextureSize = 256, format = TextureImporterFormat.ASTC_6x6 });
            importer.SaveAndReimport(); return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        private static Transform Spawn(string id, Transform root, Vector3 p, float yaw = 0)
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Prefabs + "/" + id + ".prefab"));
            go.transform.SetParent(root, false); go.transform.SetPositionAndRotation(p, Quaternion.Euler(0, yaw, 0)); return go.transform;
        }
        private static void Solid(Transform parent, Vector3 center, Vector3 size)
        {
            var go = new GameObject("Fixture collision", typeof(BoxCollider)); go.transform.SetParent(parent, false);
            var collider = go.GetComponent<BoxCollider>(); collider.center = center; collider.size = size;
        }
        private static Material Existing(string name) => AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Hospital/" + name + ".mat") ?? throw new InvalidOperationException("Missing material " + name);
        private static Material Clone(Material source, string name)
        {
            string path = Materials + "/" + name + ".mat";
            if (AssetDatabase.LoadAssetAtPath<Material>(path) != null) throw new InvalidOperationException("Preserve existing material " + name);
            var material = new Material(source) { name = name }; AssetDatabase.CreateAsset(material, path); return material;
        }
        private static void Folder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = path.Substring(0, path.LastIndexOf('/')); Folder(parent); AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
    }
}
