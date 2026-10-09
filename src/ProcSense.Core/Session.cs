using System;
using System.Collections.Generic;
using System.Linq;
using ProcSense.Core.Actions;
using ProcSense.Core.Logging;
using ProcSense.Core.Pipeline;
using ProcSense.Core.Random;
using ProcSense.Core.Time;

namespace ProcSense.Core
{
    public enum SessionMode { Practice, Assessment }

    /// <summary>One training run. Every change goes through Apply (trainee actions) or Tick (time), so it is all logged and replayable. Content and case card arrive in the next step.</summary>
    public sealed class Session
    {
        private readonly List<ISubsystem> _pipeline;

        public Session(ulong seed, SessionMode mode, TimeSpan scenarioStart, IEnumerable<ISubsystem> subsystems)
        {
            Mode = mode;
            Clock = new SimClock(scenarioStart);
            Random = new SimRandom(seed);
            _pipeline = subsystems.OrderBy(s => s.Order).ToList(); // OrderBy is stable
            foreach (var s in _pipeline) s.Init(this);
        }

        public SessionMode Mode { get; }
        public SimClock Clock { get; }
        public SimRandom Random { get; }
        public VerbRegistry Verbs { get; } = new VerbRegistry();
        public EventBus Events { get; } = new EventBus();
        public SessionLog Log { get; } = new SessionLog();

        public ActionOutcome Apply(SimAction action)
        {
            var outcome = new ActionOutcome();
            if (!Verbs.IsKnown(action.Verb))
                outcome.Reject(VerbRegistry.UnknownVerb);
            else
                for (int i = 0; i < _pipeline.Count && outcome.Accepted; i++)
                    _pipeline[i].OnAction(action, outcome);

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
