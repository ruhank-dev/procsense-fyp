using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using ProcSense.VR.Views;
using ProcSense.VR.ActionEmitters;
using TMPro;

namespace ProcSense.EditorTools
{
    public static class EquipmentPrefabBuilder
    {
        public static readonly string[] Pilots = { "vital_signs_monitor", "iv_pole_pump", "wristband" };
        public static readonly string[] Sensors = { "spo2_probe", "bp_cuff" };
        [Serializable] private class Anchor { public string name; public Vector3 size; }
        [Serializable] private class Metadata { public Anchor[] anchors; }

        private static void Folder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = path.Substring(0, path.LastIndexOf('/'));
            Folder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }

        [MenuItem("ProcSense/Equipment/Import Pilot Assets")]
        public static void ImportPilots()
        {
            ImportAssets(Pilots);
        }

        [MenuItem("ProcSense/Equipment/Import Bedside Sensors")]
        public static void ImportSensors() => ImportAssets(Sensors);

        private static void ImportAssets(string[] ids)
        {
            string source = Path.GetFullPath(Path.Combine(Application.dataPath, "../../build/blender"));
            foreach (string id in ids)
                foreach (string suffix in new[] { ".fbx", ".equipment.json" })
                    if (!File.Exists(Path.Combine(source, id + suffix))) throw new FileNotFoundException("Run task blender-build-all first", id + suffix);
            Folder("Assets/Models/Equipment");
            Folder("Assets/Materials/Equipment");
            Folder("Assets/Prefabs/Equipment");
            MakeMaterial("PlasticWhite", new Color(.72f, .76f, .78f), 0, .34f);
            MakeMaterial("PlasticGrey", new Color(.055f, .075f, .085f), 0, .38f);
            MakeMaterial("SteelBrushed", new Color(.38f, .43f, .47f), .8f, .3f);
            MakeMaterial("Chrome", new Color(.62f, .67f, .7f), .95f, .18f);
            MakeMaterial("RubberBlack", new Color(.018f, .025f, .028f), 0, .75f);
            MakeMaterial("ScreenOff", new Color(.009f, .023f, .029f), .05f, .24f);
            MakeMaterial("AccentBlue", new Color(.025f, .25f, .35f), .15f, .35f);
            MakeMaterial("FabricNavy", new Color(.014f, .04f, .085f), 0, .9f);
            foreach (string id in ids)
            {
                string path = EquipmentPostprocessor.ModelFolder + id;
                File.Copy(Path.Combine(source, id + ".equipment.json"), path + ".equipment.json", true);
                File.Copy(Path.Combine(source, id + ".fbx"), path + ".fbx", true);
                AssetDatabase.ImportAsset(path + ".equipment.json", ImportAssetOptions.ForceSynchronousImport);
                AssetDatabase.ImportAsset(path + ".fbx", ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
                CreateIfMissing(path + ".fbx");
            }
            AssetDatabase.SaveAssets();
        }

        private static void MakeMaterial(string name, Color color, float metal, float roughness)
        {
            string path = "Assets/Materials/Equipment/M_" + name + ".mat";
            if (AssetDatabase.LoadAssetAtPath<Material>(path) != null) return;
            var material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "M_" + name };
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Metallic", metal);
            material.SetFloat("_Smoothness", 1 - roughness);
            AssetDatabase.CreateAsset(material, path);
        }

