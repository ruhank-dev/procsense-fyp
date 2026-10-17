using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using ProcSense.Core.Content;

namespace ProcSense.Core.Difficulty
{
    public sealed record DifficultyDimension(string Id, string Label, string RealisedBy, IReadOnlyList<string> WeakBehaviours, string Tag);

    /// <summary>The adaptive difficulty settings (content/difficulty/dimensions.json). This is a rating and a lookup, not a trained model.</summary>
    public sealed class DifficultyTables
    {
        public static readonly DifficultyTables Empty = new DifficultyTables(16, 3, 1000, new Dictionary<string, double>(), new List<string>(), new List<string>(), new List<DifficultyDimension>(), new Dictionary<string, string>());

        public DifficultyTables(double k, int minSessions, double start, IReadOnlyDictionary<string, double> tierOffsets, IReadOnlyList<string> curriculumFirst, IReadOnlyList<string> tierOrder, IReadOnlyList<DifficultyDimension> dimensions, IReadOnlyDictionary<string, string> explanations)
        {
            K = k; MinSessions = minSessions; Start = start; TierOffsets = tierOffsets; CurriculumFirst = curriculumFirst; TierOrder = tierOrder; Dimensions = dimensions; Explanations = explanations;
        }

        public double K { get; }
        public int MinSessions { get; }
        public double Start { get; }
        public IReadOnlyDictionary<string, double> TierOffsets { get; }
        public IReadOnlyList<string> CurriculumFirst { get; }
        public IReadOnlyList<string> TierOrder { get; }
        public IReadOnlyList<DifficultyDimension> Dimensions { get; }
        public IReadOnlyDictionary<string, string> Explanations { get; }

        public DifficultyDimension? ForBehaviour(string behaviour) => Dimensions.FirstOrDefault(d => d.WeakBehaviours.Contains(behaviour));

        public double CaseRating(string tier) => Start + (TierOffsets.TryGetValue(tier, out var o) ? o : 0);

        public static DifficultyTables Parse(string json, string source = "difficulty/dimensions")
        {
            var root = new JsonObj(MiniJson.Parse(json), source);
            double k = root.Num("k_factor"), start = root.Num("start_rating");
            int min = (int)root.Int("min_sessions");
            if (k <= 0 || min < 1) throw new ContentException(root.Sub("k_factor") + ": needs a positive K factor and at least 1 session");
            var offsets = new Dictionary<string, double>(StringComparer.Ordinal);
            foreach (var kv in root.Any("tier_offsets") as Dictionary<string, object?> ?? throw new ContentException(root.Sub("tier_offsets") + ": expected an object"))
                offsets[kv.Key] = kv.Value is long l ? l : kv.Value is double d ? d : throw new ContentException(root.Sub("tier_offsets") + "." + kv.Key + ": expected a number");
            var first = Strs(root, "curriculum_first");
            var order = Strs(root, "curriculum_tier_order");
            var dims = new List<DifficultyDimension>();
            var raw = root.Arr("dimensions");
            for (int i = 0; i < raw.Count; i++)
            {
                var o = new JsonObj(raw[i], JsonObj.Index(root.Sub("dimensions"), i));
                dims.Add(new DifficultyDimension(o.Str("id"), o.Str("label"), o.Str("realised_by"), Strs(o, "weak_behaviours"), o.Str("tag")));
                o.Finish();
            }
            var expl = root.StringMap("explanations");
            foreach (var r in root.Arr("refs")) if (!(r is string)) throw new ContentException(root.Sub("refs") + ": expected register ids");
            root.Finish();
            return new DifficultyTables(k, min, start, offsets, first, order, dims, new Dictionary<string, string>(expl));
        }

        private static IReadOnlyList<string> Strs(JsonObj o, string key)
        {
            var list = new List<string>();
            foreach (var x in o.Arr(key)) list.Add(x as string ?? throw new ContentException(o.Sub(key) + ": expected text"));
            return list;
        }
    }

    /// <summary>What a finished session showed in each behaviour: credits earned and penalties taken (the "competencies" of the debrief facts).</summary>
    public sealed record SessionRecord(string Case, IReadOnlyDictionary<string, (int Credits, int Penalties)> Behaviours, string? Tier = null);

