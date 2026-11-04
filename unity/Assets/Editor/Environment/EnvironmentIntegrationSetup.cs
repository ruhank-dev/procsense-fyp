using System;
using System.Linq;
using ProcSense.VR.ActionEmitters;
using ProcSense.VR.Records;
using ProcSense.VR.Views;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace ProcSense.EditorTools
{
    /// <summary>Reconciles our authored presentation owners with the incoming environment. Never rebuilds a room or lighting.</summary>
    public static class EnvironmentIntegrationSetup
    {
        [MenuItem("ProcSense/Setup/Reconcile environment integration")]
        public static void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || Lightmapping.isRunning ||
                Enumerable.Range(0, SceneManager.sceneCount).Any(i => SceneManager.GetSceneAt(i).isDirty))
                throw new InvalidOperationException("Stop Play/baking and preserve unsaved scenes first.");
            var bay = SceneManager.GetSceneByName("PatientBay"); var ot = SceneManager.GetSceneByName("OT");
            if (!bay.isLoaded || !ot.isLoaded) throw new InvalidOperationException("Open PatientBay and OT first.");
            if (!bay.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<RecordsWorkstation>(true)).Any()) RecordsWorkstationSetup.Place();
            if (!bay.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<ProcSense.VR.UI.ClinicalPanelStation>(true)).Any()) ClinicalPanelSetup.Place();
            if (!bay.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<NurseWristbandController>(true)).Any()) NurseVisitSetup.Install();
            BindPatient();
            EditorSceneManager.MarkSceneDirty(bay); EditorSceneManager.SaveScene(bay);
        }

        public static void BindPatient()
        {
            var patient = GameObject.Find("Rocketbox patient");
            var old = GameObject.Find("MPFB patient");
            if (patient == null || old == null) throw new InvalidOperationException("Both authored patient rigs must be present.");
            var bones = patient.GetComponentsInChildren<Transform>(true).GroupBy(t => t.name).ToDictionary(g => g.Key, g => g.First());
            var oldBones = old.GetComponentsInChildren<Transform>(true).GroupBy(t => t.name).ToDictionary(g => g.Key, g => g.First());
            Transform Bone(string name) => bones.TryGetValue(name, out var t) ? t : throw new InvalidOperationException("Missing visible bone: " + name);
            var transport = Object.FindAnyObjectByType<PatientTransportView>();
            if (transport == null) throw new InvalidOperationException("Missing transport owner.");
            transport.AdditionalBedsideVisuals = new[] { patient.transform }; EditorUtility.SetDirty(transport);
            // Map each attachment from the old anatomical segment to the new segment. Preserve world size and the original model's fitting offset.
            void Fit(Transform target, Transform previousStart, Transform previousEnd, Transform start, Transform end)
            {
                if (target.IsChildOf(patient.transform)) return; // already fitted: never accumulate offsets
                var from = previousEnd.position - previousStart.position; var to = end.position - start.position;
                var rotation = Quaternion.FromToRotation(from, to);
                var offset = target.position - previousStart.position;
                float along = Vector3.Dot(offset, from.normalized);
                var radial = offset - from.normalized * along;
                var position = start.position + to.normalized * (along * to.magnitude / from.magnitude) + rotation * radial;
                target.SetParent(start, true); target.SetPositionAndRotation(position, rotation * target.rotation);
                PrefabUtility.RecordPrefabInstancePropertyModifications(target);
            }
            foreach (var sensor in Object.FindObjectsByType<SensorAttachment>(FindObjectsSortMode.None))
            {
                bool cuff = new SerializedObject(sensor).FindProperty("paramValue").stringValue == "nibp_cuff";
                Fit(sensor.Destination.transform,
                    oldBones[cuff ? "upperarm_l" : "index_02_l"], oldBones[cuff ? "lowerarm_l" : "index_03_l"],
                    Bone(cuff ? "Bip01 L UpperArm" : "Bip01 L Finger11"), Bone(cuff ? "Bip01 L Forearm" : "Bip01 L Finger12"));
            }
            var wrist = GameObject.Find("Patient wristband")?.transform;
            if (wrist == null) throw new InvalidOperationException("Missing physical band.");
            Fit(wrist, oldBones["hand_l"], oldBones["lowerarm_l"], Bone("Bip01 L Hand"), Bone("Bip01 L Forearm"));
            var nurse = Object.FindAnyObjectByType<NurseWristbandController>();
            if (nurse != null) { nurse.Wrist = wrist; EditorUtility.SetDirty(nurse); }
        }
    }
}