        public static void CreateIfMissing(string modelPath)
        {
            string id = Path.GetFileNameWithoutExtension(modelPath);
            if (!Pilots.Contains(id) && !Sensors.Contains(id)) return;
            string prefabPath = "Assets/Prefabs/Equipment/" + id + ".prefab";
            // Nested model instances refresh on reimport. Never overwrite authored component overrides.
            if (AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) != null) return;
            string metadataPath = Path.ChangeExtension(modelPath, ".equipment.json");
            if (!File.Exists(metadataPath)) return;
            var metadata = JsonUtility.FromJson<Metadata>(File.ReadAllText(metadataPath));
            var root = new GameObject(id);
            root.SetActive(false);
            try
            {
                var model = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(modelPath));
                model.transform.SetParent(root.transform, false);
                var transforms = model.GetComponentsInChildren<Transform>(true);
                foreach (var part in transforms.Where(t => t.name.EndsWith("_Wrapped", StringComparison.Ordinal))) part.gameObject.SetActive(false);
                foreach (var item in metadata.anchors.Where(a => a.name.StartsWith("COL_", StringComparison.Ordinal)))
                {
                    var anchor = transforms.Single(t => t.name == item.name);
                    var collider = root.AddComponent<BoxCollider>();
                    collider.center = root.transform.InverseTransformPoint(anchor.position);
                    collider.size = item.size;
                }
                var body = root.AddComponent<Rigidbody>();
                body.mass = id == "wristband" ? .01f : Sensors.Contains(id) ? .1f : 8;
                body.isKinematic = true;
                body.useGravity = false;
                var grab = root.AddComponent<XRGrabInteractable>();
                grab.attachTransform = transforms.First(t => t.name.StartsWith("GRAB_", StringComparison.Ordinal));
                grab.movementType = XRBaseInteractable.MovementType.Kinematic;
                grab.throwOnDetach = false;
                foreach (var anchor in transforms.Where(t => t.name.StartsWith("SCREEN_", StringComparison.Ordinal) || t.name == "LABEL_Identity"))
                {
                    bool identity = anchor.name == "LABEL_Identity";
                    bool pump = anchor.name == "SCREEN_Pump";
                    var surface = identity ? EquipmentSurfaceView.Surface.Identity : pump ? EquipmentSurfaceView.Surface.Pump : EquipmentSurfaceView.Surface.Monitor;
                    var size = identity ? new Vector2(750, 220) : pump ? new Vector2(395, 145) : new Vector2(522, 326);
                    float scale = identity ? .0001f : pump ? .0002f : .0005f;
                    var canvas = UiKit.WorldCanvas("Live surface", anchor, size, scale);
                    // FBX axes are baked; set the intended surface orientation in root space.
                    canvas.transform.rotation = root.transform.rotation * (identity ? Quaternion.Euler(90, 0, 0) : Quaternion.Euler(0, 180, 0));
                    var background = canvas.GetComponentInChildren<UnityEngine.UI.Image>();
                    background.color = identity ? Color.white : new Color(.009f, .023f, .029f);
                    var label = UiKit.Label(canvas.transform, "Permitted state", identity ? "READ WRISTBAND" : pump ? "STANDBY" : "SpO2   --\nPULSE  --\nBP     --/--\nTEMP   --",
                        new Vector2(18, 12), size - new Vector2(36, 24), identity ? 48 : pump ? 38 : 42,
                        TextAlignmentOptions.TopLeft, identity ? Color.black : new Color(.65f, 1, .83f));
                    anchor.gameObject.AddComponent<EquipmentSurfaceView>().Configure(surface, label);
                }
                if (id == "wristband")
                {
                    var emitter = root.AddComponent<XRSelectEmitter>();
                    emitter.Route = XRSelectEmitter.SelectionRoute.Grab;
                    emitter.Configure("read_wristband", "patient", fallbackLabel: "Read wristband");
                }
                foreach (var anchor in transforms.Where(t => t.name.StartsWith("SOCKET_Bag_", StringComparison.Ordinal)))
                {
                    var socket = anchor.gameObject.AddComponent<XRSocketInteractor>();
                    // Reserved equipment layer; ordinary props cannot be stolen by hook sockets.
                    socket.interactionLayers = 1 << 8;
                    socket.showInteractableHoverMeshes = false;
                    var trigger = anchor.gameObject.AddComponent<SphereCollider>();
                    trigger.radius = .025f;
                    trigger.isTrigger = true;
                }
                root.SetActive(true);
                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }
    }
}
