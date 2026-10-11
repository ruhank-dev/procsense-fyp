using System;
using System.Collections.Generic;
using System.Linq;
using ProcSense.Core.Actions;
using ProcSense.Core.Content;
using ProcSense.Core.Pipeline;
using ProcSense.Core.Workflow.Steps;

namespace ProcSense.Core.Workflow
{
    public sealed record EndStateDef(string Id, string Code, string Label, string Meaning, string Ref);

    /// <summary>A hidden fact that surfaces at a later moment if the trainee never asked: the case and variant it belongs to, the history topic that would have revealed it, when and by whom it is found, and what it does.</summary>
    public sealed record SurfaceLater(string Id, string Case, string Variant, string Topic, string Fact, string Moment, string By, string Effect, string Ref);

    /// <summary>The five end states, what counts as an avoidable delay and the facts that surface later (content/endstate/end_states.json, built from the verified end-state table).</summary>
    public sealed class EndStateTables
    {
        public static readonly EndStateTables Empty = new EndStateTables(new List<EndStateDef>(), new List<string>(), "critical.", new List<SurfaceLater>(), new Dictionary<string, string>());

        public EndStateTables(IReadOnlyList<EndStateDef> states, IReadOnlyList<string> delayItems, string failurePrefix, IReadOnlyList<SurfaceLater> surfaceLater, IReadOnlyDictionary<string, string> items)
        {
            States = states; DelayItems = delayItems; FailurePrefix = failurePrefix; SurfaceLater = surfaceLater; Items = items;
        }

        public IReadOnlyList<EndStateDef> States { get; }
        public IReadOnlyList<string> DelayItems { get; }
        public string FailurePrefix { get; }
        public IReadOnlyList<SurfaceLater> SurfaceLater { get; }
        public IReadOnlyDictionary<string, string> Items { get; }

        public EndStateDef Find(string code) => States.First(s => s.Code == code);

        public static EndStateTables Parse(string json, string source = "end_states")
        {
            var root = new JsonObj(MiniJson.Parse(json), source);
            var states = new List<EndStateDef>();
            var raw = root.Arr("states");
            for (int i = 0; i < raw.Count; i++)
            {
                var o = new JsonObj(raw[i], JsonObj.Index(root.Sub("states"), i));
                states.Add(new EndStateDef(o.Str("id"), o.Str("code"), o.Str("label"), o.Str("meaning"), o.Str("ref")));
                o.Finish();
            }
            foreach (var need in new[] { "READY_SAFE", "POSTPONED_APPROPRIATELY", "REDIRECTED_APPROPRIATELY", "SAFE_BUT_DELAYED", "SAFETY_FAILURE" })
                if (states.All(s => s.Code != need)) throw new ContentException(root.Sub("states") + ": the end state " + need + " is missing");
            var delay = new List<string>();
            foreach (var d in root.Arr("delay_items")) delay.Add(d as string ?? throw new ContentException(root.Sub("delay_items") + ": expected score item ids"));
            string prefix = root.Str("failure_prefix");
            var surface = new List<SurfaceLater>();
            var rawSurface = root.Arr("surface_later");
            for (int i = 0; i < rawSurface.Count; i++)
            {
                var o = new JsonObj(rawSurface[i], JsonObj.Index(root.Sub("surface_later"), i));
                surface.Add(new SurfaceLater(o.Str("id"), o.Str("case"), o.Str("variant"), o.Str("topic"), o.Str("fact"), o.Enum("moment", "sign_in"), o.Enum("by", "anaesthetist"), o.Enum("effect", "stop", "inform"), o.Str("ref")));
                o.Finish();
            }
            var items = new SortedDictionary<string, string>(root.StringMap("items"), StringComparer.Ordinal);
            if (!items.ContainsKey("surfaced_late")) throw new ContentException(root.Sub("items") + ": 'surfaced_late' is missing");
            foreach (var r in root.Arr("refs")) if (!(r is string)) throw new ContentException(root.Sub("refs") + ": expected register ids");
            root.Finish();
            return new EndStateTables(states, delay, prefix, surface, items);
        }
    }

    /// <summary>
    /// How the case ended, worked out from what happened. Any critical score item is a safety failure (a blocker was violated or a life-threatening event missed). A case the trainee postponed or redirected keeps
    /// the end state that step reached; a case that reached ready for incision is ready and safe, or safe but delayed when an avoidable delay happened (the content lists which items are one). A hidden fact that the
    /// trainee never asked about surfaces at Sign In (the anaesthetist finds it); if its effect is to stop the case, the case ends safe but delayed, and the omission is a major item for the debrief.
    /// The resolver only reads what the other steps recorded; it decides nothing for the trainee.
    /// </summary>
    public sealed class EndStateSubsystem : ISubsystem
    {
        public const string StopFlag = "endstate.case_stopped";
        public const string DoneFlag = "endstate.resolved";
        public const string Event = "E-END-STATE-RESOLVED";

        private readonly ContentPackage _content;
        private readonly CaseFile? _fixed;
        private readonly List<GraphEvent> _history = new List<GraphEvent>();
        private readonly HashSet<string> _surfaced = new HashSet<string>(StringComparer.Ordinal);
        private Session _s = null!;

        /// <param name="caseFile">Test hook: use this case file instead of the one named like the case card.</param>
        public EndStateSubsystem(ContentPackage content, CaseFile? caseFile = null) { _content = content; _fixed = caseFile; }

        // after every other step has recorded what the action did, the decision step included
        public int Order => PipelineOrder.Events - 1;

