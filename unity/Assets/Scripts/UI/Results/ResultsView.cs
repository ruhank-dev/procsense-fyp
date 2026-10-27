using System.Collections.Generic;
using System.Linq;
using ProcSense.Core.Actions;
using ProcSense.Core.Observation;
using ProcSense.VR.Bridge;
using ProcSense.VR.UI.Tablet;
using ProcSense.VR.Views;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ProcSense.VR.UI.Results
{
    /// <summary>
    /// The results viewer (p0-vr-interaction-ui-s05): the ordered tests down the side; for the chosen one its table (value, H/L flag, unit, range, lab comment, previous value with its date and a trend arrow), its image with the report,
    /// or its ECG strip, all from the same Core data as the EMR (<see cref="ResultsText"/>); View and Acknowledge; and the interpretation picker. A critical result can only be acknowledged once the phone call is done up to
    /// the read-back, then with one of the content's actions and a note. Every button sends a logged action and Core decides; the viewer only shows the state. Like the EMR panel it is not placed in a scene yet: where it
    /// hangs and the headset check are UX-P0-VRUI.
    /// </summary>
    public class ResultsView : MonoBehaviour
    {
        public const string ViewVerb = "view_result", AcknowledgeVerb = "acknowledge_result";
        private const int ListRows = 8;

        public Canvas Canvas { get; private set; }
        public InterpretPicker Picker { get; private set; }
        public string Selected { get; private set; } = "";
        public string Mode { get; private set; } = ResultsText.Table;
        public string BodyText => _body != null ? _body.text : "";
        public string HintText => _hint != null ? _hint.text : "";
        public IReadOnlyList<string> Tests => _tests;
        public bool ViewEnabled => _view != null && _view.interactable;
        public bool AcknowledgeEnabled => _acknowledge != null && _acknowledge.interactable;
        public bool ActionPanelOpen => _actionRoot != null && _actionRoot.activeSelf;
        public IEnumerable<(string label, Button button)> ActionButtons => _actionButtons.Select(b => (b.option.Label, b.button));

        private readonly List<string> _tests = new List<string>();
        private readonly List<Button> _list = new List<Button>();
        private readonly List<(TabletOption option, Button button)> _actionButtons = new List<(TabletOption, Button)>();
        private TMP_Text _body, _hint;
        private Button _view, _acknowledge;
        private GameObject _actionRoot;
        private TMP_InputField _note;
        private string _action = "";
        private FormSendEmitter _send;
        private ObservableState _shown;

        private static SimulationHost Host => SimulationHost.Instance;

        private void Awake() => Build();

        public void Build()
        {
            if (Canvas != null) return;
            Canvas = UiKit.WorldCanvas("Results", transform, new Vector2(1000, 720), 0.0006f);
            var root = Canvas.transform;
            _send = gameObject.AddComponent<FormSendEmitter>();
            for (int i = 0; i < ListRows; i++)
            {
                int row = i;
                var b = UiKit.MakeButton(root, "", new Vector2(12, 12 + i * 62), new Vector2(224, 56), 18f);
                b.onClick.AddListener(() => { if (row < _tests.Count) Select(_tests[row]); });
                _list.Add(b);
            }
            _body = UiKit.Label(root, "Body", "", new Vector2(248, 12), new Vector2(740, 370), 20);
            _view = UiKit.MakeButton(root, LayoutText.Label(ViewVerb), new Vector2(248, 392), new Vector2(230, 52), 18f);
            _view.onClick.AddListener(() => View(true));
            _acknowledge = UiKit.MakeButton(root, LayoutText.Label(AcknowledgeVerb), new Vector2(486, 392), new Vector2(230, 52), 18f);
            _acknowledge.onClick.AddListener(OpenOrAcknowledge);
            _hint = UiKit.Label(root, "Hint", "", new Vector2(724, 392), new Vector2(264, 52), 16, TextAlignmentOptions.MidlineLeft, UiKit.WarnColor);

            _actionRoot = new GameObject("Critical action", typeof(RectTransform));
            _actionRoot.transform.SetParent(root, false);
            var options = LayoutText.Options(AcknowledgeVerb, "action");
            for (int i = 0; i < options.Count; i++)
            {
                var option = options[i];
                var b = UiKit.MakeButton(_actionRoot.transform, option.Label, new Vector2(248 + i * 188, 454), new Vector2(180, 52), 16f);
                b.onClick.AddListener(() => { _action = option.Value; ShowAction(); });
                _actionButtons.Add((option, b));
            }
            _note = UiKit.Input(_actionRoot.transform, new Vector2(248, 514), new Vector2(560, 48));
            UiKit.MakeButton(_actionRoot.transform, "Send", new Vector2(816, 514), new Vector2(172, 48), 18f).onClick.AddListener(() => Acknowledge(_action, _note.text, true));
            _actionRoot.SetActive(false);

            Picker = new InterpretPicker(root, _send, new Vector2(248, 576));
            RenderNow();
        }

        private void Update()
        {
            if (Host != null && Host.Observable != _shown) RenderNow();
        }

        public bool Select(string test)
        {
            RenderNow();                                                  // the list follows the session, which may have moved since the last frame
            if (!_tests.Contains(test)) return false;
            Selected = test;
            _action = "";
            if (_note != null) _note.text = "";
            _actionRoot.SetActive(false);
            RenderNow();
            return true;
        }

        /// <summary>Opens the chosen result (view_result). Returns Core's answer.</summary>
        public ActionOutcome View(bool click = false) => Selected.Length == 0 ? null : After(PanelSend.Send(_send, ViewVerb, new Dictionary<string, string> { ["test"] = Selected }, click));

        /// <summary>
        /// Acknowledges the chosen result (acknowledge_result). For a critical result the action and the note go with it; Core refuses a missing one, and refuses the whole acknowledgement until the call has been read back.
        /// </summary>
        public ActionOutcome Acknowledge(string action = "", string note = "", bool click = false)
        {
            if (Selected.Length == 0) return null;
            var p = new Dictionary<string, string> { ["test"] = Selected };
            if (action.Length > 0) p["action"] = action;
            if (note.Trim().Length > 0) p["note"] = note;
            var outcome = After(PanelSend.Send(_send, AcknowledgeVerb, p, click));
            if (outcome != null && outcome.Accepted) _actionRoot.SetActive(false);
            return outcome;
        }

        /// <summary>A critical result still on the phone or awaiting its action: the result of that test cannot be acknowledged without an action and a note.</summary>
        public bool CriticalOpen(string test) => Host?.Session != null && Host.Session.Patient.Clinical.CriticalCalls.Any(c => c.Test == test && c.State != "DONE");

        private void OpenOrAcknowledge()
        {
            if (CriticalOpen(Selected)) { _actionRoot.SetActive(true); ShowAction(); }
            else Acknowledge(click: true);
        }

        private void ShowAction()
        {
            foreach (var (option, button) in _actionButtons)
                button.GetComponentInChildren<TMP_Text>().text = (option.Value == _action ? "> " : "") + option.Label;
        }

        private ActionOutcome After(ActionOutcome outcome)
        {
            if (outcome != null) _hint.text = outcome.Accepted ? "" : ModeGate.Practice(HintTexts.For(outcome.RejectReason));
            RenderNow();
            return outcome;
        }

        public void RenderNow()
        {
            if (Canvas == null) return;
            var s = Host != null ? Host.Observable : null;
            _shown = s;
            _tests.Clear();
            if (s != null) _tests.AddRange(s.Investigations.Select(i => i.Test).Distinct());
            if (!_tests.Contains(Selected)) Selected = "";
            for (int i = 0; i < _list.Count; i++)
            {
                bool used = i < _tests.Count;
                _list[i].gameObject.SetActive(used);
                if (!used) continue;
                var latest = s.Investigations.Last(x => x.Test == _tests[i]);
                _list[i].GetComponentInChildren<TMP_Text>().text = (_tests[i] == Selected ? "> " : "") + latest.Label + (latest.State == "RESULT_AVAILABLE" ? "  (new)" : "");
            }

            var inv = Selected.Length > 0 ? s.Investigations.Last(x => x.Test == Selected) : null;
            string kind = inv != null ? Host.Content.Investigations.Find(Selected)?.Kind ?? "" : "";
            Mode = inv != null ? ResultsText.ModeOf(Selected, kind) : ResultsText.Table;
            var sections = Host?.Session?.Records?.Sections();
            _body.text = inv == null || sections == null ? "" : Body(inv, sections);

            bool viewed = inv != null && (inv.State == "VIEWED" || inv.State == "ACKNOWLEDGED");
            bool critical = inv != null && CriticalOpen(Selected);
            _view.interactable = inv != null && (inv.State == "RESULT_AVAILABLE" || viewed);
            // a critical result is blocked here until the call has been read back; Core refuses it as well
            _acknowledge.interactable = inv != null && inv.State == "VIEWED" && (!critical || s.Phone.State == "READ_BACK");
            if (critical && s.Phone.State != "READ_BACK" && s.Phone.State.Length > 0 && _hint.text.Length == 0) _hint.text = HintTexts.For("CALL_READ_BACK_FIRST");
            Picker.Show(Selected, Mode == ResultsText.Image, viewed);
        }

        // Every instance of the test (the first sample and its repeats) is in the EMR's rows; the latest one decides whether there is anything to show yet.
        private string Body(ObservedInvestigation inv, ProcSense.Core.Records.EmrSections sections)
        {
            string text = ResultsText.Body(sections, inv, Mode);
            if (text.Length > 0) return text;
            return inv.Note.Length > 0 ? inv.Note : inv.Ready.Length > 0 ? "Expected at " + inv.Ready : "";
        }
    }
}
