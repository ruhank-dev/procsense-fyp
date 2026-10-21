using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using ProcSense.Core.Content;

namespace ProcSense.Core.Scoring
{
    /// <summary>One of the 22 assessed behaviours (guide section 81): what is deterministic evidence for it, what the speech evaluator may report for it, and the rule that decides correctness.</summary>
    public sealed record Behaviour(string Id, string Label, IReadOnlyList<int> Steps, IReadOnlyList<string> Actions, IReadOnlyList<string> AiActs, bool Critical, string Rule, string Ref);

    /// <summary>Which behaviour and item type a score item belongs to (the first rule whose pattern matches the item id after its severity prefix).</summary>
    public sealed record RubricRule(Regex Match, string Behaviour, string Type, bool Teaching);

    /// <summary>The rubric (content/scoring/rubric.json), the behaviours and what counts as evidence (content/assessment/mapping.json) and the debrief sentences (content/debrief/templates.json).</summary>
    public sealed class ScoreTables
    {
        public static readonly ScoreTables Empty = new ScoreTables(new List<Behaviour>(), new List<RubricRule>(), new Dictionary<string, int>(), new Dictionary<string, IReadOnlyDictionary<string, double>>(), CommunicationRubric.Default,
            new List<string>(), new Dictionary<string, CriticalEvidence>(), new Dictionary<string, string>());

        public ScoreTables(IReadOnlyList<Behaviour> behaviours, IReadOnlyList<RubricRule> rules, IReadOnlyDictionary<string, int> points, IReadOnlyDictionary<string, IReadOnlyDictionary<string, double>> weights,
            CommunicationRubric communication, IReadOnlyList<string> criticalActs, IReadOnlyDictionary<string, CriticalEvidence> criticalEvidence, IReadOnlyDictionary<string, string> sentences)
        {
            Behaviours = behaviours; Rules = rules; Points = points; Weights = weights; Communication = communication; CriticalActs = criticalActs; CriticalEvidence = criticalEvidence; Sentences = sentences;
        }

        public IReadOnlyList<Behaviour> Behaviours { get; }
        public IReadOnlyList<RubricRule> Rules { get; }
        public IReadOnlyDictionary<string, int> Points { get; }
        public IReadOnlyDictionary<string, IReadOnlyDictionary<string, double>> Weights { get; }
        public CommunicationRubric Communication { get; }
        public IReadOnlyList<string> CriticalActs { get; }
        public IReadOnlyDictionary<string, CriticalEvidence> CriticalEvidence { get; }
        public IReadOnlyDictionary<string, string> Sentences { get; }

        public Behaviour? Find(string id) => Behaviours.FirstOrDefault(b => b.Id == id);

        /// <summary>The rule for an item id such as "major.handover_omitted", or null when no rule matches.</summary>
        public RubricRule? RuleFor(string itemId)
        {
            int dot = itemId.IndexOf('.');
            string name = dot < 0 ? itemId : itemId.Substring(dot + 1);
            foreach (var r in Rules) if (r.Match.IsMatch(name)) return r;
            return null;
        }

        public static ScoreTables Parse(string rubricJson, string mappingJson, string templatesJson)
        {
            var rub = new JsonObj(MiniJson.Parse(rubricJson), "scoring/rubric");
            rub.Bool("provisional");
            var points = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var kv in rub.Any("severity_points") as Dictionary<string, object?> ?? throw new ContentException("scoring/rubric.severity_points: expected an object"))
                points[kv.Key] = kv.Value is long l ? (int)l : throw new ContentException("scoring/rubric.severity_points." + kv.Key + ": expected a whole number");
            rub.Str("fail_severity");
            foreach (var t in rub.Arr("types")) if (!(t is string)) throw new ContentException("scoring/rubric.types: expected text");
            var types = rub.Arr("types").Cast<string>().ToList();

