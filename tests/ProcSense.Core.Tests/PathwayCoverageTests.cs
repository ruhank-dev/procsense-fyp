using System.IO;
using System.Linq;
using ProcSense.Core.Actions;
using ProcSense.Core.Content;
using ProcSense.Core.Flow;
using ProcSense.Core.Scoring;
using ProcSense.Core.Workflow;

namespace ProcSense.Core.Tests
{
    /// <summary>
    /// Both surgical pathways end to end in the engine (P3 reconciliation, 14 Nov 2026 UTC): every playable catalogue case of cholecystectomy and appendicectomy starts through the same
    /// <see cref="SessionStarter"/> the menu uses, in training and in the examination; training publishes goals and their "not needed" reasons (P3-TRN-02) and a free hint, the examination
    /// publishes neither and charges the hint; submitting gives a result summary and a grade.
    /// </summary>
    public class PathwayCoverageTests
    {
        private static readonly CaseCatalogue Catalogue = CaseCatalogue.Parse(File.ReadAllText(Path.Combine(TestCase.ContentRoot, "ui", "case_catalogue.json")));

        [Theory]
        [InlineData("cholecystectomy", SessionMode.Practice)]
        [InlineData("cholecystectomy", SessionMode.Assessment)]
        [InlineData("appendicectomy", SessionMode.Practice)]
        [InlineData("appendicectomy", SessionMode.Assessment)]
        public void Every_playable_case_of_the_pathway_starts_plays_and_gives_a_result(string procedure, SessionMode mode)
        {
            var reader = new FileContentReader(TestCase.ContentRoot);
            var ids = Catalogue.Entries.Where(e => e.Procedure == procedure && ContentGate.Allows(e.Status, mode)).Select(e => e.Id).ToList();
            Assert.NotEmpty(ids);

            foreach (var id in ids)
            {
                var r = SessionStarter.Start(Catalogue, new SessionRequest(mode, procedure, TrainingLevel.Beginner, Seed: 7, CaseId: id), reader, null, () => 7, checkManifest: false);
                Assert.True(r.Refusal == null, id + ": " + r.Refusal);
                var content = r.Load!.Package;
                var s = new Session(content.Cases[id], r.Plan!.Seed, mode, Subsystems.Default(content), r.Plan.Options);

                if (mode == SessionMode.Practice)
                {
                    Assert.True(s.Observable.Goals.Count > 0, id + ": no goals in training");
                    Assert.All(s.Observable.Inactive!, x => Assert.False(string.IsNullOrEmpty(x.ReasonId), id));
                }
                else
                {
                    Assert.Empty(s.Observable.Goals);
                    Assert.Empty(s.Observable.Inactive!);
                    Assert.Empty(s.Observable.Unavailable!);
                }

                s.Apply(new SimAction(ExamControl.RequestHint));
                var submitted = s.Apply(new SimAction(ExamControl.SubmitPatient));
                Assert.True(submitted.Accepted, id + ": submit refused " + submitted.RejectReason);

                var sum = ResultSummary.From(s, content, 100);
                Assert.True(sum.Submitted, id);
                Assert.Equal(mode == SessionMode.Assessment ? sum.HintsUsed : 0, sum.HintsCharged);
                Assert.NotNull(sum.Grade);
                Assert.False(sum.Grade!.Passed, id + ": an immediate submission must never pass");
            }
        }
    }
}
