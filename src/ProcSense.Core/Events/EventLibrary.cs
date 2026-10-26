using System;
using System.Collections.Generic;
using System.Linq;
using ProcSense.Core.Content;
using ProcSense.Core.Rules;

namespace ProcSense.Core.Events
{
    /// <summary>One thing the trainee is expected to do after an event fires: an accepted action with this verb (and at least these parameters) within the window.</summary>
    public sealed record ExpectedResponse(string Id, string Verb, IReadOnlyDictionary<string, string> Params, double WithinSeconds, string Label);

    /// <summary>What happens when the event is handled, missed or answered wrongly: score items, a flag, and effects on the patient.</summary>
    public sealed record EventConsequence(IReadOnlyList<string> Items, string Flag, IReadOnlyList<string> PhysioEffects);

    /// <summary>A rule that scales an event's chance (base probability times every multiplier whose rule is true).</summary>
    public sealed record EventModifier(string Event, Logic When, double Multiply, string Why);

    /// <summary>
    /// One approved event (the guide's EventDef, B-R14). Kind "caused" events follow from something the trainee did (a reaction after an allergen); "random" events are unprompted and obey the warm-up and the global cooldown.
    /// NeedsFlag is a cheap pre-check: the trigger rule is only worked out while that session flag is set, so a quiet session costs nothing.
    /// </summary>
    public sealed record EventDef(
        string Id, string WhatIf, string Category, string Kind, string Status, Logic Trigger, Logic Eligibility, string NeedsFlag, VitalWatch Watch, double BaseProbability, string Severity, string DifficultyTier, double CooldownSeconds,
        IReadOnlyList<string> Cues, IReadOnlyList<string> PhysioEffects, IReadOnlyList<string> SetFlags, IReadOnlyList<ExpectedResponse> Expected, IReadOnlyList<ExpectedResponse> Unacceptable,
        EventConsequence Handled, EventConsequence Missed, EventConsequence Wrong, IReadOnlyList<string> Intents, IReadOnlyList<string> Sources);

    /// <summary>A cheap guard on one of the patient's true vital signs (for example spo2 at or below 91). While it is false the trigger rule is not worked out, so a quiet patient costs nothing. An empty vital means no guard.</summary>
    public sealed record VitalWatch(string Vital, string Op, double Value)
    {
        public static readonly VitalWatch None = new VitalWatch("", "", 0);

        public bool Holds(State.PatientState p)
        {
            if (Vital.Length == 0) return true;
            var v = p.Physiological.Vitals;
            double x = Vital switch { "hr" => v.HeartRate, "sbp" => v.SystolicBp, "dbp" => v.DiastolicBp, "rr" => v.RespiratoryRate, "spo2" => v.Spo2, "pain" => v.PainScore, _ => v.TemperatureC };
            return Op switch { "<" => x < Value, "<=" => x <= Value, ">" => x > Value, _ => x >= Value };
        }
    }

    /// <summary>The event library and its settings (content/events/events.json).</summary>
    public sealed class EventTables
    {
        public static readonly EventTables Empty = new EventTables(20, 180, 90, 1, 1, new List<EventDef>(), new List<EventModifier>());

        public EventTables(int evaluateEverySteps, double warmupSeconds, double globalCooldownSeconds, int maxClinical, int maxOther, IReadOnlyList<EventDef> events, IReadOnlyList<EventModifier> modifiers)
        {
            EvaluateEverySteps = evaluateEverySteps; WarmupSeconds = warmupSeconds; GlobalCooldownSeconds = globalCooldownSeconds; MaxClinical = maxClinical; MaxOther = maxOther; Events = events; Modifiers = modifiers;
        }

        public int EvaluateEverySteps { get; }
        public double WarmupSeconds { get; }
        public double GlobalCooldownSeconds { get; }
        public int MaxClinical { get; }
        public int MaxOther { get; }
        public IReadOnlyList<EventDef> Events { get; }
        public IReadOnlyList<EventModifier> Modifiers { get; }

        /// <summary>The events a session in this mode may load (plan test T-E6: an event not approved for assessment is not loaded in assessment).</summary>
        public IReadOnlyList<EventDef> For(SessionMode mode) => Events.Where(e => ContentGate.Allows(e.Status, mode)).ToList();

