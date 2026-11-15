using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ProcSense.Core;
using ProcSense.Core.Actions;
using ProcSense.Core.Content;
using ProcSense.Core.Flow;
using ProcSense.Core.Workflow;
using ProcSense.VR.Auth;
using ProcSense.VR.Bridge;
using UnityEngine;

namespace ProcSense.VR.Flow
{
    /// <summary>Where the player is in the game flow: sign-in, the consent step, main menu, surgery choice, briefing, the simulation, or the results.</summary>
    public enum MenuPhase { Login, Consent, MainMenu, Surgery, Briefing, Playing, Results }

    /// <summary>
    /// The game flow (plan P3-02, P3-03, P3-07, P3-08), without any drawing: Login, Main Menu, Training or Test, Surgery, a random valid scenario (SessionPlanner and SessionStarter), the briefing,
    /// the simulation, Hint, Submit Patient to OT, the results (ResultSummary), Retry and New scenario. It changes the simulation only through <see cref="SimulationHost.Apply"/> with the engine
    /// verbs request_hint and submit_patient, so a menu button, a voice phrase (<see cref="FlowVoiceIntents"/>) and a test all take the same road. Core decides every rule; this class only sequences.
    /// </summary>
    public sealed class MenuFlow
    {
        public const string NotAllowedNow = "NOT_ALLOWED_NOW";
        public const string ConfirmNeeded = "CONFIRM_NEEDED";

        private readonly SimulationHost _host;
        private readonly CaseCatalogue _catalogue;
        private readonly IContentReader _reader;
        private readonly Func<ulong> _drawSeed;
        private readonly IReadOnlyDictionary<string, int> _credits;
        private readonly GradeTable _grades;
        private readonly List<string> _recent = new List<string>();
        private bool _submitPending;

        /// <summary>The flow the login view tells when sign-in succeeds (the last one made).</summary>
        public static MenuFlow Current { get; private set; }

        public SimulationHost Host => _host;

        public MenuFlow(SimulationHost host, IContentReader reader, Func<ulong> drawSeed, bool checkManifest = false)
        {
            _host = host;
            _reader = reader;
            _drawSeed = drawSeed;
            CheckManifest = checkManifest;
            _catalogue = CaseCatalogue.Parse(reader.ReadText("ui/case_catalogue.json"));
            _credits = CorrectPathCredits.Parse(reader.ReadText("scoring/correct_path_credits.json"));
            _grades = GradeTable.Parse(reader.ReadText("scoring/grades.json"));
            Current = this;
        }

        public MenuPhase Phase { get; private set; } = MenuPhase.Login;
        public SessionMode Mode { get; private set; } = SessionMode.Practice;
        public string Procedure { get; private set; } = "";
        public TrainingLevel Level { get; private set; } = TrainingLevel.Beginner;
        public SessionPlan Plan { get; private set; }
        public BriefingFacts Briefing { get; private set; }
        public ResultSummary Summary { get; private set; }
        public IReadOnlyList<StartSubstitution> Substitutions { get; private set; } = Array.Empty<StartSubstitution>();

        /// <summary>The last thing the player should be told (a refusal, a substitution), or "".</summary>
        public string Message { get; private set; } = "";

        /// <summary>True when the manifest is checked at every start (slower; the Core refuses the content when a file differs).</summary>
        public bool CheckManifest { get; set; }

        /// <summary>Save the session log and queue it for upload when the patient is submitted (off in tests).</summary>
        public bool EndSessionOnSubmit { get; set; } = true;

        public bool SubmitArmed => _submitPending;
        public int CaseCount(SessionMode mode, string procedure) => _catalogue.Count(mode, procedure);

        public event Action Changed;

        /// <summary>
        /// How the consent step is made for the identity that just signed in (owner decision D2): by default the signed-in service identity (the paired LAN demonstration user included) answers
        /// the service's own questions; null when nobody is signed in to a service (the offline demonstration: sessions stay on the headset, nothing is uploaded), so the menu opens directly.
        /// </summary>
        public static Func<ConsentStep> ConsentFactory { get; set; } = DefaultConsent;

        public static ConsentStep DefaultConsent() => AuthService.Current != null && AuthService.Current.IsSignedIn ? new ConsentStep(AuthService.Current) : null;

        /// <summary>The consent step of this sign-in, or null when there is none.</summary>
        public ConsentStep ConsentStep { get; private set; }

        public void SignedIn()
        {
            if (Phase != MenuPhase.Login) return;
            ConsentStep = ConsentFactory?.Invoke();
            if (ConsentStep != null) { Message = ""; Go(MenuPhase.Consent); return; }   // the panel asks the service (ConsentStep.Load) and calls ConsentUpdated
            OpenMainMenu();
        }

