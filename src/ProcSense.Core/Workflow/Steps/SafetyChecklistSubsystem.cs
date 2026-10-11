using System;
using System.Collections.Generic;
using System.Linq;
using ProcSense.Core.Actions;
using ProcSense.Core.Content;
using ProcSense.Core.Pipeline;
using ProcSense.Core.Rules;

namespace ProcSense.Core.Workflow.Steps
{
    /// <summary>One checklist line: the exact wording, whether it is a ProcSense extension, who answers it, whether 'no' stops the phase, and the rule that works out the right answer from the state.</summary>
    public sealed class ChecklistItem
    {
        public ChecklistItem(string id, string phase, int order, string prompt, bool extension, bool critical, string ifNo, bool noStops, string answeredBy, Logic? expected, string fixedAnswer, string reference)
        {
            Id = id; Phase = phase; Order = order; Prompt = prompt; Extension = extension; Critical = critical; IfNo = ifNo; NoStops = noStops; AnsweredBy = answeredBy; Expected = expected; FixedAnswer = fixedAnswer; Ref = reference;
        }

        public string Id { get; }
        public string Phase { get; }
        public int Order { get; }
        public string Prompt { get; }
        public bool Extension { get; }
        public bool Critical { get; }
        public string IfNo { get; }
        public bool NoStops { get; }
        public string AnsweredBy { get; }
        public Logic? Expected { get; }
        public string FixedAnswer { get; }
        public string Ref { get; }
    }

    /// <summary>The WHO safety checklist (content/checklist/safety_checklist.json, built from the verified item table with the exact wording).</summary>
    public sealed class SafetyChecklistTables
    {
        public static readonly SafetyChecklistTables Empty = new SafetyChecklistTables(new List<string>(), new List<ChecklistItem>(), 4, 2, new Dictionary<string, string>(), "", "");

        public SafetyChecklistTables(IReadOnlyList<string> phases, IReadOnlyList<ChecklistItem> items, int npcDelaySeconds, int skipsBeforeStop, IReadOnlyDictionary<string, string> scored, string readyFlag, string rsiFlag)
        {
            Phases = phases; Items = items; NpcDelaySeconds = npcDelaySeconds; SkipsBeforeStop = skipsBeforeStop; Scored = scored; ReadyFlag = readyFlag; RsiFlag = rsiFlag;
        }

        public IReadOnlyList<string> Phases { get; }
        public IReadOnlyList<ChecklistItem> Items { get; }
        public int NpcDelaySeconds { get; }
        public int SkipsBeforeStop { get; }
        public IReadOnlyDictionary<string, string> Scored { get; }
        public string ReadyFlag { get; }
        public string RsiFlag { get; }

        public ChecklistItem? Find(string id) => Items.FirstOrDefault(i => i.Id == id);

        public IEnumerable<ChecklistItem> InPhase(string phase) => Items.Where(i => i.Phase == phase).OrderBy(i => i.Order);

        public static SafetyChecklistTables Parse(string json, string source = "checklist")
        {
            var root = new JsonObj(MiniJson.Parse(json), source);
            var phases = new List<string>();
            foreach (var p in root.Arr("phases")) phases.Add(p as string ?? throw new ContentException(root.Sub("phases") + ": expected text"));
            var items = new List<ChecklistItem>();
            var raw = root.Arr("items");
            for (int i = 0; i < raw.Count; i++)
            {
                var o = new JsonObj(raw[i], JsonObj.Index(root.Sub("items"), i));
                string id = o.Str("id"), phase = o.Str("phase");
                if (items.Any(x => x.Id == id)) throw new ContentException(o.Sub("id") + ": duplicate item '" + id + "'");
                if (!phases.Contains(phase)) throw new ContentException(o.Sub("phase") + ": '" + phase + "' is not a phase");
                object? rule = o.Any("expected_if");
                Logic? expected = rule is string ? null : Logic.Compile(rule, o.Sub("expected_if"));
                string fixedAnswer = rule is string text ? text : "";
                if (fixedAnswer.Length > 0 && fixedAnswer != "yes" && fixedAnswer != "no" && fixedAnswer != "na") throw new ContentException(o.Sub("expected_if") + ": a fixed answer is yes, no or na");
                items.Add(new ChecklistItem(id, phase, (int)o.Int("order"), o.Str("prompt"), o.Bool("extension"), o.Bool("critical"), o.Str("if_no"), o.Bool("no_stops"), o.Enum("answered_by", "trainee", "npc"), expected, fixedAnswer, o.Str("ref")));
                o.Finish();
            }
            int delay = (int)root.Int("npc_delay_seconds"), skips = (int)root.Int("skips_before_stop");
            var scored = new SortedDictionary<string, string>(root.StringMap("scored"), StringComparer.Ordinal);
            foreach (var need in new[] { "contradiction", "skipped", "skipped_repeat", "sign_in_complete", "time_out_complete", "stopped_honestly" })
                if (!scored.ContainsKey(need)) throw new ContentException(root.Sub("scored") + ": '" + need + "' is missing");
            string ready = root.Str("ready_flag"), rsi = root.Str("rsi_flag");
            foreach (var r in root.Arr("refs")) if (!(r is string)) throw new ContentException(root.Sub("refs") + ": expected register ids");
            root.Finish();
            return new SafetyChecklistTables(phases, items, delay, skips, scored, ready, rsi);
        }
    }

