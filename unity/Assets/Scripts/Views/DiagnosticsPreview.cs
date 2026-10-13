#if UNITY_EDITOR
using UnityEngine;
using TMPro;

namespace ProcSense.VR.Views
{
    /// <summary>Editor-only presentation fixture. Never applies actions or advances a Core session.</summary>
    public sealed class DiagnosticsPreview : MonoBehaviour
    {
        public enum Phase { Idle, Requested, Dispatched, Processing, Ready, Delivered }
        public Phase Current { get; private set; }
        public bool SampleRoute { get; private set; }
        public bool Interrupted { get; private set; }
        public string ReportText => Current >= Phase.Ready
            ? "SYNTHETIC REPORT\nLayout placeholder only. No clinical values or patient data."
            : "Report unavailable\nWaiting for a fixture ResultReady event.";
        public string LocationText => SampleRoute ? "Patient: bedside throughout\nSample: " + Current
            : "Patient: " + (Current < Phase.Dispatched || Current == Phase.Delivered ? "bedside" : "diagnostic trip (fixture)");

        private TMP_Text _status, _report, _advanceLabel;
        private UnityEngine.UI.Button _advance, _interrupt, _resume;
        public Canvas Canvas { get; private set; }

        private void Awake()
        {
            Canvas = UiKit.WorldCanvas("Diagnostics preview", transform, new Vector2(900, 650), 0.0008f);
            var root = Canvas.transform;
            UiKit.Label(root, "Title", "DIAGNOSTICS — DEVELOPMENT PREVIEW", new Vector2(24, 16), new Vector2(852, 42), 30);
            UiKit.Label(root, "Fixture warning", "Synthetic events only. Not connected to clinical orders, transport or results.",
                new Vector2(24, 64), new Vector2(852, 65), 23, TextAlignmentOptions.TopLeft, UiKit.WarnColor);
            _status = UiKit.Label(root, "Status", "", new Vector2(24, 150), new Vector2(410, 190), 24);
            _report = UiKit.Label(root, "Report", "", new Vector2(462, 150), new Vector2(410, 190), 24);
            AddButton("Patient trip fixture", 24, 360, () => SelectRoute(false));
            AddButton("Lab sample fixture", 462, 360, () => SelectRoute(true));
            _advance = AddButton("", 24, 428, () => Advance());
            _advanceLabel = _advance.GetComponentInChildren<TMP_Text>();
            _interrupt = AddButton("Inject attention stop", 462, 428, () => Interrupt());
            _resume = AddButton("Resume fixture", 24, 496, () => Resume());
            AddButton("Reset fixture", 462, 496, Reset);
            AddButton("Close preview", 24, 564, () => Destroy(gameObject));
            Refresh();
        }

        private UnityEngine.UI.Button AddButton(string label, float x, float y, UnityEngine.Events.UnityAction click)
        {
            var button = UiKit.MakeButton(Canvas.transform, label, new Vector2(x, y), new Vector2(410, 52), 24);
            button.onClick.AddListener(click);
            return button;
        }

        public void SelectRoute(bool sample) { SampleRoute = sample; Reset(); }
        public void Reset() { Current = Phase.Idle; Interrupted = false; Refresh(); }
        public bool Advance()
        {
            if (Interrupted || Current == Phase.Delivered) return false;
            Current++;
            Refresh();
            return true;
        }
        public bool Interrupt()
        {
            if (Current != Phase.Processing || Interrupted) return false;
            Interrupted = true;
            Refresh();
            return true;
        }
        public bool Resume()
        {
            if (!Interrupted) return false;
            Interrupted = false;
            Refresh();
            return true;
        }
        private void Refresh()
        {
            if (_status == null) return;
            _status.text = "State: " + Current + (Interrupted ? " — ATTENTION STOP" : "") + "\n" + LocationText + "\nNo simulation time advanced.";
            _report.text = ReportText;
            _advanceLabel.text = Current switch
            {
                Phase.Idle => "Inject request",
                Phase.Requested => SampleRoute ? "Inject sample dispatch" : "Inject patient departure",
                Phase.Dispatched => "Inject processing",
                Phase.Processing => "Inject result ready",
                Phase.Ready => "Inject report delivered",
                _ => "Fixture complete"
            };
            _advance.interactable = !Interrupted && Current != Phase.Delivered;
            _interrupt.interactable = Current == Phase.Processing && !Interrupted;
            _resume.interactable = Interrupted;
        }
    }
}
#endif
