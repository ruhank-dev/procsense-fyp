using System;
using System.Collections.Generic;
using System.Linq;
using ProcSense.Core.Actions;
using ProcSense.Core.Clinical;
using ProcSense.Core.Observation;
using ProcSense.VR.Bridge;
using TMPro;
using UnityEngine;

namespace ProcSense.VR.Views
{
    /// <summary>Catalogue and disclosed reports. Every clinical operation is a logged Core request.</summary>
    public sealed class DiagnosticsTabView : StateView
    {
        private Transform _root;
        private TMP_Text _title, _status, _report, _feedback;
        private UnityEngine.UI.Button _order, _collect, _view, _ack, _wait, _answer, _tubeButton, _labelButton, _cancelTransport;
        private int _selection, _tube, _label, _page;
        private ProcSense.Core.Session _session;
        private float _lastClick = float.NegativeInfinity;
        private bool _attention;
        private string _feedbackText = "";
        public string ReportText => _report != null ? _report.text : "";
        public string StatusText => _status != null ? _status.text : "";
        public string FeedbackText => _feedbackText;
        public string SelectedTest => Definition?.Id ?? "";
        private InvestigationDef Definition => SimulationHost.Instance?.InvestigationCatalogue.ElementAtOrDefault(_selection);

        public void Build(Transform parent)
        {
            if (_root != null) return;
            _root = parent;
            _title = UiKit.Label(parent, "Test", "", new Vector2(16, 196), new Vector2(360, 46), 23);
            _title.enableAutoSizing = true; _title.fontSizeMin = 18; _title.fontSizeMax = 23;
            Button("Previous test", 16, 244, 174, () => SelectOffset(-1));
            Button("Next test", 202, 244, 174, () => SelectOffset(1));
            _order = Button("Order test", 388, 244, 194, () => Send("order_investigation"));
            _tubeButton = Button("Tube", 16, 300, 272, () => { _tube++; RenderNow(); });
            _labelButton = Button("Label", 300, 300, 282, () => { _label++; RenderNow(); });
            _collect = Button("Collect sample / test", 16, 356, 272, Collect);
            _view = Button("Open result", 300, 356, 282, () => Send("view_result"));
            _ack = Button("Acknowledge result", 16, 412, 272, () => Send("acknowledge_result"));
            _wait = Button("Wait 1 sim minute", 300, 412, 282, Wait);
            _answer = Button("Answer lab phone", 16, 468, 272, () => Send("answer_phone", false));
            Button("Next report page", 300, 468, 282, () => { _page++; RenderNow(); });
            _feedback = UiKit.Label(parent, "DiagnosticsFeedback", "", new Vector2(16, 524), new Vector2(566, 30), 20, color: UiKit.WarnColor);
            _cancelTransport = Button("Cancel transport", 388, 196, 194, () => Send("cancel_transport", false));
            UiKit.Panel(parent, "ReportBackground", new Vector2(604, 190), new Vector2(286, 400), UiKit.PanelColor);
            _status = UiKit.Label(parent, "DiagnosticsStatus", "", new Vector2(616, 198), new Vector2(260, 118), 19);
            _report = UiKit.Label(parent, "DisclosedReport", "", new Vector2(616, 322), new Vector2(260, 254), 19);
            RenderNow();
        }

        private UnityEngine.UI.Button Button(string text, float x, float y, float width, Action action)
        {
            var button = UiKit.MakeButton(_root, text, new Vector2(x, y), new Vector2(width, 46), 21);
            button.onClick.AddListener(() => action());
            return button;
        }

        private void SelectOffset(int offset)
        {
            int count = SimulationHost.Instance?.InvestigationCatalogue.Count ?? 0;
            if (count == 0) return;
            _selection = (_selection + offset + count) % count;
            _tube = _label = _page = 0;
            RenderNow();
        }

        public bool SelectTest(string id)
        {
            var host = SimulationHost.Instance;
            if (host == null) return false;
            ResetFor(host);
            for (int i = 0; i < host.InvestigationCatalogue.Count; i++)
                if (host.InvestigationCatalogue[i].Id == id)
                {
                    _selection = i; _tube = _label = _page = 0; RenderNow(); return true;
                }
            return false;
        }

        private static string Choice(IReadOnlyList<string> choices, int index) => choices.Count == 0 ? "" : choices[index % choices.Count];

        private void Collect()
        {
            var def = Definition;
            var host = SimulationHost.Instance;
            if (def == null || host == null) return;
            if (def.Kind == "blood") Send("take_sample", true, new Dictionary<string, string>
            {
                ["tube"] = Choice(host.SampleTubes, _tube), ["label"] = Choice(host.SampleLabels, _label)
            });
            else Send("perform_bedside_test");
        }

        private void Wait()
        {
            if (_attention) { _attention = false; _feedbackText = "Review the patient and any lab call before waiting again."; RenderNow(); return; }
            Send("time_skip", false, new Dictionary<string, string> { ["minutes"] = "1" });
        }