    /// <summary>
    /// The WHO safety checklist (Sign In and Time Out, walkthrough steps thirteen and fourteen). Each answer the trainee gives (checklist_item) is compared with the answer worked out from the state at that moment: a contradiction is critical and stops the phase
    /// until it is corrected; an honest 'no' where the checklist says STOP also stops it until the problem is fixed. Items go in order (a skipped one is challenged, skipping again stops the case); the theatre
    /// colleagues answer their own items from the state a few seconds after the question; the pulse oximeter cannot be 'yes' unless the probe is on; an emergency case answers 'yes' to aspiration risk and plans a
    /// rapid-sequence induction instead of stopping. Sign In completes by itself; Time Out is completed with complete_time_out, which needs the theatre ready (B-OR), and ends with READY FOR INCISION.
    /// Sign Out is P1. The five ProcSense extensions are a separate block.
    /// </summary>
    public sealed class SafetyChecklistSubsystem : ISubsystem
    {
        public const string ItemVerb = "checklist_item";
        public const string CompleteTimeOutVerb = "complete_time_out";

        public const string MissingPhase = "MISSING_PHASE";
        public const string UnknownPhase = "UNKNOWN_PHASE";
        public const string MissingItem = "MISSING_ITEM";
        public const string UnknownItem = "UNKNOWN_ITEM";
        public const string WrongPhase = "ITEM_NOT_IN_PHASE";
        public const string MissingResponse = "MISSING_RESPONSE";
        public const string UnknownResponse = "UNKNOWN_RESPONSE";
        public const string PhaseNotAvailable = "PHASE_NOT_AVAILABLE";
        public const string SignInFirst = "SIGN_IN_FIRST";
        public const string AskTheRightPerson = "ASK_THE_RIGHT_PERSON";
        public const string OutOfOrder = "ITEM_OUT_OF_ORDER";
        public const string PhaseComplete = "PHASE_COMPLETE";
        public const string CaseStopped = "CASE_STOPPED";
        public const string ItemsOutstanding = "ITEMS_OUTSTANDING";
        public const string StoppedFlag = "checklist.case_stopped";

        private static readonly string[] Responses = { "yes", "no", "na" };

        private readonly ContentPackage _content;
        private readonly List<GraphEvent> _history = new List<GraphEvent>();
        private readonly Dictionary<string, int> _skips = new Dictionary<string, int>(StringComparer.Ordinal);
        private readonly Dictionary<string, long> _npcAt = new Dictionary<string, long>(StringComparer.Ordinal);
        private Session _s = null!;

        public SafetyChecklistSubsystem(ContentPackage content) { _content = content; }

        public int Order => PipelineOrder.Investigations + 8;

        /// <summary>Answers, contradictions, skips and phase changes with sim steps and score items. The scorer and the debrief read this.</summary>
        public IReadOnlyList<GraphEvent> History => _history;

        private SafetyChecklistTables Tables => _content.Checklist;

        private SortedDictionary<string, string> Items => _s.Patient.Clinical.ChecklistItems;

        private SortedDictionary<string, string> Phases => _s.Patient.Clinical.ChecklistPhases;

        /// <summary>NOT_STARTED, IN_PROGRESS, STOPPED, ITEMS_DONE (Time Out waiting to be completed) or COMPLETE.</summary>
        public string PhaseState(string phase) => Phases.TryGetValue(phase, out var s) ? s : "NOT_STARTED";

        public string ItemState(string id) => Items.TryGetValue(id, out var s) ? s : "pending";

