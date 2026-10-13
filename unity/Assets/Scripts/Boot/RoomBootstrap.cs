using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ProcSense.VR.Boot
{
    /// <summary>Allows a room opened directly to Play without a separate camera/rig copy.</summary>
    public sealed class RoomBootstrap : MonoBehaviour
    {
        private IEnumerator Start()
        {
            // Normal Boot and navigation already own their services; never reload them on room entry.
            if (SceneManager.GetSceneByName("Sys_Core").isLoaded) yield break;
            var room = gameObject.scene;
            yield return SceneManager.LoadSceneAsync("Sys_Core", LoadSceneMode.Additive);
            var loader = FindAnyObjectByType<RoomLoader>();
            if (loader != null) loader.InitializeOpenedRoom(room);
            if (Application.isEditor && !Application.isBatchMode && !SceneManager.GetSceneByName("DevOnly").isLoaded)
                yield return SceneManager.LoadSceneAsync("DevOnly", LoadSceneMode.Additive);
            SceneManager.SetActiveScene(room);
        }
    }
}
