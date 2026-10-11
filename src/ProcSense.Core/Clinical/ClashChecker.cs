using System;
using System.Collections.Generic;
using System.Linq;
using ProcSense.Core.Content;

namespace ProcSense.Core.Clinical
{
    /// <summary>A patient factor and the words that show it in a case's wording.</summary>
    public sealed record FactorDef(string Tag, IReadOnlyList<string> Words);

    /// <summary>One treatment clash (walkthrough 14B.3): any one of the tag sets in When must be wholly present. Nothing here decides the clash: the senior or anaesthetist does (DecidedBy).</summary>
    public sealed record ClashDef(string Id, IReadOnlyList<IReadOnlyList<string>> When, string Clash, string Why, string Safer, string DecidedBy, string Tag, string ProblemType, string Ref);

    /// <summary>One perioperative medicine plan: the medicine group, the words that name it in a history, and the book's plan and care wording.</summary>
    public sealed record MedicinePlanDef(string Id, IReadOnlyList<string> Words, string Plan, string Care, string Ref);

    /// <summary>A plan that applies to this patient: the group, the word found and the wording to show.</summary>
    public sealed record MedicinePlan(string Id, string Medicine, string Plan, string Care, string Ref);

    /// <summary>A clash raised for a request: which clash and the tags that raised it.</summary>
    public sealed record ClashHit(ClashDef Clash, IReadOnlyList<string> Tags);

    /// <summary>The treatment clash table, the way a case's factors are read and the medicine plans (content/clashes/clashes.json, built from the verified tables).</summary>
    public sealed class ClashTables
    {
        public static readonly ClashTables Empty = new ClashTables(new Dictionary<string, IReadOnlyList<string>>(), new List<FactorDef>(), new List<string>(), new List<ClashDef>(), new List<MedicinePlanDef>(), 0, 0);

        public ClashTables(IReadOnlyDictionary<string, IReadOnlyList<string>> needs, IReadOnlyList<FactorDef> factors, IReadOnlyList<string> context, IReadOnlyList<ClashDef> clashes, IReadOnlyList<MedicinePlanDef> plans, double highPotassiumFrom, double lowOxygenTo)
        {
            Needs = needs; Factors = factors; Context = context; Clashes = clashes; Plans = plans; HighPotassiumFrom = highPotassiumFrom; LowOxygenTo = lowOxygenTo;
        }

        /// <summary>Treatment or test id to what is being asked for (need_pain_relief and so on).</summary>
        public IReadOnlyDictionary<string, IReadOnlyList<string>> Needs { get; }
        public IReadOnlyList<FactorDef> Factors { get; }
        public IReadOnlyList<string> Context { get; }
        public IReadOnlyList<ClashDef> Clashes { get; }
        public IReadOnlyList<MedicinePlanDef> Plans { get; }

        /// <summary>A potassium result at or above this is the 'high potassium' tag (the significant band).</summary>
        public double HighPotassiumFrom { get; }

        /// <summary>An oxygen saturation at or below this is the 'low oxygen' tag (the emergency sign).</summary>
        public double LowOxygenTo { get; }

        public static ClashTables Parse(string json, string source = "clashes")
        {
            var root = new JsonObj(MiniJson.Parse(json), source);
            var needs = new SortedDictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);
            var rawNeeds = root.Any("needs") as Dictionary<string, object?> ?? throw new ContentException(root.Sub("needs") + ": expected an object");
            foreach (var kv in rawNeeds) needs[kv.Key] = TextList(kv.Value, root.Sub("needs") + "." + kv.Key);
            var factors = new List<FactorDef>();
            var rawFactors = root.Arr("factors");
            for (int i = 0; i < rawFactors.Count; i++)
            {
                var o = new JsonObj(rawFactors[i], JsonObj.Index(root.Sub("factors"), i));
                factors.Add(new FactorDef(o.Str("tag"), TextList(o.Any("words"), o.Sub("words")).Select(w => w.ToLowerInvariant()).ToList()));
                o.Finish();
            }
            var context = TextList(root.Any("context"), root.Sub("context"));
            var pending = TextList(root.Any("pending_tags"), root.Sub("pending_tags"));
            var known = new HashSet<string>(factors.Select(f => f.Tag).Concat(needs.Values.SelectMany(x => x)).Concat(context).Concat(pending).Append("pregnancy"), StringComparer.Ordinal);
            var clashes = new List<ClashDef>();
            var rawClashes = root.Arr("clashes");
            for (int i = 0; i < rawClashes.Count; i++)
            {
                var o = new JsonObj(rawClashes[i], JsonObj.Index(root.Sub("clashes"), i));
                string id = o.Str("id");
                if (clashes.Any(c => c.Id == id)) throw new ContentException(o.Sub("id") + ": duplicate clash '" + id + "'");
                var alts = new List<IReadOnlyList<string>>();
                var rawWhen = o.Arr("when");
                for (int a = 0; a < rawWhen.Count; a++)
                {
                    var tags = TextList(rawWhen[a], JsonObj.Index(o.Sub("when"), a));
                    foreach (var t in tags) if (!known.Contains(t)) throw new ContentException(o.Sub("when") + ": '" + t + "' is not a tag anything defines");
                    if (tags.Count == 0) throw new ContentException(o.Sub("when") + ": an empty tag set would always match");
                    alts.Add(tags);
                }
                if (alts.Count == 0) throw new ContentException(o.Sub("when") + ": needs at least one tag set");
                clashes.Add(new ClashDef(id, alts, o.Str("clash"), o.Str("why"), o.Str("safer"), o.Str("decided_by"), o.Str("tag"), o.Enum("problem_type", "safety_blocker", "pathway_changing", "time_critical"), o.Str("ref")));
                o.Finish();
            }
            var plans = new List<MedicinePlanDef>();
            var rawPlans = root.Arr("plans");
            for (int i = 0; i < rawPlans.Count; i++)
            {
                var o = new JsonObj(rawPlans[i], JsonObj.Index(root.Sub("plans"), i));
                plans.Add(new MedicinePlanDef(o.Str("id"), TextList(o.Any("words"), o.Sub("words")).Select(w => w.ToLowerInvariant()).ToList(), o.Str("plan"), o.Str("care"), o.Str("ref")));
                o.Finish();
            }
            var checks = root.Arr("checks");
            foreach (var c in checks) { var o = new JsonObj(c, root.Sub("checks")); o.Str("id"); o.Str("text"); o.Str("ref"); o.Finish(); }
            var limits = root.Obj("limits");
            double highK = limits.Num("high_potassium_from"), lowO2 = limits.Num("low_oxygen_to");
            foreach (var r in limits.Arr("refs")) if (!(r is string)) throw new ContentException(limits.Sub("refs") + ": expected register ids");
            limits.Finish();
            foreach (var r in root.Arr("refs")) if (!(r is string)) throw new ContentException(root.Sub("refs") + ": expected register ids");
            root.Finish();
            return new ClashTables(needs, factors, context, clashes, plans, highK, lowO2);
        }

