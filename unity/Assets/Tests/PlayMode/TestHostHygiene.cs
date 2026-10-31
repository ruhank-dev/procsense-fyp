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
