using System;
using System.Collections.Generic;
using System.Linq;
using ProcSense.Core.Actions;
using ProcSense.Core.Content;
using ProcSense.Core.Pipeline;
using ProcSense.Core.Rules;

namespace ProcSense.Core.Workflow.Steps
{
    /// <summary>One item of the SBAR menu: its section, whether the trainee knows the fact yet, which scenarios must include it and how bad leaving it out is.</summary>
    public sealed record HandoverItem(string Id, string Section, string Label, Logic Known, IReadOnlyList<string> RequiredIn, string Severity, string Ref);

    /// <summary>The SBAR items and how a missing one is scored (content/handover/handover_required.json, built from the walkthrough's Step 12).</summary>
    public sealed class HandoverTables
    {
        public static readonly HandoverTables Empty = new HandoverTables(new List<string>(), new List<HandoverItem>(), new Dictionary<string, string>());

        public HandoverTables(IReadOnlyList<string> sections, IReadOnlyList<HandoverItem> items, IReadOnlyDictionary<string, string> scored)
        {
            Sections = sections; Items = items; Scored = scored;
        }

        public IReadOnlyList<string> Sections { get; }
        public IReadOnlyList<HandoverItem> Items { get; }
        public IReadOnlyDictionary<string, string> Scored { get; }

        public HandoverItem? Find(string id) => Items.FirstOrDefault(i => i.Id == id);

        public static HandoverTables Parse(string json, string source = "handover")
        {
            var root = new JsonObj(MiniJson.Parse(json), source);
            var sections = new List<string>();
            foreach (var s in root.Arr("sections")) sections.Add(s as string ?? throw new ContentException(root.Sub("sections") + ": expected text"));
            if (!sections.SequenceEqual(new[] { "s", "b", "a", "r" })) throw new ContentException(root.Sub("sections") + ": an SBAR has the sections s, b, a, r");
            var items = new List<HandoverItem>();
            var raw = root.Arr("items");
            for (int i = 0; i < raw.Count; i++)
            {
                var o = new JsonObj(raw[i], JsonObj.Index(root.Sub("items"), i));
                string id = o.Str("id"), section = o.Str("section");
                if (items.Any(x => x.Id == id)) throw new ContentException(o.Sub("id") + ": duplicate item '" + id + "'");
                if (!sections.Contains(section)) throw new ContentException(o.Sub("section") + ": '" + section + "' is not a section");
                var required = new List<string>();
                foreach (var r in o.Arr("required_in"))
                {
                    string sc = r as string ?? throw new ContentException(o.Sub("required_in") + ": expected text");
                    if (sc != "S1" && sc != "S2") throw new ContentException(o.Sub("required_in") + ": '" + sc + "' is not a scenario");
                    required.Add(sc);
                }
                items.Add(new HandoverItem(id, section, o.Str("label"), Logic.Compile(o.Any("known_if"), o.Sub("known_if")), required, o.Enum("severity", "critical", "major", "minor"), o.Str("ref")));
                o.Finish();
            }
            var scored = new SortedDictionary<string, string>(root.StringMap("scored"), StringComparer.Ordinal);
            foreach (var need in new[] { "complete", "critical", "major", "minor", "repeat" })
                if (!scored.ContainsKey(need)) throw new ContentException(root.Sub("scored") + ": '" + need + "' is missing");
            foreach (var r in root.Arr("refs")) if (!(r is string)) throw new ContentException(root.Sub("refs") + ": expected register ids");
            root.Finish();
            return new HandoverTables(sections, items, scored);
        }
    }

    /// <summary>What a finished handover came to: the items given, the ones the scenario required that were missing, and whether it was complete.</summary>
    public sealed record HandoverResult(long Step, IReadOnlyList<string> Given, IReadOnlyList<string> Missing, bool Complete);

    /// <summary>
    /// Step 12: the SBAR handover to the anaesthetist, given at the theatre holding area (theatre reception). The trainee fills each section from a menu of facts; only a fact they already know can be handed over,
    /// so nothing stated can contradict the state. finish_handover then judges it against what the scenario requires (a planned case's list, or the emergency case's mandatory items): a missing item is scored by its
    /// severity (allergies, fasting and aspiration risk are critical); in practice the anaesthetist asks for each missing item, and leaving the same one out again is a further major item.
    /// </summary>
    public sealed class HandoverSubsystem : ISubsystem
    {
        public const string HandoverVerb = "handover";
        public const string FinishVerb = "finish_handover";

        public const string NotAtHolding = "NOT_AT_HOLDING";
        public const string MissingSection = "MISSING_SECTION";
        public const string UnknownSection = "UNKNOWN_SECTION";
        public const string MissingItems = "MISSING_ITEMS";
        public const string UnknownItem = "UNKNOWN_ITEM";
        public const string WrongSection = "WRONG_SECTION";
        public const string UnknownFact = "UNKNOWN_FACT";
        public const string NothingHandedOver = "NOTHING_HANDED_OVER";
        public const string Holding = "theatre_reception";

        private readonly ContentPackage _content;
        private readonly List<GraphEvent> _history = new List<GraphEvent>();
        private readonly List<string> _given = new List<string>();
        private readonly List<HandoverResult> _results = new List<HandoverResult>();
        private readonly HashSet<string> _asked = new HashSet<string>(StringComparer.Ordinal);
        private Session _s = null!;

        public HandoverSubsystem(ContentPackage content) { _content = content; }

        public int Order => PipelineOrder.Investigations + 7;

