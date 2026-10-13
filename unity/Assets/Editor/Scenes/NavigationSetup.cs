using System;
using System.Linq;
using ProcSense.VR.Boot;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ProcSense.EditorTools
{
    /// <summary>Incremental navigation setup; never regenerates the saved patient bay.</summary>
    public static class NavigationSetup
    {
        public static void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play first.");
            if (Enumerable.Range(0, SceneManager.sceneCount).Any(i => SceneManager.GetSceneAt(i).isDirty))
                throw new InvalidOperationException("Save intended scene edits before navigation setup.");
            var core = Open("Sys_Core");
            var bay = Open("PatientBay");
            var rig = bay.GetRootGameObjects().FirstOrDefault(g => g.GetComponent<Unity.XR.CoreUtils.XROrigin>() != null);
            if (rig != null) SceneManager.MoveGameObjectToScene(rig, core);
            if (!core.GetRootGameObjects().Any(g => g.GetComponent<RoomLoader>() != null))
            {
                var service = new GameObject("RoomLoader", typeof(RoomLoader));
                SceneManager.MoveGameObjectToScene(service, core);
            }
            SceneManager.SetActiveScene(bay);
            if (bay.GetRootGameObjects().All(g => g.name != "Navigation")) AddBayNavigation();
            EditorSceneManager.MarkSceneDirty(core);
            EditorSceneManager.MarkSceneDirty(bay);
            EditorSceneManager.SaveScene(core);
            EditorSceneManager.SaveScene(bay);
            CreateRoom("Corridor", 4, 10);
            CreateRoom("OT", 7, 7);
            var paths = EditorBuildSettings.scenes.ToList();
            foreach (var name in new[] { "Corridor", "OT" })
            {
                string path = "Assets/Scenes/" + name + ".unity";
                if (!paths.Any(p => p.path == path)) paths.Add(new EditorBuildSettingsScene(path, true));
            }
            EditorBuildSettings.scenes = paths.ToArray();
            WorkspaceMenu.OpenPatientBay();
        }

        public static void AddBayNavigation()
        {
            var root = new GameObject("Navigation", typeof(RoomBootstrap)).transform;
            Anchor(root, "Default", "LOC-BAY", new Vector3(0, 0, -1.4f), 0);
            Anchor(root, "Corridor", "LOC-BAY", new Vector3(1.4f, 0, -0.8f), -90);
            Door(root, "Corridor", "CORRIDOR", new Vector3(2.35f, 1.45f, -0.8f), 90);
        }

        public static void CreateRoom(string name, float width, float depth)
        {
            string path = "Assets/Scenes/" + name + ".unity";
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(path) != null) return;
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            SceneManager.SetActiveScene(scene);
            var root = new GameObject(name, typeof(RoomBootstrap)).transform;
            Box(root, "Floor", new Vector3(0, -0.05f, 0), new Vector3(width, 0.1f, depth));
            Box(root, "Wall left", new Vector3(-width/2, 1.4f, 0), new Vector3(0.1f, 2.8f, depth));
            Box(root, "Wall right", new Vector3(width/2, 1.4f, 0), new Vector3(0.1f, 2.8f, depth));
            Box(root, "Wall front", new Vector3(0, 1.4f, -depth/2), new Vector3(width, 2.8f, 0.1f));
            Box(root, "Wall back", new Vector3(0, 1.4f, depth/2), new Vector3(width, 2.8f, 0.1f));
            var light = new GameObject("Room light", typeof(Light));
            light.transform.SetParent(root);
            light.transform.rotation = Quaternion.Euler(60, 30, 0);
            light.GetComponent<Light>().type = LightType.Directional;
            if (name == "Corridor")
            {
                Anchor(root, "PatientBay", "LOC-CORRIDOR", new Vector3(-0.8f, 0, -3), 0);
                Anchor(root, "OT", "LOC-CORRIDOR", new Vector3(0.8f, 0, 3), 180);
                Door(root, "PatientBay", "PATIENT BAY", new Vector3(-1.85f, 1.45f, -3), -90);
                Door(root, "OT", "OPERATING THEATRE", new Vector3(1.85f, 1.45f, 3), 90);
                foreach (var pair in new[] { ("CT / MRI", -1.5f), ("ULTRASOUND", 1.5f) })
                    Door(root, "", pair.Item1, new Vector3(-1.85f, 1.45f, pair.Item2), -90, true);
            }
            else
            {
                Anchor(root, "Corridor", "LOC-OT", new Vector3(0, 0, -2.3f), 0);
                Door(root, "Corridor", "CORRIDOR", new Vector3(0, 1.45f, -3.35f), 180);
                Box(root, "OT table placeholder", new Vector3(0, 0.45f, 0.7f), new Vector3(0.9f, 0.9f, 2));
            }
            EditorSceneManager.SaveScene(scene, path);
            EditorSceneManager.CloseScene(scene, true);
        }

        private static Scene Open(string name)
        {
            var scene = SceneManager.GetSceneByName(name);
            return scene.isLoaded ? scene : EditorSceneManager.OpenScene("Assets/Scenes/" + name + ".unity", OpenSceneMode.Additive);
        }

        private static void Anchor(Transform root, string from, string id, Vector3 position, float yaw)
        {
            var go = new GameObject("Anchor_" + id + "_From_" + from, typeof(RoomAnchor));
            go.transform.SetParent(root, false);
            go.transform.localPosition = position;
            go.transform.localRotation = Quaternion.Euler(0, yaw, 0);
            go.GetComponent<RoomAnchor>().FromRoom = from;
            go.GetComponent<RoomAnchor>().LocationId = id;
        }

        private static void Door(Transform root, string destination, string title, Vector3 position, float yaw, bool restricted = false)
        {
            var go = new GameObject("Door_" + title, typeof(RoomDoor));
            go.transform.SetParent(root, false);
            go.transform.localPosition = position;
            go.transform.localRotation = Quaternion.Euler(0, yaw, 0);
            var door = go.GetComponent<RoomDoor>();
            door.Destination = destination;
            door.Title = title;
            door.Restricted = restricted;
        }

        private static void Box(Transform parent, string name, Vector3 position, Vector3 scale)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.transform.localScale = scale;
            var material = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Prototype/Mat_" + (name == "Floor" ? "59616B" : "D1D9E0") + ".mat");
            if (material != null) go.GetComponent<Renderer>().sharedMaterial = material;
        }
    }
}
