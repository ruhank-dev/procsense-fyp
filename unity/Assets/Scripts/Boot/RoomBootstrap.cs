using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ProcSense.VR.Boot
{
    /// <summary>Allows a room opened directly to Play without a separate camera/rig copy.</summary>
    public sealed class RoomBootstrap : MonoBehaviour
    {
        private static AsyncOperation _coreLoad;

        private IEnumerator Start()
        {
            // Normal Boot and navigation already own their services; never reload them on room entry.
            if (SceneManager.GetSceneByName("Sys_Core").isLoaded) yield break;
            // Several connected rooms may be open together in the Editor.
            // Only the first bootstrap owns the asynchronous service load.
            if (_coreLoad != null && !_coreLoad.isDone) yield break;
            var room = SceneManager.GetActiveScene();
            if (room.name != "PatientBay" && room.name != "Corridor" && room.name != "OT") room = gameObject.scene;
            _coreLoad = SceneManager.LoadSceneAsync("Sys_Core", LoadSceneMode.Additive);
            yield return _coreLoad;
            var loader = FindAnyObjectByType<RoomLoader>();
            if (loader != null) loader.InitializeOpenedRoom(room);
            if (Application.isEditor && !Application.isBatchMode && ProcSense.VR.XR.EditorInputSettings.ShouldUseSimulator
                && !SceneManager.GetSceneByName("DevOnly").isLoaded)
                yield return SceneManager.LoadSceneAsync("DevOnly", LoadSceneMode.Additive);
            SceneManager.SetActiveScene(room);
        }
    }
}
