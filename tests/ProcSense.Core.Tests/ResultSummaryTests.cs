using System.Collections.Generic;
using System.IO;
using System.Linq;
using ProcSense.Core.Content;
using ProcSense.Core.Flow;
using ProcSense.Core.Replay;
using ProcSense.Core.Scoring;
using ProcSense.Core.Workflow;

namespace ProcSense.Core.Tests
{
    /// <summary>P3-RES-02 and P3-EXM-08: the summary equals Scorer and Debrief, the grade comes from the golden correct run, and a submission shows what was missed.</summary>
    public class ResultSummaryTests
    {
        private static ContentPackage Content(string id) => ContentLoader.Load(new FileContentReader(TestCase.ContentRoot), new[] { id }, new[] { id });
        private static readonly Dictionary<string, int> Credits = LoadCredits();

        private static Dictionary<string, int> LoadCredits()
        {
            using var doc = System.Text.Json.JsonDocument.Parse(File.ReadAllText(Path.Combine(TestCase.ContentRoot, "scoring", "correct_path_credits.json")));
            var result = new Dictionary<string, int>();
            foreach (var p in doc.RootElement.GetProperty("credits").EnumerateObject()) result[p.Name] = p.Value.GetInt32();
            return result;
        }

        private static Session Play(string caseId, string script, SessionMode mode, ContentPackage content)
        {
            var s = new Session(content.Cases[caseId], 42, mode, Subsystems.Default(content));
            SessionFile.Play(s, ActionScript.Parse(File.ReadAllText(Path.Combine(TestCase.Repo, "tests", "golden", caseId + "_" + script + ".actions.json"))));
            return s;
        }

        [Theory]
        [InlineData("VAR-S1-A")]
        [InlineData("VAR-S2-B")]
        [InlineData("STK-S1-D")]
        public void The_summary_equals_the_scorer_and_the_correct_run_scores_one_hundred(string caseId)
        {
            var content = Content(caseId);
            var s = Play(caseId, "correct", SessionMode.Practice, content);
            var report = Scorer.Score(s, content);
            var sum = ResultSummary.From(s, content, Credits[caseId]);
            Assert.Equal(report.Result, sum.Result);
            Assert.Equal(report.EndState, sum.EndState);
            Assert.Equal(report.Critical.Count, sum.CriticalCount);
            Assert.Equal(report.Items.Where(i => i.Status == "credit").Sum(i => i.Points), sum.CreditPoints);
            Assert.Equal(report.Items.Where(i => i.Status == "penalty").Sum(i => System.Math.Abs(i.Points)), sum.PenaltyPoints);
            Assert.Equal(DebriefFacts.Build(s, content).Lines.Where(l => l.Status == "penalty" || l.Status == "critical").Count() > 0, sum.Recommendations.Count > 0);
            Assert.NotNull(sum.Grade);
            Assert.Equal(100, sum.Grade!.Score100);
            Assert.True(sum.Grade.Passed);
            Assert.Equal("A", sum.Grade.Letter);
            Assert.True(sum.TotalMinutes > 0);
            Assert.All(sum.StageTimes, t => Assert.True(t.Minutes >= 0));
            Assert.Empty(sum.MissedGoals);
        }

        [Fact]
        public void Submitting_at_the_start_is_a_failure_that_lists_the_missed_goals_and_never_a_pass()
        {
            var content = Content("VAR-S1-A");
            var s = new Session(content.Cases["VAR-S1-A"], 42, SessionMode.Practice, Subsystems.Default(content));
            s.Apply(new Actions.SimAction(ExamControl.SubmitPatient));
            var sum = ResultSummary.From(s, content, Credits["VAR-S1-A"]);
            Assert.Equal(Scorer.ResultFail, sum.Result);
            Assert.True(sum.Submitted);
            Assert.NotEmpty(sum.MissedGoals);
            Assert.False(sum.Grade!.Passed);
            Assert.Equal("F", sum.Grade.Letter);
            Assert.Contains("0", sum.Headline);
        }

        [Fact]
        public void Stage_durations_are_listed_once_per_finished_stage_and_none_exceeds_the_total()
        {
            var content = Content("VAR-S1-A");
            var sum = ResultSummary.From(Play("VAR-S1-A", "correct", SessionMode.Practice, content), content);
            Assert.NotEmpty(sum.StageTimes);                                                                                 // P3-EXM-08: stage durations, not only the total
            Assert.Equal(sum.StageTimes.Count, sum.StageTimes.Select(t => t.Stage).Distinct().Count());
            Assert.All(sum.StageTimes, t => Assert.InRange(t.Minutes, 0, sum.TotalMinutes));
        }

        [Fact]
        public void Hints_are_charged_in_the_examination_only_and_an_abandoned_session_says_so()
        {
            var content = Content("VAR-S1-A");
            Session Start(SessionMode m) => new Session(content.Cases["VAR-S1-A"], 42, m, Subsystems.Default(content));
            var practice = Play("VAR-S1-A", "correct", SessionMode.Practice, content);
            var exam = Play("VAR-S1-A", "correct", SessionMode.Assessment, content);
            Assert.True(exam.Apply(new Actions.SimAction(ExamControl.RequestHint)).Accepted == false || exam.Hints.Count == 1);   // a finished case may have nothing left to hint at
            var a = Start(SessionMode.Assessment);
            a.Apply(new Actions.SimAction(ExamControl.RequestHint));
            a.Apply(new Actions.SimAction(ExamControl.RequestHint));
            var p = Start(SessionMode.Practice);
            p.Apply(new Actions.SimAction(ExamControl.RequestHint));
            Assert.Equal((2, 2), (ResultSummary.From(a, content).HintsUsed, ResultSummary.From(a, content).HintsCharged));
            Assert.Equal((1, 0), (ResultSummary.From(p, content).HintsUsed, ResultSummary.From(p, content).HintsCharged));
            Assert.Null(ResultSummary.From(a, content).Grade);                                                          // no baseline given: no percentage
            var gone = Start(SessionMode.Assessment);
            gone.Apply(new Actions.SimAction(ExamControl.SubmitPatient, "", Actions.Channel.Vr, new Dictionary<string, string> { [ExamControl.AbandonedParam] = "true" }));
            Assert.Equal("Not completed", ResultSummary.From(gone, content).Headline);
            Assert.NotNull(practice);
        }

        [Fact]
        public void The_credits_file_matches_the_correct_golden_runs_and_every_listed_case_exists()
        {
            foreach (var id in Credits.Keys) Assert.True(File.Exists(Path.Combine(TestCase.ContentRoot, "case_files", id + ".json")), id);
            var content = Content("VAR-S1-A");
            Assert.Equal(Credits["VAR-S1-A"], Scorer.Score(Play("VAR-S1-A", "correct", SessionMode.Practice, content), content).Items.Where(i => i.Status == "credit").Sum(i => i.Points));
        }
    }
}
