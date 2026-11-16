using System.Collections.Generic;
using System.Linq;
using System.Text;
using ProcSense.Core;
using ProcSense.VR.Auth;
using ProcSense.VR.UI.Accessibility;
using ProcSense.VR.Sync;
using ProcSense.VR.UI.Debrief;
using ProcSense.VR.Views;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace ProcSense.VR.Flow
{
    /// <summary>
    /// The glass menu of the game flow, built at run time with the UI kit (no scene editing): Main Menu (Training, Test, Settings, Quit), Surgery choice, the briefing card, the simulation bar (Hint,
    /// Submit patient to OT with a confirmation), the results panel (Retry, New scenario) and Settings (the existing readability and subtitle tab). One world-space canvas, one shared UI material,
    /// six pooled buttons and three labels (the Consent step between sign-in and the Main Menu shows the service's own consent questions with Agree and Decline, owner decision D2): no fullscreen transparent layer, no per-frame work (it redraws only when the flow changes). Every button calls MenuFlow, which sends the engine verbs.
    /// </summary>
    public sealed class FlowPanel : MonoBehaviour
    {
        private const float Width = 900f, Height = 620f;
        private const int Pool = 6;

        private MenuFlow _flow;
        private Canvas _canvas;
        private TMP_Text _title, _body, _status;
        private readonly List<Button> _buttons = new List<Button>();
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
            _canvas = UiKit.WorldCanvas("Game flow", parent, new Vector2(Width, Height), 0.0012f);
            var t = _canvas.transform;
            _title = UiKit.Label(t, "Title", "", new Vector2(20, 16), new Vector2(860, 50), 34f, TextAlignmentOptions.TopLeft);
            _body = UiKit.Label(t, "Body", "", new Vector2(20, 76), new Vector2(860, 290), 24f, TextAlignmentOptions.TopLeft);
            _status = UiKit.Label(t, "Status", "", new Vector2(20, 568), new Vector2(860, 44), 20f, TextAlignmentOptions.TopLeft, UiKit.WarnColor);
            for (int i = 0; i < Pool; i++)
                _buttons.Add(UiKit.MakeButton(t, "", new Vector2(20 + (i % 2) * 440, 380 + (i / 2) * 62), new Vector2(420, 54), 24f));

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

        // ------------------------------------------------------------------ drawing

        public void Refresh()
        {
            if (_flow == null) return;
            if (_flow.Phase != MenuPhase.Results && _debrief != null) CloseDebrief();
            foreach (var b in _buttons) b.gameObject.SetActive(false);
            _settings.gameObject.SetActive(_showSettings);
            _status.text = Say(_flow.Message);
            _body.enableAutoSizing = false;
            _body.fontSize = 24f;
            if (_showSettings)
            {
                _title.text = "Settings";
                _body.text = "";
                Slot(0, "Back", () => { _showSettings = false; Refresh(); }, 560f);
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
            Slot(0, "Continue (signed in)", () => _flow.SignedIn());
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
                    Slot(0, "Agree", () => Run(step.Answer(true, _flow.ConsentUpdated)));
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
            Slot(0, "TRAINING MODE", () => _flow.ChooseMode(SessionMode.Practice));
            Slot(1, "TEST / EXAMINATION MODE", () => _flow.ChooseMode(SessionMode.Assessment));
            Slot(2, "Settings", () => { _showSettings = true; Refresh(); });
            Slot(3, "Quit", Application.Quit);
            if (_flow.Interrupted != null)
            {
                Slot(4, "Resume interrupted session (" + _flow.Interrupted.CaseId + ", " + _flow.Interrupted.ActionsSaved + " actions)", () => _flow.Resume());
                Slot(5, "Discard it", _flow.Discard);
            }
        }

        private void DrawSurgery()
        {
            bool training = _flow.Mode == SessionMode.Practice;
            _title.text = training ? "Training: choose the surgery" : "Examination: choose the surgery";
            _body.text = "A random valid patient is drawn for the surgery you choose.";
            Slot(0, "Gallbladder removal (" + _flow.CaseCount(_flow.Mode, "cholecystectomy") + " cases)", () => _flow.ChooseSurgery("cholecystectomy", _flow.Level));
            Slot(1, "Appendectomy (" + _flow.CaseCount(_flow.Mode, "appendicectomy") + " cases)", () => _flow.ChooseSurgery("appendicectomy", _flow.Level));
            if (training) Slot(2, "Level: " + _flow.Level, () => _flow.SetLevel(_flow.Level == TrainingLevel.Beginner ? TrainingLevel.Advanced : TrainingLevel.Beginner));
            Slot(3, "Back", _flow.BackToMenu);
        }

        private void DrawBriefing()
        {
            var b = _flow.Briefing;
            _title.text = "Briefing";
            _body.text = b.Name + ", " + b.Age + " (" + b.Sex + ")\nSetting: " + b.Setting + "\nReason for admission: " + b.Reason
                + "\n\nScenario " + _flow.Plan.CaseId + ", seed " + _flow.Plan.Seed + " (shown again on the results).";
            Slot(0, "Begin", () => _flow.Begin());
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
                Slot(1, "Confirm: submit patient to OT", () => _flow.Submit());
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
            Slot(0, "Retry", () => _flow.Retry());
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

        private void Slot(int slot, string label, UnityAction onClick, float y = -1f)
        {
            var b = _buttons[slot];
            b.gameObject.SetActive(true);
            b.GetComponentInChildren<TMP_Text>().text = label;
            b.onClick.RemoveAllListeners();
            b.onClick.AddListener(onClick);
            if (y >= 0f) { var r = (RectTransform)b.transform; r.anchoredPosition = new Vector2(r.anchoredPosition.x, -y); }
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
