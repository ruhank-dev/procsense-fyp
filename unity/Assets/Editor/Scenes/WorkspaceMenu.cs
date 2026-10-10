using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ProcSense.EditorTools
{
    /// <summary>Opens the editable room and its supporting scenes for ordinary Unity editing and Play.</summary>
    public static class WorkspaceMenu
    {
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
    }
}
