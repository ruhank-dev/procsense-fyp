using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using ProcSense.Core.Actions;
using ProcSense.Core.Content;
using ProcSense.Core.Pipeline;
using ProcSense.Core.Rules;

namespace ProcSense.Core.Workflow.Steps
{
    /// <summary>One line of the ward checklist: what it says, how it is checked against the state, whether it is needed (always, or when a rule says so), whether a false tick is critical, and the physical actions that satisfy it.</summary>
    public sealed class WardItem
    {
        public WardItem(string id, int order, string label, Logic verify, bool required, Logic? requiredIf, bool critical, IReadOnlyList<string> actions, string reference)
        {
            Id = id; Order = order; Label = label; Verify = verify; Required = required; RequiredIf = requiredIf; Critical = critical; Actions = actions; Ref = reference;
        }

        public string Id { get; }
        public int Order { get; }
        public string Label { get; }
        public Logic Verify { get; }
        public bool Required { get; }
        public Logic? RequiredIf { get; }
        public bool Critical { get; }
        public IReadOnlyList<string> Actions { get; }
        public string Ref { get; }
    }

    /// <summary>The ward checklist (content/ward/ward_checklist.json, built from the walkthrough's Step 10 list).</summary>
    public sealed class WardTables
    {
        public static readonly WardTables Empty = new WardTables(new List<WardItem>(), new Dictionary<string, string>(), "", "ward.checklist_signed");

        public WardTables(IReadOnlyList<WardItem> items, IReadOnlyDictionary<string, string> scored, string discrepancyEvent, string signedFlag)
        {
            Items = items; Scored = scored; DiscrepancyEvent = discrepancyEvent; SignedFlag = signedFlag;
        }

        public IReadOnlyList<WardItem> Items { get; }
        public IReadOnlyDictionary<string, string> Scored { get; }
        public string DiscrepancyEvent { get; }
        public string SignedFlag { get; }

        public WardItem? Find(string id) => Items.FirstOrDefault(i => i.Id == id);

        public static WardTables Parse(string json, string source = "ward")
        {
            var root = new JsonObj(MiniJson.Parse(json), source);
            var items = new List<WardItem>();
            var raw = root.Arr("items");
            for (int i = 0; i < raw.Count; i++)
            {
                var o = new JsonObj(raw[i], JsonObj.Index(root.Sub("items"), i));
                string id = o.Str("id");
                if (items.Any(x => x.Id == id)) throw new ContentException(o.Sub("id") + ": duplicate item '" + id + "'");
                var verify = Logic.Compile(o.Any("verify_if"), o.Sub("verify_if"));
                var requiredIf = o.Any("required_if") == null ? null : Logic.Compile(o.Any("required_if"), o.Sub("required_if"));
                var actions = new List<string>();
                if (o.Any("action") != null) foreach (var a in o.Arr("action")) actions.Add(a as string ?? throw new ContentException(o.Sub("action") + ": expected text"));
                items.Add(new WardItem(id, (int)o.Int("order"), o.Str("label"), verify, o.Bool("required"), requiredIf, o.Bool("critical"), actions, o.Str("ref")));
                o.Finish();
            }
            var scored = new SortedDictionary<string, string>(root.StringMap("items_scored"), StringComparer.Ordinal);
            foreach (var need in new[] { "tick_unverified", "tick_false_critical", "razor", "late_fasting_not_passed", "signed_with_gaps", "signed_complete", "anaesthetist_told" })
                if (!scored.ContainsKey(need)) throw new ContentException(root.Sub("items_scored") + ": '" + need + "' is missing");
            string ev = root.Str("discrepancy_event"), flag = root.Str("signed_flag");
            foreach (var r in root.Arr("refs")) if (!(r is string)) throw new ContentException(root.Sub("refs") + ": expected register ids");
            root.Finish();
            return new WardTables(items.OrderBy(x => x.Order).ToList(), scored, ev, flag);
        }
    }

