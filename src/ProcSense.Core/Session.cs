using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using ProcSense.Core.Actions;
using ProcSense.Core.Content;
using ProcSense.Core.Logging;
using ProcSense.Core.Observation;
using ProcSense.Core.Pipeline;
using ProcSense.Core.Random;
using ProcSense.Core.State;
using ProcSense.Core.Time;

namespace ProcSense.Core
{
    public enum SessionMode { Practice, Assessment }

    /// <summary>
    /// Practice level: a beginner has safety nets (the nurse challenges an unsafe request), the easier case pool (no stacked cases) and the automatic practice hint; an advanced trainee gets every
    /// case, no automatic hint, and sees the consequence of an unsafe request. Intermediate (P3-MNU-05) sits between: every case and no automatic hint, but the nurse still challenges an unsafe
    /// request. Assessment mode ignores it. Saved as its name ("intermediate"), never as a number, so adding it changed no saved log.
    /// </summary>
    public enum TrainingLevel { Beginner, Intermediate, Advanced }

    /// <summary>Optional settings of a session that are part of what is replayed: the training level and which variant of the case is played (for example B2 of VAR-S1-B).
    /// Procedure and Source (P3-SES-06) record the request the session was planned from ("" = not from a plan / authored); HintLimit (P3-EXM-01) comes from content/scoring/grades.json "hint_limit".
    /// Each is written to the saved header only when it is not the default, so older logs read the same.</summary>
    public sealed record SessionOptions(TrainingLevel Level = TrainingLevel.Beginner, string Variant = "", bool Noise = false, string PulseTrace = "", string PlannedEvents = "", string Procedure = "", string Source = "", int HintLimit = ExamControl.DefaultHintLimit);

    /// <summary>Verbs and rejection codes the Session itself owns (pause, resume, time-skip). They are logged like any action, so a replay reproduces them.</summary>
    public static class SessionControl
    {
        public const string Pause = "pause";
        public const string Resume = "resume";
        public const string TimeSkip = "time_skip";
        public const string MinutesParam = "minutes";

        public const string SessionPaused = "SESSION_PAUSED";
        public const string NotPaused = "NOT_PAUSED";
        public const string BadMinutes = "BAD_MINUTES";

        /// <summary>Longest single skip: one day of simulated time.</summary>
        public const double MaxSkipMinutes = 24 * 60;
    }

    /// <summary>
    /// The examination verbs the Session itself owns (P3-EXM-01, P3-EXM-05). They are logged like any action, so a replay reproduces them.
    /// request_hint: the next pending goal's hint, in two tiers (first request for a goal: what to think about; the next: what to do), at most HintLimit per session. It never changes the clinical state.
    /// It is recorded as the "info.hint_used" score item in assessment only (the 5-point cost per hint is applied to the 100-point score, never to the engine verdict). submit_patient: ends the case at the trainee's decision (outcome "proceed" by default, or "transfer_request"; abandoned=true when the
    /// trainee leaves): goals still pending are missed through the existing path and every later action is refused.
    /// </summary>
    public static class ExamControl
    {
        public const string RequestHint = "request_hint";
        public const string SubmitPatient = "submit_patient";
        public const string OutcomeParam = "outcome";
        public const string AbandonedParam = "abandoned";
        public const string OutcomeProceed = "proceed";
        public const string OutcomeTransfer = "transfer_request";

        public const string HintLimitReached = "HINT_LIMIT";
        public const string NoHintAvailable = "NO_HINT_AVAILABLE";
        public const string SessionSubmitted = "SESSION_SUBMITTED";
        public const string BadOutcome = "BAD_OUTCOME";
        public const string HintScoreItem = "info.hint_used";   // the rubric severity "info" is 0 points: the 5-point cost is applied by ResultGrade (grades.json hint_penalty), once per hint, in assessment only

        /// <summary>The hint limit when content/scoring/grades.json gives none (P3-EXM-01). The session's own limit is Session.HintLimit (SessionOptions.HintLimit, from grades.json "hint_limit").</summary>
        public const int DefaultHintLimit = 3;

        /// <summary>Kept for older callers: the default limit. Read Session.HintLimit for the limit a session actually applies.</summary>
        public const int HintLimit = DefaultHintLimit;
    }

