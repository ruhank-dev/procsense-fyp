using System;
using System.IO;
using System.Linq;
using ProcSense.VR.Views;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using Unity.XR.CoreUtils;
using UnityEngine.XR.Interaction.Toolkit.UI;

namespace ProcSense.EditorTools
{
    /// <summary>
    /// Builds the scenes and wiring that were still missing (plans p0-evaluation-performance-s04, p1-theatre-extension-s02 and s03, p0-patient-records-emr-s04 and s05, p0-diagnostic-transitions-s04 and s05):
    /// the MainMenu scene (mode chooser, Start, Quit), the Recovery scene (a recovery bay with the nurse and the handover panel), and the clinical panels installer on the persistent Sys_Core object (EMR
    /// panel, event cards, diagnostics props). Run it from the menu or in batch mode with -executeMethod ProcSense.EditorTools.AddonScenesSetup.Apply. It adds to the existing scenes and never rebuilds them.
    /// </summary>
    public static class AddonScenesSetup
    {
        private const string ScenesDir = "Assets/Scenes";
        private const string PlayerRigPath = "Assets/Prefabs/XR/PlayerRig.prefab";

        [MenuItem("ProcSense/Tools/Build menu, recovery and clinical panels")]
        public static void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play first.");
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Directory.CreateDirectory(ScenesDir);
            BuildMainMenu();
            BuildRecovery();
            InstallPanelsInSysCore();
            var wanted = new[] { "Boot", "Sys_Core", "PatientBay", "Corridor", "OT", "DevOnly", "MainMenu", "Recovery" };
            var scenes = EditorBuildSettings.scenes.ToList();
            foreach (var name in wanted)
            {
                string path = ScenesDir + "/" + name + ".unity";
                if (File.Exists(path) && scenes.All(s => s.path != path)) scenes.Add(new EditorBuildSettingsScene(path, true));
            }
            EditorBuildSettings.scenes = scenes.ToArray();
            Debug.Log("[AddonScenesSetup] done");
        }

        private static Scene NewScene() => EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        private static void Rig()
        {
            var events = new GameObject("EventSystem");
            events.AddComponent<EventSystem>();
            events.AddComponent<XRUIInputModule>();
            var rig = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerRigPath);
            if (rig == null) throw new InvalidOperationException("Player rig missing: " + PlayerRigPath);
            ((GameObject)PrefabUtility.InstantiatePrefab(rig)).transform.position = new Vector3(0, 0, -1.4f);
        }

        private static void Light()
        {
            var light = new GameObject("Light", typeof(Light)).GetComponent<Light>();
            light.type = LightType.Directional;
            light.transform.rotation = Quaternion.Euler(60, 30, 0);
            RenderSettings.ambientLight = new Color(0.55f, 0.57f, 0.6f);
        }

        private static void Box(Transform parent, string name, Vector3 pos, Vector3 size, Color color)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localScale = size;
            var material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { color = color };
            go.GetComponent<Renderer>().sharedMaterial = material;
        }

        private static void BuildMainMenu()
        {
            var scene = NewScene();
            Light();
            Rig();
            var root = new GameObject("MainMenu").transform;
            Box(root, "Floor", new Vector3(0, -0.05f, 1.5f), new Vector3(6f, 0.1f, 6f), new Color(0.3f, 0.33f, 0.38f));
            var menu = new GameObject("Menu controller");
            menu.transform.SetParent(root, false);
            menu.transform.position = new Vector3(0f, 0f, 1.6f);
            menu.AddComponent<MainMenuController>();
            EditorSceneManager.SaveScene(scene, ScenesDir + "/MainMenu.unity");
        }

        private static void BuildRecovery()
        {
            var scene = NewScene();
            Light();
            Rig();
            var root = new GameObject("RecoveryBay").transform;
            Box(root, "Floor", new Vector3(0, -0.05f, 0), new Vector3(5f, 0.1f, 5f), new Color(0.35f, 0.38f, 0.42f));
            Box(root, "Wall back", new Vector3(0, 1.4f, 2.5f), new Vector3(5f, 2.8f, 0.1f), new Color(0.82f, 0.85f, 0.88f));
            Box(root, "Wall left", new Vector3(-2.5f, 1.4f, 0), new Vector3(0.1f, 2.8f, 5f), new Color(0.78f, 0.82f, 0.86f));
            Box(root, "Wall right", new Vector3(2.5f, 1.4f, 0), new Vector3(0.1f, 2.8f, 5f), new Color(0.78f, 0.82f, 0.86f));
            Box(root, "Recovery bed", new Vector3(1.2f, 0.3f, 1.2f), new Vector3(0.9f, 0.5f, 2f), new Color(0.9f, 0.9f, 0.95f));
            var handover = new GameObject("Handover station");
            handover.transform.SetParent(root, false);
            handover.transform.position = new Vector3(-0.6f, 0f, 1.6f);
            handover.AddComponent<RecoveryHandoverView>();
            RecoverySetup.Place(root);                                   // monitor, oxygen and suction by the bed (p1-theatre-extension-s04)
            EditorSceneManager.SaveScene(scene, ScenesDir + "/Recovery.unity");
        }

        private static void InstallPanelsInSysCore()
        {
            string path = ScenesDir + "/Sys_Core.unity";
            if (!File.Exists(path)) return;
            var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            bool changed = false;
            if (scene.GetRootGameObjects().All(g => g.GetComponent<ClinicalPanelsInstaller>() == null))
            {
                new GameObject("Clinical panels").AddComponent<ClinicalPanelsInstaller>();
                changed = true;
            }
            if (scene.GetRootGameObjects().All(g => g.GetComponent<GeneratedScenarioLauncher>() == null))
            {
                new GameObject("Generated scenario launcher").AddComponent<GeneratedScenarioLauncher>();
                changed = true;
            }
            if (changed) EditorSceneManager.SaveScene(scene);
        }
    }
}
