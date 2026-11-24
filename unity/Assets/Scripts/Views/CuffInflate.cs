using ProcSense.Core.Actions;
using UnityEngine;

namespace ProcSense.VR.Views
{
    /// <summary>P3-ANI-08: the wrapped BP cuff swells and relaxes once per accepted blood pressure measurement, in step with the pump sound (3 s of pumping, 2 s of bleed). It exists only while the engine has the cuff attached. Cosmetic.</summary>
    public sealed class CuffInflate : MonoBehaviour
    {
        public const float Pump = 3f, Bleed = 2f, Swell = .12f;
        private Vector3 baseScale; private float clock = float.PositiveInfinity;
        private ProcSense.VR.Bridge.SimulationHost host;

        /// <summary>Scale factor (1 = resting) this many seconds after the measurement started.</summary>
        public static float Factor(float t)
        {
            if (t < 0 || t >= Pump + Bleed) return 1f;
            return 1f + Swell * (t < Pump ? Mathf.SmoothStep(0, 1, t / Pump) : 1f - Mathf.SmoothStep(0, 1, (t - Pump) / Bleed));
        }

        public void Begin() { clock = 0; }

        private void OnEnable() { baseScale = transform.localScale; host = ProcSense.VR.Bridge.SimulationHost.Instance; if (host != null) host.Applied += OnApplied; }
        private void OnDisable() { if (host != null) host.Applied -= OnApplied; transform.localScale = baseScale; clock = float.PositiveInfinity; }
        private void OnApplied(SimAction action, ActionOutcome outcome) { if (action.Verb == "measure_nibp" && outcome != null && outcome.Accepted) Begin(); }

        private void Update()
        {
            if (float.IsPositiveInfinity(clock)) return;
            clock += Time.deltaTime; transform.localScale = baseScale * Factor(clock);
            if (clock >= Pump + Bleed) clock = float.PositiveInfinity;
        }
    }
}