    /// <summary>One hint the trainee asked for: which goal, which tier (1 or 2) and the text shown.</summary>
    public sealed record HintGiven(string GoalId, int Tier, string Text, long Step);

    /// <summary>One training run. Every change goes through Apply (trainee actions) or Tick (time), so it is all logged and replayable. Starts at the case's own clock time.</summary>
    public sealed class Session
    {
        private readonly List<ISubsystem> _pipeline;

        public Session(CaseCard card, ulong seed, SessionMode mode, IEnumerable<ISubsystem> subsystems, SessionOptions? options = null)
        {
            Case = card;
            Level = options?.Level ?? TrainingLevel.Beginner;
            Variant = options?.Variant ?? "";
            Noise = options?.Noise ?? false;
            PulseTrace = options?.PulseTrace ?? "";
            PlannedEvents = options?.PlannedEvents ?? "";
            Procedure = options?.Procedure ?? "";
            Source = options?.Source ?? "";
            HintLimit = options?.HintLimit ?? ExamControl.DefaultHintLimit;
            Seed = seed;
            Mode = mode;
            Patient = PatientFactory.Create(card);
            Clock = new SimClock(card.Timing.TimeNow);
            Random = new SimRandom(seed);
            if (Variant.Length > 0) Patient.Clinical.SetFlag("variant." + Variant);      // the decision step reads this flag to know which variant's end state applies
            _pipeline = subsystems.OrderBy(s => s.Order).ToList(); // OrderBy is stable
            Verbs.Register(SessionControl.Pause);
            Verbs.Register(SessionControl.Resume);
            Verbs.Register(SessionControl.TimeSkip);
            Verbs.Register(ExamControl.RequestHint);
            Verbs.Register(ExamControl.SubmitPatient);
            foreach (var s in _pipeline) s.Init(this);
        }

        public CaseCard Case { get; }
        public ulong Seed { get; }
        public SessionMode Mode { get; }

        /// <summary>The one check every view uses for practice-only help (hint chip, goal list, consent element tracker, nurse prompts): true in practice, false in assessment (p0-evaluation-performance-s04).</summary>
        public bool PracticeAids => Mode == SessionMode.Practice;
        public TrainingLevel Level { get; }
        public string Variant { get; }

        /// <summary>The procedure and case source of the request the session was planned from (P3-SES-06), "" when it was not started from a plan or the source is authored. Saved in the header when set.</summary>
        public string Procedure { get; }
        public string Source { get; }

        /// <summary>How many hints this session gives (P3-EXM-01): content/scoring/grades.json "hint_limit", default 3. Saved in the header when not the default.</summary>
        public int HintLimit { get; }

        /// <summary>Whether the live vital signs carry small seeded flicker (LitePhysio noise); off by default so exact-value tests and the golden runs stay exact.</summary>
        public bool Noise { get; }

        /// <summary>The id of a recorded Pulse run (content/physio/pulse_traces) that drives the vital signs instead of LitePhysio, or empty (the default). Saved in the session header so a replay uses the same engine.</summary>
        public string PulseTrace { get; }

        /// <summary>
        /// The event ids a generated case plans for this session (comma separated), or empty (the default: every approved event of the mode may fire, as before). In practice mode the Event Director considers only these, so the plan composer's
        /// choice of events (features A5 and A8) decides which of the approved events can fire; assessment mode ignores it (its schedule is fixed). Saved in the session header so a replay plays the same events.
        /// </summary>
        public string PlannedEvents { get; }
        public PatientState Patient { get; }
        public SimClock Clock { get; }
        public SimRandom Random { get; }
        public VerbRegistry Verbs { get; } = new VerbRegistry();
        public EventBus Events { get; } = new EventBus();
        public SessionLog Log { get; } = new SessionLog();

        /// <summary>The workflow content for this case (goals, decision points, blockers). Set by the readiness subsystem; empty when the case has none.</summary>
        public Workflow.CaseGraph Graph { get; internal set; } = Workflow.CaseGraph.Empty;

        /// <summary>The goal and decision tracker, or null when the session runs without one.</summary>
        internal Workflow.CaseGraphSubsystem? Tracker { get; set; }

