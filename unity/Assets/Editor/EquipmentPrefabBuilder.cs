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
        public static readonly string[] Documents = { "patient_folder" };
        /// <summary>Identity documents, bedside monitoring and sampling (p0-blender-equipment-s06 to s08): grabbable props with their named points; their live surfaces and states come with the panels that use them.</summary>
        public static readonly string[] Props =
        {
            "allergy_band", "report_sheet", "imaging_printout", "consent_form", "clipboard_chart",
            "tympanic_thermometer", "ecg_machine", "stethoscope", "glucose_meter",
            "blood_collection_set", "specimen_tube_rack", "blood_culture_bottles", "urine_kit", "sharps_bin", "tube_station",
        };
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

        [MenuItem("ProcSense/Equipment/Import Notes Folder")]
        public static void ImportDocuments() => ImportAssets(Documents);

        [MenuItem("ProcSense/Equipment/Import Documents, Monitoring and Sampling Props")]
        public static void ImportProps() => ImportAssets(Props);

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
            foreach (string id in ids) MakePaletteMaterials(Path.Combine(source, id + ".equipment.json"));
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

        // the shared M_* materials an asset's palette names that do not exist yet (see-through ones get a transparent surface)
        private static void MakePaletteMaterials(string metadataPath)
        {
            if (!(ProcSense.Core.Content.MiniJson.Parse(File.ReadAllText(metadataPath)) is System.Collections.Generic.Dictionary<string, object> root) || !root.TryGetValue("materials", out var all)) return;
            foreach (var kv in (System.Collections.Generic.Dictionary<string, object>)all)
            {
                var m = (System.Collections.Generic.Dictionary<string, object>)kv.Value;
                var c = ((System.Collections.Generic.List<object>)m["color"]).Select(x => Convert.ToSingle(x, System.Globalization.CultureInfo.InvariantCulture)).ToArray();
                string name = kv.Key.Substring(2);
                MakeMaterial(name, new Color(c[0], c[1], c[2], c[3]), Convert.ToSingle(m["metallic"], System.Globalization.CultureInfo.InvariantCulture), Convert.ToSingle(m["roughness"], System.Globalization.CultureInfo.InvariantCulture));
                var made = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Equipment/M_" + name + ".mat");
                if (c[3] < 1 && made != null && made.GetFloat("_Surface") == 0)
                {
                    made.SetFloat("_Surface", 1);   // URP Lit: transparent
                    made.SetOverrideTag("RenderType", "Transparent");
                    made.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
                    made.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
                    made.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                    made.SetFloat("_ZWrite", 0);
                    made.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                    EditorUtility.SetDirty(made);
                }
            }
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
            if (!Pilots.Contains(id) && !Sensors.Contains(id) && !Documents.Contains(id) && !Props.Contains(id)) return;
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
                body.mass = id == "wristband" || id == "allergy_band" ? .01f : Documents.Contains(id) ? .4f : Sensors.Contains(id) ? .1f : Props.Contains(id) ? (id == "ecg_machine" ? 7 : id == "tube_station" ? 20 : .2f) : 8;
                body.isKinematic = false;
                body.useGravity = true;
                body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
                body.interpolation = RigidbodyInterpolation.Interpolate;
                var grab = root.AddComponent<XRGrabInteractable>();
                // Child controls own their colliders; do not auto-register those on the machine.
                grab.colliders.AddRange(root.GetComponents<Collider>());
                grab.attachTransform = transforms.First(t => t.name.StartsWith("GRAB_", StringComparison.Ordinal));
                grab.movementType = XRBaseInteractable.MovementType.Kinematic;
                grab.throwOnDetach = false;
                if (id == "tube_station") { body.isKinematic = true; body.useGravity = false; grab.enabled = false; }   // fixed to the wall; its carrier is handled with the sample dispatch
                foreach (var anchor in transforms.Where(t => t.name == "SCREEN_Main" || t.name == "SCREEN_Pump" || t.name == "LABEL_Identity"))
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
                if (id == "patient_folder") CreateNotes(root, transforms);
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

        private static void CreateNotes(GameObject root, Transform[] transforms)
        {
            var page = UiKit.WorldCanvas("Notes page", root.transform, new Vector2(630, 900), .0003f);
            page.transform.localPosition = new Vector3(.003f, .0088f, 0);
            page.transform.localRotation = Quaternion.Euler(90, 0, 0);
            page.GetComponentInChildren<UnityEngine.UI.Image>().color = new Color(.96f,.96f,.93f);
            UiKit.Label(page.transform,"Title","PATIENT NOTES",new Vector2(35,35),new Vector2(560,70),40,TextAlignmentOptions.TopLeft,Color.black);
            var body = UiKit.Label(page.transform,"Identity","",new Vector2(35,140),new Vector2(560,540),38,TextAlignmentOptions.TopLeft,Color.black);
            page.gameObject.AddComponent<NotesPanelView>().Configure(body);
            UiKit.Label(page.transform,"Hint","Grab folder • Trigger: close",new Vector2(35,800),new Vector2(560,70),25,TextAlignmentOptions.TopLeft,Color.black);
            var cover = transforms.Single(t=>t.name == "EQ_patient_folder_Cover");
            var label = UiKit.WorldCanvas("Folder cover label",root.transform,new Vector2(560,220),.0003f);
            label.transform.localPosition = new Vector3(0,.0108f,0);
            label.transform.localRotation = Quaternion.Euler(90,0,0);
            label.GetComponentInChildren<UnityEngine.UI.Image>().color=Color.white;
            UiKit.Label(label.transform,"Title","PATIENT NOTES\nGrab • Trigger to open",new Vector2(20,20),new Vector2(520,180),38,TextAlignmentOptions.Center,Color.black);
            label.transform.SetParent(cover,true);
            root.AddComponent<NotesFolderInteraction>().ConfigureFolder(cover,page.gameObject);
            root.AddComponent<ProcSense.VR.Records.Folder.FolderController>();   // pages, page turns and the report pocket (p0-patient-records-emr-s02)
        }
    }
}