        public static EventTables Parse(string json, string source = "events")
        {
            var root = new JsonObj(MiniJson.Parse(json), source);
            int every = (int)root.Int("evaluate_every_steps");
            if (every <= 0) throw new ContentException(root.Sub("evaluate_every_steps") + ": must be positive");
            double warmup = root.Num("warmup_seconds"), cooldown = root.Num("global_cooldown_seconds");
            int maxClinical = (int)root.Int("max_clinical"), maxOther = (int)root.Int("max_other");
            var events = new List<EventDef>();
            var raw = root.Arr("events");
            for (int i = 0; i < raw.Count; i++)
            {
                var o = new JsonObj(raw[i], JsonObj.Index(root.Sub("events"), i));
                string id = o.Str("id");
                if (events.Any(x => x.Id == id)) throw new ContentException(o.Sub("id") + ": duplicate event '" + id + "'");
                string status = o.Enum("status", "DRAFT", "CLINICAL_REVIEW", "APPROVED_FOR_TRAINING", "APPROVED_FOR_ASSESSMENT");
                double p = o.Num("base_probability");
                if (p < 0 || p > 1) throw new ContentException(o.Sub("base_probability") + ": needs 0 to 1");
                events.Add(new EventDef(id, o.Str("whatif"), o.Enum("category", "clinical", "other"), o.Enum("kind", "caused", "random"), status,
                    Logic.Compile(o.Any("trigger"), o.Sub("trigger")), Logic.Compile(o.Any("eligibility"), o.Sub("eligibility")), o.Str("needs_flag"), Watch(o), p,
                    o.Enum("severity", "low", "moderate", "high", "critical"), o.Enum("difficulty_tier", "beginner", "standard", "advanced"), o.Num("cooldown_seconds"),
                    Strings(o, "cues"), Strings(o, "physio_effects"), Strings(o, "set_flags"), Responses(o, "expected_responses"), Responses(o, "unacceptable"),
                    Consequence(o, "handled"), Consequence(o, "missed"), Consequence(o, "wrong"), Strings(o, "communication_intents"), Strings(o, "sources")));
                o.Finish();
            }
            var modifiers = new List<EventModifier>();
            var rawM = root.Arr("modifiers");
            for (int i = 0; i < rawM.Count; i++)
            {
                var o = new JsonObj(rawM[i], JsonObj.Index(root.Sub("modifiers"), i));
                string ev = o.Str("event");
                if (events.All(x => x.Id != ev)) throw new ContentException(o.Sub("event") + ": '" + ev + "' is not an event");
                modifiers.Add(new EventModifier(ev, Logic.Compile(o.Any("when"), o.Sub("when")), o.Num("multiply"), o.Str("why")));
                o.Finish();
            }
            foreach (var r in root.Arr("refs")) if (!(r is string)) throw new ContentException(root.Sub("refs") + ": expected register ids");
            root.Finish();
            return new EventTables(every, warmup, cooldown, maxClinical, maxOther, events, modifiers);
        }

        private static VitalWatch Watch(JsonObj o)
        {
            var w = o.Obj("watch");
            string vital = w.Str("vital"), op = w.Str("op");
            double value = w.Num("value");
            w.Finish();
            if (vital.Length == 0) return VitalWatch.None;
            if (!new[] { "hr", "sbp", "dbp", "rr", "spo2", "temp", "pain" }.Contains(vital)) throw new ContentException(o.Sub("watch") + ": '" + vital + "' is not a vital sign");
            if (!new[] { "<", "<=", ">", ">=" }.Contains(op)) throw new ContentException(o.Sub("watch") + ": '" + op + "' is not a comparison");
            return new VitalWatch(vital, op, value);
        }

        private static IReadOnlyList<string> Strings(JsonObj o, string key)
        {
            var list = new List<string>();
            foreach (var x in o.Arr(key)) list.Add(x as string ?? throw new ContentException(o.Sub(key) + ": expected text"));
            return list;
        }

        private static IReadOnlyList<ExpectedResponse> Responses(JsonObj o, string key)
        {
            var list = new List<ExpectedResponse>();
            var raw = o.Arr(key);
            for (int i = 0; i < raw.Count; i++)
            {
                var r = new JsonObj(raw[i], JsonObj.Index(o.Sub(key), i));
                var ps = new Dictionary<string, string>(StringComparer.Ordinal);
                foreach (var kv in r.StringMap("params")) ps[kv.Key] = kv.Value;
                double within = r.Num("within_seconds");
                if (within <= 0) throw new ContentException(r.Sub("within_seconds") + ": must be positive");
                list.Add(new ExpectedResponse(r.Str("id"), r.Str("verb"), ps, within, r.Str("label")));
                r.Finish();
            }
            return list;
        }

        private static EventConsequence Consequence(JsonObj o, string key)
        {
            var c = o.Obj("consequences").Obj(key);
            var result = new EventConsequence(Strings(c, "items"), c.Str("flag"), Strings(c, "physio_effects"));
            c.Finish();
            return result;
        }
    }
}