        /// <summary>Handover sections, finishes and omissions with sim steps and score items. The scorer and the debrief read this.</summary>
        public IReadOnlyList<GraphEvent> History => _history;

        /// <summary>The items handed over so far, in order.</summary>
        public IReadOnlyList<string> Given => _given;

        public IReadOnlyList<HandoverResult> Results => _results;

        private HandoverTables Tables => _content.Handover;

        public void Init(Session session)
        {
            _s = session;
            session.HandoverStep = this;
            session.Verbs.Register(HandoverVerb, requiresIdentity: true);
            session.Verbs.Register(FinishVerb, requiresIdentity: true);
        }

        public void OnAction(SimAction a, ActionOutcome o)
        {
            switch (a.Verb)
            {
                case HandoverVerb: Hand(a, o); break;
                case FinishVerb: Finish(o); break;
            }
        }

        public void OnTick() { }

        /// <summary>The items this scenario requires.</summary>
        public IReadOnlyList<HandoverItem> Required() => Tables.Items.Where(i => i.RequiredIn.Contains(_s.Case.Scenario)).ToList();

        // L12-02: one section at a time, from the menu of known facts
        private void Hand(SimAction a, ActionOutcome o)
        {
            // DT-080: a handover said aloud can also DENY a fact ("no allergies"); the param `not` lists the items the trainee states do NOT apply. A denial the state contradicts is worse than leaving the item out:
            // it is a critical item when the item is one whose omission is critical (allergies, fasting, aspiration risk) and a major item otherwise. A denial of something the state does not hold is simply a true statement.
            if (a.Params.TryGetValue("not", out var denied) && denied.Trim().Length > 0)
            {
                if (_s.Patient.Clinical.TraineeLocation != Holding) { o.Reject(NotAtHolding); return; }
                if (!a.Params.TryGetValue("section", out var denySection) || denySection.Length == 0) { o.Reject(MissingSection); return; }
                if (!Tables.Sections.Contains(denySection)) { o.Reject(UnknownSection); return; }
                var state = RuleState.Build(_s);
                foreach (var id in denied.Split(',').Select(x => x.Trim()).Where(x => x.Length > 0))
                {
                    var item = Tables.Find(id);
                    if (item == null) { o.Reject(UnknownItem); return; }
                    if (item.Section != denySection) { o.Reject(WrongSection); return; }
                    if (!item.Known.Test(state)) continue;
                    string severity = item.Severity == "critical" ? "critical" : "major";
                    o.Effects.Add("contradicted:" + id);
                    o.Effects.Add("score:" + severity + ".handover_contradicted");
                    _history.Add(new GraphEvent("handover_contradicted", id, _s.Clock.Steps, new[] { severity + ".handover_contradicted" }));
                }
                if (!a.Params.ContainsKey("items") || a.Params["items"].Trim().Length == 0) return;      // only denials: nothing to hand over beside them
            }
            if (_s.Patient.Clinical.TraineeLocation != Holding) { o.Reject(NotAtHolding); return; }
            if (!a.Params.TryGetValue("section", out var section) || section.Length == 0) { o.Reject(MissingSection); return; }
            if (!Tables.Sections.Contains(section)) { o.Reject(UnknownSection); return; }
            if (!a.Params.TryGetValue("items", out var list) || list.Trim().Length == 0) { o.Reject(MissingItems); return; }
            var ids = list.Split(',').Select(x => x.Trim()).Where(x => x.Length > 0).ToList();
            var data = RuleState.Build(_s);
            foreach (var id in ids)
            {
                var item = Tables.Find(id);
                if (item == null) { o.Reject(UnknownItem); return; }
                if (item.Section != section) { o.Reject(WrongSection); return; }
                if (!item.Known.Test(data)) { o.Effects.Add("not_known:" + id); o.Reject(UnknownFact); return; }   // you cannot hand over what you do not know yet
            }
            foreach (var id in ids) if (!_given.Contains(id)) _given.Add(id);
            o.Effects.Add("handed_over:" + section + ":" + string.Join(",", ids));
            _history.Add(new GraphEvent("handover:" + section, string.Join(",", ids), _s.Clock.Steps, Array.Empty<string>()));
        }

        // the anaesthetist judges what was given against what the scenario needs
        private void Finish(ActionOutcome o)
        {
            if (_s.Patient.Clinical.TraineeLocation != Holding) { o.Reject(NotAtHolding); return; }
            if (_given.Count == 0) { o.Reject(NothingHandedOver); return; }
            var missing = Required().Where(i => !_given.Contains(i.Id)).ToList();
            bool practice = _s.Mode == SessionMode.Practice;
            var items = new List<string>();
            foreach (var m in missing)
            {
                items.Add(Tables.Scored[m.Severity]);
                if (_asked.Contains(m.Id)) items.Add(Tables.Scored["repeat"]);          // asked for it last time and still left it out
                if (practice) { o.Effects.Add("anaesthetist_asks:" + m.Id); _asked.Add(m.Id); }
                o.Effects.Add("missing:" + m.Id);
            }
            bool complete = missing.Count == 0;
            if (complete) items.Add(Tables.Scored["complete"]);
            _results.Add(new HandoverResult(_s.Clock.Steps, _given.ToList(), missing.Select(m => m.Id).ToList(), complete));
            _history.Add(new GraphEvent(complete ? "handover_complete" : "handover_incomplete", string.Join(",", missing.Select(m => m.Id)), _s.Clock.Steps, items));
            if (complete) o.Effects.Add("handover_complete");
        }
    }
}
