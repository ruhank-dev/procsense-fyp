using System;
using System.Collections.Generic;
using System.Linq;
using ProcSense.Core.Actions;
using ProcSense.Core.Observation;
using ProcSense.Core.Pipeline;

namespace ProcSense.Core.Workflow
{
    /// <summary>
    /// Runs the scenario master map (20 stages) next to the goals. Several stages can be active at once, one per track (the patient line, the theatre
    /// preparation, S2's optimisation). An interrupt stage (deterioration, emergency) stops the tracks it names while it is active; those stages keep their
    /// place and carry on when the last interrupt is resolved. Like the goals, the map is looked at after every accepted action, never on a tick.
    /// </summary>
    public sealed class FlowSubsystem : ISubsystem
    {
        private sealed class StageRun
        {
            public FlowStageDef Def = null!;
            public FlowState State;
        }

        private readonly FlowMaps _maps;
        private readonly List<StageRun> _runs = new List<StageRun>();
        private readonly List<StageRun> _interrupts = new List<StageRun>();   // active interrupts, oldest first: the last one is the one being handled
        private readonly List<GraphEvent> _history = new List<GraphEvent>();
        private Session? _session;

        public FlowSubsystem(FlowMaps maps) { _maps = maps; }

        public int Order => PipelineOrder.Events + 1;

        /// <summary>Stages started, finished, skipped and interrupts raised, with the sim step. The scorer reads this.</summary>
        public IReadOnlyList<GraphEvent> History => _history;

        public void Init(Session session)
        {
            _session = session;
            session.Flow = this;
            var map = _maps.For(session.Case.Scenario);
            if (map != null)
                foreach (var def in map.Stages) _runs.Add(new StageRun { Def = def, State = FlowState.Waiting });
            Reevaluate();
        }

        public void OnAction(SimAction action, ActionOutcome outcome)
        {
            if (outcome.Accepted) Reevaluate();
        }

        public void OnTick() { }

        // ---------------------------------------------------------------- what can be read

        private bool IsSuspended(StageRun r)
        {
            if (r.Def.Interrupt) return false;
            foreach (var i in _interrupts)
                if (i.Def.Suspends.Contains(r.Def.Track)) return true;
            return false;
        }

        private static string Name(FlowState state) => state.ToString().ToLowerInvariant();

        private FlowStageStatus Status(StageRun r) =>
            new FlowStageStatus(r.Def.Id, r.Def.Title, r.Def.Track, r.State == FlowState.Active && IsSuspended(r) ? "suspended" : Name(r.State));

        /// <summary>Every stage of the scenario map with its state: waiting, active, suspended (active but stopped by an interrupt), done or skipped.</summary>
        public IReadOnlyList<FlowStageStatus> Stages() => _runs.Select(Status).ToList();

        /// <summary>The workflow step numbers (1 to 15) of the stages that are active and not suspended by an interrupt, ascending: where the trainee is now. Speech evidence is judged against these (ADR-0022).</summary>
        public IReadOnlyList<int> RunningSteps() => _runs.Where(x => x.State == FlowState.Active && !IsSuspended(x)).SelectMany(x => x.Def.Steps).Distinct().OrderBy(n => n).ToList();

        /// <summary>What the tablet shows: the stages running or stopped right now, interrupts first, then by stage number.</summary>
        internal IReadOnlyList<FlowStageStatus> Running()
        {
            var list = new List<FlowStageStatus>();
            foreach (var r in _runs.Where(x => x.State == FlowState.Active).OrderByDescending(x => x.Def.Interrupt).ThenBy(x => x.Def.Number)) list.Add(Status(r));
            return list;
        }

        // ---------------------------------------------------------------- evaluation

        private bool PrerequisitesDone(StageRun r)
        {
            foreach (var a in r.Def.After)
                foreach (var other in _runs)
                    if (other.Def.Id == a && other.State != FlowState.Done && other.State != FlowState.Skipped) return false;
            return true;
        }

        private void Reevaluate()
        {
            var state = RuleState.Build(_session!);
            bool progress;
            do
            {
                progress = false;
                foreach (var r in _runs)
                {
                    if (r.State == FlowState.Waiting)
                    {
                        if (!r.Def.Interrupt && !PrerequisitesDone(r)) continue;
                        if (!r.Def.AppliesIf.Test(state)) { Set(r, FlowState.Skipped, "stage_skipped"); progress = true; continue; }
                        if (!r.Def.StartIf.Test(state)) continue;
                        Set(r, FlowState.Active, r.Def.Interrupt ? "interrupt_raised" : "stage_start");
                        if (r.Def.Interrupt) _interrupts.Add(r);
                        progress = true;
                    }
                    else if (r.State == FlowState.Active && !IsSuspended(r) && r.Def.DoneIf.Test(state))
                    {
                        Set(r, FlowState.Done, r.Def.Interrupt ? "interrupt_resolved" : "stage_done");
                        if (r.Def.Interrupt) _interrupts.Remove(r);
                        progress = true;
                    }
                }
            } while (progress);
        }

        private void Set(StageRun r, FlowState to, string kind)
        {
            r.State = to;
            _history.Add(new GraphEvent(kind, r.Def.Id, _session!.Clock.Steps, Array.Empty<string>()));
        }
    }
}
