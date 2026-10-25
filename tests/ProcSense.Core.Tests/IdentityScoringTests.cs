using System.Collections.Generic;
using System.Linq;
using ProcSense.Core.Actions;
using ProcSense.Core.Scoring;
using ProcSense.Core.Workflow;

namespace ProcSense.Core.Tests
{
    /// <summary>DT-003 and DT-004: what the identity step scores. The engine already refused these actions; now the attempt and the skipped habits are score items.</summary>
    public class IdentityScoringTests
    {
        private static Session New() => new Session(TestCase.Card(), 42, SessionMode.Practice, Subsystems.Default(TestCase.Package()));
        private static ActionOutcome Do(Session s, string verb) => s.Apply(new SimAction(verb));
        private static IReadOnlyList<ScoreItem> Items(Session s) => Scorer.Collect(s, TestCase.Package());

        [Fact]
        public void A_test_ordered_before_identity_is_refused_and_is_one_critical_item()
        {
            var s = New();
            Do(s, "introduce_self");
            var tried = s.Apply(new SimAction("order_investigation", parameters: new Dictionary<string, string> { ["test"] = "fbc" }));
            s.Apply(new SimAction("order_investigation", parameters: new Dictionary<string, string> { ["test"] = "crp" }));
            Assert.False(tried.Accepted);
            var critical = Items(s).Where(i => i.Id == "critical.action_before_identity").ToList();
            Assert.Single(critical);
            Assert.Equal("critical", critical[0].Status);
            Assert.Equal("b13", critical[0].Category);
        }

        [Fact]
        public void No_attempt_no_critical_item_and_a_confirmed_identity_scores_nothing_here()
        {
            var s = New();
            foreach (var v in new[] { "introduce_self", "ask_identity", "read_wristband", "open_notes", "confirm_identity" }) Do(s, v);
            Assert.DoesNotContain(Items(s), i => i.Id.EndsWith("action_before_identity") || i.Id == "minor.no_introduction" || i.Id == "minor.identity_asked_closed");
        }

        [Fact]
        public void Starting_with_the_identity_question_and_no_introduction_is_one_minor_item()
        {
            var s = New();
            Do(s, "ask_identity");
            Do(s, "introduce_self");
            var item = Assert.Single(Items(s), i => i.Id == "minor.no_introduction");
            Assert.Equal(("b06", -1), (item.Category, item.Points));
        }

        [Fact]
        public void A_closed_question_and_an_unneeded_mismatch_report_are_minor_items()
        {
            var s = New();
            Do(s, "introduce_self");
            Do(s, "read_name_to_patient");
            Do(s, "read_wristband"); Do(s, "ask_identity"); Do(s, "open_notes");
            Do(s, "report_mismatch");
            var ids = Items(s).Select(i => i.Id).ToList();
            Assert.Contains("minor.identity_asked_closed", ids);
            Assert.Contains("minor.unnecessary_mismatch_report", ids);
        }
    }
}
