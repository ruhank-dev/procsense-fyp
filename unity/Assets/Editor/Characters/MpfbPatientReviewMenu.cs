#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ProcSense.EditorTools
{
    public static class MpfbPatientReviewMenu
    {
        public const string ScenePath = "Assets/Scenes/MPFBPatientReview.unity";

        [MenuItem("ProcSense/Characters/Open MPFB Patient Review")]
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
            EditorSceneManager.OpenScene(ScenePath);
        }
    }
}
#endif
