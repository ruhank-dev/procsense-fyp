using System.Collections.Generic;
using ProcSense.VR.Audio;
using ProcSense.VR.Diagnostics;
using ProcSense.VR.Records.Emr;
using ProcSense.VR.Records.Folder;
using ProcSense.VR.UI.Checklist;
using ProcSense.VR.UI.Debrief;
using ProcSense.VR.UI.Results;
using UnityEditor;
using UnityEngine;

namespace ProcSense.EditorTools
{
    /// <summary>
    /// Puts the panels that are not placed in any scene yet (DT-266) around the player during Play, so they can be tried in the Editor before Sannan places them for good: the EMR shell, the results
    /// viewer and the critical-result phone, the WHO board and the ward checklist, the debrief, the waiting overlay, the voice player with its subtitles, and a report tray on the bay's desk. They exist
    /// only while Play lasts (Play mode objects are never saved), so no scene is changed. ProcSense > Review > Place review panels (in Play), or switch on "Place review panels on Play".
    /// </summary>
    [InitializeOnLoad]
    public static class ReviewPanels
    {
        private const string MenuPath = "ProcSense/Review/";
        private const string AutoKey = "ProcSense.ReviewPanels.Auto";
        public const string RootName = "Review panels (not saved)";

        private static object _placedFor;

        // With "on Play" switched on, the panels are placed as soon as a case is running, and again whenever a new case starts (the EMR's tabs and the debrief are built from the case).
        static ReviewPanels()
        {
            EditorApplication.update += () =>
            {
                if (!Application.isPlaying) { _placedFor = null; return; }
                if (!EditorPrefs.GetBool(AutoKey, false)) return;
                var session = ProcSense.VR.Bridge.SimulationHost.Instance != null ? ProcSense.VR.Bridge.SimulationHost.Instance.Session : null;
                if (session != null && !ReferenceEquals(session, _placedFor)) Place();
            };
        }

        [MenuItem(MenuPath + "Place review panels", false, 1)]
        public static void PlaceMenu()
        {
            if (ProcSense.VR.Bridge.SimulationHost.Instance?.Session == null)
                Debug.LogWarning("[ProcSense] start a case first (the EMR's tabs and the debrief are built from the running case); placing the panels anyway");
            Place();
        }

        [MenuItem(MenuPath + "Place review panels", true)]
        private static bool CanPlace() => Application.isPlaying;

        [MenuItem(MenuPath + "Place review panels on Play", false, 2)]
        private static void ToggleAuto() => EditorPrefs.SetBool(AutoKey, !EditorPrefs.GetBool(AutoKey, false));

        [MenuItem(MenuPath + "Place review panels on Play", true)]
        private static bool ToggleAutoCheck()
        {
            Menu.SetChecked(MenuPath + "Place review panels on Play", EditorPrefs.GetBool(AutoKey, false));
            return true;
        }

        [MenuItem(MenuPath + "Refresh the debrief", false, 3)]
        private static void RefreshDebrief()
        {
            foreach (var d in Object.FindObjectsByType<DebriefView>(FindObjectsSortMode.None)) d.Refresh();
        }

        [MenuItem(MenuPath + "Refresh the debrief", true)]
        private static bool CanRefresh() => Application.isPlaying;

        /// <summary>
        /// The panels in a ring around where the player stands, each facing the player: to the left the record (EMR shell) and the results with the phone, ahead the WHO board and the ward checklist,
        /// to the right the debrief. The waiting overlay and the subtitles follow the camera. Placing again replaces the earlier set. Returns the root (null outside Play).
        /// </summary>
        public static GameObject Place()
        {
            if (!Application.isPlaying) return null;
            var old = GameObject.Find(RootName);
            if (old != null) Object.Destroy(old);
            var cam = Camera.main != null ? Camera.main.transform : null;
            Vector3 eye = cam != null ? cam.position : new Vector3(0, 1.6f, 0);
            Vector3 ahead = cam != null ? Vector3.ProjectOnPlane(cam.forward, Vector3.up).normalized : Vector3.forward;
            if (ahead.sqrMagnitude < 0.01f) ahead = Vector3.forward;
            var root = new GameObject(RootName);
            _placedFor = ProcSense.VR.Bridge.SimulationHost.Instance != null ? ProcSense.VR.Bridge.SimulationHost.Instance.Session : null;

            void At<T>(string name, float yaw, float distance, float height, System.Action<T> setup = null) where T : Component
            {
                var go = new GameObject(name);
                go.transform.SetParent(root.transform, false);
                var dir = Quaternion.Euler(0, yaw, 0) * ahead;
                go.transform.position = new Vector3(eye.x, 0, eye.z) + dir * distance + Vector3.up * height;
                go.transform.rotation = Quaternion.LookRotation(dir, Vector3.up);      // a world canvas reads from behind its forward: the player looks along dir
                var c = go.AddComponent<T>();
                setup?.Invoke(c);
            }

            At<EmrShell>("EMR shell", -70f, 1.6f, 1.45f);
            At<ResultsView>("Results viewer", -35f, 1.7f, 1.5f);
            At<PhoneView>("Phone", -35f, 1.5f, 1.0f);
            At<ChecklistBoard>("WHO board", 0f, 2.2f, 1.6f);
            At<WardChecklistPanel>("Ward checklist", 25f, 1.8f, 1.45f);
            At<DebriefView>("Debrief", 60f, 1.9f, 1.5f);
            At<ReportTray>("Report tray", -10f, 0.9f, 0.85f);

            var hud = new GameObject("Waiting overlay and subtitles");
            hud.transform.SetParent(root.transform, false);
            if (cam != null) { hud.transform.SetParent(cam, false); hud.transform.localPosition = new Vector3(0, 0.15f, 1.2f); hud.transform.localRotation = Quaternion.identity; }
            hud.AddComponent<WaitOverlay>();
            var voice = new GameObject("Voice player");
            voice.transform.SetParent(root.transform, false);
            var player = voice.AddComponent<VoicePlayer>();
            if (cam != null) player.Subtitles.Follow = cam;

            Debug.Log("[ProcSense] review panels placed around the player (left: EMR and results with the phone; ahead: WHO board and ward checklist; right: debrief). They are not saved.");
            return root;
        }
    }
}