        /// <summary>The result review system (critical calls, interpretation), or null when the session runs without one.</summary>
        internal Workflow.Steps.ResultReviewSubsystem? ResultReview { get; set; }
        internal Workflow.Steps.ProblemsSubsystem? ProblemsStep { get; set; }
        internal Workflow.Steps.SepsisSubsystem? SepsisStep { get; set; }
        internal Workflow.Steps.ClashSubsystem? ClashStep { get; set; }
        internal Workflow.Steps.DecisionSubsystem? DecisionStep { get; set; }
        internal Workflow.Steps.WardChecklistSubsystem? WardStep { get; set; }
        internal Events.EventDirector? DirectorStep { get; set; }

        /// <summary>The Event Director: what has fired, what is active and the cues on show (null when the session runs without one).</summary>
        public Events.EventDirector? Director => DirectorStep;
        internal Workflow.OrReadinessSubsystem? OrStep { get; set; }
        internal Workflow.Steps.HandoverSubsystem? HandoverStep { get; set; }
        internal Workflow.Steps.RecoveryHandoverSubsystem? RecoveryHandoverStep { get; set; }
        internal Workflow.Steps.IntraOpSubsystem? IntraOpStep { get; set; }
        internal Workflow.Steps.SafetyChecklistSubsystem? SafetyStep { get; set; }
        internal Workflow.EndStateSubsystem? EndStateStep { get; set; }
        internal Physiology.PhysiologySubsystem? PhysiologyStep { get; set; }
        internal Behaviour.BehaviourSubsystem? BehaviourStep { get; set; }
        internal Workflow.Steps.ConsentSubsystem? ConsentStep { get; set; }
        internal Diagnostics.DiagnosticsLoop? DiagnosticsStep { get; set; }
        internal Records.RecordsSubsystem? RecordsStep { get; set; }

        /// <summary>The patient record: the folder (this admission's paperwork) and the EMR (everything), and what the trainee opened (null when the session has none).</summary>
        public Records.RecordsSubsystem? Records => RecordsStep;

        /// <summary>The diagnostics loop: each test's trip (requested, patient away or sample sent, result ready, patient back, report delivered) for the views (null when the session has none).</summary>
        public Diagnostics.DiagnosticsLoop? Diagnostics => DiagnosticsStep;

        /// <summary>The consent conversation: elements explained, questions, capacity and whether consent is signed (null when the session has no consent step).</summary>
        public Workflow.Steps.ConsentSubsystem? Consent => ConsentStep;

        /// <summary>The patient's behaviour: attributes, mode and capacity (null when the session has no behaviour step).</summary>
        public Behaviour.BehaviourSubsystem? Behaviour => BehaviourStep;

        /// <summary>The physiology engine of the session, for the steps that nudge the patient (null when the session has none).</summary>
        public Physiology.PhysiologySubsystem? Physiology => PhysiologyStep;

        /// <summary>How the case ended, worked out from everything that happened (null when the session has no end-state step).</summary>
        public Workflow.EndStateSubsystem? EndStates => EndStateStep;

        /// <summary>The WHO safety checklist: phases, answers and what was contradicted (null when the session has no checklist step).</summary>
        public Workflow.Steps.SafetyChecklistSubsystem? Safety => SafetyStep;

        /// <summary>The SBAR handover: what was handed over and what was missing (null when the session has no handover step).</summary>
        public Workflow.Steps.HandoverSubsystem? Handover => HandoverStep;

        /// <summary>The handover to the recovery nurse after Sign Out, a ProcSense educational extension (null when the session has no such step).</summary>
        public Workflow.Steps.RecoveryHandoverSubsystem? RecoveryHandover => RecoveryHandoverStep;

        /// <summary>The intra-operative training event of the ProcSense educational extension (null when the session has no such step).</summary>
        public Workflow.Steps.IntraOpSubsystem? IntraOp => IntraOpStep;

        /// <summary>The theatre stations and the readiness of the theatre (null when the session has no theatre step).</summary>
        public Workflow.OrReadinessSubsystem? Or => OrStep;

        /// <summary>The ward checklist: what is ticked, whether it is signed (null when the session has no ward step).</summary>
        public Workflow.Steps.WardChecklistSubsystem? Ward => WardStep;

        /// <summary>The outcome the engine expects for the master decision now, for the score only (null when the session has no decision step). Never shown to the trainee.</summary>
        public Workflow.Steps.DecisionSubsystem? Decision => DecisionStep;

        /// <summary>Clashes raised so far and the medicine plans for the patient (null when the session has no clash step).</summary>
        public Workflow.Steps.ClashSubsystem? Clashes => ClashStep;