    /// <summary>
    /// A rating per behaviour for one trainee (a pseudonym, never a name), updated after each session in order. A behaviour with no items in a session is not rated that time.
    /// The score of a session in a behaviour is credits / (credits + penalties); the expected score is the Elo expectation against the case's rating (its tier); K is 16.
    /// </summary>
    public sealed class Ratings
    {
        private readonly Dictionary<string, double> _rating = new Dictionary<string, double>(StringComparer.Ordinal);
        private readonly Dictionary<string, int> _rated = new Dictionary<string, int>(StringComparer.Ordinal);

        public Ratings(DifficultyTables tables) { Tables = tables; }

        public DifficultyTables Tables { get; }

        /// <summary>How many sessions have been recorded (the selector adapts only after the minimum).</summary>
        public int Sessions { get; private set; }

        public double Of(string behaviour) => _rating.TryGetValue(behaviour, out var r) ? r : Tables.Start;

        public int TimesRated(string behaviour) => _rated.TryGetValue(behaviour, out var n) ? n : 0;

        public IEnumerable<string> RatedBehaviours => _rated.Keys.OrderBy(x => x, StringComparer.Ordinal);

        public void Add(SessionRecord session, string tier)
        {
            Sessions++;
            double caseRating = Tables.CaseRating(tier);
            foreach (var kv in session.Behaviours.OrderBy(k => k.Key, StringComparer.Ordinal))
            {
                int total = kv.Value.Credits + kv.Value.Penalties;
                if (total == 0) continue;
                double score = (double)kv.Value.Credits / total;
                double r = Of(kv.Key);
                double expected = 1.0 / (1.0 + Math.Pow(10, (caseRating - r) / 400.0));
                _rating[kv.Key] = r + Tables.K * (score - expected);
                _rated[kv.Key] = TimesRated(kv.Key) + 1;
            }
        }

        public static Ratings From(IEnumerable<SessionRecord> history, DifficultyTables tables, Func<string, string> tierOf)
        {
            var ratings = new Ratings(tables);
            foreach (var s in history) ratings.Add(s, s.Tier ?? tierOf(s.Case));         // a generated case is not in the library, so the record carries its tier
            return ratings;
        }
    }

    /// <summary>The difficulty tags a case earns from its own content. They are simple rules over the case file (design heuristics, SR-2866), so the same case always has the same tags.</summary>
    public static class CaseTags
    {
        private static readonly string[] HistoryTopics = { "medicines", "past_history", "herbal", "previous_anaesthetic" };

        public static IReadOnlyList<string> Of(CaseFile f)
        {
            var tags = new List<string>();
            var baseProblems = f.Problems.Where(p => p.Variant == null || p.Variant == "base").ToList();
            int busyHistory = f.History.Count(h => h.Variant == null && HistoryTopics.Contains(h.Topic) && !IsNone(h.Answer));
            if (busyHistory >= 2) tags.Add("history_complex");
            if (baseProblems.Count >= 2) tags.Add("multiple_problems");
            if (f.History.Any(h => h.DiscloseOn.Count > 0) || f.Examination.Any(e => e.DiscloseOn.Count > 0) || f.History.Any(h => h.Variant != null)) tags.Add("hidden_facts");
            if (f.Allergies.Count > 0 || baseProblems.Any(p => p.Type == "life_threat")) tags.Add("unexpected_events");
            if (baseProblems.Count >= 3 || f.Results.Count(r => r.Variant == null && r.Flag.Length > 0 && r.Flag != "normal") >= 3) tags.Add("complex_handover");
            if (f.Scenario == "S2") tags.Add("emergency_pace");
            return tags;
        }

        private static bool IsNone(string answer)
        {
            string a = answer.Trim().ToLowerInvariant();
            return a.Length == 0 || a == "none." || a == "none" || a == "no." || a == "no" || a.StartsWith("none ", StringComparison.Ordinal) || a.StartsWith("no ", StringComparison.Ordinal) || a.StartsWith("nil", StringComparison.Ordinal);
        }
    }

    public sealed record Selection(string CaseId, string Tier, string Reason, string Dimension, string Behaviour, string Explanation);