        /// <summary>Surfaced facts and the resolved end state, with sim steps and score items.</summary>
        public IReadOnlyList<GraphEvent> History => _history;

        /// <summary>The end state once the case has ended: READY_SAFE, POSTPONED_APPROPRIATELY, REDIRECTED_APPROPRIATELY, SAFE_BUT_DELAYED or SAFETY_FAILURE; "" while it goes on. Never shown to the trainee in assessment.</summary>
        public string Resolved { get; private set; } = "";

        private EndStateTables Tables => _content.EndStates;

        public void Init(Session session)
        {
            _s = session;
            session.EndStateStep = this;
        }

        public void OnAction(SimAction a, ActionOutcome o)
        {
            if (o.Accepted && a.Verb == SafetyChecklistSubsystem.ItemVerb && a.Params.TryGetValue("phase", out var phase) && phase == "sign_in") SurfaceAt("sign_in", o);
            if (Resolved.Length == 0) TryResolve();
        }

        // an action a step refused never reaches this step (the pipeline stops at the first refusal), so time also asks
        public void OnTick()
        {
            if (Resolved.Length == 0) TryResolve();
        }

        // cheap to ask every action and every tick: only flags and a string are read until the case has really ended
        private bool Ended()
        {
            var clinical = _s.Patient.Clinical;
            return (_s.DecisionStep?.EndState.Length ?? 0) > 0 || _s.SafetyStep?.ReadyForIncision == true || clinical.HasFlag(StopFlag) || clinical.HasFlag(SafetyChecklistSubsystem.StoppedFlag);
        }

        /// <summary>Every score-bearing event recorded so far, by every step (the end state, the scorer and the debrief all read this one list).</summary>
        public IEnumerable<GraphEvent> AllEvents()
        {
            IEnumerable<IReadOnlyList<GraphEvent>> sources = new IReadOnlyList<GraphEvent>?[]
            {
                _s.ResultReviewHistory, _s.InvestigationHistory, _s.MedicineHistory, _s.TransportHistory, _s.GraphHistory, _s.SepsisStep?.History, _s.DecisionStep?.History, _s.WardStep?.History, _s.OrStep?.History,
                _s.HandoverStep?.History, _s.SafetyStep?.History, _s.ClashStep?.History, _s.ConsentStep?.History, _s.DirectorStep?.History, _s.BehaviourStep?.History, _s.Flow?.History, _history,
            }.Where(x => x != null)!;
            return sources.SelectMany(h => h);
        }

        /// <summary>Every score item recorded so far, by every step.</summary>
        public IEnumerable<string> AllItems() => AllEvents().SelectMany(e => e.ScoreItems);

        // ---------------------------------------------------------------- hidden facts that surface later

        private void SurfaceAt(string moment, ActionOutcome o)
        {
            string caseId = _fixed?.Id ?? _s.Case.Id;
            foreach (var e in Tables.SurfaceLater.Where(x => x.Moment == moment && x.Case == caseId))
            {
                if (_surfaced.Contains(e.Id)) continue;
                if (!_s.Patient.Clinical.HasFlag(DecisionSubsystem.VariantPrefix + e.Variant)) continue;       // this variant is not the one being played
                _surfaced.Add(e.Id);
                if (_s.Patient.Clinical.IsRevealed(e.Topic)) continue;                                           // the trainee asked: nothing is hidden
                o.Effects.Add("surfaced:" + e.Id);
                o.Effects.Add(e.By + "_finds:" + e.Fact);
                _history.Add(new GraphEvent("surfaced_late:" + e.Id, e.Topic, _s.Clock.Steps, new[] { Tables.Items["surfaced_late"] }));
                if (e.Effect == "stop")
                {
                    _s.Patient.Clinical.SetFlag(StopFlag);
                    _s.Patient.Clinical.SetFlag(SafetyChecklistSubsystem.StoppedFlag);                           // no more checklist answers: the case stops
                    o.Effects.Add("case_stopped");
                    _s.Events.Publish(new WorkflowEvent("E-CASE-STOPPED", e.Id));
                }
            }
        }

        // ---------------------------------------------------------------- the end state

        /// <summary>The end state the history so far gives, or "" while the case goes on.</summary>
        public string Evaluate()
        {
            var clinical = _s.Patient.Clinical;
            string chosen = _s.DecisionStep?.EndState ?? "";
            bool ready = _s.SafetyStep?.ReadyForIncision == true;
            bool stopped = clinical.HasFlag(StopFlag) || clinical.HasFlag(SafetyChecklistSubsystem.StoppedFlag);
            if (chosen.Length == 0 && !ready && !stopped) return "";                                      // the case goes on
            var items = AllItems().ToList();
            if (items.Any(i => i.StartsWith(Tables.FailurePrefix, StringComparison.Ordinal))) return "SAFETY_FAILURE";
            if (clinical.HasFlag(StopFlag)) return "SAFE_BUT_DELAYED";
            if (chosen.Length > 0) return chosen;
            if (ready) return items.Any(i => Tables.DelayItems.Contains(i)) ? "SAFE_BUT_DELAYED" : "READY_SAFE";
            return "SAFETY_FAILURE";                                                                      // the checklist stopped the case (a skipped item, repeatedly)
        }

        private void TryResolve()
        {
            if (!Ended()) return;
            string end = Evaluate();
            if (end.Length == 0) return;
            Resolved = end;
            _s.Patient.Clinical.SetFlag(DoneFlag);
            _history.Add(new GraphEvent("end_state:" + end, end, _s.Clock.Steps, Array.Empty<string>()));
            _s.Events.Publish(new WorkflowEvent(Event, end));
        }
    }
}
