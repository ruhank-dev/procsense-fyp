using ProcSense.VR.Bridge;
using UnityEngine;
using UnityEngine.EventSystems;

namespace ProcSense.Tests.PlayMode
{
    /// <summary>
    /// A test that loads the full room leaves the scene's persistent objects alive (the SimulationHost, the XR rig with its camera, the EventSystem). SimulationHost deliberately destroys a duplicate, so a later
    /// test that creates its own host would silently send its actions to the leftover one, and tests that count cameras or expect no host see extras. Fixtures that build their own host, and fixtures that load
    /// the whole room, call these so every fixture starts from a clean singleton.
    /// </summary>
    public static class TestHostHygiene
    {
        public static void DestroyLeftoverHost()
        {
            var host = SimulationHost.Instance;
            if (host == null) return;
            host.SaveLogOnDestroy = false;
            Object.DestroyImmediate(host.gameObject);
        }

        /// <summary>Destroys every persistent (DontDestroyOnLoad) root that holds a camera, a simulation host or an event system. The test runner's own persistent objects hold none of these.</summary>
        /// <summary>Waits the given minimum number of frames and then until the scene host exists (up to 900 frames): the rooms now carry the Rocketbox people, the clothed patient and eight lightmaps and load more slowly than the fixed 30-60 frame waits the older fixtures used.</summary>
        public static System.Collections.IEnumerator WaitForHost(int minFrames)
        {
            for (int i = 0; i < minFrames; i++) yield return null;
            for (int i = 0; i < 900 && ProcSense.VR.Bridge.SimulationHost.Instance == null; i++) yield return null;
        }

        /// <summary>After a sign-in or boot: waits for the host and the bay, then until the set of loaded scenes has been stable for 40 frames and no room load is running, so the BootLoader has finished adding Sys_Core, the room and DevOnly before the test ends (a load still in flight would land in the next test and duplicate Sys_Core and its camera).</summary>
        public static System.Collections.IEnumerator WaitForWorkspaceSettled()
        {
            for (int i = 0; i < 1500 && (SimulationHost.Instance == null || !UnityEngine.SceneManagement.SceneManager.GetSceneByName("PatientBay").isLoaded); i++) yield return null;
            int last = -1, stable = 0;
            for (int i = 0; i < 1500 && stable < 40; i++)
            {
                var rooms = Object.FindAnyObjectByType<ProcSense.VR.Boot.RoomLoader>();
                int n = UnityEngine.SceneManagement.SceneManager.loadedSceneCount;
                stable = (n == last && (rooms == null || !rooms.Busy)) ? stable + 1 : 0; last = n; yield return null;
            }
        }

        public static void DestroyRoomLeftovers()
        {
            DestroyLeftoverHost();
            var probe = new GameObject("persistent-probe"); Object.DontDestroyOnLoad(probe);
            foreach (var root in probe.scene.GetRootGameObjects())
            {
                if (root == probe) continue;
                if (root.GetComponentInChildren<Camera>(true) != null || root.GetComponentInChildren<SimulationHost>(true) != null || root.GetComponentInChildren<EventSystem>(true) != null)
                    Object.DestroyImmediate(root);
            }
            Object.DestroyImmediate(probe);
        }
    }
}
