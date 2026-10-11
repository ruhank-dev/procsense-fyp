using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using ProcSense.Core.Content;
using ProcSense.Core.Rules;

namespace ProcSense.Core.Behaviour
{
    /// <summary>One attribute: its range, where it starts and what drives it.</summary>
    public sealed record BehaviourAttribute(string Id, double Min, double Max, double Start, string DrivenBy);

    /// <summary>One transition: when this trigger happens (and the guard is true) the deltas are applied, scaled down each time by the diminishing factor, on every Nth occurrence.</summary>
    public sealed record TransitionRule(string Id, string Trigger, IReadOnlyDictionary<string, double> Deltas, double Diminishing, int Every, Logic? When, string Ref, string Question);

    /// <summary>A mode and the limit that brings it on: the attribute reaching the limit (at or above for '&gt;=', at or below for '&lt;=').</summary>
    public sealed record ModeRule(string Mode, string Attribute, string Op, double Limit, double Hysteresis);

    /// <summary>What takes capacity away (walkthrough Part 10) and the limits that count.</summary>
    public sealed record CapacityRule(double ConfusionAt, double SeverePainAt, int StrongPainkillerMinutes, double SodiumBelow, IReadOnlyList<string> SepsisTiers, IReadOnlyList<string> PhysioFlags);

    /// <summary>A sensitive fact is only told when trust and willingness reach these limits.</summary>
    public sealed record DisclosureGate(string Topic, double Trust, double Willingness);

    /// <summary>The patient behaviour model's tables (content/behaviour/transitions.json, modes.json and disclosure.json).</summary>
    public sealed class BehaviourTables
    {
        public static readonly BehaviourTables Empty = new BehaviourTables(new List<BehaviourAttribute>(), new Dictionary<string, string>(), 10, new List<TransitionRule>(), new List<ModeRule>(), "CALM", new CapacityRule(1, 8, 120, 125, new List<string>(), new List<string>()), new List<DisclosureGate>());

        public BehaviourTables(IReadOnlyList<BehaviourAttribute> attributes, IReadOnlyDictionary<string, string> persona, int waitMinutes, IReadOnlyList<TransitionRule> rules, IReadOnlyList<ModeRule> modes, string defaultMode, CapacityRule capacity, IReadOnlyList<DisclosureGate> gates)
        {
            Attributes = attributes; Persona = persona; WaitMinutes = waitMinutes; Rules = rules; Modes = modes; DefaultMode = defaultMode; Capacity = capacity; Gates = gates;
        }

        public IReadOnlyList<BehaviourAttribute> Attributes { get; }
        /// <summary>The persona constants a case without its own gets (communication style, question frequency, need for reassurance, communication difficulty).</summary>
        public IReadOnlyDictionary<string, string> Persona { get; }
        public int WaitMinutes { get; }
        public IReadOnlyList<TransitionRule> Rules { get; }
        public IReadOnlyList<ModeRule> Modes { get; }
        public string DefaultMode { get; }
        public CapacityRule Capacity { get; }
        public IReadOnlyList<DisclosureGate> Gates { get; }

        public BehaviourAttribute? Find(string id) => Attributes.FirstOrDefault(a => a.Id == id);