            var map = new JsonObj(MiniJson.Parse(mappingJson), "assessment/mapping");
            var behaviours = new List<Behaviour>();
            var rawB = map.Arr("behaviours");
            for (int i = 0; i < rawB.Count; i++)
            {
                var o = new JsonObj(rawB[i], JsonObj.Index(map.Sub("behaviours"), i));
                string id = o.Str("id");
                if (behaviours.Any(x => x.Id == id)) throw new ContentException(o.Sub("id") + ": duplicate behaviour");
                behaviours.Add(new Behaviour(id, o.Str("label"), Ints(o, "steps"), Strs(o, "actions"), Strs(o, "ai_acts"), o.Bool("critical"), o.Str("rule"), o.Str("ref")));
                o.Finish();
            }
            var rules = new List<RubricRule>();
            var rawR = rub.Arr("rules");
            for (int i = 0; i < rawR.Count; i++)
            {
                var o = new JsonObj(rawR[i], JsonObj.Index(rub.Sub("rules"), i));
                string beh = o.Str("behaviour"), type = o.Str("type");
                if (behaviours.All(x => x.Id != beh)) throw new ContentException(o.Sub("behaviour") + ": '" + beh + "' is not a behaviour");
                if (!types.Contains(type)) throw new ContentException(o.Sub("type") + ": '" + type + "' is not an item type");
                rules.Add(new RubricRule(new Regex(o.Str("match"), RegexOptions.CultureInvariant), beh, type, o.Bool("teaching")));
                o.Finish();
            }
            var weights = new Dictionary<string, IReadOnlyDictionary<string, double>>(StringComparer.Ordinal);
            foreach (var kv in rub.Any("weights") as Dictionary<string, object?> ?? throw new ContentException("scoring/rubric.weights: expected an object"))
            {
                var w = new Dictionary<string, double>(StringComparer.Ordinal);
                foreach (var kv2 in kv.Value as Dictionary<string, object?> ?? throw new ContentException("scoring/rubric.weights." + kv.Key + ": expected an object"))
                {
                    if (behaviours.All(x => x.Id != kv2.Key)) throw new ContentException("scoring/rubric.weights." + kv.Key + "." + kv2.Key + ": not a behaviour");
                    w[kv2.Key] = kv2.Value is long l ? l : kv2.Value is double d ? d : throw new ContentException("scoring/rubric.weights: expected a number");
                }
                weights[kv.Key] = w;
            }
            foreach (var r in rub.Arr("refs")) if (!(r is string)) throw new ContentException("scoring/rubric.refs: expected register ids");
            var communication = CommunicationRubric.Parse(rub.Obj("communication"));
            rub.Finish();

            var criticalActs = Strs(map, "critical_acts");
            var evidence = new Dictionary<string, CriticalEvidence>(StringComparer.Ordinal);
            var rawE = map.Obj("critical_act_evidence");
            foreach (var act in criticalActs)
            {
                var e = rawE.Obj(act);
                string beh = e.Str("behaviour");
                if (behaviours.All(x => x.Id != beh)) throw new ContentException(e.Sub("behaviour") + ": '" + beh + "' is not a behaviour");
                evidence[act] = new CriticalEvidence(beh, Strs(e, "actions"));
                e.Finish();
            }
            rawE.Finish();
            foreach (var r in map.Arr("refs")) if (!(r is string)) throw new ContentException("assessment/mapping.refs: expected register ids");
            map.Finish();

            var tpl = new JsonObj(MiniJson.Parse(templatesJson), "debrief/templates");
            var sentences = tpl.StringMap("sentences");
            foreach (var p in tpl.Arr("placeholders")) if (!(p is string)) throw new ContentException("debrief/templates.placeholders: expected text");
            foreach (var r in tpl.Arr("refs")) if (!(r is string)) throw new ContentException("debrief/templates.refs: expected register ids");
            tpl.Finish();
            return new ScoreTables(behaviours, rules, points, weights, communication, criticalActs, evidence, new Dictionary<string, string>(sentences));
        }

        private static IReadOnlyList<string> Strs(JsonObj o, string key)
        {
            var list = new List<string>();
            foreach (var x in o.Arr(key)) list.Add(x as string ?? throw new ContentException(o.Sub(key) + ": expected text"));
            return list;
        }

        private static IReadOnlyList<int> Ints(JsonObj o, string key)
        {
            var list = new List<int>();
            foreach (var x in o.Arr(key)) list.Add(x is long l ? (int)l : throw new ContentException(o.Sub(key) + ": expected whole numbers"));
            return list;
        }
    }
}
