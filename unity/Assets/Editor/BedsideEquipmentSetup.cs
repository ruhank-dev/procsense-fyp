using System;
using System.Linq;
using ProcSense.VR.ActionEmitters;
using ProcSense.VR.Views;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace ProcSense.EditorTools
{
    public static class BedsideEquipmentSetup
    {
        [MenuItem("ProcSense/Equipment/Install Bedside Sensors")]
        public static void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play first.");
            var scene = SceneManager.GetSceneByPath(WorkspaceMenu.BayPath);
            if (!scene.isLoaded || scene.isDirty) throw new InvalidOperationException("Open and save PatientBay first; existing edits will not be overwritten.");
            var bay = scene.GetRootGameObjects().Single(r => r.name == "PatientBay");
            if (bay.transform.Find("Physical bedside monitoring") != null) return;
            foreach (var id in EquipmentPrefabBuilder.Sensors.Concat(new[] { "vital_signs_monitor" }))
                if (AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Equipment/" + id + ".prefab") == null)
                    throw new InvalidOperationException("Import the sensor and monitor assets first.");
            var root = new GameObject("Physical bedside monitoring");
            SceneManager.MoveGameObjectToScene(root, scene);
            root.transform.SetParent(bay.transform, false);
            var monitor = Spawn("vital_signs_monitor", root.transform, new Vector3(1.1f, 0, 1.6f));
            monitor.transform.rotation = Quaternion.Euler(0, 180, 0);
            // Keep the old geometry recoverable; no scene regeneration or deletion.
            foreach (string name in new[] { "Monitor", "Monitor stand", "Monitor view" })
            {
                var old = bay.transform.Find(name);
                if (old != null) old.gameObject.SetActive(false);
            }
            // Greybox arm/finger placeholders pending the separate patient-character work.
            var arm = Primitive("Patient forearm placeholder", root.transform, new Vector3(.29f, .91f, 1.0f), new Vector3(.075f, .075f, .55f), PrimitiveType.Capsule);
            arm.transform.rotation = Quaternion.Euler(90, 0, 0);
            arm.transform.localScale = new Vector3(.09f, .27f, .09f);
            var finger = Primitive("Finger placeholder", root.transform, new Vector3(.29f, .91f, .70f), new Vector3(.016f, .016f, .06f), PrimitiveType.Cube);
            var skin = bay.transform.Find("Patient").GetComponent<Renderer>().sharedMaterial;
            arm.GetComponent<Renderer>().sharedMaterial = skin;
            finger.GetComponent<Renderer>().sharedMaterial = skin;
            var tray = Primitive("Sensor tray", root.transform, new Vector3(.78f, .83f, 1.13f), new Vector3(.43f, .025f, .39f), PrimitiveType.Cube);
            tray.GetComponent<Renderer>().sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Equipment/M_PlasticWhite.mat");
            InstallSensor("spo2_probe", "spo2", 9, root.transform, monitor.transform, new Vector3(.29f,.91f,.70f), new Vector3(.67f,.865f,1.05f), "ATTACH_Cable", "ATTACH_SpO2");
            InstallSensor("bp_cuff", "nibp_cuff", 10, root.transform, monitor.transform, new Vector3(.29f,.91f,1.09f), new Vector3(.78f,.85f,1.22f), "ATTACH_Hose", "ATTACH_NIBP");
            var button = Primitive("Start BP cycle", monitor.transform, new Vector3(0, 1.195f, .12f), new Vector3(.15f,.035f,.012f), PrimitiveType.Cube);
            button.AddComponent<XRSimpleInteractable>();
            button.AddComponent<XRSelectEmitter>().Configure("measure_nibp", "patient", fallbackLabel: "Measure BP");
            var canvas = UiKit.WorldCanvas("BP control label", button.transform, new Vector2(300,70), .0005f);
            // Button inherits nonuniform visual scale; compensate for readable world-space text.
            canvas.transform.SetParent(monitor.transform, true);
            canvas.transform.localPosition = new Vector3(0,1.195f,.128f);
            canvas.transform.localRotation = Quaternion.Euler(0,180,0);
            canvas.transform.localScale = Vector3.one*.0005f;
            UiKit.Label(canvas.transform,"Label","START BP",new Vector2(8,4),new Vector2(284,62),36,TMPro.TextAlignmentOptions.Center);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        private static void InstallSensor(string id, string sensor, int bit, Transform root, Transform monitor, Vector3 target, Vector3 home, string cablePoint, string port)
        {
            var mount = new GameObject(sensor + " patient socket");
            mount.transform.SetParent(root,false);
            mount.transform.localPosition = target;
            var socket = mount.AddComponent<XRSocketInteractor>();
            socket.interactionLayers = 1 << bit;
            socket.showInteractableHoverMeshes = false;
            socket.attachTransform = mount.transform;
            var trigger = mount.AddComponent<SphereCollider>();
            trigger.radius = sensor == "spo2" ? .045f : .065f;
            trigger.isTrigger = true;
            var rest = new GameObject(id + " tray pose").transform;
            rest.SetParent(root,false);
            rest.localPosition = home;
            var prop = Spawn(id,root,home);
            var grab = prop.GetComponent<XRGrabInteractable>();
            grab.interactionLayers = 1 | (1 << bit);
            var transforms = prop.GetComponentsInChildren<Transform>(true);
            // Socket alignment uses the fit anchor, and grabbing uses the same stable pose.
            grab.attachTransform = transforms.Single(t=>t.name == (sensor == "spo2" ? "SOCKET_Finger" : "SOCKET_Arm"));
            var wrapped = transforms.FirstOrDefault(t=>t.name.EndsWith("_Wrapped",StringComparison.Ordinal));
            var loose = transforms.Where(t=>t.name.EndsWith("_Body",StringComparison.Ordinal)||t.name.EndsWith("_Closure",StringComparison.Ordinal)).Select(t=>t.gameObject).ToArray();
            if(wrapped != null) wrapped.gameObject.SetActive(false);
            var attachment = prop.AddComponent<SensorAttachment>();
            attachment.ConfigureSensor(sensor,socket,rest,loose,wrapped != null ? wrapped.gameObject : null);
            attachment.ConfigurePlacementHighlight(AssetDatabase.LoadAssetAtPath<Material>(SensorPlacementSetup.MaterialPath));
            var cable = new GameObject(id + " cable");
            cable.transform.SetParent(root,false);
            var line = cable.AddComponent<LineRenderer>();
            line.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Equipment/M_RubberBlack.mat");
            var view = cable.AddComponent<SensorCableView>();
            view.DevicePort = monitor.GetComponentsInChildren<Transform>().Single(t=>t.name==port);
            view.SensorPort = transforms.Single(t=>t.name==cablePoint);
        }

        private static GameObject Spawn(string id,Transform parent,Vector3 position)
        {
            var obj=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Equipment/"+id+".prefab"),parent);
            obj.transform.localPosition=position;
            return obj;
        }

        private static GameObject Primitive(string name,Transform parent,Vector3 position,Vector3 size,PrimitiveType type)
        {
            var obj=GameObject.CreatePrimitive(type);
            obj.name=name;
            obj.transform.SetParent(parent,false);
            obj.transform.localPosition=position;
            obj.transform.localScale=size;
            obj.GetComponent<Renderer>().sharedMaterial=AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Equipment/M_PlasticGrey.mat");
            return obj;
        }
    }
}
