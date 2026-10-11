using System.Collections.Generic;
using System.Linq;
using ProcSense.Core.Actions;
using ProcSense.Core.Content;
using ProcSense.Core.Pipeline;
using ProcSense.Core.Workflow;
using ProcSense.Core.Workflow.Steps;

namespace ProcSense.Core.Tests
{
    /// <summary>Workflow Steps 6 and 7, choosing tests and handling results (p0-simulation-workflow-s09): plan tests T06-1, T06-2, T06-5, T07-1, T07-2, T07-WT1 and T07-WT2.</summary>
    public class TestsStepTests
    {
        private static readonly ContentPackage Content = TestCase.Package();

        private static SimAction Act(string verb, params (string k, string v)[] p) => new SimAction(verb, "patient", Channel.Cli, p.ToDictionary(x => x.k, x => x.v));

        private static ActionOutcome Do(Session s, string verb, params (string k, string v)[] p) => s.Apply(Act(verb, p));

        private static string Reject(ActionOutcome o) { Assert.False(o.Accepted); return o.RejectReason; }

        private static readonly (string, string)[] Trolley = { ("mode", "trolley"), ("escort", "porter,nurse"), ("accompany", "true") };

        // VAR-S1-A is a woman of 19 to 55, so the pregnancy gate applies to her. `askPregnancy` opens it.
        private static Session NewSession(bool askPregnancy = false, string[]? identity = null)
        {
            var s = new Session(Content.Cases["VAR-S1-A"], 42, SessionMode.Practice, Subsystems.Default(Content));
            foreach (var verb in identity ?? new[] { "ask_identity", "read_wristband", "open_notes", "confirm_identity" }) Assert.True(Do(s, verb).Accepted, verb);
            if (askPregnancy) Assert.True(Do(s, "ask", ("topic", "pregnancy")).Accepted);
            return s;
        }

        private static string StateOf(Session s, string goal) => s.Observable.Goals.Single(g => g.Id == goal).State;

        private static void Minutes(Session s, double minutes) => s.Tick(minutes * 60);

        // ---------------------------------------------------------------- the pregnancy gate (E6-D, L6-05)

        [Fact]
        public void T06_1_Imaging_with_radiation_is_refused_until_the_pregnancy_status_of_an_eligible_patient_is_known()
        {
            var s = NewSession();
            Assert.Equal(InvestigationsSubsystem.PregnancyStatusRequired, Reject(Do(s, "order_investigation", new[] { ("test", "ct") }.Concat(Trolley).ToArray())));
            Assert.Equal(InvestigationsSubsystem.PregnancyStatusRequired, Reject(Do(s, "order_investigation", new[] { ("test", "chest_xray") }.Concat(Trolley).ToArray())));
            Assert.True(s.Patient.Clinical.HasFlag(InvestigationsSubsystem.PregnancyFlag));
        }

        [Fact]
        public void T06_2_Scans_without_radiation_and_blood_tests_are_never_blocked_by_the_gate()
        {
            var s = NewSession();
            Assert.True(Do(s, "order_investigation", new[] { ("test", "ultrasound") }.Concat(Trolley).ToArray()).Accepted);
            Assert.True(Do(s, "order_investigation", ("test", "lft")).Accepted);
        }

        [Fact]
        public void Asking_the_pregnancy_question_opens_the_gate_and_clears_the_blocker()
        {
            var s = NewSession();
            Assert.Equal("BLOCKED:B-PREGNANCY", Reject(Do(s, "transfer_to_theatre", Trolley)));
            Assert.True(Do(s, "ask", ("topic", "pregnancy")).Accepted);
            Assert.False(s.Patient.Clinical.HasFlag(InvestigationsSubsystem.PregnancyFlag));
            Assert.True(Do(s, "order_investigation", new[] { ("test", "chest_xray") }.Concat(Trolley).ToArray()).Accepted);
            Assert.NotEqual("BLOCKED:B-PREGNANCY", Do(s, "transfer_to_theatre", Trolley).RejectReason);
        }

        [Fact]
        public void A_resulted_pregnancy_test_also_opens_the_gate()
        {
            var s = NewSession();
            Do(s, "order_investigation", ("test", "pregnancy")); Do(s, "perform_bedside_test", ("test", "pregnancy"));
            Assert.True(s.Patient.Clinical.HasFlag(InvestigationsSubsystem.PregnancyFlag));      // ordered is not known
            Minutes(s, 60);
            Do(s, "order_investigation", ("test", "lft"));                                          // any accepted action refreshes the flag
            Assert.False(s.Patient.Clinical.HasFlag(InvestigationsSubsystem.PregnancyFlag));
        }

        // ---------------------------------------------------------------- credit and goals (T06-5, T07-1, T07-2)

