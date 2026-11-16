using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ProcSense.Core.Content;
using ProcSense.Core.Flow;
using ProcSense.Core.Scoring;

namespace ProcSense.Core.Tests
{
    /// <summary>P3-SCN-02/03/04/05/09 and P3-RES-10: the catalogue, the picker, the briefing and the 100-point grade (all pure).</summary>
    public class ScenarioFlowTests
    {
        private static readonly CaseCatalogue Catalogue = CaseCatalogue.Parse(File.ReadAllText(Path.Combine(TestCase.ContentRoot, "ui", "case_catalogue.json")));
        private static readonly string[] Procedures = { CaseCatalogue.Cholecystectomy, CaseCatalogue.Appendicectomy };

        [Fact]
        public void The_catalogue_lists_every_case_file_with_its_own_status()
        {
            var files = Directory.GetFiles(Path.Combine(TestCase.ContentRoot, "case_files"), "*.json").Select(p => Path.GetFileNameWithoutExtension(p)!).OrderBy(x => x, StringComparer.Ordinal).ToList();
            Assert.Equal(files, Catalogue.Entries.Select(e => e.Id).ToList());
            foreach (var e in Catalogue.Entries) Assert.Equal(ContentLoader.LoadCaseFile(new FileContentReader(TestCase.ContentRoot), e.Id).Status, e.Status);
        }

        [Fact]
        public void Practice_counts_exclude_drafts_and_the_examination_pool_is_the_63_assessment_approved_cases()
        {
            Assert.Equal(63, Procedures.Sum(p => Catalogue.Count(SessionMode.Practice, p)));
            Assert.Equal(63, Procedures.Sum(p => Catalogue.Count(SessionMode.Assessment, p)));   // P3-SCN-07, D1: both pathways, no DRAFT
            Assert.Equal(Catalogue.Entries.Count(e => e.Status == "APPROVED_FOR_ASSESSMENT"), Procedures.Sum(p => Catalogue.Count(SessionMode.Assessment, p)));
            Assert.All(Catalogue.Entries.Where(e => e.Status == "DRAFT"), e => Assert.DoesNotContain(e.Id, Procedures.SelectMany(p => Enumerable.Range(0, 400).Select(s => ScenarioPicker.Pick(Catalogue, SessionMode.Practice, p, TrainingLevel.Advanced, (ulong)s)?.CaseId ?? "")).ToHashSet()));
        }

        [Theory]
        [InlineData(CaseCatalogue.Cholecystectomy)]
        [InlineData(CaseCatalogue.Appendicectomy)]
        public void The_pick_is_deterministic_never_a_draft_and_covers_the_pool(string procedure)
        {
            var seen = new HashSet<string>();
            for (ulong seed = 1; seed <= 10000; seed++)
            {
                var a = ScenarioPicker.Pick(Catalogue, SessionMode.Practice, procedure, TrainingLevel.Advanced, seed)!;
                var b = ScenarioPicker.Pick(Catalogue, SessionMode.Practice, procedure, TrainingLevel.Advanced, seed)!;
                Assert.Equal(a, b);
                var entry = Catalogue.Find(a.CaseId)!;
                Assert.Equal(procedure, entry.Procedure);
                Assert.NotEqual("DRAFT", entry.Status);
                seen.Add(a.CaseId);
            }
            Assert.Equal(Catalogue.Count(SessionMode.Practice, procedure), seen.Count);
        }

        [Fact]
        public void Ten_training_sessions_give_at_least_five_different_cases()
        {
            var recent = new List<string>();
            for (ulong seed = 100; seed < 110; seed++) recent.Add(ScenarioPicker.Pick(Catalogue, SessionMode.Practice, CaseCatalogue.Cholecystectomy, TrainingLevel.Beginner, seed, recent)!.CaseId);
            Assert.True(recent.Distinct().Count() >= 5, string.Join(",", recent));
        }

        [Fact]
        public void The_draw_is_roughly_uniform_over_ten_thousand_seeds()
        {
            var counts = new Dictionary<string, int>();
            for (ulong seed = 1; seed <= 10000; seed++) { var id = ScenarioPicker.Pick(Catalogue, SessionMode.Practice, CaseCatalogue.Appendicectomy, TrainingLevel.Advanced, seed)!.CaseId; counts[id] = counts.GetValueOrDefault(id) + 1; }
            double mean = 10000.0 / counts.Count;
            Assert.All(counts.Values, c => Assert.InRange(c, mean * 0.6, mean * 1.4));
        }

        [Fact]
        public void A_beginner_is_never_given_a_stacked_case_while_another_exists()
        {
            for (ulong seed = 1; seed <= 3000; seed++)
                foreach (var p in Procedures)
                {
                    var pick = ScenarioPicker.Pick(Catalogue, SessionMode.Practice, p, TrainingLevel.Beginner, seed)!;
                    Assert.False(Catalogue.Find(pick.CaseId)!.Stacked);
                    Assert.Null(pick.Relaxed);
                }
        }

