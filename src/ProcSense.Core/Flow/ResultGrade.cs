using System;
using System.Collections.Generic;
using System.Linq;
using ProcSense.Core.Scoring;

namespace ProcSense.Core.Flow
{
    /// <summary>The grade bands and pass mark (content/scoring/grades.json): the lowest score for each letter, highest first, the pass mark, the points one hint costs and how many hints a session gives
    /// (P3-EXM-01; the session applies it through SessionOptions.HintLimit). Configurable data, not code; tools/content/validate_grades.py checks the thresholds (P3-RES-10).</summary>
    public sealed record GradeTable(IReadOnlyList<KeyValuePair<string, int>> Bands, int PassMark, int HintPenalty, int HintLimit = ExamControl.DefaultHintLimit)
    {
        public static GradeTable Default { get; } = new GradeTable(new[]
        {
            new KeyValuePair<string, int>("A", 90), new KeyValuePair<string, int>("B", 80), new KeyValuePair<string, int>("C", 70), new KeyValuePair<string, int>("D", 60),
        }, 60, 5);

        public string Letter(int score) { foreach (var b in Bands) if (score >= b.Value) return b.Key; return "F"; }

        /// <summary>Reads content/scoring/grades.json: {"bands": {"A": 90, ...}, "pass_mark": 60, "hint_penalty": 5, "hint_limit": 3}. Bands are kept highest first; no hint_limit means the default 3.</summary>
        public static GradeTable Parse(string json)
        {
            var root = new Content.JsonObj(Content.MiniJson.Parse(json), "grades");
            var bands = new List<KeyValuePair<string, int>>();
            var raw = root.Obj("bands");
            foreach (var letter in new[] { "A", "B", "C", "D" }) if (raw.Has(letter)) bands.Add(new KeyValuePair<string, int>(letter, (int)raw.Int(letter)));
            return new GradeTable(bands.OrderByDescending(b => b.Value).ToList(), (int)root.Int("pass_mark"), (int)root.Int("hint_penalty"),
                                  root.Has("hint_limit") ? (int)root.Int("hint_limit") : ExamControl.DefaultHintLimit);
        }
    }

    /// <summary>The number on the results panel (decision D4): a pure function of the existing ScoreReport, never a second scoring system. A critical failure decides the verdict whatever the number.</summary>
    public sealed record ResultGrade(int Score100, string Letter, bool Passed, bool Critical, int Hints)
    {
        /// <summary>
        /// Score100 = clamp(round(100 x (credits - penalties) / credits of the case's correct path) - HintPenalty x hints, 0, 100). The correct-path credits come from the case's golden correct script
        /// (the same Scorer on the correct session), so the number never depends on a second rubric. Passed = the engine's result is not FAIL, no critical item, Score100 at or above the pass mark.
        /// </summary>
        public static ResultGrade From(ScoreReport report, int correctPathCredits, int hints, GradeTable? table = null, SessionMode mode = SessionMode.Assessment)
        {
            table ??= GradeTable.Default;
            if (mode != SessionMode.Assessment) hints = 0;                                   // plan P3-RES-10 / D5: hints cost points in the examination only; they are free in training
            int credits = report.Items.Where(i => i.Status == "credit").Sum(i => i.Points);
            int penalties = report.Items.Where(i => i.Status == "penalty").Sum(i => Math.Abs(i.Points));
            double raw = correctPathCredits <= 0 ? 0 : 100.0 * (credits - penalties) / correctPathCredits;
            int score = (int)Math.Max(0, Math.Min(100, Math.Round(raw, MidpointRounding.AwayFromZero) - table.HintPenalty * Math.Max(0, hints)));
            bool critical = report.Critical.Count > 0;
            bool passed = report.Result != Scorer.ResultFail && !critical && score >= table.PassMark;
            return new ResultGrade(score, table.Letter(score), passed, critical, hints);
        }
    }
}
