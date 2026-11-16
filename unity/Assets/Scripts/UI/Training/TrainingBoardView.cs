using System.Linq;
using System.Text;
using ProcSense.Core;
using ProcSense.Core.Observation;
using ProcSense.VR.Bridge;
using ProcSense.VR.Views;
using TMPro;
using UnityEngine;

namespace ProcSense.VR.UI.Training
{
    /// <summary>
    /// The training bulletin board (P3-TRN-03): the current objective, progress, each section with its steps (done, now, open, waiting, missed, not needed with the engine's reason), the how-to of the
    /// selected step in the trainee's input mode (menu or voice), the last refusal and what is blocked. It redraws on every engine change and applies no action. In the examination its canvas is hidden
    /// (no goal list, no objective, no reasons; P3-EXM-07). Placed by <see cref="ClinicalPanelsInstaller"/>.
    /// </summary>
    public sealed class TrainingBoardView : MonoBehaviour
    {
        public TrainingGuide Guide;
        public System.Func<Session> SessionProvider = () => ModeGate.SessionProvider();   // the same running session the mode gate reads

        public Canvas Canvas { get; private set; }
        public BoardModel Model { get; private set; } = new BoardModel();
        public string InputMode { get; private set; } = "menu";
        public string SelectedId { get; private set; } = "";
        public bool Collapsed { get; private set; }

        public string ObjectiveText => _objective != null ? _objective.text : "";
        public string BodyText => _body != null ? _body.text : "";
        public string HowText => _how != null ? _how.text : "";
        public string FeedbackText => _feedback != null ? _feedback.text : "";

        private TMP_Text _title, _objective, _progress, _body, _how, _feedback, _modeLabel;
        private GameObject _content;
        private SimulationHost _host;

        public void Build()
        {
            if (Canvas != null) return;
            Canvas = UiKit.WorldCanvas("Training board", transform, new Vector2(900, 1180), 0.0006f);
            var root = Canvas.transform;
            _title = UiKit.Label(root, "Title", "Training board", new Vector2(24, 16), new Vector2(560, 44), 32);
            var hide = UiKit.MakeButton(root, "Hide", new Vector2(756, 14), new Vector2(120, 48));
            hide.onClick.AddListener(ToggleCollapsed);
            _content = new GameObject("Content", typeof(RectTransform));
            _content.transform.SetParent(root, false);
            var c = _content.transform;
            _objective = UiKit.Label(c, "Objective", "", new Vector2(24, 70), new Vector2(852, 80), 28, color: UiKit.WarnColor);
            _progress = UiKit.Label(c, "Progress", "", new Vector2(24, 150), new Vector2(852, 36), 24, color: UiKit.DimTextColor);
            _body = UiKit.Label(c, "Sections", "", new Vector2(24, 192), new Vector2(852, 560), 24);
            _body.overflowMode = TextOverflowModes.Truncate;
            UiKit.MakeButton(c, "< Step", new Vector2(24, 762), new Vector2(160, 52)).onClick.AddListener(() => Select(-1));
            UiKit.MakeButton(c, "Step >", new Vector2(196, 762), new Vector2(160, 52)).onClick.AddListener(() => Select(+1));
            UiKit.MakeButton(c, "Objective", new Vector2(368, 762), new Vector2(180, 52)).onClick.AddListener(() => { SelectedId = Model.ObjectiveId; Draw(); });
            var mode = UiKit.MakeButton(c, "Menu / Voice", new Vector2(660, 762), new Vector2(216, 52));
            mode.onClick.AddListener(ToggleInputMode);
            _modeLabel = UiKit.Label(c, "Mode", "", new Vector2(560, 768), new Vector2(96, 40), 22, TextAlignmentOptions.Right, UiKit.DimTextColor);
            _how = UiKit.Label(c, "HowTo", "", new Vector2(24, 826), new Vector2(852, 200), 24);
            _feedback = UiKit.Label(c, "Feedback", "", new Vector2(24, 1032), new Vector2(852, 136), 24, color: UiKit.WarnColor);
            Refresh();
        }

        private void OnEnable() => Bind();
        private void OnDisable() { if (_host != null) _host.Changed -= OnChanged; _host = null; }
        private void Update() { if (_host == null) Bind(); }

        private void Bind()
        {
            var host = SimulationHost.Instance;
            if (host == null || ReferenceEquals(host, _host)) return;
            if (_host != null) _host.Changed -= OnChanged;
            _host = host;
            _host.Changed += OnChanged;
            Refresh();
        }

        private void OnChanged(ObservableState _) => Refresh();

        /// <summary>Rebuilds the model from the session and redraws; hides the board in the examination or with no session.</summary>
        public void Refresh()
        {
            if (Canvas == null) return;
            var session = SessionProvider();
            Model = TrainingBoardText.Build(session, Guide);
            Canvas.gameObject.SetActive(Model.Visible);
            if (!Model.Visible) { SelectedId = ""; return; }
            if (SelectedId.Length == 0 || Model.AllSteps.All(r => r.Id != SelectedId)) SelectedId = Model.ObjectiveId;
            Draw();
        }

        public void ToggleInputMode() { InputMode = InputMode == "menu" ? "voice" : "menu"; Draw(); }

        public void ToggleCollapsed() { Collapsed = !Collapsed; if (_content != null) _content.SetActive(!Collapsed); }

        /// <summary>Moves the selection through the steps the board lists (skips the ones not needed).</summary>
        public void Select(int delta)
        {
            var steps = Model.AllSteps.Where(r => r.State != "not_needed").ToList();
            if (steps.Count == 0) return;
            int i = steps.FindIndex(r => r.Id == SelectedId);
            i = i < 0 ? 0 : ((i + delta) % steps.Count + steps.Count) % steps.Count;
            SelectedId = steps[i].Id;
            Draw();
        }

        private void Draw()
        {
            if (_body == null || !Model.Visible) return;
            _objective.text = "Now: " + Model.Objective;
            _progress.text = Model.Progress;
            _modeLabel.text = InputMode == "voice" ? "Voice" : "Menu";
            var selectedSection = Model.Sections.FirstOrDefault(s => s.Steps.Any(r => r.Id == SelectedId));
            var sb = new StringBuilder();
            foreach (var section in Model.Sections)
            {
                sb.Append("<b>").Append(section.Title).Append("</b>  ").Append(section.Done).Append('/').Append(section.Counted).Append('\n');
                if (!ReferenceEquals(section, selectedSection)) continue;             // only the selected step's section is open, so the board stays readable at arm's length
                foreach (var r in section.Steps)
                {
                    sb.Append(r.Id == SelectedId ? "> " : "  ").Append(TrainingBoardText.Mark(r.State)).Append(' ').Append(r.Label);
                    if (r.Note.Length > 0) sb.Append("  <color=#9fa9b8>(").Append(r.Note).Append(")</color>");
                    sb.Append('\n');
                }
            }
            _body.text = sb.ToString();
            var selected = Model.AllSteps.FirstOrDefault(r => r.Id == SelectedId);
            _how.text = selected == null ? "" : "<b>How (" + (InputMode == "voice" ? "voice" : "menu") + "):</b> " + selected.Label + "\n" + TrainingBoardText.HowTo(Guide, SelectedId, InputMode);
            var fb = new StringBuilder(Model.Feedback);
            foreach (var b in Model.Blocked.Take(3)) { if (fb.Length > 0) fb.Append('\n'); fb.Append("Blocked: ").Append(b); }
            _feedback.text = fb.ToString();
        }
    }
}