    /// <summary>
    /// Step 10: preparing the patient. The trainee does the physical things (prepare: gown, jewellery, hair, stockings and so on), ticks each line, tells the anaesthetist about a late fasting discovery
    /// and signs the checklist. Every tick is checked against the real state when it is made, so a tick that contradicts the state (for example 'fasted' during a breach) is an item and a discrepancy event,
    /// not an accepted fact. The signed checklist is what lets the transfer to theatre through (readiness blocker B-WARD).
    /// </summary>
    public sealed class WardChecklistSubsystem : ISubsystem
    {
        public const string PrepareVerb = "prepare";
        public const string TickVerb = "tick_ward_item";
        public const string TellVerb = "tell_anaesthetist";
        public const string SignVerb = "sign_ward_checklist";

        public const string MissingItem = "MISSING_ITEM";
        public const string UnknownItem = "UNKNOWN_ITEM";
        public const string MissingDetail = "MISSING_DETAIL";
        public const string UnknownDetail = "UNKNOWN_DETAIL";
        public const string NothingToDo = "NOTHING_TO_DO";
        public const string NotAllTicked = "NOT_ALL_TICKED";
        public const string AlreadySigned = "ALREADY_SIGNED";
        public const string MissingTopic = "MISSING_TOPIC";
        public const string UnknownTopic = "UNKNOWN_TOPIC";
        public const string ToldPrefix = "anaesthetist.told.";

        public static readonly string[] Topics = { "fasting", "temperature", "consent", "pregnancy", "other" };

        private readonly ContentPackage _content;
        private readonly List<GraphEvent> _history = new List<GraphEvent>();
        private readonly List<string> _ticked = new List<string>();
        private Session _s = null!;

        public WardChecklistSubsystem(ContentPackage content) { _content = content; }

        // after the readiness gate and the clinical steps have settled this action
        public int Order => PipelineOrder.Investigations + 5;

        /// <summary>Ticks, discrepancies, the late-fasting handover and the signing, with sim steps and score items. The scorer and the debrief read this.</summary>
        public IReadOnlyList<GraphEvent> History => _history;

        /// <summary>The ids ticked so far, in the order they were ticked.</summary>
        public IReadOnlyList<string> Ticked => _ticked;

        public bool Signed => _s.Patient.Clinical.HasFlag(Tables.SignedFlag);

        private WardTables Tables => _content.Ward;

        public void Init(Session session)
        {
            _s = session;
            session.WardStep = this;
            foreach (var verb in new[] { PrepareVerb, TickVerb, SignVerb }) session.Verbs.Register(verb, requiresIdentity: true);
            session.Verbs.Register(TellVerb);
        }

        public void OnAction(SimAction a, ActionOutcome o)
        {
            switch (a.Verb)
            {
                case PrepareVerb: Prepare(a, o); break;
                case TickVerb: Tick(a, o); break;
                case TellVerb: Tell(a, o); break;
                case SignVerb: Sign(o); break;
            }
        }

        public void OnTick() { }

        private Dictionary<string, object?> State() => RuleState.Build(_s);

        /// <summary>The lines the case needs ticked now: the ones always needed and those whose own rule says so.</summary>
        public IReadOnlyList<WardItem> Needed()
        {
            var data = State();
            return Tables.Items.Where(i => i.Required || (i.RequiredIf != null && i.RequiredIf.Test(data))).ToList();
        }

        /// <summary>True when this line's check passes against the state now.</summary>
        public bool Verified(string id) => Tables.Find(id)?.Verify.Test(State()) ?? false;

        // ---------------------------------------------------------------- prepare

