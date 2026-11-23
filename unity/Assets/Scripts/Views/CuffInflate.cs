using UnityEngine;

namespace ProcSense.VR.Views
{
    /// <summary>P3-ANI-08: the wrapped BP cuff swells and relaxes while it is on the arm (it exists only while the engine has the sensor attached). Cosmetic; it reads and changes no clinical state.</summary>
    public sealed class CuffInflate : MonoBehaviour
    {
        public const float Inflate = 4f, Hold = 1f, Deflate = 3f, Rest = 6f, Swell = .12f;
        private Vector3 baseScale; private float clock;

        /// <summary>Scale factor (1 = resting) at a point in the cycle.</summary>
        public static float Factor(float t)
        {
            t = Mathf.Repeat(t, Inflate + Hold + Deflate + Rest);
            if (t < Inflate) return 1f + Swell * Mathf.SmoothStep(0, 1, t / Inflate);
            t -= Inflate; if (t < Hold) return 1f + Swell;
            t -= Hold; if (t < Deflate) return 1f + Swell * (1f - Mathf.SmoothStep(0, 1, t / Deflate));
            return 1f;
        }

        private void OnEnable() { baseScale = transform.localScale; clock = 0; }
        private void OnDisable() { transform.localScale = baseScale; }
        private void Update() { clock += Time.deltaTime; transform.localScale = baseScale * Factor(clock); }
    }
}
