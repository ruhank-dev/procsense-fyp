using ProcSense.VR.Bridge;
using ProcSense.VR.Views;
using UnityEditor;
using UnityEngine;

namespace ProcSense.EditorTools
{
    /// <summary>Editor shortcuts for trying the tablet's Session tab without VR controllers (Play Mode only).</summary>
    public static class SessionDevMenu
    {
        [MenuItem("ProcSense/Development/Show tablet Session tab (Play Mode)")]
        public static void ShowSessionTab()
        {
            var tablet = Object.FindFirstObjectByType<TabletView>();
            if (tablet == null) { Debug.LogWarning("[ProcSense] No tablet found. Press Play on the application first and wait for the bay to load."); return; }
            tablet.ShowTab(TabletView.SessionTabId);
            Selection.activeObject = tablet.gameObject;
            SceneView.lastActiveSceneView?.FrameSelected();   // the Scene view looks at the tablet; the Game view shows what the player sees
        }

        [MenuItem("ProcSense/Development/Finish session and upload (Play Mode)")]
        public static void FinishSession()
        {
            var host = SimulationHost.Instance;
            if (host == null) { Debug.LogWarning("[ProcSense] No session is running. Press Play on the application first."); return; }
            string file = host.EndSession();
            Debug.Log(file == null ? "[ProcSense] Nothing to finish yet (no actions) or already finished." : "[ProcSense] Session saved to " + file + " and queued for the signed-in person.");
        }

        [MenuItem("ProcSense/Development/Show tablet Session tab (Play Mode)", true)]
        [MenuItem("ProcSense/Development/Finish session and upload (Play Mode)", true)]
        private static bool OnlyInPlayMode() => EditorApplication.isPlaying;
    }
}
