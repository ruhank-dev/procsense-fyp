using System.Collections.Generic;
using System.Linq;
using ProcSense.Core.Actions;
using ProcSense.VR.Bridge;
using ProcSense.VR.UI.Results;
using ProcSense.VR.UI.Tablet;
using ProcSense.VR.Views;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ProcSense.VR.UI.Checklist
{
    /// <summary>
    /// The WHO checklist board in theatre (p0-vr-interaction-ui-s08), for the board's screen (SCREEN_WHOBoard; greybox until it is placed). It shows Sign In and Time Out item by item in content order, each with who
    /// answers it (you, or the team), pending, confirmed in green, or stopped in red with the reason and what fixes it; the ProcSense extensions are a section of their own. The trainee picks an item and answers
    /// it (checklist_item with the layout's yes / no / not applicable); a team item is answered by the team when it comes up, and answering it yourself is refused by Core (ask the right person). Complete Time Out
    /// sends complete_time_out. A stopped phase resumes when Core says the correction has been made (T-SC6). The board shows Core's state only.
    /// </summary>
    public class ChecklistBoard : MonoBehaviour
    {
        public const string ItemVerb = "checklist_item", CompleteVerb = "complete_time_out";
        private const int RowCount = 8;
        private static readonly Color Pending = new Color(0.20f, 0.30f, 0.42f), Done = new Color(0.12f, 0.50f, 0.24f), Stopped = new Color(0.72f, 0.12f, 0.12f);

        public Canvas Canvas { get; private set; }
        public string Phase { get; private set; } = "sign_in";
        public string Selected { get; private set; } = "";
        public IReadOnlyList<BoardRow> Rows => _rows;
        public string PhaseText => _phase != null ? _phase.text : "";
        public string StopText => _stop != null ? _stop.text : "";
        public string HintText => _hint != null ? _hint.text : "";
        public Color RowColour(string id) { int i = _rows.FindIndex(r => r.Id == id); return i < 0 ? Color.clear : _rowButtons[i].colors.normalColor; }

        private readonly List<BoardRow> _rows = new List<BoardRow>();
        private readonly List<Button> _rowButtons = new List<Button>();
        private readonly List<Button> _answers = new List<Button>();
        private TMP_Text _phase, _stop, _hint;
        private FormSendEmitter _send;
        private object _shown;

        private static SimulationHost Host => SimulationHost.Instance;

        private void Awake() => Build();

        public void Build()
        {
            if (Canvas != null) return;
            Canvas = UiKit.WorldCanvas("WHO board", transform, new Vector2(1100, 860), 0.001f);
            var root = Canvas.transform;
            _send = gameObject.AddComponent<FormSendEmitter>();
            var tabs = new[] { ("sign_in", "Sign In"), ("time_out", "Time Out"), ("extension", "ProcSense checks") };
            for (int i = 0; i < tabs.Length; i++)
            {
                string id = tabs[i].Item1;
                UiKit.MakeButton(root, tabs[i].Item2, new Vector2(12 + i * 236, 12), new Vector2(228, 52), 20f).onClick.AddListener(() => ShowPhase(id));
            }
            _phase = UiKit.Label(root, "Phase", "", new Vector2(728, 12), new Vector2(360, 52), 20, TextAlignmentOptions.MidlineRight);
            for (int i = 0; i < RowCount; i++)
            {
                int n = i;
                var b = UiKit.MakeButton(root, "", new Vector2(12, 76 + i * 70), new Vector2(1076, 64), 17f);
                var label = b.GetComponentInChildren<TMP_Text>();
                label.alignment = TextAlignmentOptions.MidlineLeft;
                b.onClick.AddListener(() => { if (n < _rows.Count) Select(_rows[n].Id); });
                _rowButtons.Add(b);
            }
            _stop = UiKit.Label(root, "Stop", "", new Vector2(12, 640), new Vector2(1076, 60), 20, TextAlignmentOptions.MidlineLeft, new Color(1f, 0.45f, 0.4f));
            var responses = LayoutText.Options(ItemVerb, "response");
            for (int i = 0; i < responses.Count; i++)
            {
                var option = responses[i];
                var b = UiKit.MakeButton(root, option.Label, new Vector2(12 + i * 200, 712), new Vector2(190, 60), 20f);
                b.onClick.AddListener(() => Answer(Selected, option.Value, true));
                _answers.Add(b);
            }
            UiKit.MakeButton(root, LayoutText.Label(CompleteVerb), new Vector2(788, 712), new Vector2(300, 60), 18f).onClick.AddListener(() => CompleteTimeOut(true));
            _hint = UiKit.Label(root, "Hint", "", new Vector2(12, 784), new Vector2(1076, 60), 18, TextAlignmentOptions.MidlineLeft, UiKit.WarnColor);
            RenderNow();
        }

        private void Update()
        {
            if (Host != null && Host.Observable != _shown) RenderNow();
        }

        public void ShowPhase(string phase)
        {
            Phase = phase;
            Selected = "";
            RenderNow();
        }

        public bool Select(string id)
        {
            if (_rows.All(r => r.Id != id)) return false;
            Selected = id;
            RenderNow();
            return true;
        }

        /// <summary>Answers a checklist item (checklist_item). Core checks the answer against the patient and the order; a wrong answer stops the phase.</summary>
        public ActionOutcome Answer(string id, string response, bool click = false)
        {
            var row = _rows.FirstOrDefault(r => r.Id == id);
            if (row == null) return null;
            return After(PanelSend.Send(_send, ItemVerb, new Dictionary<string, string> { ["phase"] = row.Phase, ["item"] = id, ["response"] = response }, click));
        }

        public ActionOutcome CompleteTimeOut(bool click = false) => After(PanelSend.Send(_send, CompleteVerb, new Dictionary<string, string>(), click));

        private ActionOutcome After(ActionOutcome outcome)
        {
            if (outcome != null) _hint.text = outcome.Accepted ? "" : ModeGate.Practice(HintTexts.For(outcome.RejectReason));
            RenderNow();
            return outcome;
        }

        public void RenderNow()
        {
            if (Canvas == null) return;
            _shown = Host != null ? Host.Observable : null;
            var session = Host?.Session;
            var content = Host?.Content;
            _rows.Clear();
            if (session != null && content != null) _rows.AddRange(ChecklistBoardText.Rows(session, content, Phase));
            string state = ChecklistBoardText.PhaseState(session, Phase);
            _phase.text = Phase == "extension" ? "" : state.Replace("_", " ");
            _phase.color = state == "STOPPED" ? new Color(1f, 0.45f, 0.4f) : state == "COMPLETE" ? new Color(0.5f, 0.9f, 0.55f) : UiKit.TextColor;
            for (int i = 0; i < _rowButtons.Count; i++)
            {
                bool used = i < _rows.Count;
                _rowButtons[i].gameObject.SetActive(used);
                if (!used) continue;
                var r = _rows[i];
                _rowButtons[i].GetComponentInChildren<TMP_Text>().text = (r.Id == Selected ? "> " : "") + ChecklistBoardText.Mark(r.State) + "  " + r.Prompt + "   (" + ChecklistBoardText.WhoLabel(r.AnsweredBy) + ")";
                var colours = _rowButtons[i].colors;
                colours.normalColor = r.State == "confirmed" ? Done : r.State == "stopped" || r.State == "contradicted" ? Stopped : Pending;
                colours.highlightedColor = colours.normalColor * 1.2f;
                _rowButtons[i].colors = colours;
            }
            var stopped = _rows.Where(r => r.Reason.Length > 0).ToList();
            _stop.text = stopped.Count == 0 ? "" : "STOPPED: " + string.Join("; ", stopped.Select(r => r.Prompt + " - " + r.Reason));
            var selected = _rows.FirstOrDefault(r => r.Id == Selected);
            foreach (var b in _answers) b.interactable = selected != null && selected.State != "confirmed";
        }
    }
}