        public static BehaviourTables Parse(string transitionsJson, string modesJson, string disclosureJson)
        {
            var t = new JsonObj(MiniJson.Parse(transitionsJson), "behaviour/transitions");
            var attrs = new List<BehaviourAttribute>();
            var rawA = t.Arr("attributes");
            for (int i = 0; i < rawA.Count; i++)
            {
                var o = new JsonObj(rawA[i], JsonObj.Index(t.Sub("attributes"), i));
                var a = new BehaviourAttribute(o.Str("id"), o.Num("min"), o.Num("max"), o.Num("start"), o.Str("driven_by"));
                if (attrs.Any(x => x.Id == a.Id)) throw new ContentException(o.Sub("id") + ": duplicate attribute '" + a.Id + "'");
                if (a.Max <= a.Min || a.Start < a.Min || a.Start > a.Max) throw new ContentException(o.Sub("start") + ": needs min < max and a start inside the range");
                attrs.Add(a);
                o.Finish();
            }
            var personaObj = t.Any("persona") as Dictionary<string, object?> ?? throw new ContentException(t.Sub("persona") + ": expected an object");
            var persona = new SortedDictionary<string, string>(StringComparer.Ordinal);
            foreach (var kv in personaObj) persona[kv.Key] = Convert.ToString(kv.Value, CultureInfo.InvariantCulture) ?? "";
            var styles = new List<string>();
            foreach (var s in t.Arr("persona_styles")) styles.Add(s as string ?? throw new ContentException(t.Sub("persona_styles") + ": expected text"));
            foreach (var s in t.Arr("persona_difficulties")) _ = s as string ?? throw new ContentException(t.Sub("persona_difficulties") + ": expected text");
            if (!styles.Contains(persona.GetValueOrDefault("communication_style", ""))) throw new ContentException(t.Sub("persona") + ": the communication style is not one of the styles");
            int wait = (int)t.Int("wait_minutes");
            var rules = new List<TransitionRule>();
            var rawR = t.Arr("rules");
            for (int i = 0; i < rawR.Count; i++)
            {
                var o = new JsonObj(rawR[i], JsonObj.Index(t.Sub("rules"), i));
                var deltas = new SortedDictionary<string, double>(StringComparer.Ordinal);
                var rawD = o.Any("deltas") as Dictionary<string, object?> ?? throw new ContentException(o.Sub("deltas") + ": expected an object");
                foreach (var kv in rawD)
                {
                    if (attrs.All(a => a.Id != kv.Key)) throw new ContentException(o.Sub("deltas") + "." + kv.Key + ": not an attribute");
                    deltas[kv.Key] = kv.Value is long l ? l : kv.Value is double d ? d : throw new ContentException(o.Sub("deltas") + "." + kv.Key + ": expected a number");
                }
                double dim = o.Num("diminishing");
                if (dim <= 0 || dim > 1) throw new ContentException(o.Sub("diminishing") + ": needs 0 < diminishing <= 1");
                int every = (int)o.Int("every");
                if (every < 1) throw new ContentException(o.Sub("every") + ": must be at least 1");
                var when = o.Any("when") == null ? null : Logic.Compile(o.Any("when"), o.Sub("when"));
                string id = o.Str("id");
                if (rules.Any(x => x.Id == id)) throw new ContentException(o.Sub("id") + ": duplicate rule '" + id + "'");
                rules.Add(new TransitionRule(id, o.Str("trigger"), deltas, dim, every, when, o.Str("ref"), o.Str("question")));
                o.Str("note"); o.Finish();
            }
            foreach (var r in t.Arr("refs")) if (!(r is string)) throw new ContentException(t.Sub("refs") + ": expected register ids");
            t.Finish();

            var m = new JsonObj(MiniJson.Parse(modesJson), "behaviour/modes");
            var modes = new List<ModeRule>();
            var rawM = m.Arr("modes");
            for (int i = 0; i < rawM.Count; i++)
            {
                var o = new JsonObj(rawM[i], JsonObj.Index(m.Sub("modes"), i));
                double hy = o.Num("hysteresis");
                if (hy < 0) throw new ContentException(o.Sub("hysteresis") + ": must not be negative");
                modes.Add(new ModeRule(o.Str("mode"), o.Str("attribute"), o.Enum("op", ">=", "<="), o.Num("limit"), hy));
                o.Finish();
            }
            string def = m.Str("default");
            var c = m.Obj("capacity");
            var tiers = new List<string>(); foreach (var x in c.Arr("sepsis_tiers")) tiers.Add(x as string ?? throw new ContentException(c.Sub("sepsis_tiers") + ": expected text"));
            var pf = new List<string>(); foreach (var x in c.Arr("physio_flags")) pf.Add(x as string ?? throw new ContentException(c.Sub("physio_flags") + ": expected text"));
            var cap = new CapacityRule(c.Num("confusion_at"), c.Num("severe_pain_at"), (int)c.Int("strong_painkiller_minutes"), c.Num("sodium_below"), tiers, pf);
            c.Finish();
            foreach (var r in m.Arr("refs")) if (!(r is string)) throw new ContentException(m.Sub("refs") + ": expected register ids");
            m.Finish();

            var d2 = new JsonObj(MiniJson.Parse(disclosureJson), "behaviour/disclosure");
            var gates = new List<DisclosureGate>();
            var rawG = d2.Arr("gates");
            for (int i = 0; i < rawG.Count; i++)
            {
                var o = new JsonObj(rawG[i], JsonObj.Index(d2.Sub("gates"), i));
                gates.Add(new DisclosureGate(o.Str("topic"), o.Num("trust"), o.Num("willingness")));
                o.Finish();
            }
            foreach (var r in d2.Arr("refs")) if (!(r is string)) throw new ContentException(d2.Sub("refs") + ": expected register ids");
            d2.Finish();
            return new BehaviourTables(attrs, persona, wait, rules, modes, def, cap, gates);
        }
    }

