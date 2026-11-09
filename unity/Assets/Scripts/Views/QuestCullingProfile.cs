using UnityEngine;
using UnityEngine.Rendering;

namespace ProcSense.VR.Views
{
    /// <summary>
    /// Quest culling profile (P3-17, 9 Nov 2026). The baked occlusion data (PatientBay/OcclusionCullingData.asset) saved little next to room-level and window-cone culling
    /// (RoomVisibility, ExteriorVisibility) and had a visible side effect: it drops some of the corridor's realtime ceiling-fill lights from the per-object light list, which shifts
    /// the ceiling shading near light fixtures (found by the per-pose comparison, docs/qa/quest2_optimisation_round2.md). On Android no camera uses the baked occlusion data
    /// unless <see cref="OcclusionEnabled"/> is switched on; desktop and the Editor keep the authored behaviour. The data asset stays in the project so the choice is reversible.
    /// </summary>
    public static class QuestCullingProfile
    {
        private static bool? _occlusion;

        /// <summary>The platform default: off on Android (the Quest tier), on elsewhere.</summary>
        public static bool OcclusionDefault => !Application.isMobilePlatform;

        /// <summary>Whether cameras may use the baked occlusion data.</summary>
        public static bool OcclusionEnabled { get => _occlusion ?? OcclusionDefault; set => _occlusion = value; }

        public static void ResetToDefault() { _occlusion = null; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Boot()
        {
            RenderPipelineManager.beginCameraRendering -= OnBegin;
            RenderPipelineManager.beginCameraRendering += OnBegin;
        }

        private static void OnBegin(ScriptableRenderContext context, Camera cam)
        {
            if (!OcclusionEnabled && cam.useOcclusionCulling) cam.useOcclusionCulling = false;
        }
    }
}
