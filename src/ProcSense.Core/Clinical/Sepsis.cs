using System;
using System.Collections.Generic;
using System.Linq;
using ProcSense.Core.Content;

namespace ProcSense.Core.Clinical
{
    /// <summary>One way of reaching a tier: a NEWS2 total range, optionally needing a single parameter that scores 3.</summary>
    public sealed record SepsisCriterion(string Id, string Tier, int? Min, int? Max, bool Single3, string Ref);

    /// <summary>What a tier asks for and by when (hours from the moment the tier is first met; a missing value means the tier does not ask for it).</summary>
    public sealed record SepsisTierDef(string Id, int Rank, double? AntibioticsHours, double? FluidsHours, double? ReviewHours, int? ReassessMinMinutes, int? ReassessMaxMinutes, bool Lactate, string Ref);

    /// <summary>The tier the engine works out: Tier is "none" when no infection is suspected.</summary>
    public sealed record SepsisAssessment(string Tier, int Rank, bool Raised);

    /// <summary>
    /// NICE NG253 risk tiers for suspected sepsis in adults (content/sepsis/sepsis.json, built from the verified tier table). A tier is only given when infection is suspected; the highest tier whose criteria
    /// are met wins; clinical concern raises it by one. NEWS2 is a risk tool, not a diagnosis, and qSOFA is never used on its own (walkthrough 4.10).
    /// </summary>
    public sealed class SepsisTables
    {
        public const string None = "none";

        public static readonly SepsisTables Empty = new SepsisTables(new List<SepsisTierDef>(), new List<SepsisCriterion>(), 1, new Dictionary<string, string>());

        public SepsisTables(IReadOnlyList<SepsisTierDef> tiers, IReadOnlyList<SepsisCriterion> criteria, int concernRaisesBy, IReadOnlyDictionary<string, string> infectionCases)
        {
            Tiers = tiers; Criteria = criteria; ConcernRaisesBy = concernRaisesBy; InfectionCases = infectionCases;
        }

        /// <summary>Highest risk first.</summary>
        public IReadOnlyList<SepsisTierDef> Tiers { get; }
        public IReadOnlyList<SepsisCriterion> Criteria { get; }
        public int ConcernRaisesBy { get; }
        /// <summary>Case id to the reason infection is the working diagnosis there.</summary>
        public IReadOnlyDictionary<string, string> InfectionCases { get; }

        public SepsisTierDef? Find(string tier) => Tiers.FirstOrDefault(t => t.Id == tier);

        public bool IsInfectionCase(string caseId) => InfectionCases.ContainsKey(caseId);

        /// <summary>The tier for these findings. No suspected infection: none (look for other causes, and still respond to NEWS2).</summary>
        public SepsisAssessment Assess(bool infectionSuspected, int news2, bool single3, bool concern)
        {
            if (!infectionSuspected || Tiers.Count == 0) return new SepsisAssessment(None, -1, false);
            SepsisTierDef? found = null;
            foreach (var tier in Tiers)
            {
                if (Criteria.Any(c => c.Tier == tier.Id && (!c.Min.HasValue || news2 >= c.Min.Value) && (!c.Max.HasValue || news2 <= c.Max.Value) && (!c.Single3 || single3))) { found = tier; break; }
            }
            if (found == null) return new SepsisAssessment(None, -1, false);
            if (!concern) return new SepsisAssessment(found.Id, found.Rank, false);
            int rank = Math.Min(Tiers.Max(t => t.Rank), found.Rank + ConcernRaisesBy);
            var raised = Tiers.First(t => t.Rank == rank);
            return new SepsisAssessment(raised.Id, raised.Rank, raised.Id != found.Id);
        }

        private static int? Opt(JsonObj o, string key) => o.Any(key) == null ? (int?)null : (int)o.Int(key);

        public static SepsisTables Parse(string json, string source = "sepsis")
        {
            var root = new JsonObj(MiniJson.Parse(json), source);
            var rawTiers = root.Arr("tiers");
            var ranks = new Dictionary<string, int>(StringComparer.Ordinal);
            for (int i = 0; i < rawTiers.Count; i++)
            {
                var o = new JsonObj(rawTiers[i], JsonObj.Index(root.Sub("tiers"), i));
                string id = o.Str("id");
                if (ranks.ContainsKey(id)) throw new ContentException(o.Sub("id") + ": duplicate tier '" + id + "'");
                ranks[id] = (int)o.Int("rank");
                o.Finish();
            }
            var criteria = new List<SepsisCriterion>();
            var rawCriteria = root.Arr("criteria");
            for (int i = 0; i < rawCriteria.Count; i++)
            {
                var o = new JsonObj(rawCriteria[i], JsonObj.Index(root.Sub("criteria"), i));
                string tier = o.Str("tier");
                if (!ranks.ContainsKey(tier)) throw new ContentException(o.Sub("tier") + ": '" + tier + "' is not a tier");
                criteria.Add(new SepsisCriterion(o.Str("id"), tier, Opt(o, "news2_min"), Opt(o, "news2_max"), o.Bool("single3"), o.Str("ref")));
                o.Finish();
            }
            var timers = root.Any("timers") as Dictionary<string, object?> ?? throw new ContentException(root.Sub("timers") + ": expected an object");
            var defs = new List<SepsisTierDef>();
            foreach (var kv in timers)
            {
                if (!ranks.TryGetValue(kv.Key, out int rank)) throw new ContentException(root.Sub("timers") + "." + kv.Key + ": not a tier");
                var o = new JsonObj(kv.Value, root.Sub("timers") + "." + kv.Key);
                double? Hours(string name) => o.Has(name) ? o.Int(name) : (double?)null;
                var def = new SepsisTierDef(kv.Key, rank, Hours("antibiotics_hours"), Hours("fluids_hours"), Hours("review_hours"),
                    o.Has("reassess_minutes_min") ? (int)o.Int("reassess_minutes_min") : (int?)null, o.Has("reassess_minutes_max") ? (int)o.Int("reassess_minutes_max") : (int?)null,
                    o.Has("lactate") && o.Bool("lactate"), o.Str("ref"));
                o.Finish();
                defs.Add(def);
            }
            foreach (var t in ranks.Keys) if (defs.All(d => d.Id != t)) throw new ContentException(root.Sub("timers") + ": no entry for the tier '" + t + "'");
            int raise = (int)root.Int("concern_raises_by");
            var infection = new SortedDictionary<string, string>(root.StringMap("infection_cases"), StringComparer.Ordinal);
            foreach (var r in root.Arr("refs")) if (!(r is string)) throw new ContentException(root.Sub("refs") + ": expected register ids");
            root.Finish();
            return new SepsisTables(defs.OrderByDescending(d => d.Rank).ToList(), criteria, raise, infection);
        }
    }
}