        private static List<string> TextList(object? node, string path)
        {
            var list = node as List<object?> ?? throw new ContentException(path + ": expected a list");
            var result = new List<string>();
            foreach (var x in list) result.Add(x as string ?? throw new ContentException(path + ": expected text"));
            return result;
        }
    }

    /// <summary>The factors a patient has, read from the case: pregnancy from the case's own flag, the rest from the words in the history answers, the presentation, the problems and the allergies.</summary>
    public static class PatientFactors
    {
        public static SortedSet<string> From(CaseFile file, ClashTables tables)
        {
            var tags = new SortedSet<string>(StringComparer.Ordinal);
            if (file.Pregnant) tags.Add("pregnancy");
            var text = new List<string>();
            text.AddRange(file.History.Select(h => h.Answer));
            text.Add(file.Presentation.Summary);
            text.AddRange(file.Problems.Select(p => p.Label + " " + p.Why));
            text.AddRange(file.Allergies.Select(a => a.Substance + " " + a.Reaction));
            text.AddRange(file.Facts);
            string all = string.Join(" | ", text).ToLowerInvariant();
            foreach (var f in tables.Factors)
                if (f.Words.Any(w => ContainsWord(all, w))) tags.Add(f.Tag);
            return tags;
        }

        /// <summary>The word starts at a word boundary (so "stent" is not found inside "consistent"); a stem such as "dehydrat" may carry a suffix.</summary>
        public static bool ContainsWord(string text, string word)
        {
            int from = 0;
            while (true)
            {
                int i = text.IndexOf(word, from, StringComparison.Ordinal);
                if (i < 0) return false;
                if (i == 0 || !char.IsLetterOrDigit(text[i - 1])) return true;
                from = i + 1;
            }
        }
    }

    /// <summary>Checks a request against the clash table: no pairwise code, only the tags.</summary>
    public static class ClashChecker
    {
        /// <summary>The clashes whose tags are all present, in table order.</summary>
        public static IReadOnlyList<ClashHit> Check(ClashTables tables, IReadOnlyCollection<string> tags)
        {
            var hits = new List<ClashHit>();
            foreach (var c in tables.Clashes)
            {
                var alt = c.When.FirstOrDefault(a => a.All(tags.Contains));
                if (alt != null) hits.Add(new ClashHit(c, alt));
            }
            return hits;
        }

        /// <summary>The tags for a request: what is being asked for plus the patient's factors and the context.</summary>
        public static SortedSet<string> TagsFor(ClashTables tables, string treatmentOrTest, IEnumerable<string> factors, IEnumerable<string> context)
        {
            var tags = new SortedSet<string>(factors.Concat(context), StringComparer.Ordinal);
            if (tables.Needs.TryGetValue(treatmentOrTest, out var needs)) foreach (var n in needs) tags.Add(n);
            return tags;
        }
    }

    /// <summary>The perioperative plans for the medicines a case lists.</summary>
    public static class MedicinePlans
    {
        public static IReadOnlyList<MedicinePlan> For(CaseFile file, ClashTables tables)
        {
            string all = string.Join(" | ", file.History.Select(h => h.Answer).Append(file.Presentation.Summary)).ToLowerInvariant();
            var plans = new List<MedicinePlan>();
            foreach (var p in tables.Plans)
            {
                var word = p.Words.FirstOrDefault(w => PatientFactors.ContainsWord(all, w));
                if (word != null) plans.Add(new MedicinePlan(p.Id, word, p.Plan, p.Care, p.Ref));
            }
            return plans;
        }
    }
}
