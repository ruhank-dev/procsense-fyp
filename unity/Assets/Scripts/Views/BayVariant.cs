using System.Collections.Generic;
using System.Linq;
using ProcSense.VR.Bridge;
using UnityEngine;

namespace ProcSense.VR.Views
{
    /// <summary>
    /// P3-AST-08: the bay looks like where the case happens. Cases set in the emergency department get the ED dressing (the headwall sign, the triage chart) and lose the calm pre-assessment wall art; every other setting keeps the
    /// pre-assessment dressing. Two groups of scene objects, switched when a session starts. Presentation only: it reads the case's setting and decides nothing clinical.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BayVariant : MonoBehaviour
    {
        [Tooltip("Shown for pre-assessment and ward cases (the calm wall art, the quiet-please poster).")] public List<GameObject> quiet = new List<GameObject>();
        [Tooltip("Shown for emergency department cases.")] public List<GameObject> emergency = new List<GameObject>();
        public bool Emergency { get; private set; }
        private object session;

        /// <summary>True for the setting the cases name "emergency department" (case-insensitive; "ED" and "A&E" count too).</summary>
        public static bool IsEmergency(string setting)
        {
            if (string.IsNullOrWhiteSpace(setting)) return false;
            var s = setting.Trim().ToLowerInvariant();
            return s.Contains("emergency") || s == "ed" || s == "a&e";
        }

        public void Apply(string setting)
        {
            Emergency = IsEmergency(setting);
            foreach (var g in quiet) if (g != null) g.SetActive(!Emergency);
            foreach (var g in emergency) if (g != null) g.SetActive(Emergency);
        }

        private void Update()
        {
            var host = SimulationHost.Instance; var now = host != null ? host.Session : null;
            if (ReferenceEquals(now, session)) return;
            session = now;
            if (now != null && host.Content != null && host.Content.CaseFiles.TryGetValue(now.Case.Id, out var file)) Apply(file.Presentation.Setting);
        }
    }
}