        private void Prepare(SimAction a, ActionOutcome o)
        {
            if (!a.Params.TryGetValue("item", out var id) || id.Length == 0) { o.Reject(MissingItem); return; }
            var item = Tables.Find(id);
            if (item == null) { o.Reject(UnknownItem); return; }
            if (item.Actions.Count == 0) { o.Reject(NothingToDo); return; }
            if (!a.Params.TryGetValue("detail", out var detail) || detail.Length == 0) { o.Reject(MissingDetail); return; }
            if (!item.Actions.Contains(detail)) { o.Reject(UnknownDetail); return; }
            var c = _s.Patient.Clinical;
            if (item.Id == "jewellery" && detail != "left_on") foreach (var other in item.Actions) c.ClearFlag("ward.jewellery." + other);
            c.SetFlag("ward." + item.Id + "." + detail);
            o.Effects.Add("prepared:" + item.Id + ":" + detail);
            if (item.Id == "hair" && detail == "razor")
            {
                _history.Add(new GraphEvent("razor_used", item.Id, _s.Clock.Steps, new[] { Tables.Scored["razor"] }));
                o.Effects.Add("infection_risk:razor");
            }
        }

        // ---------------------------------------------------------------- tick

        private void Tick(SimAction a, ActionOutcome o)
        {
            if (!a.Params.TryGetValue("item", out var id) || id.Length == 0) { o.Reject(MissingItem); return; }
            var item = Tables.Find(id);
            if (item == null) { o.Reject(UnknownItem); return; }
            bool ok = item.Verify.Test(State());
            if (!_ticked.Contains(id)) _ticked.Add(id);
            if (ok) { o.Effects.Add("ticked:" + id); return; }
            // E10-A: the tick contradicts the state
            string score = item.Critical ? Tables.Scored["tick_false_critical"] : Tables.Scored["tick_unverified"];
            _history.Add(new GraphEvent("ward_tick_unverified", id, _s.Clock.Steps, new[] { score }));
            o.Effects.Add("ticked:" + id);
            o.Effects.Add("discrepancy:" + id);
            _s.Events.Publish(new WorkflowEvent(Tables.DiscrepancyEvent, id));
        }

        // ---------------------------------------------------------------- tell the anaesthetist

        private void Tell(SimAction a, ActionOutcome o)
        {
            if (!a.Params.TryGetValue("topic", out var topic) || topic.Length == 0) { o.Reject(MissingTopic); return; }
            if (!Topics.Contains(topic)) { o.Reject(UnknownTopic); return; }
            var c = _s.Patient.Clinical;
            c.SetFlag(ToldPrefix + topic);
            o.Effects.Add("told_anaesthetist:" + topic);
            if (topic == "fasting" && c.Fasting?.Status == "breach") _history.Add(new GraphEvent("late_fasting_passed", topic, _s.Clock.Steps, new[] { Tables.Scored["anaesthetist_told"] }));
        }

        // ---------------------------------------------------------------- sign

        private void Sign(ActionOutcome o)
        {
            var c = _s.Patient.Clinical;
            if (Signed) { o.Reject(AlreadySigned); return; }
            var missing = Needed().Where(i => !_ticked.Contains(i.Id)).Select(i => i.Id).ToList();
            if (missing.Count > 0)
            {
                o.Effects.Add("not_ticked:" + string.Join(",", missing));                      // the nurse: 'checklist is complete except ...'
                o.Reject(NotAllTicked);
                return;
            }
            var data = State();
            var gaps = _ticked.Where(id => !Tables.Find(id)!.Verify.Test(data)).ToList();
            var items = new List<string>();
            if (gaps.Count > 0) items.Add(Tables.Scored["signed_with_gaps"]);
            else items.Add(Tables.Scored["signed_complete"]);
            if (c.Fasting?.Status == "breach" && !c.HasFlag(ToldPrefix + "fasting")) items.Add(Tables.Scored["late_fasting_not_passed"]);
            c.SetFlag(Tables.SignedFlag);
            _history.Add(new GraphEvent("ward_checklist_signed", gaps.Count == 0 ? "complete" : "with_gaps", _s.Clock.Steps, items));
            o.Effects.Add("ward_checklist_signed");
            if (gaps.Count > 0) o.Effects.Add("gaps:" + string.Join(",", gaps));
        }
    }
}
