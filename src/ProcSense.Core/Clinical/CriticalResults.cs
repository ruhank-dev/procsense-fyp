using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using ProcSense.Core.Content;
using ProcSense.Core.State;

namespace ProcSense.Core.Clinical
{
    /// <summary>One limit the lab phones about: the analyte, which way (low, high, or a fall from the last value), the value and whether the limit itself counts, and the fact the condition needs.</summary>
    public sealed record CriticalRule(string Id, string Analyte, string Direction, bool Inclusive, double Value, string Unit, string Condition, string Ref);

    /// <summary>A result that crosses a limit: the rule it crossed, the result, and how the lab says it on the phone ("Potassium 6.6 mmol/L").</summary>
    public sealed record CriticalFinding(string RuleId, string Analyte, string ResultId, double Value, string Unit, string Spoken);

    /// <summary>The limits the lab phones about and the actions a trainee can choose when acknowledging one (content/investigations/critical.json, built from the verified table).</summary>
    public sealed class CriticalTable
    {
        public static readonly CriticalTable Empty = new CriticalTable(new List<CriticalRule>(), new Dictionary<string, IReadOnlyList<string>>(), new List<string>(), DefaultCallTemplate);

        /// <summary>The words of the phone call when a file carries none (older packages); {name}, {dob} and {values} are filled from the case.</summary>
        public const string DefaultCallTemplate = "This is the laboratory with a critical result for {name}, date of birth {dob}: {values}.";

        public CriticalTable(IReadOnlyList<CriticalRule> rules, IReadOnlyDictionary<string, IReadOnlyList<string>> aliases, IReadOnlyList<string> actions, string callTemplate = DefaultCallTemplate)
        {
            Rules = rules; Aliases = aliases; Actions = actions; CallTemplate = callTemplate;
        }

        public IReadOnlyList<CriticalRule> Rules { get; }
        /// <summary>The result names the case files use for each analyte (Hb and Haemoglobin are both haemoglobin).</summary>
        public IReadOnlyDictionary<string, IReadOnlyList<string>> Aliases { get; }
        public IReadOnlyList<string> Actions { get; }
        /// <summary>What the laboratory says on the phone, with {name}, {dob} and {values} (content, so the voice catalogue can carry it, DT-180).</summary>
        public string CallTemplate { get; }

        /// <summary>The spoken call for a patient and the spoken findings.</summary>
        public string Say(string name, string dob, IEnumerable<string> spoken) =>
            CallTemplate.Replace("{name}", name).Replace("{dob}", dob).Replace("{values}", string.Join("; ", spoken));

        public static CriticalTable Parse(string json, string source = "critical")
        {
            var root = new JsonObj(MiniJson.Parse(json), source);
            var rules = new List<CriticalRule>();
            var raw = root.Arr("rules");
            string[] conditions = { "always", "no_known_kidney_failure", "on_warfarin", "not_on_warfarin" };
            for (int i = 0; i < raw.Count; i++)
            {
                var o = new JsonObj(raw[i], JsonObj.Index(root.Sub("rules"), i));
                string id = o.Str("id");
                if (rules.Any(r => r.Id == id)) throw new ContentException(o.Sub("id") + ": duplicate rule '" + id + "'");
                rules.Add(new CriticalRule(id, o.Str("analyte"), o.Enum("direction", "low", "high", "fall"), o.Bool("inclusive"), o.Num("value"), o.Str("unit"), o.Enum("condition", conditions), o.Str("ref")));
                o.Finish();
            }
            var aliasObj = root.Any("aliases") as Dictionary<string, object?> ?? throw new ContentException(root.Sub("aliases") + ": expected an object");
            var aliases = new SortedDictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);
            foreach (var kv in aliasObj)
            {
                var names = new List<string>();
                foreach (var n in kv.Value as List<object?> ?? throw new ContentException(root.Sub("aliases") + "." + kv.Key + ": expected a list")) names.Add(n as string ?? throw new ContentException(root.Sub("aliases") + "." + kv.Key + ": expected text"));
                aliases[kv.Key] = names;
            }
            foreach (var r in rules)
                if (!aliases.ContainsKey(r.Analyte)) throw new ContentException(root.Sub("rules") + ": the analyte '" + r.Analyte + "' has no result names");
            var actions = new List<string>();
            foreach (var a in root.Arr("actions")) actions.Add(a as string ?? throw new ContentException(root.Sub("actions") + ": expected text"));
            if (actions.Count == 0) throw new ContentException(root.Sub("actions") + ": needs at least one action");
            foreach (var r in root.Arr("refs")) if (!(r is string)) throw new ContentException(root.Sub("refs") + ": expected register ids");
            string template = root.Has("call_template") ? root.Str("call_template") : DefaultCallTemplate;
            foreach (var token in new[] { "{name}", "{dob}", "{values}" })
                if (!template.Contains(token)) throw new ContentException(root.Sub("call_template") + ": must contain " + token);
            root.Finish();
            return new CriticalTable(rules, aliases, actions, template);
        }

        /// <summary>
        /// The limits a set of results crosses. Facts (on_warfarin, known_kidney_failure) switch the conditional limits. A number is only compared when the result's unit is the rule's unit
        /// (neutrophils given as a percentage are not compared with a count), and a fall needs the previous value.
        /// </summary>
        public IReadOnlyList<CriticalFinding> Evaluate(IEnumerable<ResultRecord> results, ISet<string> facts)
        {
            var found = new List<CriticalFinding>();
            foreach (var r in results)
            {
                if (r.Quality == "mislabelled" || !double.TryParse(r.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out double v)) continue;
                string name = InvestigationTables.BaseName(r.Analyte);
                foreach (var rule in Rules)
                {
                    if (!Aliases[rule.Analyte].Contains(name) || !SameUnit(rule.Unit, r.Unit) || !Applies(rule.Condition, facts)) continue;
                    bool hit;
                    if (rule.Direction == "fall")
                    {
                        if (!double.TryParse(r.Previous, NumberStyles.Float, CultureInfo.InvariantCulture, out double before)) continue;
                        double fall = before - v;
                        hit = rule.Inclusive ? fall >= rule.Value : fall > rule.Value;
                    }
                    else if (rule.Direction == "low") hit = rule.Inclusive ? v <= rule.Value : v < rule.Value;
                    else hit = rule.Inclusive ? v >= rule.Value : v > rule.Value;
                    if (hit) found.Add(new CriticalFinding(rule.Id, rule.Analyte, r.Id, v, r.Unit, name + " " + r.Value + (r.Unit.Length > 0 ? " " + r.Unit : "")));
                }
            }
            return found;
        }

        private static bool SameUnit(string a, string b) => a.Replace(" ", "").Equals(b.Replace(" ", ""), StringComparison.OrdinalIgnoreCase);

        private static bool Applies(string condition, ISet<string> facts) => condition switch
        {
            "no_known_kidney_failure" => !facts.Contains("known_kidney_failure"),
            "on_warfarin" => facts.Contains("on_warfarin"),
            "not_on_warfarin" => !facts.Contains("on_warfarin"),
            _ => true,
        };
    }
}
