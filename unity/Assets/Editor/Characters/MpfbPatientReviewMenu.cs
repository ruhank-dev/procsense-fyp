#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ProcSense.EditorTools
{
    public static class MpfbPatientReviewMenu
    {
        public const string ScenePath = "Assets/Scenes/PatientBay.unity";

        [MenuItem("ProcSense/Characters/Open MPFB Patient")]
        public static void Open()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning("Stop Play before switching patient review scenes.");
                return;
            }
            if (!File.Exists(ScenePath))
            {
                Debug.LogWarning("The MPFB review scene has not been prepared yet.");
                return;
            }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            WorkspaceMenu.OpenPatientBay();
        }
    }
}
#endif
