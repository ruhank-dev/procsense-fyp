using System;
using System.Linq;
using ProcSense.Core.Actions;
using ProcSense.Core.Observation;
using ProcSense.VR.Bridge;
using TMPro;
using UnityEngine;

namespace ProcSense.VR.Views
{
    /// <summary>Nonblocking public diagnostic status. Never ticks Core, opens a result or acknowledges a call.</summary>
    public sealed class DiagnosticWaitOverlay : StateView
    {
        /// <summary>How long the time-skip note stays on screen, from content (wait_overlay.seconds).</summary>
        public float AdvanceSeconds => (float)(SimulationHost.Instance?.Content?.Diagnostics.WaitOverlaySeconds ?? 10);
        public Canvas Canvas { get; private set; }
        public string LocationText => _location != null ? _location.text : "";
        public string TimingText => _timing != null ? _timing.text : "";
        public string AlertText => _alert != null ? _alert.text : "";
        public string AdvanceText { get; private set; } = "";
        private TMP_Text _location, _timing, _alert;
        private SimulationHost _appliedHost;
        private ProcSense.Core.Session _session;
        private string _interruption = "";
        private float _advanceUntil;

        public void Build()
        {
            if (Canvas != null) return;
            Canvas = UiKit.WorldCanvas("Diagnostic wait status", transform, new Vector2(900, 156), .0006f);
            Canvas.transform.localPosition = new Vector3(0, -.24f, 0);
            foreach (var raycaster in Canvas.GetComponents<UnityEngine.EventSystems.BaseRaycaster>()) raycaster.enabled = false;
            _location = UiKit.Label(Canvas.transform, "Location", "", new Vector2(14, 8), new Vector2(872, 28), 21);
            _timing = UiKit.Label(Canvas.transform, "Timing", "", new Vector2(14, 38), new Vector2(872, 50), 20, color: UiKit.DimTextColor);
            _alert = UiKit.Label(Canvas.transform, "Notification", "", new Vector2(14, 92), new Vector2(872, 56), 20, color: UiKit.WarnColor);
            Canvas.gameObject.SetActive(false);
            BindActions();
            RenderNow();
        }

        protected override void OnEnable() { base.OnEnable(); BindActions(); }
        protected override void OnDisable()
        {
            base.OnDisable();
            if (_appliedHost != null) _appliedHost.Applied -= OnApplied;
            _appliedHost = null;
            _session = null;
            _interruption = AdvanceText = "";
            if (Canvas != null) Canvas.gameObject.SetActive(false);
        }
        protected override void Update()
        {
            BindActions();
            base.Update();
            // the time-skip note stays for the content's wait (content/diagnostics/diagnostics.json wait_overlay.seconds, p0-diagnostic-transitions-s06), then the card shows the state again
            if (AdvanceText.Length > 0 && Time.unscaledTime > _advanceUntil) { AdvanceText = ""; RenderNow(); }
        }
        private void BindActions()
        {
            var host = SimulationHost.Instance;
            if (ReferenceEquals(host, _appliedHost)) return;
            if (_appliedHost != null) _appliedHost.Applied -= OnApplied;
            _appliedHost = host;
            if (_appliedHost != null) _appliedHost.Applied += OnApplied;
            else { _session = null; _interruption = AdvanceText = ""; if (Canvas != null) Canvas.gameObject.SetActive(false); }
        }
        private void ResetSession(SimulationHost host)
        {
            if (ReferenceEquals(_session, host?.Session)) return;
            _session = host?.Session;
            _interruption = AdvanceText = "";
        }
        private void OnApplied(SimAction action, ActionOutcome outcome)
        {
            ResetSession(_appliedHost);
            if (action.Verb != "time_skip" || !outcome.Accepted) return;
            AdvanceText = "Time advanced to " + _appliedHost.Observable.ClockTime;
            _advanceUntil = Time.unscaledTime + (float)(_appliedHost.Content?.Diagnostics.WaitOverlaySeconds ?? 10);
            _interruption = outcome.Effects.FirstOrDefault(e => e.StartsWith("stopped:", StringComparison.Ordinal))?.Substring(8) ?? "";
            RenderNow();
        }
        protected override void Render(ObservableState state)
        {
            if (Canvas == null) return;
            var host = SimulationHost.Instance;
            ResetSession(host);
            var trip = state.Transport;
            bool travelling = trip != null && trip.State != "BACK" && trip.State != "CANCELLED";
            var processing = state.Investigations.Where(i => i.State == "PROCESSING").ToArray();
            int ready = state.Investigations.Count(i => i.State == "RESULT_AVAILABLE");
            bool ringing = state.Phone?.State == "RINGING";
            Canvas.gameObject.SetActive(travelling || processing.Length > 0 || ready > 0 || ringing || _interruption.Length > 0 || AdvanceText.Length > 0);
            string Place(string id) => host?.Content?.Locations.Find(id)?.Label ?? id.Replace('_', ' ');
            SetText(_location, "Patient: " + Place(state.Location) + "  |  Sim " + state.ClockTime + (travelling ? "  |  " + trip.State : ""));
            string timing = travelling ? (trip.Arrives.Length > 0 ? "Expected arrival: " + trip.Arrives + " at " + Place(trip.To) : "Return time not available yet.") : "You can continue other tasks.";
            if (trip?.State == "REQUESTED" || trip?.State == "DELAYED") timing = trip.Message.Length > 0 ? trip.Message : "Awaiting transport; arrival time not published yet.";
            if (processing.Length > 0) timing += "\n" + string.Join(" · ", processing.Take(2).Select(i => i.Label + (i.Ready.Length > 0 ? " due " + i.Ready : " processing"))) + (processing.Length > 2 ? " · +" + (processing.Length - 2) + " more" : "");
            SetText(_timing, timing);
            string notice = ready > 0 ? ready + (ready == 1 ? " result ready" : " results ready") + " — open Results or Diagnostics." : "";
            if (ringing) notice += (notice.Length > 0 ? "\n" : "") + "Lab phone ringing — answer in Diagnostics.";
            if (_interruption.Length > 0)
            {
                notice = "Wait stopped: " + _interruption + " — review before waiting again.";
                if (ready > 0 || ringing)
                    notice += "\n" + (ready > 0 ? ready + (ready == 1 ? " result ready" : " results ready") : "")
                        + (ringing ? (ready > 0 ? " · " : "") + "Lab phone ringing" : "") + " — open Diagnostics.";
            }
            if (host != null && host.Paused) notice = "Session paused — " + host.PauseReason;
            else if (host != null && host.Ended) notice = "Session finished.";
            else if (notice.Length == 0) notice = AdvanceText;
            SetText(_alert, notice);
        }
    }
}
