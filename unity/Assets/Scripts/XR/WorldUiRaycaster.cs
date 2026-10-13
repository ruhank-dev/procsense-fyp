using ProcSense.VR.Views;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.UI;

namespace ProcSense.VR.XR
{
    /// <summary>Lets controller rays and pokes press the world-space canvases UiKit builds (tablet, monitor).</summary>
    public static class WorldUiRaycaster
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Hook()
        {
            UiKit.CanvasCreated -= Add;
            UiKit.CanvasCreated += Add;
        }

        private static void Add(Canvas canvas)
        {
            if (canvas.GetComponent<TrackedDeviceGraphicRaycaster>() == null) canvas.gameObject.AddComponent<TrackedDeviceGraphicRaycaster>();
        }
    }
}
