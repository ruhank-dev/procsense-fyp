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

    /// <summary>One training run. Every change goes through Apply (trainee actions) or Tick (time), so it is all logged and replayable. Starts at the case's own clock time.</summary>
    public sealed class Session
    {
        private readonly List<ISubsystem> _pipeline;

        public Session(CaseCard card, ulong seed, SessionMode mode, IEnumerable<ISubsystem> subsystems)
        {
            Case = card;
            Seed = seed;
            Mode = mode;
            Patient = PatientFactory.Create(card);
            Clock = new SimClock(card.Timing.TimeNow);
            Random = new SimRandom(seed);
            _pipeline = subsystems.OrderBy(s => s.Order).ToList(); // OrderBy is stable
            Verbs.Register(SessionControl.Pause);
            Verbs.Register(SessionControl.Resume);
            Verbs.Register(SessionControl.TimeSkip);
            foreach (var s in _pipeline) s.Init(this);
        }

        public CaseCard Case { get; }
        public ulong Seed { get; }
        public SessionMode Mode { get; }
        public PatientState Patient { get; }
        public SimClock Clock { get; }
        public SimRandom Random { get; }
        public VerbRegistry Verbs { get; } = new VerbRegistry();
        public EventBus Events { get; } = new EventBus();
        public SessionLog Log { get; } = new SessionLog();

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
            else if (Paused && action.Verb != SessionControl.Resume)
                outcome.Reject(SessionControl.SessionPaused);
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
            if (Paused) return;
            int steps = Clock.Advance(dt);
            for (int n = 0; n < steps; n++) RunStep();
        }

        private void RunStep()
        {
            for (int i = 0; i < _pipeline.Count; i++) _pipeline[i].OnTick();
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