        [Fact]
        public void T06_5_An_appropriate_order_is_credited_and_an_unnecessary_one_is_not()
        {
            var s = NewSession(askPregnancy: true);
            Do(s, "order_investigation", ("test", "lft"));
            Assert.Contains(s.InvestigationHistory, h => h.ScoreItems.Contains("credit.test_appropriate"));
            int credits = s.InvestigationHistory.Count(h => h.ScoreItems.Contains("credit.test_appropriate"));
            Do(s, "order_investigation", ("test", "hba1c"));
            Assert.Equal(credits, s.InvestigationHistory.Count(h => h.ScoreItems.Contains("credit.test_appropriate")));
            Assert.Contains(s.InvestigationHistory, h => h.ScoreItems.Contains("unnecessary.test_ordered"));
        }

        [Fact]
        public void T07_1_The_tests_goal_completes_when_every_needed_test_is_ordered()
        {
            var s = NewSession(askPregnancy: true);
            Assert.Equal("pending", StateOf(s, "G-TESTS"));
            Do(s, "order_investigation", ("test", "lft"));
            Assert.NotEqual("complete", StateOf(s, "G-TESTS"));
            Do(s, "order_investigation", ("test", "pregnancy"));
            Assert.Equal("complete", StateOf(s, "G-TESTS"));
        }

        [Fact]
        public void T07_2_The_results_goal_completes_only_when_every_needed_result_is_opened()
        {
            var s = NewSession(askPregnancy: true);
            Do(s, "order_investigation", ("test", "lft")); Do(s, "take_sample", ("test", "lft"), ("tube", "serum_gold"));
            Do(s, "order_investigation", ("test", "pregnancy")); Do(s, "perform_bedside_test", ("test", "pregnancy"));
            Minutes(s, 60);
            Assert.NotEqual("complete", StateOf(s, "G-RESULTS"));
            Do(s, "view_result", ("test", "lft"));
            Assert.NotEqual("complete", StateOf(s, "G-RESULTS"));
            Do(s, "view_result", ("test", "pregnancy"));
            Assert.Equal("complete", StateOf(s, "G-RESULTS"));
        }

        // ---------------------------------------------------------------- the plan's named runs

        [Fact]
        public void T06_1_The_walkthroughs_right_set_gets_credit_and_no_unnecessary_item()
        {
            var s = NewSession(askPregnancy: true);                                              // VAR-S1-A: liver tests and a urine pregnancy test, nothing else
            Do(s, "order_investigation", ("test", "lft")); Do(s, "order_investigation", ("test", "pregnancy"));
            Assert.Equal(2, s.InvestigationHistory.Count(h => h.ScoreItems.Contains("credit.test_appropriate")));
            Assert.DoesNotContain(s.InvestigationHistory, h => h.ScoreItems.Contains("unnecessary.test_ordered"));
        }

        [Fact]
        public void T06_2_Ordering_everything_gives_one_minor_item_per_unnecessary_test()
        {
            var s = NewSession(askPregnancy: true);
            foreach (var t in new[] { "hba1c", "crp", "clotting", "urine_dipstick" }) Do(s, "order_investigation", ("test", t));
            Assert.Equal(4, s.InvestigationHistory.Count(h => h.Kind == "test_not_indicated"));
        }

        [Fact]
        public void T06_5_A_tube_not_labelled_at_the_bedside_is_rejected_after_a_delay_and_the_test_can_be_re_ordered()
        {
            var s = NewSession(askPregnancy: true);
            var events = new List<WorkflowEvent>();
            s.Events.Subscribe<WorkflowEvent>(events.Add);
            Do(s, "order_investigation", ("test", "lft")); Do(s, "take_sample", ("test", "lft"), ("label", "unlabelled"));
            Assert.Equal("PROCESSING", s.Patient.Clinical.Investigations.Single().State);
            Minutes(s, 60);
            Assert.Equal("REJECTED", s.Patient.Clinical.Investigations.Single().State);
            Assert.Contains(events, e => e.Name == "E-SAMPLE-REJECTED");
            Assert.True(Do(s, "order_investigation", ("test", "lft")).Accepted);
        }

        [Fact]
        public void T07_2_A_needed_result_left_unopened_blocks_the_transfer_to_theatre()
        {
            var s = NewSession(askPregnancy: true);
            Do(s, "order_investigation", ("test", "lft")); Do(s, "take_sample", ("test", "lft"), ("tube", "serum_gold"));
            Minutes(s, 60);
            Assert.Equal("BLOCKED:B-RESULTS", Reject(Do(s, "transfer_to_theatre", Trolley)));
            Do(s, "view_result", ("test", "lft"));
            Assert.NotEqual("BLOCKED:B-RESULTS", Do(s, "transfer_to_theatre", Trolley).RejectReason);
        }

        // T07-WT1 (repeat of a haemolysed potassium) and T07-WT2 (no read-back on a critical call) are covered where the lab phones: CriticalCallTests and ResultGeneratorTests.
    }
}
