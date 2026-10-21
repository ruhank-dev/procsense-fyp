using System;
using System.Collections.Generic;
using System.Linq;
using ProcSense.Core.Workflow;

namespace ProcSense.Core.Scoring
{
    /// <summary>
    /// One scored fact: what was recorded (Id, e.g. "major.handover_omitted"), the behaviour it belongs to, its type and severity, the points it earns or costs, and the log entries that justify it.
    /// Status is "credit", "penalty", "critical" (fails the case), "info" or "teaching" (a moment that is explained but carries no penalty).
    /// </summary>
    public sealed record ScoreItem(string Id, string Category, string Type, string Severity, string Status, int Points, IReadOnlyList<int> Evidence, long Step, string RuleId, string Source);

    /// <summary>One behaviour's result: credits earned, penalties taken, the net and the net times the scenario's weight (weights are provisional).</summary>
    public sealed record CategoryScore(string Behaviour, string Label, int Credits, int Penalties, int Net, double Weight, double Weighted, int Items);

    /// <summary>How late something was: what it was and by how many minutes past its limit (from the sepsis timers) or that it was recorded as a delay.</summary>
    public sealed record Lateness(string What, double Minutes, string Source);

    /// <summary>The result of a case: the end state, the pass or fail verdict, every item, the criticals, the score per behaviour and the lateness.</summary>
    public sealed record ScoreReport(string Case, string Mode, string EndState, string Result, IReadOnlyList<ScoreItem> Items, IReadOnlyList<ScoreItem> Critical, IReadOnlyList<CategoryScore> Categories, IReadOnlyList<Lateness> Lateness, IReadOnlyList<CommunicationItem> Communication)
    {
        /// <summary>The weighted process score over all behaviours (provisional; the verdict does not depend on it).</summary>
        public double ProcessTotal => Categories.Sum(c => c.Weighted);

        /// <summary>The graded communication credit over all behaviours (EXPERIMENTAL weights, ADR-0025): what the evaluator judged the trainee said, converted by the engine. Never includes any physical credit.</summary>
        public double CommunicationTotal => Communication.Sum(c => c.Credit);

        /// <summary>Process score plus communication credit (provisional; the pass or fail verdict depends on neither).</summary>
        public double Total => ProcessTotal + CommunicationTotal;
    }

    /// <summary>Turns what the engine recorded into score items (p0-scoring-debrief s01) and the outcome (s03). Nothing here judges medicine: the rules that raised each item did; this only files and adds them up.</summary>
    public static class Scorer
    {
        public const string ResultSuccess = "SUCCESS", ResultDelayed = "SUCCESS_WITH_DELAY", ResultFail = "FAIL", ResultInProgress = "IN_PROGRESS";

        /// <summary>Every score item the session has recorded, once each (the same item at the same step from the same event is one item), in the order it happened.</summary>
        public static IReadOnlyList<ScoreItem> Collect(Session s, Content.ContentPackage content)
        {
            var tables = content.Scoring;
            var items = new List<ScoreItem>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            var events = s.EndStates?.AllEvents() ?? Enumerable.Empty<GraphEvent>();
            foreach (var e in events.OrderBy(x => x.Step))
                foreach (var id in e.ScoreItems)
                {
                    if (!seen.Add(id + "|" + e.Step.ToString(System.Globalization.CultureInfo.InvariantCulture) + "|" + e.Kind + "|" + e.Id)) continue;
                    items.Add(Make(tables, id, e, s.Log.SeqAtOrBefore(e.Step)));
                }
            // items an action reported as an effect ("score:<id>") that no history event repeats at the same step
            foreach (var entry in s.Log.Entries)
            {
                if (!entry.Accepted) continue;
                foreach (var eff in entry.Effects)
                {
                    if (!eff.StartsWith("score:", StringComparison.Ordinal)) continue;
                    string id = eff.Substring(6);
                    if (id.IndexOf('.') < 0 || items.Any(i => i.Id == id && i.Step == entry.Step)) continue;
                    items.Add(Make(tables, id, new GraphEvent("effect", entry.Verb, entry.Step, Array.Empty<string>()), entry.Seq));
                }
            }
            return items.OrderBy(i => i.Step).ToList();
        }

        private static ScoreItem Make(ScoreTables t, string id, GraphEvent e, int seq)
        {
            string severity = id.Substring(0, id.IndexOf('.'));
            var rule = t.RuleFor(id);
            string category = rule?.Behaviour ?? "";
            bool teaching = rule?.Teaching ?? false;
            int points = t.Points.TryGetValue(severity, out var p) ? p : 0;
            string status = severity == "critical" ? "critical" : severity == "info" ? "info" : teaching && points < 0 ? "teaching" : points > 0 ? "credit" : points < 0 ? "penalty" : "info";
            if (status == "teaching") points = 0;
            var evidence = seq >= 0 ? new[] { seq } : Array.Empty<int>();
            return new ScoreItem(id, category, rule?.Type ?? "", severity, status, points, evidence, e.Step, "RUBRIC." + id.Substring(id.IndexOf('.') + 1), rule == null ? "" : t.Find(category)?.Ref ?? "");
        }

        /// <summary>The outcome: the end state with the fail gate (any critical item fails the case; a correct postponement or redirection is a success), the score per behaviour and the lateness.</summary>
        public static ScoreReport Score(Session s, Content.ContentPackage content)
        {
            var tables = content.Scoring;
            var items = Collect(s, content);
            var critical = items.Where(i => i.Status == "critical").ToList();
            string end = s.EndStates?.Resolved ?? "";
            string result;
            if (end.Length == 0) result = critical.Count > 0 ? ResultFail : ResultInProgress;
            else if (critical.Count > 0 || end == "SAFETY_FAILURE") result = ResultFail;
            else if (end == "SAFE_BUT_DELAYED") result = ResultDelayed;
            else result = ResultSuccess;
            var weights = tables.Weights.TryGetValue(s.Case.Scenario, out var w) ? w : new Dictionary<string, double>();
            var categories = new List<CategoryScore>();
            foreach (var b in tables.Behaviours)
            {
                var mine = items.Where(i => i.Category == b.Id).ToList();
                int credits = mine.Where(i => i.Points > 0).Sum(i => i.Points), penalties = mine.Where(i => i.Points < 0).Sum(i => -i.Points);
                double weight = weights.TryGetValue(b.Id, out var x) ? x : 1.0;
                categories.Add(new CategoryScore(b.Id, b.Label, credits, penalties, credits - penalties, weight, (credits - penalties) * weight, mine.Count));
            }
            var late = new List<Lateness>();
            var sepsis = s.Sepsis;
            if (sepsis != null) foreach (var t in sepsis.Timers) if (t.LateMinutes > 0) late.Add(new Lateness(t.Kind, Math.Round(t.LateMinutes, 1), "SR-2859"));
            foreach (var i in items) if (content.EndStates.DelayItems.Contains(i.Id)) late.Add(new Lateness(i.Id, 0, "SR-0813"));
            var store = new EvidenceStore(tables);
            store.FromLog(s.Log);
            var communication = CommunicationScorer.Collect(store, tables, weights);
            return new ScoreReport(s.Case.Id, s.Mode.ToString().ToLowerInvariant(), end, result, items, critical, categories, late, communication);
        }
    }
}
