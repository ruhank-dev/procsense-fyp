using System;
using System.Linq;
using ProcSense.VR.ActionEmitters;
using ProcSense.VR.Views;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ProcSense.EditorTools
{
    public static class ThermometerSetup
    {
        [MenuItem("ProcSense/Setup/Add physical bedside thermometer")]
        public static void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || Lightmapping.isRunning ||
                Enumerable.Range(0, SceneManager.sceneCount).Any(i => SceneManager.GetSceneAt(i).isDirty))
                throw new InvalidOperationException("Stop Play/baking and preserve unsaved scenes first.");
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/PatientBay.unity", OpenSceneMode.Single);
            if (scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<ContactActionEmitter>(true)).Any())
                throw new InvalidOperationException("Thermometer already fitted; preserve its reviewed placement.");
            var all = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true)).ToArray();
            var patient = all.Single(t => t.name == "Rocketbox patient");
            var head = patient.GetComponentsInChildren<Transform>(true).Single(t => t.name == "Bip01 Head");
            var ear = new GameObject("Thermometer ear contact").transform;
            ear.SetParent(head, false);
            ear.position = head.position + new Vector3(.09f, .055f, .015f);
            var tool = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Equipment/tympanic_thermometer.prefab"), scene);
            tool.name = "tympanic_thermometer";
            tool.transform.SetPositionAndRotation(new Vector3(.91f, .858f, 1.25f), Quaternion.Euler(-90, 0, 0));
            var anchors = tool.GetComponentsInChildren<Transform>(true);
            var screen = anchors.Single(t => t.name == "SCREEN_Temp");
            var canvas = UiKit.WorldCanvas("Temperature readout", screen, new Vector2(240, 150), .0001f);
            canvas.GetComponent<ProcSense.VR.UI.Accessibility.AccessibleCanvas>().ViewingDistance = .2f;
            canvas.transform.rotation = tool.transform.rotation * Quaternion.Euler(0, 180, 0);
            canvas.GetComponentInChildren<UnityEngine.UI.Image>().color = new Color(.009f, .023f, .029f);
            var digits = UiKit.Label(canvas.transform, "Public temperature", "--.- C", new Vector2(5, 5), new Vector2(230, 85), 62,
                TextAlignmentOptions.Center, new Color(.65f, 1, .83f));
            var hint = UiKit.Label(canvas.transform, "Contact instruction", "HOLD AT EAR", new Vector2(5, 90), new Vector2(230, 55), 34,
                TextAlignmentOptions.Center);
            screen.gameObject.AddComponent<EquipmentSurfaceView>().Configure(EquipmentSurfaceView.Surface.Temperature, digits);
            var emitter = tool.AddComponent<ContactActionEmitter>();
            emitter.Configure("measure_temperature", fallbackLabel: "Measure temperature");
            emitter.ConfigureContact(anchors.Single(t => t.name == "ATTACH_Ear"), ear, hint);
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
        }
    }
}
