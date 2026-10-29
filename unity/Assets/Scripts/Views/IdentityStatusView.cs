using System.Collections.Generic;
using ProcSense.Core.Actions;
using ProcSense.Core.Observation;
using ProcSense.VR.Bridge;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ProcSense.VR.Views
{
    /// <summary>
    /// The identity banner on the tablet (p0-vr-interaction-ui-s12). In practice it says "Identity not confirmed" until Core confirms it, then that it is confirmed; in both
    /// modes it follows the nurse's visit after a mismatch is reported or a band is asked for, and says when the band is missing. Core does the visit at once (owner decision 09 Oct 2026) and lists
    /// its steps in the action's effects (nurse:CALLED, AT_BEDSIDE, REPLACING, DONE); the banner shows them one after another so the trainee sees the visit happen, and then asks
    /// for the new band to be read. A missing band shows its own line. All the words come from content/ui/tablet.json (identity_status). Reads state, applies nothing.
    /// </summary>
    public sealed class IdentityStatusView : StateView
    {
        /// <summary>How long each step of the nurse's visit stays on the chip.</summary>
        public const float StepSeconds = 1.5f;
        public static readonly string[] NurseSteps = { "CALLED", "AT_BEDSIDE", "REPLACING", "DONE" };

        public string BannerText => _banner != null ? _banner.text : "";
        /// <summary>The nurse step the banner shows now ("" when no visit is on).</summary>
        public string NurseStep { get; private set; } = "";
        public Color BannerColor => _back != null ? _back.color : Color.clear;

        public static readonly Color NotConfirmed = new Color(.55f, .12f, .12f), Confirmed = new Color(.12f, .42f, .22f), Waiting = new Color(.45f, .32f, .05f);

        private TMP_Text _banner;
        private Image _back;
        private SimulationHost _host;
        private readonly Queue<string> _steps = new Queue<string>();
        private float _nextStep;
        private bool _reRead, _bandMissing;

        private static IReadOnlyDictionary<string, string> Words => TabletView.Layout.IdentityStatus;
        private static string W(string key) => Words.TryGetValue(key, out var w) ? w : key;

        /// <summary>Draws the banner at the top right of the tablet, beside the hint chip.</summary>
        public void Build(Transform root)
        {
            if (_banner != null) return;
            _back = UiKit.Panel(root, "Identity banner", new Vector2(630, 4), new Vector2(262, 38), Color.clear).GetComponent<Image>();
            _banner = UiKit.Label(root, "Identity banner text", "", new Vector2(636, 5), new Vector2(250, 36), 15, TextAlignmentOptions.Center);
            _banner.verticalAlignment = VerticalAlignmentOptions.Middle;
            Bind();
        }
        protected override void OnEnable() { base.OnEnable(); Bind(); }
        protected override void OnDisable()
        {
            base.OnDisable();
            if (_host != null) _host.Applied -= OnApplied;
            _host = null;
        }

        private void Bind()
        {
            var host = SimulationHost.Instance;
            if (ReferenceEquals(host, _host)) return;
            if (_host != null) _host.Applied -= OnApplied;
            _host = host;
            _steps.Clear(); NurseStep = ""; _reRead = _bandMissing = false;
            if (_host != null) _host.Applied += OnApplied;
        }

        private void OnApplied(SimAction action, ActionOutcome outcome)
        {
            if (action.Verb == "read_wristband" && outcome.RejectReason == "WRISTBAND_MISSING") { _bandMissing = true; RenderNow(); }   // stays until the nurse has put one on
            if (!outcome.Accepted) return;
            foreach (var e in outcome.Effects)
                if (e.StartsWith("nurse:", System.StringComparison.Ordinal)) _steps.Enqueue(e.Substring(6));
            if (_steps.Count > 0 && NurseStep.Length == 0) Advance();
            if (action.Verb == "read_wristband") _reRead = false;
        }

        private void Advance()
        {
            NurseStep = _steps.Count > 0 ? _steps.Dequeue() : "";
            _nextStep = Time.unscaledTime + StepSeconds;
            if (NurseStep == "DONE") { _reRead = true; _bandMissing = false; }
            RenderNow();
        }

        protected override void Update()
        {
            Bind();
            base.Update();
            if (NurseStep.Length > 0 && NurseStep != "DONE" && Time.unscaledTime >= _nextStep) Advance();
        }

        /// <summary>Shows the rest of the nurse's visit at once (tests, and a trainee who skips ahead).</summary>
        public void FinishVisit()
        {
            while (_steps.Count > 0) Advance();
        }

        protected override void Render(ObservableState s)
        {
            if (_banner == null) return;
            var host = SimulationHost.Instance;
            bool practice = host != null && host.Session != null && host.Session.Mode == ProcSense.Core.SessionMode.Practice;
            string text;
            Color color;
            if (NurseStep.Length > 0 && NurseStep != "DONE") { text = W("nurse_" + NurseStep.ToLowerInvariant()); color = Waiting; }
            else if (_reRead && !s.IdentityConfirmed) { text = W("read_new_band"); color = Waiting; }
            else if (_bandMissing && !s.IdentityConfirmed) { text = W("band_missing"); color = Waiting; }
            else if (!practice) { text = ""; color = Color.clear; }   // assessment: no banner that gives the answer away
            else if (s.IdentityConfirmed) { text = W("confirmed"); color = Confirmed; }
            else { text = W("not_confirmed"); color = NotConfirmed; }
            SetText(_banner, text);
            _back.color = color;
        }
    }
}