        [Fact]
        public void The_last_three_cases_are_not_repeated_and_a_tiny_pool_says_so()
        {
            var recent = new List<string> { "SC-1", "SC-2", "SC-3" };
            for (ulong seed = 1; seed <= 2000; seed++) Assert.DoesNotContain(ScenarioPicker.Pick(Catalogue, SessionMode.Practice, CaseCatalogue.Appendicectomy, TrainingLevel.Advanced, seed, recent)!.CaseId, recent);
            var tiny = new CaseCatalogue(new[] { new CatalogueEntry("X-1", "appendicectomy", "P0", "APPROVED_FOR_TRAINING", false, "ward", "F", "18-39", "x", "F-18-39") });
            var only = ScenarioPicker.Pick(tiny, SessionMode.Practice, "appendicectomy", TrainingLevel.Advanced, 7, new[] { "X-1" })!;
            Assert.Equal("X-1", only.CaseId);
            Assert.NotNull(only.Relaxed);
            Assert.Null(ScenarioPicker.Pick(tiny, SessionMode.Assessment, "appendicectomy", TrainingLevel.Advanced, 7));
        }

        [Fact]
        public void Retry_keeps_case_and_seed_and_a_new_scenario_draws_another_seed_and_case()
        {
            var first = ScenarioPicker.Pick(Catalogue, SessionMode.Practice, CaseCatalogue.Cholecystectomy, TrainingLevel.Advanced, 42)!;
            Assert.Equal(first, ScenarioPicker.Retry(first));
            var next = ScenarioPicker.NewScenario(Catalogue, SessionMode.Practice, CaseCatalogue.Cholecystectomy, TrainingLevel.Advanced, first)!;
            Assert.NotEqual(first.Seed, next.Seed);
            Assert.NotEqual(first.CaseId, next.CaseId);
            Assert.Equal(next, ScenarioPicker.NewScenario(Catalogue, SessionMode.Practice, CaseCatalogue.Cholecystectomy, TrainingLevel.Advanced, first));
        }

        [Fact]
        public void The_briefing_shows_only_the_admission_facts()
        {
            var file = ContentLoader.LoadCaseFile(new FileContentReader(TestCase.ContentRoot), "SC-5");
            var b = BriefingFacts.From(file);
            Assert.Equal(new[] { "CaseId", "Name", "Age", "Sex", "Setting", "Reason" }, typeof(BriefingFacts).GetProperties().Where(p => p.Name != "EqualityContract").Select(p => p.Name).ToArray());
            Assert.Equal(file.ReasonAnswer, b.Reason);
            Assert.DoesNotContain("apixaban", b.Reason, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("HR 124", b.Reason);
        }

        private static ScoreReport Report(string result, int credit, int penalty, bool critical)
        {
            var items = new List<ScoreItem> { new ScoreItem("a.b", "x", "t", "minor", "credit", credit, new int[0], 1, "r", "s"), new ScoreItem("c.d", "x", "t", "minor", "penalty", -penalty, new int[0], 2, "r", "s") };
            var crit = critical ? new List<ScoreItem> { new ScoreItem("z.z", "x", "t", "critical", "critical", 0, new int[0], 3, "r", "s") } : new List<ScoreItem>();
            return new ScoreReport("C", "practice", "end", result, items, crit, new List<CategoryScore>(), new List<Lateness>(), new List<CommunicationItem>());
        }

        [Fact]
        public void Hints_lower_the_score_in_the_examination_only()
        {
            var r = Report(Scorer.ResultSuccess, 100, 0, false);
            Assert.Equal(90, ResultGrade.From(r, 100, 2, null, SessionMode.Assessment).Score100);   // 5 points each, plan P3-RES-10 / D5
            Assert.Equal(100, ResultGrade.From(r, 100, 2, null, SessionMode.Practice).Score100);    // free in training
            Assert.Equal(0, ResultGrade.From(r, 100, 2, null, SessionMode.Practice).Hints);
        }

        [Fact]
        public void The_score_and_grade_follow_the_formula_and_a_critical_failure_decides_the_verdict()
        {
            var good = ResultGrade.From(Report(Scorer.ResultSuccess, 100, 0, false), 100, 0);
            Assert.Equal((100, "A", true), (good.Score100, good.Letter, good.Passed));
            var hinted = ResultGrade.From(Report(Scorer.ResultSuccess, 100, 0, false), 100, 2);
            Assert.Equal((90, "A"), (hinted.Score100, hinted.Letter));
            var mid = ResultGrade.From(Report(Scorer.ResultSuccess, 80, 10, false), 100, 0);
            Assert.Equal((70, "C", true), (mid.Score100, mid.Letter, mid.Passed));
            var low = ResultGrade.From(Report(Scorer.ResultSuccess, 50, 0, false), 100, 0);
            Assert.Equal((50, "F", false), (low.Score100, low.Letter, low.Passed));
            var critical = ResultGrade.From(Report(Scorer.ResultFail, 100, 0, true), 100, 0);
            Assert.Equal((100, "A", false, true), (critical.Score100, critical.Letter, critical.Passed, critical.Critical));
            Assert.Equal(0, ResultGrade.From(Report(Scorer.ResultSuccess, 0, 40, false), 100, 3).Score100);
            Assert.Equal(0, ResultGrade.From(Report(Scorer.ResultSuccess, 10, 0, false), 0, 0).Score100);
        }

        [Fact]
        public void The_grade_table_is_read_from_content_scoring_grades_json()
        {
            var t = GradeTable.Parse(File.ReadAllText(Path.Combine(TestCase.ContentRoot, "scoring", "grades.json")));
            Assert.Equal(GradeTable.Default.PassMark, t.PassMark);
            Assert.Equal(GradeTable.Default.HintPenalty, t.HintPenalty);
            Assert.Equal(new[] { "A", "B", "C", "D" }, t.Bands.Select(b => b.Key).ToArray());
            Assert.Equal(new[] { "A", "B", "C", "D", "F" }, new[] { 95, 85, 75, 65, 10 }.Select(t.Letter).ToArray());
        }
    }
}