        /// <summary>Sepsis tiers, timers and lateness for the debrief (null when the session has no sepsis step).</summary>
        public Workflow.Steps.SepsisReport? Sepsis => SepsisStep?.Report();

        /// <summary>The problems found so far in this case, in the order to deal with them (null when the session has no problems step).</summary>
        public Clinical.ProblemRegistry? Problems => ProblemsStep?.Registry;

        /// <summary>Critical calls, interpretations and unreviewed results, with sim steps and score items.</summary>
        public IReadOnlyList<Workflow.GraphEvent> ResultReviewHistory => ResultReview?.History ?? (IReadOnlyList<Workflow.GraphEvent>)System.Array.Empty<Workflow.GraphEvent>();

        /// <summary>The test ordering system, or null when the session runs without one.</summary>
        internal Workflow.Steps.InvestigationsSubsystem? Investigations { get; set; }

        /// <summary>Tests ordered, their states, tests not indicated and tests missed, with sim steps and score items.</summary>
        public IReadOnlyList<Workflow.GraphEvent> InvestigationHistory => Investigations?.History ?? (IReadOnlyList<Workflow.GraphEvent>)System.Array.Empty<Workflow.GraphEvent>();

        /// <summary>The request, confirm and give system for medicines, fluids, oxygen and IV access, or null when the session runs without one.</summary>
        internal Clinical.MedicineWorkflowSubsystem? Medicine { get; set; }

        /// <summary>Treatments requested, confirmed, given or challenged, and failed checks, with sim steps and score items.</summary>
        public IReadOnlyList<Workflow.GraphEvent> MedicineHistory => Medicine?.History ?? (IReadOnlyList<Workflow.GraphEvent>)System.Array.Empty<Workflow.GraphEvent>();

        /// <summary>The place and transfer system, or null when the session runs without one.</summary>
        internal Location.TransportSubsystem? Transport { get; set; }

        /// <summary>Transfer states, safety findings and the corridor event, with sim steps and score items.</summary>
        public IReadOnlyList<Workflow.GraphEvent> TransportHistory => Transport?.History ?? (IReadOnlyList<Workflow.GraphEvent>)System.Array.Empty<Workflow.GraphEvent>();

        /// <summary>The scenario flow map runner (stages, tracks, interrupts), or null when the session runs without one.</summary>
        internal Workflow.FlowSubsystem? Flow { get; set; }

        /// <summary>Stages started, finished or skipped and interrupts raised or resolved, with sim steps.</summary>
        public IReadOnlyList<Workflow.GraphEvent> FlowHistory => Flow?.History ?? (IReadOnlyList<Workflow.GraphEvent>)System.Array.Empty<Workflow.GraphEvent>();

        /// <summary>The workflow step numbers the trainee is in now (active stages not stopped by an interrupt); empty without a flow map. Speech evidence is judged against these.</summary>
        public IReadOnlyList<int> RunningSteps => Flow?.RunningSteps() ?? (IReadOnlyList<int>)System.Array.Empty<int>();

        /// <summary>Every stage of this scenario's map and its state (for the scorer and tests; the trainee only sees <see cref="ObservableState.Flow"/>).</summary>
        public IReadOnlyList<Observation.FlowStageStatus> FlowStages => Flow?.Stages() ?? (IReadOnlyList<Observation.FlowStageStatus>)System.Array.Empty<Observation.FlowStageStatus>();

        /// <summary>Goals completed or missed and decisions made, with sim steps and score items. The scorer reads this.</summary>
        public IReadOnlyList<Workflow.GraphEvent> GraphHistory => Tracker?.History ?? (IReadOnlyList<Workflow.GraphEvent>)System.Array.Empty<Workflow.GraphEvent>();

        /// <summary>Reason the most recent action was rejected, or "" if it was accepted.</summary>
        public string LastRejection { get; private set; } = "";

        /// <summary>What the trainee can perceive right now. Built on request, so it is never stale.</summary>
        public ObservableState Observable => ObservableBuilder.Build(this);

        /// <summary>True while the session is paused: time does not move and every action except resume is rejected.</summary>
        public bool Paused { get; private set; }

        private string? _attention;