        /// <summary>
        /// Called after the consent step talked to the service (a check or an answer). The menu opens only when the service says the required answers are yes to the current text; a decline or a
        /// failure keeps the player on the consent step with the reason (nothing is uploaded and no session can be started from here).
        /// </summary>
        public void ConsentUpdated()
        {
            if (Phase != MenuPhase.Consent) return;
            if (ConsentStep == null || ConsentStep.Satisfied) { Message = ""; OpenMainMenu(); return; }
            Message = ConsentStep.Message;
            Changed?.Invoke();
        }

        private void OpenMainMenu()
        {
            FindInterrupted();
            Go(MenuPhase.MainMenu);
        }

        /// <summary>Training or Test, chosen only before a session starts (ModeChoice refuses it while one runs).</summary>
        public bool ChooseMode(SessionMode mode)
        {
            if (Phase != MenuPhase.MainMenu && Phase != MenuPhase.Surgery) return Refuse(NotAllowedNow);
            if (!ModeChoice.Choose(mode)) return Refuse(NotAllowedNow);
            Mode = mode;
            Message = "";
            Go(MenuPhase.Surgery);
            return true;
        }

        public void SetLevel(TrainingLevel level) { Level = level; Changed?.Invoke(); }

        /// <summary>Draws a random valid scenario for the procedure and loads it; on success the briefing is shown.</summary>
        public bool ChooseSurgery(string procedure, TrainingLevel level)
        {
            if (Phase != MenuPhase.Surgery) return Refuse(NotAllowedNow);
            Procedure = procedure;
            Level = level;
            return Start(new SessionRequest(Mode, procedure, level));
        }

        public bool Begin()
        {
            if (Phase != MenuPhase.Briefing) return Refuse(NotAllowedNow);
            Go(MenuPhase.Playing);
            return true;
        }

        /// <summary>The hint: the same request_hint action a voice phrase sends. The text is on the session's LastHint.</summary>
        public ActionOutcome Hint()
        {
            if (Phase != MenuPhase.Playing) return Rejected(NotAllowedNow);
            var outcome = _host.Apply(new SimAction(ExamControl.RequestHint, "", Channel.Vr));
            Message = outcome.Accepted ? "" : outcome.RejectReason;
            Changed?.Invoke();
            return outcome;
        }

        /// <summary>
        /// Submit the patient to the theatre. The first call arms it (the confirmation the plan asks for) and returns CONFIRM_NEEDED; the second sends submit_patient. One submission per session: the
        /// engine refuses a second. After an accepted submission the log is saved and queued for upload and the results are shown.
        /// </summary>
        public ActionOutcome Submit(bool abandoned = false)
        {
            if (Phase != MenuPhase.Playing) return Rejected(NotAllowedNow);
            if (!_submitPending && !abandoned) { _submitPending = true; Message = ConfirmNeeded; Changed?.Invoke(); return Rejected(ConfirmNeeded); }
            _submitPending = false;
            Message = "";
            var p = new Dictionary<string, string>();
            if (abandoned) p[ExamControl.AbandonedParam] = "true";
            var outcome = _host.Apply(new SimAction(ExamControl.SubmitPatient, "", Channel.Vr, p));
            if (outcome.Accepted)
            {
                Summary = ResultSummary.From(_host.Session, _host.Content, _credits.TryGetValue(_host.Session.Case.Id, out var c) ? c : (int?)null, _grades);
                if (EndSessionOnSubmit) _host.EndSession();
                Go(MenuPhase.Results);
            }
            else Changed?.Invoke();
            return outcome;
        }

        public void CancelSubmit() { _submitPending = false; Message = ""; Changed?.Invoke(); }

        /// <summary>Retry: the same case and seed, so the same events happen.</summary>
        public bool Retry()
        {
            if (Phase != MenuPhase.Results || Plan == null) return Refuse(NotAllowedNow);
            return Start(SessionPlanner.Retry(Plan, Level));
        }

        /// <summary>New scenario: a new seed, and another case when the pool has one.</summary>
        public bool NewScenario()
        {
            if (Phase != MenuPhase.Results) return Refuse(NotAllowedNow);
            return Start(new SessionRequest(Mode, Procedure, Level));
        }

        public void BackToMenu()
        {
            if (Phase == MenuPhase.Login || Phase == MenuPhase.Consent) { Refuse(NotAllowedNow); return; }   // the menu opens only after sign-in and the consent step
            ModeChoice.Unlock();
            Message = "";
            FindInterrupted();
            Go(MenuPhase.MainMenu);
        }