        private void Send(string verb, bool test = true, Dictionary<string, string> parameters = null)
        {
            var host = SimulationHost.Instance;
            if (host?.Session == null || host.Ended) return;
            ResetFor(host);
            if (Time.unscaledTime - _lastClick < 0.35f) return;
            _lastClick = Time.unscaledTime;
            parameters = parameters ?? new Dictionary<string, string>();
            if (test)
            {
                if (Definition == null) return;
                parameters["test"] = Definition.Id;
            }
            var outcome = host.Apply(new SimAction(verb, "patient", Channel.Vr, parameters));
            var stop = outcome.Effects.FirstOrDefault(e => e.StartsWith("stopped:", StringComparison.Ordinal));
            _attention = stop != null;
            _feedbackText = !outcome.Accepted ? "Request refused: " + outcome.RejectReason
                : stop != null ? "Wait stopped: " + stop.Substring(8) : "Request accepted.";
            _page = 0;
            RenderNow();
        }

        private void ResetFor(SimulationHost host)
        {
            if (ReferenceEquals(_session, host.Session)) return;
            _session = host.Session;
            _selection = _tube = _label = _page = 0;
            _attention = false; _feedbackText = ""; _lastClick = float.NegativeInfinity;
        }

        protected override void Render(ObservableState s)
        {
            if (_root == null) return;
            var host = SimulationHost.Instance;
            if (host == null) return;
            ResetFor(host);
            var def = Definition;
            var order = s.Investigations.LastOrDefault(o => o.Test == def?.Id);
            SetText(_title, def == null ? "No investigations available" : def.Label + " · " + def.Kind);
            SetText(_status, "Patient: " + s.Location + "\n" + (s.Transport == null ? "No patient transfer" : s.Transport.State + " → " + s.Transport.To)
                + "\n" + (order == null ? "Not ordered" : order.State + (order.Ready.Length > 0 ? " · " + order.Ready : "")));
            var movement = FindAnyObjectByType<PatientTransportView>();
            if(movement!=null && (movement.Status.Contains("blocked") || movement.Status.Contains("unavailable"))) SetText(_status,_status.text+"\nVisual route "+(movement.Status.Contains("blocked")?"blocked":"unavailable"));
            bool active = !host.Paused && !host.Ended;
            bool pending = order != null && (order.State == "REQUESTED" || order.State == "PROCESSING");
            _order.interactable = active && def != null && !pending;
            _collect.interactable = active && order?.State == "REQUESTED" && def.Kind != "imaging";
            _view.interactable = active && order != null && (order.State == "RESULT_AVAILABLE" || order.State == "VIEWED" || order.State == "ACKNOWLEDGED");
            _ack.interactable = active && order?.State == "VIEWED";
            _wait.interactable = active && (s.Investigations.Any(o => o.State == "PROCESSING") || s.Transport != null && s.Transport.State != "BACK" && s.Transport.State != "CANCELLED");
            _answer.interactable = active && s.Phone?.State == "RINGING";
            _cancelTransport.interactable = active && s.Transport != null && new[] { "REQUESTED", "DELAYED", "PREPARING", "IN_TRANSIT" }.Contains(s.Transport.State);
            _tubeButton.interactable = _labelButton.interactable = active && def?.Kind == "blood";
            SetText(_tubeButton.GetComponentInChildren<TMP_Text>(), "Tube: " + Choice(host.SampleTubes, _tube));
            SetText(_labelButton.GetComponentInChildren<TMP_Text>(), "Label: " + Choice(host.SampleLabels, _label));
            SetText(_wait.GetComponentInChildren<TMP_Text>(), _attention ? "Review wait interruption" : "Wait 1 sim minute");
            SetText(_feedback, _feedbackText);
            var pages = new List<string>();
            if (s.Phone != null && s.Phone.State.Length > 0) AddPages(pages, "Lab call: " + s.Phone.State + "\n" + s.Phone.Message);
            if (order == null) pages.Add("Order a test to track its progress. Imaging requires a separate patient transport request.");
            else
            {
                if (order.Note.Length > 0) AddPages(pages, order.Note);
                foreach (var result in order.Results) AddPages(pages, result.Analyte + ": " + result.Value + " " + result.Unit + "\nRange: " + result.Range + "\n" + result.Flag + " " + result.Comment + "\n" + result.Quality + " " + result.Origin);
                if (order.Report != null) AddPages(pages, order.Report.Text + "\n" + string.Join("\n", order.Report.Measurements));
                if (pages.Count == 0) pages.Add(order.State == "RESULT_AVAILABLE" ? "Result ready. Select Open result to read it." : "No disclosed result yet.");
            }
            SetText(_report, "Page " + (_page % pages.Count + 1) + "/" + pages.Count + "\n" + pages[_page % pages.Count]);
        }

        private static void AddPages(List<string> pages, string text)
        {
            // Conservative page size avoids truncating long reports on the existing tablet.
            for (int start = 0; start < text.Length; start += 150) pages.Add(text.Substring(start, Math.Min(150, text.Length - start)));
        }
    }
}
