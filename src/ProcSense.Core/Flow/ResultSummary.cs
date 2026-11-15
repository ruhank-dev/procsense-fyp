using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using ProcSense.Core.Content;
using ProcSense.Core.Scoring;

namespace ProcSense.Core.Flow
{
    /// <summary>How long one stage of the scenario took, in simulated minutes (P3-EXM-08: recorded, never used as time pressure).</summary>
    public sealed record StageTime(string Stage, double Minutes);

    /// <summary>
    /// The numbers and lines on the results panel (P3-RES-02), built only from the engine: the verdict and end state from Scorer and the end-state step, the points and critical items from the same
    /// ScoreReport, the goals the graph completed or missed, the hints, the times from the flow history and the clock, and the first recommendations the Debrief gives. Nothing here is a second scoring system.
    /// Grade is null when no correct-path credit total is known for the case (it comes from the case's golden correct run); the panel then shows the verdict and the items without a percentage.
    /// </summary>
    public sealed record ResultSummary(
        string CaseId, ulong Seed, string Mode, string Result, string EndState, bool Submitted, string SubmitOutcome, bool Abandoned, ResultGrade? Grade,
        int CreditPoints, int PenaltyPoints, int CriticalCount, IReadOnlyList<string> CompletedGoals, IReadOnlyList<string> MissedGoals, int HintsUsed, int HintsCharged,
        double TotalMinutes, IReadOnlyList<StageTime> StageTimes, IReadOnlyList<string> Recommendations)
    {
        public static ResultSummary From(Session s, ContentPackage content, int? correctPathCredits = null, GradeTable? table = null, int maxRecommendations = 3)
        {
            var report = Scorer.Score(s, content);
            int credits = report.Items.Where(i => i.Status == "credit").Sum(i => i.Points);
            int penalties = report.Items.Where(i => i.Status == "penalty").Sum(i => Math.Abs(i.Points));
            int hints = s.Hints.Count;
            int charged = s.Mode == SessionMode.Assessment ? hints : 0;                      // the examination charges for hints; practice does not
            ResultGrade? grade = correctPathCredits.HasValue ? ResultGrade.From(report, correctPathCredits.Value, charged, table, s.Mode) : null;

            var completed = s.GraphHistory.Where(e => e.Kind == "goal_complete").Select(e => e.Id).ToList();
            var missed = s.GraphHistory.Where(e => e.Kind == "goal_missed").Select(e => e.Id).ToList();

            var times = new List<StageTime>();
            var started = new Dictionary<string, long>(StringComparer.Ordinal);
            foreach (var e in s.FlowHistory)
            {
                if (e.Kind == "stage_start") started[e.Id] = e.Step;
                else if (e.Kind == "stage_done" && started.TryGetValue(e.Id, out var from)) times.Add(new StageTime(e.Id, Math.Round((e.Step - from) * Time.SimClock.Step / 60.0, 2)));
            }
            double total = Math.Round(s.Clock.Steps * Time.SimClock.Step / 60.0, 2);

            var recs = DebriefFacts.Build(s, content).Lines
                .Where(l => l.Status == "penalty" || l.Status == "critical").OrderBy(l => l.Status == "critical" ? 0 : 1).ThenBy(l => l.Points)
                .Select(l => l.Sentence).Take(maxRecommendations).ToList();

            return new ResultSummary(s.Case.Id, s.Seed, s.Mode.ToString().ToLowerInvariant(), report.Result, report.EndState, s.Submitted, s.SubmitOutcome, s.Abandoned, grade, credits, penalties,
                report.Critical.Count, completed, missed, hints, charged, total, times, recs);
        }

        /// <summary>The one line the panel shows when the trainee left before finishing.</summary>
        public string Headline => Abandoned ? "Not completed" : Grade != null ? Grade.Letter + " " + Grade.Score100.ToString(CultureInfo.InvariantCulture) + (Grade.Passed ? " passed" : " not passed") : Result;
    }
}

namespace ProcSense.Core.Flow
{
    /// <summary>The correct-path credit total of each case (content/scoring/correct_path_credits.json, made by procsense-sim credits): the baseline of the 100-point score. A case missing from it has no percentage.</summary>
    public static class CorrectPathCredits
    {
        public static IReadOnlyDictionary<string, int> Parse(string json)
        {
            var root = new Content.JsonObj(Content.MiniJson.Parse(json), "correct_path_credits");
            var result = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var kv in (Dictionary<string, object?>)root.Any("credits")!) result[kv.Key] = (int)(long)kv.Value!;
            return result;
        }
    }
}
