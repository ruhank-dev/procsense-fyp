using System;
using System.Linq;
using ProcSense.VR.Boot;
using ProcSense.VR.Views;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace ProcSense.EditorTools
{
    /// <summary>Targeted, idempotent setup of diagnostic endpoints. Reuses authored geometry and never rebuilds a room.</summary>
    public static class DiagnosticDoorSetup
    {
        public const string CopyPath = "Assets/Data/diagnostic-door-presentation.json";

        public static object Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || Lightmapping.isRunning ||
                Enumerable.Range(0, SceneManager.sceneCount).Any(i => SceneManager.GetSceneAt(i).isDirty))
                throw new InvalidOperationException("Preserve active Play, bake or unsaved scene work before diagnostic setup.");
            AssetDatabase.Refresh();
            var copy = AssetDatabase.LoadAssetAtPath<TextAsset>(CopyPath);
            if (copy == null) throw new InvalidOperationException("Missing diagnostic presentation copy.");
            var words = JsonUtility.FromJson<DiagnosticDoorView.Wording>(copy.text);
            var scene = SceneManager.GetSceneByName("Corridor");
            if (!scene.isLoaded) scene = EditorSceneManager.OpenScene("Assets/Scenes/Corridor.unity", OpenSceneMode.Additive);
            var root = scene.GetRootGameObjects().Single(g => g.GetComponent<RoomLayout>() != null).transform;
            var navigation = root.GetComponentsInChildren<RoomDoor>(true);
            var ct = navigation.Single(d => d.Restricted && (d.Title == "CT / MRI" || d.Title == "CT SCAN"));
            var ultrasound = navigation.Single(d => d.Restricted && d.Title == "ULTRASOUND");
            var architecture = root.GetComponentsInChildren<Transform>(true).Single(t => t.name == "Door_CT / MRI architecture");
            var hospital = architecture.parent;
            var usArchitecture = root.GetComponentsInChildren<Transform>(true).Single(t => t.name == "Door_ULTRASOUND architecture");
            Bind(ct, architecture, "ct", words.ct, "D01", false);
            Bind(ultrasound, usArchitecture, "ultrasound", words.ultrasound, "D02", false);
            Add("XRay", "xray", words.xray, "D03", new Vector3(0, 0, -4.85f), 180, false);
            Add("Laboratory", "", words.lab, "D04", new Vector3(.8f, 0, 4.85f), 0, true);
            Add("MRI_MRCP", "mri", words.mri, "D05", new Vector3(-.8f, 0, 4.85f), 0, false);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            return new { endpoints = root.GetComponentsInChildren<DiagnosticDoorView>(true).Length, scene = scene.path, saved = true };

            void Add(string key, string destination, string title, string number, Vector3 position, float yaw, bool laboratory)
            {
                var existing = root.GetComponentsInChildren<RoomDoor>(true).SingleOrDefault(d => d.name == "Door_" + key);
                Transform dressed;
                if (existing == null)
                {
                    dressed = UnityEngine.Object.Instantiate(architecture.gameObject, hospital).transform;
                    dressed.name = "Door_" + key + " architecture";
                    dressed.SetPositionAndRotation(root.TransformPoint(position), root.rotation * Quaternion.Euler(0, yaw, 0));
                    foreach (var item in dressed.GetComponentsInChildren<Transform>(true)) GameObjectUtility.SetStaticEditorFlags(item.gameObject, 0);
                    foreach (var renderer in dressed.GetComponentsInChildren<MeshRenderer>(true))
                    {
                        renderer.lightmapIndex = -1;
                        renderer.receiveGI = ReceiveGI.LightProbes;
                        renderer.lightProbeUsage = LightProbeUsage.BlendProbes;
                    }
                    var go = new GameObject("Door_" + key, typeof(RoomDoor));
                    go.transform.SetParent(ct.transform.parent, false);
                    go.transform.SetPositionAndRotation(dressed.TransformPoint(new Vector3(-.12f, 1.13f, -.015f)), dressed.rotation);
                    existing = go.GetComponent<RoomDoor>();
                }
                else dressed = root.GetComponentsInChildren<Transform>(true).Single(t => t.name == "Door_" + key + " architecture");
                dressed.SetPositionAndRotation(root.TransformPoint(position), root.rotation * Quaternion.Euler(0, yaw, 0));
                existing.transform.SetPositionAndRotation(dressed.TransformPoint(new Vector3(-.12f, 1.13f, -.015f)), dressed.rotation);
                Bind(existing, dressed, destination, title, number, laboratory);
            }

            void Bind(RoomDoor door, Transform dressed, string destination, string title, string number, bool laboratory)
            {
                door.Restricted = true;
                door.ArchitecturalSign = true;
                if (door == ct) door.Title = "CT SCAN";
                else if (door != ultrasound) door.Title = title;
                var label = dressed.Find("Room title").GetComponent<TMP_Text>();
                label.text = title;
                label.fontSize = title.Length > 12 ? .69f : .83f;
                dressed.Find("Room number").GetComponent<TMP_Text>().text = number;
                var view = door.GetComponent<DiagnosticDoorView>();
                if (view == null) view = door.gameObject.AddComponent<DiagnosticDoorView>();
                view.Presentation = copy;
                view.PanelOffset = laboratory || destination == "mri" ? new Vector3(0, .6f, -.12f) : new Vector3(1.14f, .55f, -.12f);
                view.Laboratory = laboratory;
                view.DestinationIds = laboratory ? Array.Empty<string>() : new[] { destination };
                view.OutsideAnchor = Anchor("DOOR_" + (laboratory ? "lab" : destination) + "_Outside", -.8f);
                view.InsideAnchor = Anchor("DOOR_" + (laboratory ? "lab" : destination) + "_Inside", .65f);
                EditorUtility.SetDirty(door);
                EditorUtility.SetDirty(view);

                Transform Anchor(string name, float z)
                {
                    var anchor = door.transform.Find(name);
                    if (anchor == null) { anchor = new GameObject(name).transform; anchor.SetParent(door.transform, false); }
                    anchor.localPosition = new Vector3(0, -door.transform.position.y, z);
                    return anchor;
                }
            }
        }
    }
}