        /// <summary>True once Time Out is complete: READY FOR INCISION.</summary>
        public bool ReadyForIncision => _s.Patient.Clinical.HasFlag(Tables.ReadyFlag);

        public void Init(Session session)
        {
            _s = session;
            session.SafetyStep = this;
            session.Verbs.Register(ItemVerb, requiresIdentity: true);
            session.Verbs.Register(CompleteTimeOutVerb, requiresIdentity: true);
        }

        public void OnAction(SimAction a, ActionOutcome o)
        {
            switch (a.Verb)
            {
                case ItemVerb: Answer(a, o); break;
                case CompleteTimeOutVerb: CompleteTimeOut(o); break;
            }
        }

        public void OnTick()
        {
            if (_s.Patient.Clinical.ChecklistItems.Count == 0 && _npcAt.Count == 0) return;      // the checklist has not been used: nothing to do, and no allocation
            // a colleague whose answer stopped the phase is asked again once the state has been put right
            foreach (var item in Tables.Items)
                if (item.AnsweredBy == "npc" && ItemState(item.Id) == "stopped" && !_npcAt.ContainsKey(item.Id) && Expected(item) != "no") Queue(item);
            if (_npcAt.Count == 0) return;
            foreach (var kv in _npcAt.ToList())
            {
                if (_s.Clock.Steps < kv.Value) continue;
                _npcAt.Remove(kv.Key);
                var item = Tables.Find(kv.Key)!;
                if (ItemState(item.Id) == "confirmed") continue;
                Apply(item, Expected(item), null, true);
                Advance(item.Phase);
            }
        }

        private string Expected(ChecklistItem item) => item.Expected == null ? item.FixedAnswer : (item.Expected.Evaluate(RuleState.Build(_s)) as string ?? "no");

        private void Start(string phase)
        {
            if (PhaseState(phase) == "NOT_STARTED") Phases[phase] = "IN_PROGRESS";
        }

        // ---------------------------------------------------------------- the trainee's answers

        private void Answer(SimAction a, ActionOutcome o)
        {
            if (!a.Params.TryGetValue("phase", out var phase) || phase.Length == 0) { o.Reject(MissingPhase); return; }
            if (!Tables.Phases.Contains(phase)) { o.Reject(UnknownPhase); return; }
            if (!a.Params.TryGetValue("item", out var id) || id.Length == 0) { o.Reject(MissingItem); return; }
            var item = Tables.Find(id);
            if (item == null) { o.Reject(UnknownItem); return; }
            if (item.Phase != phase) { o.Reject(WrongPhase); return; }
            if (!a.Params.TryGetValue("response", out var response) || response.Length == 0) { o.Reject(MissingResponse); return; }
            response = response.ToLowerInvariant();
            if (!Responses.Contains(response)) { o.Reject(UnknownResponse); return; }
            if (phase == "sign_out") { o.Reject(PhaseNotAvailable); return; }                       // Sign Out is P1
            if (_s.Patient.Clinical.HasFlag(StoppedFlag)) { o.Reject(CaseStopped); return; }
            if (phase == "time_out" && PhaseState("sign_in") != "COMPLETE") { o.Reject(SignInFirst); return; }
            if (PhaseState(phase) == "COMPLETE") { o.Reject(PhaseComplete); return; }
            if (item.AnsweredBy == "npc") { o.Effects.Add("challenge:direct_to_the_right_person"); o.Reject(AskTheRightPerson); return; }
            if (!item.Extension && NextPending(phase) is { } next && next.Id != item.Id && ItemState(item.Id) != "contradicted" && ItemState(item.Id) != "stopped")
            {
                Skip(phase, next, o);
                return;
            }
            Start(phase);
            Apply(item, response, o, false);
            Advance(phase);
        }

        // the first item of a phase that is not confirmed yet
        private ChecklistItem? NextPending(string phase) => Tables.InPhase(phase).FirstOrDefault(i => !i.Extension && ItemState(i.Id) != "confirmed");

        private void Skip(string phase, ChecklistItem next, ActionOutcome o)
        {
            _skips[phase] = _skips.TryGetValue(phase, out var n) ? n + 1 : 1;
            o.Effects.Add("challenge:" + next.Id);                                                  // 'Wait: we have not done item N'
            if (_skips[phase] >= Tables.SkipsBeforeStop)
            {
                Phases[phase] = "STOPPED";
                _s.Patient.Clinical.SetFlag(StoppedFlag);
                _history.Add(new GraphEvent("checklist_skipped_repeatedly", next.Id, _s.Clock.Steps, new[] { Tables.Scored["skipped_repeat"] }));
                o.Effects.Add("case_stopped");
                _s.Events.Publish(new WorkflowEvent("E-CASE-STOPPED", next.Id));
            }
            else _history.Add(new GraphEvent("checklist_item_skipped", next.Id, _s.Clock.Steps, new[] { Tables.Scored["skipped"] }));
            o.Reject(OutOfOrder);
        }