        /// <summary>A subsystem calls this (from OnTick) when something needs the trainee now, e.g. a critical result. A running time-skip stops after that step and reports the reason.</summary>
        public void RequestAttention(string reason) { if (_attention == null) _attention = reason; }

        public ActionOutcome Pause() => Apply(new SimAction(SessionControl.Pause, "", Channel.System));

        public ActionOutcome Resume() => Apply(new SimAction(SessionControl.Resume, "", Channel.System));

        /// <summary>Runs the same fixed 0.5 s steps as normal play, through the whole pipeline, so skipping N minutes equals playing N minutes. Stops early if a subsystem asks for attention.</summary>
        public ActionOutcome TimeSkip(double minutes) => Apply(new SimAction(SessionControl.TimeSkip, "", Channel.System,
            new Dictionary<string, string> { [SessionControl.MinutesParam] = minutes.ToString("R", CultureInfo.InvariantCulture) }));

        public ActionOutcome Apply(SimAction action)
        {
            var outcome = new ActionOutcome();
            long startStep = Clock.Steps;   // logged with the step the action started at (a time-skip moves the clock)
            if (!Verbs.IsKnown(action.Verb))
                outcome.Reject(VerbRegistry.UnknownVerb);
            else if (Submitted)
                outcome.Reject(ExamControl.SessionSubmitted);
            else if (Paused && action.Verb != SessionControl.Resume)
                outcome.Reject(SessionControl.SessionPaused);
            else if (action.Verb == ExamControl.RequestHint)
                RunHint(outcome);
            else if (action.Verb == ExamControl.SubmitPatient)
                RunSubmit(action, outcome);
            else if (action.Verb == SessionControl.Pause || action.Verb == SessionControl.Resume || action.Verb == SessionControl.TimeSkip)
                RunControl(action, outcome);
            else
                for (int i = 0; i < _pipeline.Count && outcome.Accepted; i++)
                    _pipeline[i].OnAction(action, outcome);

            LastRejection = outcome.RejectReason;
            Log.Append(startStep, action, outcome);
            return outcome;
        }

        /// <summary>Moves time forward and runs every subsystem once per whole step. Does nothing while paused (frames are not logged, the clock simply stands still).</summary>
        public void Tick(double dt)
        {
            if (Paused || Submitted) return;
            int steps = Clock.Advance(dt);
            Clock.Rewind(steps);                                   // each step is run with the clock at that step, so a long tick behaves exactly like many short ones
            for (int n = 0; n < steps; n++) { Clock.StepOnce(); RunStep(); }
        }

        private void RunStep()
        {
            for (int i = 0; i < _pipeline.Count; i++) _pipeline[i].OnTick();
        }

        // ---------------------------------------------------------------- Check (P3-SES-02)

        /// <summary>What Check needs to rebuild this session: the content and the subsystems factory it was started with (set by whoever starts the session).</summary>
        public sealed record CheckSource(Content.ContentPackage Content, Func<IEnumerable<ISubsystem>> Subsystems);

        /// <summary>The answer of Check: accepted, or the reason code Apply would give.</summary>
        public sealed record CheckResult(bool Accepted, string Reason);

        /// <summary>Set by the starter so that Check works. Without it Check throws.</summary>
        public CheckSource? Checker { get; set; }

        /// <summary>
        /// The same validation as Apply, without any state change: the session is rebuilt from its own log in a throw-away copy, brought to the same time, and the action is applied to the copy. This session,
        /// its log and its replay hash are never touched. The cost grows with the length of the log, so it is for greying out a button or a read-back, not for every frame.
        /// </summary>
        public CheckResult Check(SimAction action)
        {
            if (Checker == null) throw new InvalidOperationException("Check needs Session.Checker (content and a subsystems factory); the starter sets it.");
            var copy = Replay.SessionFile.Replay(Replay.SessionFile.Write(this), Checker.Content, Checker.Subsystems).Session;
            double dt = Clock.SimSeconds - copy.Clock.SimSeconds;
            if (dt > 0) copy.Tick(dt);
            var outcome = copy.Apply(action);
            return new CheckResult(outcome.Accepted, outcome.RejectReason);
        }

        // ---------------------------------------------------------------- examination verbs (P3-EXM-01, P3-EXM-05)

        private readonly List<HintGiven> _hints = new List<HintGiven>();

        /// <summary>True once the patient has been submitted: the case is closed, time stands still and every action is refused (SESSION_SUBMITTED).</summary>
        public bool Submitted { get; private set; }

