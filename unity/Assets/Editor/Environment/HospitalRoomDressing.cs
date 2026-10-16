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
    // Deliberate, one-time additions to authored scenes; never rebuilds a room shell.
    public static class HospitalRoomDressing
    {
        public const string RootName = "Hospital room dressing";
        private const string Models = "Assets/Models/Environment/RoomDressing";
        private const string Prefabs = "Assets/Prefabs/Environment/RoomDressing";
        private const string Materials = "Assets/Materials/Hospital/RoomDressing";
        private static readonly string[] Ids = { "hospital_bed", "privacy_curtain", "handrub", "pedal_bin", "waiting_bench", "clinical_trolley", "operating_table", "anaesthesia_unit", "mayo_stand", "supply_cupboard", "surgical_light" };

        private static void Guard()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || Lightmapping.isRunning)
                throw new InvalidOperationException("Stop Play/baking first");
            if (Enumerable.Range(0, SceneManager.sceneCount).Any(i => SceneManager.GetSceneAt(i).isDirty))
                throw new InvalidOperationException("Preserve unsaved scenes first");
        }

        public static object Import()
        {
            Guard();
            foreach (string id in Ids)
            {
                if (!File.Exists("Logs/RoomDressing/" + id + ".fbx")) throw new FileNotFoundException("Build all room props first", id);
                if (AssetDatabase.LoadAssetAtPath<GameObject>(Prefabs + "/" + id + ".prefab") != null)
                    throw new InvalidOperationException("Preserve existing prefab " + id);
            }
            Folder(Models); Folder(Prefabs); Folder(Materials);
            var materials = new Dictionary<string, Material> {
                ["M_Ivory"] = Existing("Bedside/Clean ivory housing"),
                ["M_Sage"] = Existing("Doors/Sage clinical door"),
                ["M_PlasticWhite"] = Existing("Satin ivory trim"),
                ["M_PlasticGrey"] = Existing("Bedside/Recesses and tablet bezel"),
                ["M_SteelBrushed"] = Existing("Doors/Brushed stainless hardware"),
                ["M_RubberBlack"] = Existing("Bedside/Castor rubber"),
                ["M_ScreenOff"] = Material("Inactive display glass", "101F24", .45f),
                ["M_Curtain"] = Material("Sage privacy fabric", "B2C7BA", .03f),
                ["M_Upholstery"] = Material("Teal clinical upholstery", "345B60", .18f),
                ["M_Amber"] = Material("Amber waste lid", "C5983A", .15f)
            };
            foreach (string id in Ids)
            {
                string path = Models + "/" + id + ".fbx";
                File.Copy("Logs/RoomDressing/" + id + ".fbx", path, true);
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
                var importer = (ModelImporter)AssetImporter.GetAtPath(path);
                importer.globalScale = 1; importer.useFileScale = true; importer.importAnimation = false;
                importer.importCameras = false; importer.importLights = false; importer.isReadable = false;
                importer.generateSecondaryUV = true; importer.materialImportMode = ModelImporterMaterialImportMode.None;
                importer.SaveAndReimport();
                var wrapper = new GameObject(id);
                try
                {
                    var model = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(path));
                    model.transform.SetParent(wrapper.transform, false);
                    var front = model.GetComponentsInChildren<Transform>().Single(t => t.name == "FRONT_MARKER");
                    if (model.transform.InverseTransformPoint(front.position).z > 0) model.transform.localRotation = Quaternion.Euler(0, 180, 0);
                    foreach (var renderer in model.GetComponentsInChildren<MeshRenderer>())
                    {
                        renderer.sharedMaterial = materials.Single(p => renderer.name.EndsWith(p.Key, StringComparison.Ordinal)).Value;
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

        public static object Apply(string room)
        {
            Guard();
            if (!new[] { "PatientBay", "Corridor", "OT" }.Contains(room)) throw new ArgumentException("Unknown room");
            foreach (string id in Ids) if (AssetDatabase.LoadAssetAtPath<GameObject>(Prefabs + "/" + id + ".prefab") == null) throw new InvalidOperationException("Import props first");
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/" + room + ".unity", OpenSceneMode.Single);
            if (scene.GetRootGameObjects().Any(g => g.name == RootName)) throw new InvalidOperationException("Already dressed: preserve manual edits");
            // Resolve required legacy objects before creating anything in the scene.
            var originalRoom = scene.GetRootGameObjects().Single(g => g.name == room).transform;
            var oldTable = room == "OT" ? originalRoom.GetComponentsInChildren<Transform>(true).Single(t => t.name == "OT table placeholder").GetComponent<Renderer>() : null;
            if (room == "PatientBay")
                foreach (string name in new[] { "Bed", "Bed head", "Bed frame deck" })
                    if (originalRoom.Find(name) == null || originalRoom.Find(name).GetComponent<Renderer>() == null) throw new InvalidOperationException("Missing legacy bed part " + name);
            var root = new GameObject(RootName).transform;
            if (room == "PatientBay")
            {
                var bay = scene.GetRootGameObjects().Single(g => g.name == room).transform;
                // Keep patient, soft mattress, bedding and all interaction/collision identities.
                foreach (string name in new[] { "Bed", "Bed head", "Bed frame deck" }) bay.Find(name).GetComponent<Renderer>().enabled = false;
                foreach (var renderer in bay.Find("Bed frame deck").GetComponentsInChildren<Renderer>()) renderer.enabled = false;
                foreach (var line in bay.GetComponentsInChildren<LineRenderer>(true).Where(l => l.GetComponent<ProcSense.VR.Views.SensorCableView>() != null))
                {
                    line.positionCount = 0; line.startWidth = line.endWidth = .004f;
                }
                Spawn("hospital_bed", root, new Vector3(0, 0, 1));
                Spawn("privacy_curtain", root, Vector3.zero);
                Spawn("handrub", root, new Vector3(-1.45f, 1.35f, -1.84f), 180);
                var bin = Spawn("pedal_bin", root, new Vector3(-2.05f, 0, -1.55f));
                Solid(bin, new Vector3(0, .245f, 0), new Vector3(.34f, .49f, .34f));
            }
            else if (room == "Corridor")
            {
                var bench = Spawn("waiting_bench", root, new Vector3(1.64f, 0, -1.15f), 90);
                Solid(bench, new Vector3(0, .47f, 0), new Vector3(1.54f, .94f, .46f));
                var cart = Spawn("clinical_trolley", root, new Vector3(1.56f, 0, .85f), 90);
                Solid(cart, new Vector3(0, .53f, 0), new Vector3(.64f, 1.06f, .52f));
                Spawn("handrub", root, new Vector3(1.83f, 1.36f, 1.65f), 90);
            }
            else
            {
                oldTable.enabled = false;
                Spawn("operating_table", root, new Vector3(0, 0, .7f));
                var anaesthesia = Spawn("anaesthesia_unit", root, new Vector3(-1.05f, 0, 2.05f), -20);
                Solid(anaesthesia, new Vector3(0, .84f, 0), new Vector3(.76f, 1.69f, .62f));
                var mayo = Spawn("mayo_stand", root, new Vector3(1.05f, 0, 1.15f));
                Solid(mayo, new Vector3(0, 1.025f, 0), new Vector3(.66f, .065f, .46f));
                Solid(mayo, new Vector3(0, .52f, .12f), new Vector3(.05f, 1, .05f));
                Solid(mayo, new Vector3(0, .07f, 0), new Vector3(.48f, .14f, .46f));
                var cart = Spawn("clinical_trolley", root, new Vector3(-2.78f, 0, .8f), -90);
                Solid(cart, new Vector3(0, .53f, 0), new Vector3(.64f, 1.06f, .52f));
                var cabinet = Spawn("supply_cupboard", root, new Vector3(2.35f, 0, 3.10f));
                Solid(cabinet, new Vector3(0, .94f, -.03f), new Vector3(1.2f, 1.88f, .53f));
                Spawn("surgical_light", root, new Vector3(-.65f, 0, 1.2f));
                Spawn("handrub", root, new Vector3(-1.35f, 1.36f, -3.32f), 180);
                var bin = Spawn("pedal_bin", root, new Vector3(-2.95f, 0, -2.85f), 180);
                Solid(bin, new Vector3(0, .245f, 0), new Vector3(.34f, .49f, .34f));
            }
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); AssetDatabase.SaveAssets();
            return new { room, props = root.childCount, readyToBake = true };
        }

        private static Transform Spawn(string id, Transform parent, Vector3 position, float yaw = 0)
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Prefabs + "/" + id + ".prefab"));
            go.transform.SetParent(parent, false); go.transform.SetPositionAndRotation(position, Quaternion.Euler(0, yaw, 0)); return go.transform;
        }
        private static void Solid(Transform parent, Vector3 center, Vector3 size)
        {
            var go = new GameObject("Furniture collision", typeof(BoxCollider)); go.transform.SetParent(parent, false);
            var box = go.GetComponent<BoxCollider>(); box.center = center; box.size = size;
        }
        private static void Folder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            Folder(path.Substring(0, path.LastIndexOf('/'))); AssetDatabase.CreateFolder(path.Substring(0, path.LastIndexOf('/')), Path.GetFileName(path));
        }
        private static Material Existing(string name)
        {
            var value = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Hospital/" + name + ".mat");
            if (value == null) throw new InvalidOperationException("Missing shared material " + name); return value;
        }
        private static Material Material(string name, string hex, float smoothness)
        {
            string path = Materials + "/" + name + ".mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path); if (existing != null) return existing;
            ColorUtility.TryParseHtmlString("#" + hex, out var color);
            var value = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name, enableInstancing = true };
            value.SetColor("_BaseColor", color); value.SetFloat("_Smoothness", smoothness); AssetDatabase.CreateAsset(value, path); return value;
        }
    }
}