        // ---------------------------------------------------------------- comparing an answer with the state

        private void Apply(ChecklistItem item, string response, ActionOutcome? o, bool byNpc)
        {
            string expected = Expected(item);
            string who = byNpc ? "npc" : "trainee";
            if (byNpc) Start(item.Phase);
            if (response == expected)
            {
                if (response == "no" && item.NoStops)
                {
                    Items[item.Id] = "stopped";
                    Phases[item.Phase] = "STOPPED";
                    _history.Add(new GraphEvent("checklist_stop:" + item.Id, who, _s.Clock.Steps, new[] { Tables.Scored["stopped_honestly"] }));
                    o?.Effects.Add("stop:" + item.Id);
                }
                else
                {
                    Items[item.Id] = "confirmed";
                    _history.Add(new GraphEvent("checklist_confirmed:" + item.Id, who, _s.Clock.Steps, Array.Empty<string>()));
                    o?.Effects.Add("confirmed:" + item.Id);
                    if (item.Id == "sign_in.06" && response == "yes" && _s.Case.Scenario == "S2") { _s.Patient.Clinical.SetFlag(Tables.RsiFlag); o?.Effects.Add("plan:rsi"); }   // plan, do not stop
                    if (item.Phase != "extension" && Phases.TryGetValue(item.Phase, out var st) && st == "STOPPED" && !Tables.InPhase(item.Phase).Any(i => ItemState(i.Id) == "stopped" || ItemState(i.Id) == "contradicted")) Phases[item.Phase] = "IN_PROGRESS";
                }
                return;
            }
            // a contradiction of the state
            Items[item.Id] = "contradicted";
            Phases[item.Phase] = "STOPPED";
            _history.Add(new GraphEvent("checklist_contradiction:" + item.Id, who, _s.Clock.Steps, new[] { Tables.Scored["contradiction"] }));
            o?.Effects.Add("contradiction:" + item.Id);
            o?.Effects.Add("stop:" + item.Id);
        }

        // the phase is done when every item (not the extensions) is confirmed; the NPC items are queued when they become the next one
        private void Advance(string phase)
        {
            if (phase == "extension") return;
            var pending = NextPending(phase);
            if (pending == null)
            {
                if (phase == "sign_in")
                {
                    Phases[phase] = "COMPLETE";
                    _history.Add(new GraphEvent("sign_in_complete", phase, _s.Clock.Steps, new[] { Tables.Scored["sign_in_complete"] }));
                    var first = Tables.InPhase("time_out").FirstOrDefault(i => !i.Extension);
                    if (first != null && first.AnsweredBy == "npc") Queue(first);                      // Time Out opens with the team introductions
                }
                else if (phase == "time_out") Phases[phase] = "ITEMS_DONE";
                return;
            }
            if (pending.AnsweredBy == "npc" && PhaseState(phase) != "STOPPED") Queue(pending);
        }

        // the colleague answers a few seconds after the question
        private void Queue(ChecklistItem item)
        {
            if (_npcAt.ContainsKey(item.Id)) return;
            _npcAt[item.Id] = _s.Clock.Steps + (long)(Tables.NpcDelaySeconds / Time.SimClock.Step);
        }

        // ---------------------------------------------------------------- Time Out completion

        private void CompleteTimeOut(ActionOutcome o)
        {
            if (PhaseState("sign_in") != "COMPLETE") { o.Reject(SignInFirst); return; }
            if (PhaseState("time_out") != "ITEMS_DONE") { o.Reject(ItemsOutstanding); return; }
            Phases["time_out"] = "COMPLETE";
            _s.Patient.Clinical.SetFlag(Tables.ReadyFlag);
            _history.Add(new GraphEvent("time_out_complete", "time_out", _s.Clock.Steps, new[] { Tables.Scored["time_out_complete"] }));
            o.Effects.Add("ready_for_incision");
            _s.Events.Publish(new WorkflowEvent("E-READY-FOR-INCISION", "time_out"));
        }
    }
}