    /// <summary>
    /// Picks the next case. Assessment mode never adapts: it follows the fixed order. Practice mode follows the standard order until the trainee has played the minimum number of sessions, then looks at the weakest rated behaviour,
    /// takes the dimension that trains it and chooses an approved case with that dimension's tag whose tier rating is nearest the trainee's rating (not the case just played). It chooses only from the approved pool it is given.
    /// </summary>
    public static class Selector
    {
        public static Selection Next(Ratings ratings, IReadOnlyList<string> played, IReadOnlyDictionary<string, CaseFile> pool, SessionMode mode, Func<string, string>? labelOf = null)
        {
            var t = ratings.Tables;
            var approved = pool.Values.Where(f => ContentGate.Allows(f.Status, mode)).ToList();
            if (approved.Count == 0) return new Selection("", "", "no case is approved for " + mode.ToString().ToLowerInvariant() + " mode", "", "", "No case is approved for this mode.");
            var curriculum = Curriculum(approved, t);
            var standard = curriculum.FirstOrDefault(f => !played.Contains(f.Id)) ?? curriculum[played.Count % curriculum.Count];
            if (mode == SessionMode.Assessment)
                return new Selection(standard.Id, standard.Tier, "assessment follows the fixed order", "", "", Fill(t.Explanations["assessment"], ("case", standard.Id)));
            if (ratings.Sessions < t.MinSessions)
                return new Selection(standard.Id, standard.Tier, "fewer than " + t.MinSessions.ToString(CultureInfo.InvariantCulture) + " sessions: standard order", "", "", Fill(t.Explanations["default"], ("case", standard.Id), ("min_sessions", t.MinSessions.ToString(CultureInfo.InvariantCulture))));

            string weakest = ratings.RatedBehaviours.OrderBy(b => ratings.Of(b)).ThenBy(b => b, StringComparer.Ordinal).FirstOrDefault() ?? "";
            var dimension = weakest.Length == 0 ? null : t.ForBehaviour(weakest);
            if (dimension != null)
            {
                double r = ratings.Of(weakest);
                string last = played.Count > 0 ? played[played.Count - 1] : "";
                var candidates = approved.Where(f => f.Id != last && CaseTags.Of(f).Contains(dimension.Tag))
                    .OrderBy(f => Math.Abs(t.CaseRating(f.Tier) - r)).ThenBy(f => f.Id, StringComparer.Ordinal).ToList();
                if (candidates.Count > 0)
                {
                    var pick = candidates[0];
                    return new Selection(pick.Id, pick.Tier, "weakest behaviour " + weakest + " points at " + dimension.Id, dimension.Id, weakest,
                        Fill(t.Explanations["adapted"], ("case", pick.Id), ("behaviour", labelOf == null ? weakest : labelOf(weakest)), ("rating", Math.Round(r).ToString(CultureInfo.InvariantCulture)), ("dimension", dimension.Label)));
                }
            }
            return new Selection(standard.Id, standard.Tier, dimension == null ? "no rated behaviour yet: standard order" : "no approved case carries the tag for " + dimension.Id + ": standard order", dimension?.Id ?? "", weakest,
                Fill(t.Explanations["default"], ("case", standard.Id), ("min_sessions", t.MinSessions.ToString(CultureInfo.InvariantCulture))));
        }

        /// <summary>The standard order: the named first cases, then by tier (P0, P1, P2), scenario (S1 first) and id.</summary>
        public static IReadOnlyList<CaseFile> Curriculum(IEnumerable<CaseFile> approved, DifficultyTables t)
        {
            var list = approved.ToList();
            int TierIndex(string tier) { int i = t.TierOrder.ToList().IndexOf(tier); return i < 0 ? 99 : i; }
            return list.OrderBy(f => { int i = t.CurriculumFirst.ToList().IndexOf(f.Id); return i < 0 ? 999 : i; }).ThenBy(f => TierIndex(f.Tier)).ThenBy(f => f.Scenario, StringComparer.Ordinal).ThenBy(f => f.Id, StringComparer.Ordinal).ToList();
        }

        private static string Fill(string template, params (string k, string v)[] values)
        {
            foreach (var (k, v) in values) template = template.Replace("{" + k + "}", v);
            return template;
        }
    }
}
