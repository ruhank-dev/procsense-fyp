using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ProcSense.EditorTools
{
    /// <summary>Opens the editable room and its supporting scenes for ordinary Unity editing and Play.</summary>
    [InitializeOnLoad]
    public static class WorkspaceMenu
    {
        static WorkspaceMenu()
        {
            EditorApplication.playModeStateChanged += state =>
            {
                if (state == PlayModeStateChange.ExitingPlayMode) ClearDiagnosticsPreviews();
            };
            AssemblyReloadEvents.beforeAssemblyReload += ClearDiagnosticsPreviews;
        }

        private static void ClearDiagnosticsPreviews()
        {
            foreach (var preview in Resources.FindObjectsOfTypeAll<ProcSense.VR.Views.DiagnosticsPreview>())
                if (!EditorUtility.IsPersistent(preview)) Object.DestroyImmediate(preview.gameObject);
        }
        public const string BootPath = "Assets/Scenes/Boot.unity";
        public const string BayPath = "Assets/Scenes/PatientBay.unity";

        [MenuItem("ProcSense/Open Patient Bay", false, 0)]
        public static void OpenPatientBay()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorSceneManager.OpenScene("Assets/Scenes/Sys_Core.unity", OpenSceneMode.Single);
            var bay = EditorSceneManager.OpenScene(BayPath, OpenSceneMode.Additive);
            EditorSceneManager.OpenScene("Assets/Scenes/DevOnly.unity", OpenSceneMode.Additive);
            SceneManager.SetActiveScene(bay);
            // Use Unity's normal Play behaviour for the open scenes, including the Test Runner's own scene.
            EditorSceneManager.playModeStartScene = null;
            Selection.activeObject = GameObject.Find("PatientBay");
            var view = EditorWindow.GetWindow<SceneView>();
            view.Show();
            view.Focus();
            view.in2DMode = false;
            // Keep the Scene camera inside the enclosed bay rather than behind its front wall.
            view.LookAt(new Vector3(-0.4f, 1.2f, 0.9f), Quaternion.Euler(12, 0, 0), 0.6f, false, true);
            view.Repaint();
        }

        [MenuItem("ProcSense/Open Patient Bay", true)]
        private static bool CanOpenPatientBay() => !EditorApplication.isPlayingOrWillChangePlaymode;

        [MenuItem("ProcSense/Development/Diagnostics Preview (Play Mode)")]
        public static void OpenDiagnosticsPreview()
        {
            if (!EditorApplication.isPlaying) return;
            var camera = Camera.main;
            if (camera == null) return;
            // FindAnyObjectByType excludes DontSave objects, including this preview.
            var preview = System.Array.Find(Resources.FindObjectsOfTypeAll<ProcSense.VR.Views.DiagnosticsPreview>(),
                p => p.gameObject.scene.IsValid());
            if (preview == null)
            {
                var go = new GameObject("Diagnostics development preview")
                { hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild };
                preview = go.AddComponent<ProcSense.VR.Views.DiagnosticsPreview>();
            }
            preview.transform.SetPositionAndRotation(camera.transform.position + camera.transform.forward * 0.9f, camera.transform.rotation);
        }

        [MenuItem("ProcSense/Development/Diagnostics Preview (Play Mode)", true)]
        private static bool CanPreviewDiagnostics() => EditorApplication.isPlaying;
    }
}
