using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ProcSense.VR.Boot
{
    /// <summary>
    /// Boot scene: loads Sys_Core (session, player rig and room loader) first, then the bay and the Editor simulator.
    /// Sys_Core goes first so the Session already exists when the views wake up.
    /// </summary>
    public class BootLoader : MonoBehaviour
    {
        [SerializeField] private string[] scenes = { "Sys_Core", "PatientBay" };
        [SerializeField] private string editorOnlyScene = "DevOnly";

        private IEnumerator Start()
        {
            foreach (var scene in scenes) yield return Load(scene);
            if (Application.isEditor && !Application.isBatchMode && !string.IsNullOrEmpty(editorOnlyScene)) yield return Load(editorOnlyScene);
        }

        private static IEnumerator Load(string scene)
        {
            if (SceneManager.GetSceneByName(scene).isLoaded) yield break;
            var op = SceneManager.LoadSceneAsync(scene, LoadSceneMode.Additive);
            while (op != null && !op.isDone) yield return null;
        }
    }
}