        /// <summary>The outcome the trainee chose on submission ("proceed" or "transfer_request"), or "" before submission.</summary>
        public string SubmitOutcome { get; private set; } = "";

        /// <summary>True when the trainee left the session instead of finishing it (the results say "not completed").</summary>
        public bool Abandoned { get; private set; }

        /// <summary>The sim step the patient was submitted at, or -1.</summary>
        public long SubmittedAtStep { get; private set; } = -1;

        /// <summary>The goals still pending when the patient was submitted (they were missed at that step).</summary>
        public int MissedAtSubmit { get; private set; }

        /// <summary>Every hint given so far, in order.</summary>
        public IReadOnlyList<HintGiven> Hints => _hints;

        /// <summary>The newest hint, or null.</summary>
        public HintGiven? LastHint => _hints.Count == 0 ? null : _hints[_hints.Count - 1];

        private void RunHint(ActionOutcome outcome)
        {
            if (_hints.Count >= HintLimit) { outcome.Reject(ExamControl.HintLimitReached); return; }
            var next = Tracker?.NextGoalHint();
            if (next == null) { outcome.Reject(ExamControl.NoHintAvailable); return; }
            int tier = _hints.Any(h => h.GoalId == next.Value.Id) ? 2 : 1;
            string text = tier == 1 && next.Value.Hint.Length > 0 ? next.Value.Hint : next.Value.Label;
            _hints.Add(new HintGiven(next.Value.Id, tier, text, Clock.Steps));
            outcome.Effects.Add("hint:" + next.Value.Id + ":tier" + tier.ToString(CultureInfo.InvariantCulture) + ":" + _hints.Count.ToString(CultureInfo.InvariantCulture));
            if (Mode == SessionMode.Assessment) outcome.Effects.Add("score:" + ExamControl.HintScoreItem);     // the examination charges for a hint; practice does not
        }

        private void RunSubmit(SimAction action, ActionOutcome outcome)
        {
            string result = action.Params.TryGetValue(ExamControl.OutcomeParam, out var o) && o.Length > 0 ? o : ExamControl.OutcomeProceed;
            if (result != ExamControl.OutcomeProceed && result != ExamControl.OutcomeTransfer) { outcome.Reject(ExamControl.BadOutcome); return; }
            bool abandoned = action.Params.TryGetValue(ExamControl.AbandonedParam, out var a) && a == "true";
            MissedAtSubmit = Tracker?.MissPending() ?? 0;
            EndStateStep?.ResolveOnSubmit();
            Submitted = true;
            SubmitOutcome = result;
            Abandoned = abandoned;
            SubmittedAtStep = Clock.Steps;
            outcome.Effects.Add("submitted:" + result);
            outcome.Effects.Add("missed_at_submit:" + MissedAtSubmit.ToString(CultureInfo.InvariantCulture));
            if (abandoned) outcome.Effects.Add("abandoned");
        }

        private void RunControl(SimAction action, ActionOutcome outcome)
        {
            switch (action.Verb)
            {
                case SessionControl.Pause:
                    Paused = true;
                    outcome.Effects.Add("paused");
                    break;
                case SessionControl.Resume:
                    if (!Paused) { outcome.Reject(SessionControl.NotPaused); break; }
                    Paused = false;
                    outcome.Effects.Add("resumed");
                    break;
                default:
                    SkipTime(action, outcome);
                    break;
            }
        }

        private void SkipTime(SimAction action, ActionOutcome outcome)
        {
            if (!action.Params.TryGetValue(SessionControl.MinutesParam, out var text)
                || !double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var minutes)
                || double.IsNaN(minutes) || minutes <= 0 || minutes > SessionControl.MaxSkipMinutes)
            {
                outcome.Reject(SessionControl.BadMinutes);
                return;
            }

            long wanted = (long)Math.Round(minutes * 60 / SimClock.Step);
            long done = 0;
            _attention = null;
            while (done < wanted)
            {
                done += Clock.Advance(SimClock.Step);
                RunStep();
                if (_attention != null) break;
            }
            outcome.Effects.Add("skipped_steps:" + done.ToString(CultureInfo.InvariantCulture));
            if (_attention != null) outcome.Effects.Add("stopped:" + _attention);
            _attention = null;
        }
    }
}
