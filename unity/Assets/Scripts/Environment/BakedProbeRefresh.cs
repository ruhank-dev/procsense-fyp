using UnityEngine;
using UnityEngine.SceneManagement;

namespace ProcSense.VR.Environment
{
    /// <summary>Keep baked illumination on movable objects valid when additive rooms change.</summary>
    public static class BakedProbeRefresh
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Register()
        {
            // Domain-reload-disabled Play must not accumulate event handlers.
            LightProbes.needsRetetrahedralization -= Refresh;
            LightProbes.needsRetetrahedralization += Refresh;
            SceneManager.activeSceneChanged -= ActiveRoomChanged;
            SceneManager.activeSceneChanged += ActiveRoomChanged;
        }

        private static void ActiveRoomChanged(Scene previous, Scene current) => Refresh();

        private static void Refresh()
        {
            var probes = LightmapSettings.lightProbes;
            if (probes != null && probes.count >= 4) LightProbes.TetrahedralizeAsync();
        }
    }
}