        // ------------------------------------------------------------------ interrupted sessions (P3-SES-09)

        /// <summary>A saved session that was left unfinished (not submitted), or null. The pause or quit already saved its log (SimulationHost); the menu offers Resume or Discard.</summary>
        public ResumeOffer Interrupted { get; private set; }
        public string InterruptedPath { get; private set; }

        /// <summary>Looks at the newest saved logs and keeps the first one that replays cleanly and was not submitted. A damaged or finished log is skipped, never offered.</summary>
        public void FindInterrupted(string directory = null)
        {
            Interrupted = null;
            InterruptedPath = null;
            string dir = directory ?? SimulationHost.SessionsDirectoryOverride ?? Path.Combine(Application.persistentDataPath, "sessions");
            if (Directory.Exists(dir))
            {
                foreach (var file in new DirectoryInfo(dir).GetFiles("*.jsonl").Where(f => !f.Name.Contains("_followup_")).OrderByDescending(f => f.LastWriteTimeUtc).Take(5))
                {
                    ResumeOffer offer;
                    try { offer = SessionResume.Open(File.ReadAllText(file.FullName), _reader, CheckManifest); }
                    catch (Exception e) when (e is IOException || e is UnauthorizedAccessException || e is ContentException || e is FormatException || e is InvalidCastException || e is ArgumentException) { continue; }
                    if (!offer.CanResume || offer.Session.Submitted) continue;
                    Interrupted = offer;
                    InterruptedPath = file.FullName;
                    break;
                }
            }
            Changed?.Invoke();
        }

        /// <summary>Continues the interrupted session exactly where it stopped (same state, same log hash). The old log file is removed: the next save writes the whole log again.</summary>
        public bool Resume()
        {
            if (Interrupted == null || Phase != MenuPhase.MainMenu) return Refuse(NotAllowedNow);
            var offer = Interrupted;
            var session = offer.Session;
            Mode = session.Mode;
            Level = session.Level;
            ModeChoice.Choose(Mode);
            _host.ResumeSession(session, offer.Content, _reader);
            var entry = _catalogue.Find(session.Case.Id);
            Procedure = entry != null ? entry.Procedure : "";
            Plan = new SessionPlan(session.Case.Id, session.Seed, session.Mode, new SessionOptions(session.Level, session.Variant, session.Noise, session.PulseTrace, session.PlannedEvents), Procedure, CaseSource.Authored, null);
            if (!_recent.Contains(Plan.CaseId)) _recent.Add(Plan.CaseId);
            Briefing = BriefingFacts.From(offer.Content.CaseFiles[Plan.CaseId]);
            Summary = null;
            _submitPending = false;
            Message = "";
            TryDelete(InterruptedPath);
            Interrupted = null;
            InterruptedPath = null;
            Go(MenuPhase.Playing);
            return true;
        }

        /// <summary>Throws the interrupted session away.</summary>
        public void Discard()
        {
            TryDelete(InterruptedPath);
            Interrupted = null;
            InterruptedPath = null;
            Changed?.Invoke();
        }

        private static void TryDelete(string path)
        {
            try { if (!string.IsNullOrEmpty(path) && File.Exists(path)) File.Delete(path); }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException) { }
        }

        private bool Start(SessionRequest request)
        {
            var result = SessionStarter.Start(_catalogue, request, _reader, _recent, _drawSeed, null, CheckManifest);
            Substitutions = result.Substitutions;
            if (result.Plan == null)
            {
                Message = result.Refusal ?? SessionStarter.NoStartableCase;
                if (Phase != MenuPhase.Results) Go(MenuPhase.Surgery); else Changed?.Invoke();
                return false;
            }
            var plan = result.Plan;
            _host.StartSession(plan.CaseId, plan.Seed, plan.Mode, _reader, plan.Options);
            var content = _host.Content;
            _host.Session.Checker = new Session.CheckSource(content, () => (_host.SubsystemFactory ?? Subsystems.Default)(content));
            Plan = plan;
            _recent.Add(plan.CaseId);
            Briefing = BriefingFacts.From(content.CaseFiles[plan.CaseId]);
            Summary = null;
            _submitPending = false;
            Message = result.Substitutions.Count > 0 ? "Another case was started: " + result.Substitutions[0].CaseId + " could not be used." : "";
            Go(MenuPhase.Briefing);
            return true;
        }

        private bool Refuse(string message) { Message = message; Changed?.Invoke(); return false; }

        private static ActionOutcome Rejected(string reason)
        {
            var o = new ActionOutcome();
            o.Reject(reason);
            return o;
        }

        private void Go(MenuPhase phase) { Phase = phase; Changed?.Invoke(); }
    }
}
