using System;
using System.Linq;
using ProcSense.VR.XR;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Turning;

namespace ProcSense.EditorTools
{
    public static class ComfortSetup
    {
        public static void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play first.");
            if (Enumerable.Range(0, SceneManager.sceneCount).Any(i => SceneManager.GetSceneAt(i).isDirty)) throw new InvalidOperationException("Save intended scene edits first.");
            var prefab = PrefabUtility.LoadPrefabContents("Assets/Prefabs/XR/PlayerRig.prefab");
            var old = prefab.GetComponentInChildren<TeleportationProvider>(true);
            var go = old.gameObject;
            var mediator = old.mediator;
            var safe = old as SafeTeleportProvider;
            if (safe == null) { UnityEngine.Object.DestroyImmediate(old); safe = go.AddComponent<SafeTeleportProvider>(); }
            safe.mediator = mediator;
            safe.transformationPriority = 20;
            if (prefab.GetComponent<LocomotionFade>() == null) prefab.AddComponent<LocomotionFade>();
            if (prefab.GetComponent<ComfortControls>() == null) prefab.AddComponent<ComfortControls>();
            foreach (var ray in prefab.GetComponentsInChildren<UnityEngine.XR.Interaction.Toolkit.Interactors.XRRayInteractor>(true).Where(r => r.name == "Teleport Interactor"))
                ray.raycastTriggerInteraction = QueryTriggerInteraction.Collide;
            foreach (var snap in prefab.GetComponentsInChildren<SnapTurnProvider>(true)) { snap.turnAmount = 45; snap.enableTurnAround = false; }
            // Keep settings in the application variant, leaving third-party samples untouched.
            foreach (var manager in prefab.GetComponentsInChildren<MonoBehaviour>(true).Where(c => c != null && c.GetType().Name == "ControllerInputActionManager"))
            {
                var serialized = new SerializedObject(manager);
                serialized.FindProperty("m_SmoothMotionEnabled").boolValue = false;
                serialized.FindProperty("m_SmoothTurnEnabled").boolValue = false;
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }
            PrefabUtility.SaveAsPrefabAsset(prefab, "Assets/Prefabs/XR/PlayerRig.prefab");
            PrefabUtility.UnloadPrefabContents(prefab);
            foreach (var name in new[] { "PatientBay", "Corridor", "OT" })
            {
                var scene = SceneManager.GetSceneByName(name);
                bool opened = !scene.isLoaded;
                if (opened) scene = EditorSceneManager.OpenScene("Assets/Scenes/" + name + ".unity", OpenSceneMode.Additive);
                SceneManager.SetActiveScene(scene);
                AddTargets(name);
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                if (opened) EditorSceneManager.CloseScene(scene, true);
            }
        }

        public static void AddTargets(string room)
        {
            var existing = SceneManager.GetActiveScene().GetRootGameObjects().FirstOrDefault(g => g.name == "TeleportTargets");
            if (existing != null)
            {
                foreach (var anchor in existing.GetComponentsInChildren<SafeTeleportAnchor>())
                { anchor.interactionLayers = 1 << 31; anchor.GetComponent<Collider>().isTrigger = true; }
                foreach (var area in SceneManager.GetActiveScene().GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<SafeTeleportArea>())) area.interactionLayers = 1 << 31;
                return;
            }
            var root = new GameObject("TeleportTargets").transform;
            if (room == "PatientBay")
            {
                Pad(root, "TELEPORT_Tablet", new Vector3(-0.6f, 0.02f, -1.2f), 0);
                Pad(root, "TELEPORT_Bedside", new Vector3(0.95f, 0.02f, 0.5f), -90);
                Pad(root, "TELEPORT_Exit", new Vector3(1.4f, 0.02f, -0.8f), 90);
            }
            else if (room == "Corridor")
            {
                Pad(root, "TELEPORT_BayDoor", new Vector3(-0.8f, 0.02f, -3), -90);
                Pad(root, "TELEPORT_OTDoor", new Vector3(0.8f, 0.02f, 3), 90);
                var floor = SceneManager.GetActiveScene().GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Transform>()).First(t => t.name == "Floor");
                floor.gameObject.AddComponent<SafeTeleportArea>().interactionLayers = 1 << 31;
            }
            else
            {
                Pad(root, "TELEPORT_Station", new Vector3(-1.2f, 0.02f, 0.7f), 90);
                Pad(root, "TELEPORT_Exit", new Vector3(0, 0.02f, -2.3f), 180);
            }
        }

        private static void Pad(Transform parent, string name, Vector3 position, float yaw)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.position = position;
            go.transform.rotation = Quaternion.Euler(0, yaw, 0);
            go.transform.localScale = new Vector3(0.65f, 0.01f, 0.65f);
            UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());
            var collider = go.AddComponent<BoxCollider>();
            collider.size = new Vector3(1, 2, 1);
            collider.isTrigger = true;
            var anchor = go.AddComponent<SafeTeleportAnchor>();
            anchor.interactionLayers = 1 << 31;
            var point = new GameObject("Destination").transform;
            point.SetParent(parent, false);
            point.position = position;
            point.rotation = Quaternion.Euler(0, yaw, 0);
            anchor.teleportAnchorTransform = point;
            anchor.matchOrientation = MatchOrientation.TargetUpAndForward;
            var material = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Prototype/Mat_295C94.mat");
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                material.color = new Color(0.16f, 0.36f, 0.58f);
                AssetDatabase.CreateAsset(material, "Assets/Materials/Prototype/Mat_295C94.mat");
            }
            go.GetComponent<Renderer>().sharedMaterial = material;
        }
    }
}
