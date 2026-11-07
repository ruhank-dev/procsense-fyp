using System.Collections.Generic;
using UnityEngine;

namespace ProcSense.VR.Views
{
    /// <summary>
    /// Presentation-only sunlight extras (sun shafts and dust motes) and their visibility on Android / Quest.
    /// History: until 7 November 2026 (UTC) this component switched its host off on Android so the headset never drew the effects (DC-12, "desktop only"). The owner decided (D7, 7 November 2026 UTC) that the
    /// approved look includes them on Quest; their shaders were made stereo-safe for single-pass instanced / Vulkan multiview at the same time (P3-PRF-11). The class keeps its name so the four scene
    /// hosts (bay and corridor "Door sun shafts", corridor and OT "Sun shafts") keep their reference without a scene change.
    /// <see cref="ShowOnAndroid"/> is the tier switch: true by default; the diagnostic build (PROCSENSE_PERF_DIAG) turns it off and on to measure the effects' cost. Desktop and Editor are never affected.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DesktopOnly : MonoBehaviour
    {
        private static readonly List<DesktopOnly> Live = new List<DesktopOnly>();
        private static bool _showOnAndroid = true;

        /// <summary>Whether the effects draw on Android. Changing it applies to every live host at once.</summary>
        public static bool ShowOnAndroid
        {
            get => _showOnAndroid;
            set { _showOnAndroid = value; foreach (var h in Live) if (h != null) h.Apply(); }
        }

        /// <summary>The visibility rule: the Editor and desktop always draw the effects; Android draws them when the tier switch allows it.</summary>
        public static bool Visible(RuntimePlatform platform, bool showOnAndroid) => platform != RuntimePlatform.Android || showOnAndroid;

        // each child's authored active state, read once: switching back on restores exactly what the scene had (some prisms or dust systems are authored off)
        private readonly Dictionary<Transform, bool> _authored = new Dictionary<Transform, bool>();

        private void Awake()
        {
            foreach (Transform child in transform) _authored[child] = child.gameObject.activeSelf;
            Live.Add(this);
            Apply();
        }

        private void OnDestroy() => Live.Remove(this);

        private void Apply()
        {
            bool on = Visible(Application.platform, _showOnAndroid);
            foreach (var kv in _authored) if (kv.Key != null) kv.Key.gameObject.SetActive(on && kv.Value);
        }
    }
}
