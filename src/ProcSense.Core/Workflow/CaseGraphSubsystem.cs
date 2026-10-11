using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using ProcSense.Core.Actions;
using ProcSense.Core.Pipeline;
using ProcSense.Core.Rules;

namespace ProcSense.Core.Workflow
{
    public enum GoalState { Pending, Complete, Missed }

    /// <summary>Published on the session's EventBus when a goal is missed or a decision enables an event. The scorer and the event director subscribe later.</summary>
    public sealed record WorkflowEvent(string Name, string Source);

    /// <summary>What happened to a goal or decision, with the sim step and the score items attached to it (for the scorer).</summary>
    public sealed record GraphEvent(string Kind, string Id, long Step, IReadOnlyList<string> ScoreItems);

    /// <summary>The facts rules can read. Built from the engine's truth (not from what the trainee can see), so goals judge what was really done.</summary>
    internal static class RuleState
    {
        /// <summary>Findings found by an airway check carry this region name; they are counted apart from the region findings.</summary>
        public const string AirwayRegion = "airway_check:";

        public static Dictionary<string, object?> Build(Session s)
        {
            var c = s.Patient.Clinical;
            var topics = new List<object?>();
            foreach (var t in c.RevealedTopics) topics.Add(t);
            var flags = new List<object?>();
            foreach (var f in c.Flags) flags.Add(f);
            var sensors = new List<object?>();
            foreach (var x in s.Patient.Physiological.AttachedSensors) sensors.Add(x);
            var measured = new List<object?>();
            foreach (var m in s.Patient.Physiological.Measured.Keys) measured.Add(m);
            var findings = new List<object?>();
            foreach (var f in c.Findings) findings.Add(f.Id);
            var airway = new List<object?>();
            foreach (var a in c.Airway.Keys) airway.Add(a);

            return new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["case"] = s.Case.Id,
                ["scenario"] = s.Case.Scenario,
                ["mode"] = s.Mode.ToString().ToLowerInvariant(),
                ["sim_seconds"] = s.Clock.SimSeconds,
                ["identity_confirmed"] = c.IdentityVerified,
                ["allergies_known"] = c.AllergiesKnown,
                ["wristband_read"] = c.WristbandRead,
                ["notes_opened"] = c.NotesOpened,
                ["allergy_band"] = c.AllergyBand,
                ["nurse"] = c.NurseState,
                ["location"] = c.Location,
                ["topics"] = topics,
                ["flags"] = flags,
                ["sensors"] = sensors,
                ["measured"] = measured,
                ["findings"] = findings,
                ["fasting"] = c.Fasting?.Status ?? "unknown",
                ["temperature_c"] = s.Patient.Physiological.Measured.TryGetValue("temperature_c", out var tm) && double.TryParse(tm.Value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double tempC) ? (object?)tempC : null,
                ["airway"] = airway,
                ["exam_total"] = (long)c.ExamTotal,
                ["exam_done"] = (long)c.Findings.Count(f => !f.Region.StartsWith(AirwayRegion, StringComparison.Ordinal)),
                ["airway_total"] = (long)c.AirwayTotal,
                ["airway_done"] = (long)c.Airway.Count,
                ["or_stations"] = c.OrStations.ToDictionary(kv => kv.Key, kv => (object?)kv.Value, StringComparer.Ordinal),
                ["has_allergy"] = s.Patient.Profile.Allergies.Count > 0,
                ["antibiotic_given_minutes_ago"] = (object?)s.SepsisStep?.GivenMinutesAgo("antibiotics"),
                ["imaging_viewed"] = c.Investigations.Any(x => x.Report != null && (x.State == "VIEWED" || x.State == "ACKNOWLEDGED")),
            };
        }
    }

    /// <summary>
    /// Runs before the other subsystems and refuses a verb while one of the readiness blockers for it is active (for example "no transfer without consent").
    /// The refusal code is BLOCKED:&lt;blocker id&gt;; the reason in words is shown to the trainee in practice mode and only logged in assessment mode.
    /// </summary>
    public sealed class ReadinessSubsystem : ISubsystem
    {
        private readonly CaseGraph _graph;
        private readonly Dictionary<string, List<BlockerDef>> _byVerb = new Dictionary<string, List<BlockerDef>>(StringComparer.Ordinal);
        private Session? _session;

        public ReadinessSubsystem(CaseGraph graph)
        {
            _graph = graph;
            foreach (var b in graph.Blockers)
                foreach (var verb in b.CheckAt)
                {
                    if (!_byVerb.TryGetValue(verb, out var list)) _byVerb[verb] = list = new List<BlockerDef>();
                    list.Add(b);
                }
        }

        public int Order => PipelineOrder.Gate;

        public void Init(Session session)
        {
            _session = session;
            session.Graph = _graph;
        }

        public void OnAction(SimAction action, ActionOutcome outcome)
        {
            if (!_byVerb.TryGetValue(action.Verb, out var blockers)) return;
            var state = RuleState.Build(_session!);
            foreach (var b in blockers)
                if (b.BlockedIf.Test(state))
                {
                    outcome.Reject(BlockedPrefix + b.Id);
                    return;
                }
        }

        public void OnTick() { }

        public const string BlockedPrefix = "BLOCKED:";
    }

    /// <summary>
    /// Keeps track of what the trainee still has to do: instantiates the goals that apply to the case, completes them as the rules say, misses them at their
    /// deadline, opens decision cards and records the choice against the expected one. A goal is only re-evaluated when a state key it reads has changed.
    /// </summary>
    public sealed class CaseGraphSubsystem : ISubsystem
    {
        public const string DecideVerb = "decide";
        public const string DecisionParam = "dp";
        public const string OptionParam = "option";
        public const string NotOpen = "DECISION_NOT_OPEN";
        public const string UnknownOption = "UNKNOWN_OPTION";

        private sealed class GoalRun
        {
            public GoalDef Def = null!;
            public GoalState State;
            public long AtStep = -1;
            public long DeadlineStep = -1;
            public bool NeedsEval = true;
            public bool Active;     // false until the goal's active_if holds; an inactive goal is not shown, hinted, or completed
        }

        private sealed class PointRun
        {
            public DecisionPointDef Def = null!;
            public bool Open;
            public bool Decided;
            public bool NeedsEval = true;
        }

        private readonly CaseGraph _graph;
        private readonly List<GoalRun> _goals = new List<GoalRun>();
        private readonly List<GoalRun> _withDeadline = new List<GoalRun>();
        private readonly List<PointRun> _points = new List<PointRun>();
        private readonly List<GraphEvent> _history = new List<GraphEvent>();
        private readonly HashSet<string> _changed = new HashSet<string>(StringComparer.Ordinal);
        private Dictionary<string, object?> _previous = new Dictionary<string, object?>(StringComparer.Ordinal);
        private Session? _session;
        private long _step;

        public CaseGraphSubsystem(CaseGraph graph) { _graph = graph; }

        public int Order => PipelineOrder.Events;

        public IReadOnlyList<GraphEvent> History => _history;

        public void Init(Session session)
        {
            _session = session;
            session.Tracker = this;
            session.Verbs.Register(DecideVerb);
            var state = RuleState.Build(session);
            foreach (var def in _graph.Goals)
            {
                if (!def.AppliesIf.Test(state)) continue;
                var run = new GoalRun { Def = def, Active = def.ActiveIf.Test(state) };
                if (def.DeadlineMinutes.HasValue)
                {
                    run.DeadlineStep = (long)Math.Round(def.DeadlineMinutes.Value * 60 / Time.SimClock.Step);
                    _withDeadline.Add(run);
                }
                _goals.Add(run);
            }
            foreach (var def in _graph.DecisionPoints) _points.Add(new PointRun { Def = def });
            Reevaluate(state);
        }

        public void OnAction(SimAction action, ActionOutcome outcome)
        {
            if (action.Verb == DecideVerb) Decide(action, outcome);
            if (outcome.Accepted) Reevaluate(RuleState.Build(_session!));
        }

        // A goal is missed once the clock reaches its deadline. An action applied at that exact moment arrives after the tick, so it is too late.
        public void OnTick()
        {
            _step++;
            for (int i = 0; i < _withDeadline.Count; i++)
            {
                var g = _withDeadline[i];
                if (g.Active && g.State == GoalState.Pending && _step >= g.DeadlineStep) Miss(g);
            }
        }

        // ---------------------------------------------------------------- what the trainee can see

        internal IReadOnlyList<Observation.GoalStatus> GoalStatuses()
        {
            var list = new List<Observation.GoalStatus>(_goals.Count);
            foreach (var g in _goals)
                if (g.Active) list.Add(new Observation.GoalStatus(g.Def.Id, g.Def.Label, g.State.ToString().ToLowerInvariant(),
                    g.AtStep < 0 ? "" : Observation.ObservableBuilder.Hms(_session!.Clock.Start + TimeSpan.FromSeconds(g.AtStep * Time.SimClock.Step))));
            return list;
        }

        internal IReadOnlyList<Observation.DecisionCard> OpenCards()
        {
            var list = new List<Observation.DecisionCard>();
            foreach (var p in _points)
                if (p.Open && !p.Decided)
                    list.Add(new Observation.DecisionCard(p.Def.Id, p.Def.Label, p.Def.Options.Select(o => new Observation.DecisionOption(o.Id, o.Label)).ToList()));
            return list;
        }

        /// <summary>The next goal worth suggesting (practice mode only), or "".</summary>
        internal string Hint()
        {
            foreach (var g in _goals)
                if (g.Active && g.State == GoalState.Pending && PrerequisitesDone(g)) return g.Def.Hint;
            return "";
        }

        // ---------------------------------------------------------------- evaluation

        private bool PrerequisitesDone(GoalRun g)
        {
            foreach (var a in g.Def.After)
                foreach (var other in _goals)
                    if (other.Def.Id == a && other.State != GoalState.Complete) return false;   // a goal that does not apply to this case is not waited for
            return true;
        }

        private void Reevaluate(Dictionary<string, object?> state)
        {
            _changed.Clear();
            foreach (var kv in state)
                if (!_previous.TryGetValue(kv.Key, out var old) || !Logic.Same(old, kv.Value)) _changed.Add(kv.Key);
            _previous = state;

            bool progress;
            do
            {
                progress = false;
                foreach (var g in _goals)
                {
                    if (g.State != GoalState.Pending) continue;
                    if (!g.Active)
                    {
                        if (!g.Def.ActiveIf.Test(state)) continue;
                        g.Active = true;
                        g.NeedsEval = true;
                    }
                    if (!PrerequisitesDone(g)) continue;
                    if (!g.NeedsEval && !Reads(g.Def.CompleteIf, _changed)) continue;
                    g.NeedsEval = false;
                    if (!g.Def.CompleteIf.Test(state)) continue;
                    g.State = GoalState.Complete;
                    g.AtStep = _session!.Clock.Steps;
                    _history.Add(new GraphEvent("goal_complete", g.Def.Id, g.AtStep, g.Def.ScoreItems));
                    foreach (var other in _goals)
                        if (other.State == GoalState.Pending && other.Def.After.Contains(g.Def.Id)) other.NeedsEval = true;
                    progress = true;
                }
            } while (progress);

            foreach (var p in _points)
            {
                if (p.Open || p.Decided) continue;
                if (!p.NeedsEval && !Reads(p.Def.Trigger, _changed)) continue;
                p.NeedsEval = false;
                if (p.Def.Trigger.Test(state)) p.Open = true;
            }
        }

        private static bool Reads(Logic rule, HashSet<string> changed)
        {
            if (rule.ReadKeys == null) return changed.Count > 0;
            foreach (var key in rule.ReadKeys) if (changed.Contains(key)) return true;
            return false;
        }

        private void Miss(GoalRun g)
        {
            g.State = GoalState.Missed;
            g.AtStep = _session!.Clock.Steps;
            foreach (var flag in g.Def.OnMissedFlags) _session.Patient.Clinical.SetFlag(flag);
            foreach (var name in g.Def.OnMissedEvents) _session.Events.Publish(new WorkflowEvent(name, g.Def.Id));
            _history.Add(new GraphEvent("goal_missed", g.Def.Id, _step, g.Def.ScoreItems));
        }

        // ---------------------------------------------------------------- decisions

        private void Decide(SimAction action, ActionOutcome outcome)
        {
            action.Params.TryGetValue(DecisionParam, out var dpId);
            action.Params.TryGetValue(OptionParam, out var optionId);
            var point = _points.FirstOrDefault(p => p.Def.Id == dpId);
            if (point == null || !point.Open || point.Decided) { outcome.Reject(NotOpen); return; }
            var option = point.Def.Options.FirstOrDefault(o => o.Id == optionId);
            if (option == null) { outcome.Reject(UnknownOption); return; }

            point.Decided = true;
            foreach (var flag in option.SetFlags) _session!.Patient.Clinical.SetFlag(flag);
            foreach (var name in option.EventsEnable) _session!.Events.Publish(new WorkflowEvent(name, point.Def.Id));
            outcome.Effects.Add("decision:" + point.Def.Id + " chose:" + option.Id + " expected:" + string.Join("|", point.Def.Expected)
                + " " + (point.Def.Expected.Contains(option.Id) ? "as_expected" : "not_expected"));
            if (option.ScoreItem.Length > 0) outcome.Effects.Add("score:" + option.ScoreItem);
            _history.Add(new GraphEvent("decision:" + option.Id, point.Def.Id, _session!.Clock.Steps, option.ScoreItem.Length > 0 ? new[] { option.ScoreItem } : Array.Empty<string>()));
        }
    }
}
