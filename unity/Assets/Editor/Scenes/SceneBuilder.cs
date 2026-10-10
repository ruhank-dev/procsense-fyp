using System;
using System.IO;
using System.Linq;
using ProcSense.VR.ActionEmitters;
using ProcSense.VR.Boot;
using ProcSense.VR.Bridge;
using ProcSense.VR.Views;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.UI;

namespace ProcSense.EditorTools
{
    /// <summary>
    /// Generates the four scenes (Boot, Sys_Core, PatientBay, DevOnly) from code and the room manifest, so nothing is hand-placed
    /// and a re-run gives the same scenes. Batch: -executeMethod ProcSense.EditorTools.SceneBuilder.BuildAll
    /// </summary>
    public static class SceneBuilder
    {
        private const string ScenesDir = "Assets/Scenes";
        private const string ManifestPath = "Assets/Data/room_manifest_bay.json";
        private const string MaterialsDir = "Assets/Materials/Prototype";
        private const string PlayerRigPath = "Assets/Prefabs/XR/PlayerRig.prefab";
        private const string SimulatorPrefab = "Assets/Samples/XR Interaction Toolkit/3.6.1/XR Device Simulator/XR Device Simulator.prefab";

        [Serializable] private class Room { public float width, depth, height; }
        [Serializable]
        private class Item
        {
            public string name, kind, verb, label, view;
            public float[] pos, size, color, rot, viewPos, viewRot;
        }
        [Serializable] private class Manifest { public Room room; public Item[] items; }

        [MenuItem("ProcSense/Tools/Rebuild Prototype Scenes")]
        private static void RebuildFromMenu()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (!EditorUtility.DisplayDialog("Rebuild prototype scenes?",
                "This replaces the saved Boot, Sys_Core, PatientBay and DevOnly scenes from the room manifest. " +
                "Manual scene layout edits will be overwritten. Use Open Patient Bay to edit the existing scenes.",
                "Rebuild", "Cancel")) return;
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            BuildAll();
            WorkspaceMenu.OpenPatientBay();
        }

        public static void BuildAll()
        {
            Directory.CreateDirectory(ScenesDir);
            Directory.CreateDirectory(MaterialsDir);
            var config = MakeConfig();
            BuildSysCore(config);
            BuildPatientBay();
            BuildDevOnly();
            BuildBoot();
            EditorBuildSettings.scenes = new[] { "Boot", "Sys_Core", "PatientBay", "DevOnly" }
                .Select(n => new EditorBuildSettingsScene(ScenesDir + "/" + n + ".unity", true)).ToArray();
            Debug.Log("[SceneBuilder] done");
        }

        private static SessionConfig MakeConfig()
        {
            string path = "Assets/Data/SessionConfig_Skeleton1.asset";
            var config = AssetDatabase.LoadAssetAtPath<SessionConfig>(path);
            if (config == null) { config = ScriptableObject.CreateInstance<SessionConfig>(); AssetDatabase.CreateAsset(config, path); }
            return config;
        }

        private static Scene NewScene() => EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        private static void Save(Scene scene, string name) => EditorSceneManager.SaveScene(scene, ScenesDir + "/" + name + ".unity");

        private static void BuildSysCore(SessionConfig config)
        {
            var scene = NewScene();
            var host = new GameObject("SimulationHost").AddComponent<SimulationHost>();
            host.Config = config;
            var events = new GameObject("EventSystem");
            events.AddComponent<EventSystem>();
            events.AddComponent<XRUIInputModule>();
            Save(scene, "Sys_Core");
        }

        private static void BuildBoot()
        {
            var scene = NewScene();
            // No camera here: the XR rig in the patient bay owns the only camera (a second one would draw over it).
            new GameObject("BootLoader").AddComponent<BootLoader>();
            Save(scene, "Boot");
        }

        private static void BuildDevOnly()
        {
            var scene = NewScene();
            var sim = AssetDatabase.LoadAssetAtPath<GameObject>(SimulatorPrefab);
            if (sim != null) PrefabUtility.InstantiatePrefab(sim);
            else Debug.LogWarning("[SceneBuilder] XR Device Simulator sample missing; run ProcSense/Project setup first");
            Save(scene, "DevOnly");
        }

