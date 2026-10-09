using System;
using System.Collections.Generic;
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

        public ActionOutcome Apply(SimAction action)
        {
            var outcome = new ActionOutcome();
            if (!Verbs.IsKnown(action.Verb))
                outcome.Reject(VerbRegistry.UnknownVerb);
            else
                for (int i = 0; i < _pipeline.Count && outcome.Accepted; i++)
                    _pipeline[i].OnAction(action, outcome);

            LastRejection = outcome.RejectReason;
            Log.Append(Clock.Steps, action, outcome);
            return outcome;
        }

        /// <summary>Moves time forward and runs every subsystem once per whole step.</summary>
        public void Tick(double dt)
        {
            int steps = Clock.Advance(dt);
            for (int n = 0; n < steps; n++)
                for (int i = 0; i < _pipeline.Count; i++) _pipeline[i].OnTick();
        }
    }
}