    /// <summary>The patient's attributes (each bounded) and the persona constants. Pain and the physiology-driven confusion come from outside; the rest move by transitions.</summary>
    public sealed class BehaviouralState
    {
        private readonly Dictionary<string, double> _values = new Dictionary<string, double>(StringComparer.Ordinal);
        private readonly BehaviourTables _t;

        public BehaviouralState(BehaviourTables tables)
        {
            _t = tables;
            foreach (var a in tables.Attributes) _values[a.Id] = a.Start;
            Persona = new Dictionary<string, string>(tables.Persona, StringComparer.Ordinal);
        }

        public IReadOnlyDictionary<string, double> Values => _values;

        public IDictionary<string, string> Persona { get; }

        public double Get(string attribute) => _values[attribute];

        /// <summary>Sets an attribute, held inside its range. Returns the value it ended up with.</summary>
        public double Set(string attribute, double value)
        {
            var a = _t.Find(attribute) ?? throw new ArgumentException("unknown attribute '" + attribute + "'", nameof(attribute));
            return _values[attribute] = Math.Max(a.Min, Math.Min(a.Max, value));
        }

        /// <summary>How strongly this patient reacts to things that worry them (1 is the usual).</summary>
        public double NeedForReassurance => double.TryParse(Persona["need_for_reassurance"], NumberStyles.Float, CultureInfo.InvariantCulture, out var d) ? d : 1.0;
    }

    /// <summary>Applies the transition table: each trigger runs its rules; a rule's deltas shrink by its diminishing factor every time it fires and may only fire on every Nth occurrence.</summary>
    public sealed class TransitionEngine
    {
        private readonly BehaviourTables _t;
        private readonly Dictionary<string, int> _occurrences = new Dictionary<string, int>(StringComparer.Ordinal);
        private readonly Dictionary<string, int> _fired = new Dictionary<string, int>(StringComparer.Ordinal);

        public TransitionEngine(BehaviourTables tables) { _t = tables; }

        /// <summary>Runs every rule of this trigger and returns the ids of the rules that changed something, with the question the patient asks (if a rule has one).</summary>
        public IReadOnlyList<string> Apply(string trigger, BehaviouralState state, out string question)
        {
            question = "";
            var fired = new List<string>();
            foreach (var rule in _t.Rules.Where(r => r.Trigger == trigger))
            {
                int seen = _occurrences.TryGetValue(rule.Id, out var s) ? s + 1 : 1;
                _occurrences[rule.Id] = seen;
                if (seen % rule.Every != 0) continue;
                if (rule.When != null && !rule.When.Test(state.Values.ToDictionary(kv => kv.Key, kv => (object?)kv.Value, StringComparer.Ordinal))) continue;
                int times = _fired.TryGetValue(rule.Id, out var f) ? f : 0;
                double factor = Math.Pow(rule.Diminishing, times);
                _fired[rule.Id] = times + 1;
                foreach (var kv in rule.Deltas)
                {
                    double delta = kv.Value * factor;
                    if ((kv.Key == "anxiety" || kv.Key == "fear") && delta > 0) delta *= state.NeedForReassurance;
                    state.Set(kv.Key, state.Get(kv.Key) + delta);
                }
                fired.Add(rule.Id);
                if (rule.Question.Length > 0) question = rule.Question;
            }
            return fired;
        }
    }

    /// <summary>First match wins; a mode is entered a little past its limit (the hysteresis) and held while the value stays at the limit or beyond, so a value hovering at the limit does not flicker.</summary>
    public sealed class ModeResolver
    {
        private readonly BehaviourTables _t;

        public ModeResolver(BehaviourTables tables) { _t = tables; }

        public string Resolve(IReadOnlyDictionary<string, double> values, string previous)
        {
            foreach (var rule in _t.Modes)
            {
                if (!values.TryGetValue(rule.Attribute, out var v)) continue;
                bool holding = rule.Mode == previous;
                bool up = rule.Op == ">=";
                double enter = up ? rule.Limit + rule.Hysteresis : rule.Limit - rule.Hysteresis;
                if (holding ? (up ? v >= rule.Limit : v <= rule.Limit) : (up ? v >= enter : v <= enter)) return rule.Mode;
            }
            return _t.DefaultMode;
        }
    }
}