        private static void BuildPatientBay()
        {
            var manifest = JsonUtility.FromJson<Manifest>(File.ReadAllText(ManifestPath));
            var scene = NewScene();
            var root = new GameObject("PatientBay").transform;

            var light = new GameObject("Light", typeof(Light)).GetComponent<Light>();
            light.type = LightType.Directional;
            light.transform.rotation = Quaternion.Euler(60, 30, 0);
            RenderSettings.ambientLight = new Color(0.55f, 0.57f, 0.6f);

            var r = manifest.room;
            Box(root, "Floor", new Vector3(0, -0.05f, 0), new Vector3(r.width, 0.1f, r.depth), new Color(0.35f, 0.38f, 0.42f));
            Box(root, "Wall back", new Vector3(0, r.height / 2, r.depth / 2), new Vector3(r.width, r.height, 0.1f), new Color(0.82f, 0.85f, 0.88f));
            Box(root, "Wall front", new Vector3(0, r.height / 2, -r.depth / 2), new Vector3(r.width, r.height, 0.1f), new Color(0.82f, 0.85f, 0.88f));
            Box(root, "Wall left", new Vector3(-r.width / 2, r.height / 2, 0), new Vector3(0.1f, r.height, r.depth), new Color(0.78f, 0.82f, 0.86f));
            Box(root, "Wall right", new Vector3(r.width / 2, r.height / 2, 0), new Vector3(0.1f, r.height, r.depth), new Color(0.78f, 0.82f, 0.86f));

            foreach (var item in manifest.items) Place(root, item);

            var rig = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerRigPath);
            if (rig != null)
            {
                var go = (GameObject)PrefabUtility.InstantiatePrefab(rig);
                go.transform.position = new Vector3(0, 0, -1.4f);
            }
            else throw new InvalidOperationException("Player rig missing: " + PlayerRigPath);
            Save(scene, "PatientBay");
        }

        private static Vector3 V(float[] a) => new Vector3(a[0], a[1], a[2]);

        private static GameObject Box(Transform parent, string name, Vector3 pos, Vector3 size, Color color, PrimitiveType type = PrimitiveType.Cube)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localScale = size;
            go.GetComponent<Renderer>().sharedMaterial = Material(color);
            return go;
        }

        private static Material Material(Color color)
        {
            string path = MaterialsDir + "/Mat_" + ColorUtility.ToHtmlStringRGB(color) + ".mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m != null) return m;
            m = new Material(Shader.Find("Universal Render Pipeline/Lit")) { color = color };
            AssetDatabase.CreateAsset(m, path);
            return m;
        }

        private static void Place(Transform root, Item item)
        {
            var color = item.color != null ? new Color(item.color[0], item.color[1], item.color[2]) : Color.grey;
            var type = item.kind == "capsule" ? PrimitiveType.Capsule : PrimitiveType.Cube;
            var go = Box(root, item.name, V(item.pos), V(item.size), color, type);
            if (item.rot != null) go.transform.localRotation = Quaternion.Euler(V(item.rot));

            if (item.kind == "select")
            {
                go.AddComponent<XRSimpleInteractable>();
                go.AddComponent<XRSelectEmitter>().Configure(item.verb, "patient", fallbackLabel: item.label);
            }
            if (string.IsNullOrEmpty(item.view)) return;

            var host = new GameObject(item.name + " view").transform;
            host.SetParent(root, false);
            host.localPosition = V(item.viewPos);
            host.localRotation = Quaternion.Euler(V(item.viewRot));
            switch (item.view)
            {
                case "tablet": host.gameObject.AddComponent<TabletView>(); break;
                case "monitor": host.gameObject.AddComponent<MonitorView>(); break;
                case "card": host.gameObject.AddComponent<IdentityCardView>(); break;
                case "notes": host.gameObject.AddComponent<NotesPanelView>(); break;
            }
        }
    }
}
