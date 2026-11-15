using System.Collections.Generic;
using System.Linq;
using System.Text;
using ProcSense.Core;
using ProcSense.VR.Auth;
using ProcSense.VR.UI.Accessibility;
using ProcSense.VR.Sync;
using ProcSense.VR.UI.Debrief;
using ProcSense.VR.UI.Kit;
using ProcSense.VR.UI.Theme;
using ProcSense.VR.Views;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace ProcSense.VR.Flow
{
    /// <summary>
    /// The glass menu of the game flow, built at run time with the component kit (P3-UIX-04 look since P3-MNU-03; no scene editing): Main Menu (Training, Test, Settings, Quit), Surgery choice, the briefing card, the simulation bar (Hint,
    /// Submit patient to OT with a confirmation), the results panel (Retry, New scenario) and Settings (the existing readability and subtitle tab). One world-space canvas, one shared UI material,
    /// six pooled buttons and three labels (the Consent step between sign-in and the Main Menu shows the service's own consent questions with Agree and Decline, owner decision D2): no fullscreen transparent layer, no per-frame work (it redraws only when the flow changes). Every button calls MenuFlow, which sends the engine verbs.
    /// </summary>
    public sealed class FlowPanel : MonoBehaviour
    {
        private const float Width = 900f, Height = 620f, Scale = 0.0012f;
        private const int Pool = 6;

        private MenuFlow _flow;
        private Canvas _canvas;
        private TMP_Text _title, _body, _status;
        private readonly List<Button> _buttons = new List<Button>();
        private readonly List<(Vector2 at, Vector2 size)> _home = new List<(Vector2, Vector2)>();
        private RectTransform _settings;
        private bool _showSettings;
        private GameObject _debrief;
        private ProcSense.Core.Flow.ResultSummary _rewordFor;
        private RewordedRecommendations _reworded;
        private bool _drawingResults;

        /// <summary>True when the results panel shows the service's AI wording (labelled "Reworded") instead of the engine's deterministic lines.</summary>
        public bool ShowsRewording => _reworded != null && _reworded.Reworded && _flow != null && ReferenceEquals(_rewordFor, _flow.Summary);

        /// <summary>True while the debrief view (UI/Debrief) is open beside the results.</summary>
        public bool DebriefOpen => _debrief != null;
        public DebriefView DebriefViewOpen => _debrief != null ? _debrief.GetComponent<DebriefView>() : null;

        public Canvas Canvas => _canvas;
        public MenuFlow Flow => _flow;
        public string TitleText => _title != null ? _title.text : "";
        public string BodyText => _body != null ? _body.text : "";
        public string StatusText => _status != null ? _status.text : "";

        public void Build(MenuFlow flow, Transform parent)
        {
            _flow = flow;
            // P3-MNU-03: the component kit's glass panel, type styles and buttons (P3-UIX-04), at the size and scale the menu had, read from MenuFlowInstaller.Distance
            _canvas = Kit.Canvas("Game flow", parent, new Vector2(Width, Height), MenuFlowInstaller.Distance);
            var readable = _canvas.GetComponent<AccessibleCanvas>();
            readable.BaseScale = Scale;
            readable.Apply();
            var t = _canvas.transform;
            Kit.Panel(t, "Background", Vector2.zero, new Vector2(Width, Height));
            _title = Kit.Text(t, "Title", "", new Vector2(32, 24), new Vector2(Width - 64, 64), TextStyle.Title);
            _body = Kit.Text(t, "Body", "", new Vector2(32, 96), new Vector2(Width - 64, 268), TextStyle.Body);
            _status = Kit.Text(t, "Status", "", new Vector2(32, 584), new Vector2(Width - 64, 32), TextStyle.Caption, UiTheme.Current.HintText);
            for (int i = 0; i < Pool; i++)
            {
                var b = Kit.Button(t, " ", new Vector2(32 + (i % 2) * 426, 372 + (i / 2) * 72), new Vector2(410, Kit.ButtonHeight - 8), ButtonKind.Secondary);
                _home.Add((((RectTransform)b.transform).anchoredPosition, ((RectTransform)b.transform).sizeDelta));
                _buttons.Add(b);
            }

            var go = new GameObject("Settings container", typeof(RectTransform));
            go.transform.SetParent(t, false);
            _settings = (RectTransform)go.transform;
            _settings.anchorMin = _settings.anchorMax = _settings.pivot = new Vector2(0f, 1f);
            _settings.anchoredPosition = Vector2.zero;
            _settings.sizeDelta = new Vector2(Width, Height);
            go.AddComponent<SettingsTabView>().Build(_settings);
            _settings.gameObject.SetActive(false);

            _flow.Changed += Refresh;
            Refresh();
        }

        private void OnEnable() { if (_flow != null) Refresh(); }   // a consent check that waited for the panel to be shown starts now

        private void OnDestroy()
        {
            if (_flow != null) _flow.Changed -= Refresh;
            CloseDebrief();
        }

        /// <summary>Opens the existing debrief view beside the results panel (built from the finished session), or closes it.</summary>
        public void ToggleDebrief()
        {
            if (_debrief != null) { CloseDebrief(); Refresh(); return; }
            if (_flow == null || _flow.Phase != MenuPhase.Results) return;
            _debrief = new GameObject("Debrief (flow)");
            _debrief.transform.SetParent(transform.parent, false);
            _debrief.transform.SetPositionAndRotation(transform.position + transform.right * 1.5f, transform.rotation);
            _debrief.AddComponent<DebriefView>();
            Refresh();
        }

        private void CloseDebrief()
        {
            if (_debrief != null) Destroy(_debrief);
            _debrief = null;
        }

        /// <summary>Presses the visible button with this label (case-insensitive). The headless way to drive the panel in tests; returns false when no such button is showing.</summary>
        public bool Click(string label)
        {
            foreach (var b in _buttons)
            {
                if (!b.gameObject.activeSelf || !b.interactable) continue;
                var text = b.GetComponentInChildren<TMP_Text>();
                if (text != null && string.Equals(text.text, label, System.StringComparison.OrdinalIgnoreCase)) { b.onClick.Invoke(); return true; }
            }
            return false;
        }

        public IReadOnlyList<string> VisibleButtons()
        {
            var list = new List<string>();
            foreach (var b in _buttons) if (b.gameObject.activeSelf) list.Add(b.GetComponentInChildren<TMP_Text>().text);
            return list;
        }

        /// <summary>Settings from the main menu (P3-SET-02): this panel shows "Settings" with Back, and the full settings panel opens beside it; closing either closes both.</summary>
        private void OpenSettings()
        {
            _showSettings = true;
            var panel = ProcSense.VR.UI.SettingsUi.SettingsPanel.Open ?? ProcSense.VR.UI.SettingsUi.SettingsPanel.Toggle(transform);
            if (panel != null) panel.Closed += () => { if (_showSettings) { _showSettings = false; Refresh(); } };
            Refresh();
        }

        private void CloseSettings()
        {
            _showSettings = false;
            if (ProcSense.VR.UI.SettingsUi.SettingsPanel.Open != null) ProcSense.VR.UI.SettingsUi.SettingsPanel.Open.Close();
            Refresh();
        }

        // ------------------------------------------------------------------ drawing

        public void Refresh()
        {
            if (_flow == null) return;
            if (_flow.Phase != MenuPhase.Results && _debrief != null) CloseDebrief();
            for (int i = 0; i < _buttons.Count; i++) { _buttons[i].gameObject.SetActive(false); _buttons[i].interactable = true; Resize(_buttons[i], _home[i].at, _home[i].size); }
            _settings.gameObject.SetActive(false);   // the tablet keeps this older tab; the menu opens the full settings panel (P3-SET-02)
            _status.text = Say(_flow.Message);
            _body.enableAutoSizing = false;
            _body.fontSize = 24f;
            if (_showSettings)
            {
                _title.text = "Settings";
                _body.text = "The settings are on the panel beside this one. Changes apply at once.";
                Slot(0, "Back", CloseSettings, 560f);
                return;
            }
            switch (_flow.Phase)
            {
                case MenuPhase.Login: DrawLogin(); break;
                case MenuPhase.Consent: DrawConsent(); break;
                case MenuPhase.MainMenu: DrawMainMenu(); break;
                case MenuPhase.Surgery: DrawSurgery(); break;
                case MenuPhase.Briefing: DrawBriefing(); break;
                case MenuPhase.Playing: DrawPlaying(); break;
                default: DrawResults(); break;
            }
        }

        private void DrawLogin()
        {
            _title.text = "Welcome to ProcSense";
            _body.text = "Sign in to continue.";
            Slot(0, "Continue (signed in)", () => _flow.SignedIn(), -1f, ButtonKind.Primary);
        }

        /// <summary>
        /// The consent step: the service's required questions (title and text as the server gives them, with its version), Agree and Decline. Nothing is answered for the player. The check and
        /// each answer go through ConsentStep (the signed-in identity's token); MenuFlow opens the main menu only when the service says the answers are complete.
        /// </summary>
        private void DrawConsent()
        {
            var step = _flow.ConsentStep;
            _title.text = "Your consent";
            if (step == null) { _body.text = ""; return; }
            if (step.Status == ConsentStatus.Unknown) { _body.text = "Checking your privacy answers with the service..."; Run(step.Load(_flow.ConsentUpdated)); return; }
            if (step.Busy) { _body.text = step.Status == ConsentStatus.Saving ? "Saving your answer..." : "Checking your privacy answers with the service..."; return; }
            switch (step.Status)
            {
                case ConsentStatus.Needed:
                {
                    var sb = new StringBuilder();
                    sb.Append("Before your training sessions can be stored, please read and answer (version ").Append(step.Version).Append(").\n\n");
                    foreach (var q in step.Questions) sb.Append("<b>").Append(q.Title).Append("</b>\n").Append(q.Text).Append("\n\n");
                    sb.Append("Agree: you accept all of the above. Decline: nothing is stored and training cannot continue.");
                    _body.text = sb.ToString();
                    _body.enableAutoSizing = true;
                    _body.fontSizeMin = 12f;
                    _body.fontSizeMax = 24f;
                    Slot(0, "Agree", () => Run(step.Answer(true, _flow.ConsentUpdated)), -1f, ButtonKind.Primary);
                    Slot(1, "Decline", () => Run(step.Answer(false, _flow.ConsentUpdated)));
                    break;
                }
                case ConsentStatus.Declined:
                    _body.text = "You declined. Your sessions will not be stored, and training as this user cannot continue.";
                    Slot(0, "Review again", () => Run(step.Load(_flow.ConsentUpdated)));
                    Slot(1, "Quit", Application.Quit);
                    break;
                case ConsentStatus.Agreed:
                    _body.text = "";
                    break;
                default:   // Unreachable, WebOnly, Failed: the reason is in the status line
                    _body.text = "Your consent could not be checked or saved.";
                    if (step.Status != ConsentStatus.WebOnly) Slot(0, "Try again", () => Run(step.Load(_flow.ConsentUpdated)));
                    Slot(1, "Quit", Application.Quit);
                    break;
            }
        }

        /// <summary>Runs a consent request on this panel (only while it is shown; OnEnable redraws and starts it otherwise).</summary>
        private void Run(System.Collections.IEnumerator request)
        {
            if (!isActiveAndEnabled) return;
            StartCoroutine(request);                                                     // runs at once up to its first wait; an answer that is already there redraws through MenuFlow.Changed
            var step = _flow.ConsentStep;
            if (step != null && step.Busy && _flow.Phase == MenuPhase.Consent) Refresh();  // shows "checking" or "saving" while the request is out
        }

        private void DrawMainMenu()
        {
            _title.text = "ProcSense";
            _body.text = "Choose how you want to practise.\n\nTraining: help, goals and hints are available.\nTest / Examination: no goal list; hints cost points.";
            Slot(0, "TRAINING MODE", () => _flow.ChooseMode(SessionMode.Practice), -1f, ButtonKind.Primary);
            Slot(1, "TEST / EXAMINATION MODE", () => _flow.ChooseMode(SessionMode.Assessment), -1f, ButtonKind.Primary);
            Slot(2, "Settings", OpenSettings);
            Slot(3, "Quit", Application.Quit, -1f, ButtonKind.Danger);
            if (_flow.Interrupted != null)
            {
                Slot(4, "Resume interrupted session (" + _flow.Interrupted.CaseId + ", " + _flow.Interrupted.ActionsSaved + " actions)", () => _flow.Resume(), -1f, ButtonKind.Primary);
                Slot(5, "Discard it", _flow.Discard);
            }
        }

        private void DrawSurgery()
        {
            bool training = _flow.Mode == SessionMode.Practice;
            _title.text = training ? "Training: choose the surgery" : "Examination: choose the surgery";
            _body.text = "You prepare the patient for the operation; you do not perform it. A random valid patient is drawn for the surgery you choose.";
            ProcedureCard(0, "Gallbladder removal", "Laparoscopic cholecystectomy", "cholecystectomy", training);
            ProcedureCard(1, "Appendectomy", "Appendicectomy", "appendicectomy", training);
            if (training) Slot(2, "Level: " + _flow.Level, () => _flow.SetLevel(NextLevel(_flow.Level)));   // P3-MNU-05: every level Core defines, in order
            Slot(3, "Back", _flow.BackToMenu);
        }

        /// <summary>
        /// A procedure card (P3-MNU-04): the plain name, the operation's name and "pre-operative preparation", and how many cases the chosen mode can draw. With none (the examination before any
        /// case is approved, open decision D1) the card is shown disabled with the reason instead of failing after a press. The label still starts with the plain name.
        /// </summary>
        private void ProcedureCard(int slot, string name, string operation, string procedure, bool training)
        {
            int n = _flow.CaseCount(_flow.Mode, procedure);
            string detail = n > 0 ? operation + "\nPre-operative preparation · " + n + (n == 1 ? " case" : " cases")
                                  : training ? "No cases are available for training yet" : "No cases are approved for examination yet";
            Slot(slot, name + " (" + n + " cases)\n<size=20>" + detail + "</size>", () => _flow.ChooseSurgery(procedure, _flow.Level), -1f, n > 0 ? ButtonKind.Primary : ButtonKind.Secondary,
                 new Vector2(32 + slot * 426, 196), new Vector2(410, 160));
            _buttons[slot].interactable = n > 0;
        }

        /// <summary>The next training level after this one, through every value of Core's <see cref="TrainingLevel"/> (P3-MNU-05), so a level Core adds appears here by itself.</summary>
        public static TrainingLevel NextLevel(TrainingLevel level)
        {
            var all = (TrainingLevel[])System.Enum.GetValues(typeof(TrainingLevel));
            return all[(System.Array.IndexOf(all, level) + 1) % all.Length];
        }

        private void DrawBriefing()
        {
            var b = _flow.Briefing;
            _title.text = "Briefing";
            _body.text = b.Name + ", " + b.Age + " (" + b.Sex + ")\nSetting: " + b.Setting + "\nReason for admission: " + b.Reason
                + "\n\nScenario " + _flow.Plan.CaseId + ", seed " + _flow.Plan.Seed + " (shown again on the results).";
            Slot(0, "Begin", () => _flow.Begin(), -1f, ButtonKind.Primary);
            Slot(1, "Back", _flow.BackToMenu);
        }

        private void DrawPlaying()
        {
            var host = _flow.Host;
            var last = host != null && host.Session != null ? host.Session.LastHint : null;
            int used = host != null && host.Session != null ? host.Session.Hints.Count : 0;
            _title.text = "Patient preparation";
            _body.text = last == null ? "Prepare the patient for theatre, then submit them." : "Hint " + used + " of " + host.Session.HintLimit + ": " + last.Text;
            Slot(0, "Hint", () => _flow.Hint());
            if (_flow.SubmitArmed)
            {
                Slot(1, "Confirm: submit patient to OT", () => _flow.Submit(), -1f, ButtonKind.Danger);
                Slot(2, "Cancel", _flow.CancelSubmit);
            }
            else Slot(1, "Submit patient to OT", () => _flow.Submit());
        }

        private void DrawResults()
        {
            var s = _flow.Summary;
            _title.text = s.Headline;
            var sb = new StringBuilder();
            sb.Append("Result: ").Append(s.Result).Append("  |  ").Append(s.EndState).Append('\n');
            if (s.Grade != null) sb.Append("Score ").Append(s.Grade.Score100).Append(" / 100, grade ").Append(s.Grade.Letter).Append(s.Grade.Passed ? " (passed)" : " (not passed)").Append('\n');
            sb.Append("Points: +").Append(s.CreditPoints).Append(" / -").Append(s.PenaltyPoints).Append("  |  Critical items: ").Append(s.CriticalCount).Append('\n');
            sb.Append("Hints used: ").Append(s.HintsUsed).Append(s.HintsCharged > 0 ? " (charged)" : "").Append("  |  Time: ").Append(s.TotalMinutes.ToString("0.#")).Append(" min\n");
            if (s.StageTimes.Count > 0) sb.Append("Stages: ").Append(string.Join(", ", s.StageTimes.Select(t => t.Stage + " " + t.Minutes.ToString("0.#")))).Append(" min\n");
            if (s.MissedGoals.Count > 0 && s.Mode != "assessment") sb.Append("Missed: ").Append(string.Join(", ", s.MissedGoals)).Append('\n');   // the examination shows no goal list (P3-EXM-09 leak test)
            RequestRewording(s);
            bool reworded = _reworded != null && _reworded.Reworded && ReferenceEquals(_rewordFor, s);
            if (reworded) sb.Append(DebriefRewriteClient.LabelLine).Append('\n');                  // P3-RES-07: AI wording is always labelled; the lines above are the engine's
            foreach (var r in reworded ? _reworded.Lines : s.Recommendations) sb.Append("- ").Append(r).Append('\n');
            sb.Append("Scenario: ").Append(_flow.Briefing != null ? _flow.Briefing.Name : "");
            _body.text = sb.ToString();
            Slot(0, "Retry", () => _flow.Retry(), -1f, ButtonKind.Primary);
            Slot(1, "New scenario", () => _flow.NewScenario());
            Slot(2, "Main menu", _flow.BackToMenu);
            Slot(3, _debrief != null ? "Hide debrief" : "Debrief", ToggleDebrief);
        }

        /// <summary>
        /// Asks the service once per result for the optional AI wording (P3-RES-07). Not signed in, not playing or offline: nothing is asked and the engine's deterministic lines stay. The answer
        /// replaces only the recommendation lines, and only for the result it was asked for.
        /// </summary>
        private void RequestRewording(ProcSense.Core.Flow.ResultSummary s)
        {
            if (ReferenceEquals(_rewordFor, s)) return;
            _rewordFor = s;
            _reworded = null;
            var auth = DebriefRewriteClient.Auth?.Invoke();
            var host = _flow.Host;
            if (auth == null || !auth.IsSignedIn || host == null || host.Session == null || host.Content == null || !Application.isPlaying || !isActiveAndEnabled) return;
            string report = ProcSense.Core.Scoring.DebriefFacts.Build(host.Session, host.Content).ToJson();
            _drawingResults = true;
            StartCoroutine(DebriefRewriteClient.Fetch(auth, report, s.Recommendations, r =>
            {
                if (!ReferenceEquals(_rewordFor, s)) return;                                     // the player moved on to another result
                _reworded = r;
                if (!_drawingResults && _flow.Phase == MenuPhase.Results) Refresh();
            }));
            _drawingResults = false;
        }

        private void Slot(int slot, string label, UnityAction onClick, float y = -1f, ButtonKind kind = ButtonKind.Secondary, Vector2? at = null, Vector2? size = null)
        {
            var b = _buttons[slot];
            if (at.HasValue || size.HasValue) Resize(b, at.HasValue ? new Vector2(at.Value.x, -at.Value.y) : ((RectTransform)b.transform).anchoredPosition, size ?? ((RectTransform)b.transform).sizeDelta);
            b.gameObject.SetActive(true);
            var text = b.GetComponentInChildren<TMP_Text>();
            text.text = label;
            Paint(b, text, kind);
            b.onClick.RemoveAllListeners();
            b.onClick.AddListener(onClick);
            if (y >= 0f) { var r = (RectTransform)b.transform; r.anchoredPosition = new Vector2(r.anchoredPosition.x, -y); }
        }

        private static void Resize(Button b, Vector2 anchored, Vector2 size)
        {
            var r = (RectTransform)b.transform;
            r.anchoredPosition = anchored;
            r.sizeDelta = size;
            var label = b.GetComponentInChildren<TMP_Text>(true);
            if (label != null) label.rectTransform.sizeDelta = new Vector2(size.x - 2 * UiTheme.Current.Spacing[2], size.y);
        }

        /// <summary>The look of a pooled button for this slot: teal for the way forward, red for leaving or the irreversible step, light glass otherwise.</summary>
        private static void Paint(Button b, TMP_Text label, ButtonKind kind)
        {
            var glass = (GlassPanel)b.targetGraphic;
            var theme = UiTheme.Current;
            bool solid = kind == ButtonKind.Primary || kind == ButtonKind.Danger;
            glass.PanelStyle = solid ? GlassPanel.Style.Solid : GlassPanel.Style.Light;
            glass.color = kind == ButtonKind.Primary ? theme.Action : kind == ButtonKind.Danger ? theme.Danger : Color.white;
            label.color = solid ? theme.TextOnAccent : theme.TextPrimary;
        }

        private string Say(string code)
        {
            switch (code)
            {
                case "": return "";
                case MenuFlow.ConfirmNeeded: return "Press again to confirm: the patient goes to theatre and the case ends.";
                case MenuFlow.NotAllowedNow: return "That is not available right now.";
                case "HINT_LIMIT": return "You have used all " + (_flow.Host != null && _flow.Host.Session != null ? _flow.Host.Session.HintLimit : ExamControl.DefaultHintLimit) + " hints.";
                case "NO_HINT_AVAILABLE": return "There is nothing to hint at right now.";
                case "NO_CASE_AVAILABLE": return "No case is available for this choice. Examination cases need an approved case record first.";
                default: return code;
            }
        }
    }
}
