using System.Collections.Generic;
using System.IO;
using System.Linq;
using ProcSense.Core.Actions;
using ProcSense.Core.Content;
using ProcSense.Core.Observation;
using ProcSense.Core.Pipeline;
using ProcSense.Core.Workflow;

namespace ProcSense.Core.Tests
{
    /// <summary>P3-TRN-02 (DT-298): goals and checklist lines that are not needed, and actions a blocker stops, worked out by the engine from its own state, practice mode only.</summary>
    public class AvailabilityTests
    {
        private static CaseGraph Fixture() =>
            CaseGraph.Parse(File.ReadAllText(Path.Combine(TestCase.Repo, "tests", "fixtures", "workflow", "test_workflow.json")), "test_workflow");

        private static Session Real(SessionMode mode = SessionMode.Practice)
        {
            var content = TestCase.Package();
            return new Session(content.Cases["VAR-S1-A"], 42, mode, Subsystems.Default(content));
        }

        private static Session WithFixture(SessionMode mode = SessionMode.Practice)
        {
            var content = TestCase.Package();
            var chain = Subsystems.Default(content).Where(s => !(s is ReadinessSubsystem) && !(s is CaseGraphSubsystem))
                .Concat(new ISubsystem[] { new ReadinessSubsystem(Fixture()), new CaseGraphSubsystem(Fixture()) });
            return new Session(content.Cases["VAR-S1-A"], 42, mode, chain);
        }

        private static SimAction Act(string verb) => new SimAction(verb, "patient", Channel.Cli, new Dictionary<string, string>());

        [Fact]
        public void A_non_diabetic_case_lists_the_glucose_step_as_not_needed_with_its_reason_and_step()
        {
            var s = Real();

            var cbg = s.Observable.Inactive!.Single(i => i.Id == "ward.cbg");

            Assert.Equal("ward_item", cbg.Kind);
            Assert.Equal("NOT_DIABETIC", cbg.ReasonId);
            Assert.Equal(Availability.WardStep, cbg.Step);
        }

        [Fact]
        public void The_glucose_step_is_needed_again_once_the_engine_knows_the_patient_is_diabetic()
        {
            var s = Real();
            s.Patient.Clinical.SetFlag("condition.diabetes");

            Assert.DoesNotContain(s.Observable.Inactive!, i => i.Id == "ward.cbg");
        }

        [Fact]
        public void An_optional_line_without_a_named_reason_is_listed_as_not_required()
        {
            var hair = Real().Observable.Inactive!.Single(i => i.Id == "ward.hair");
            Assert.Equal(Availability.NotRequired, hair.ReasonId);
        }

        [Fact]
        public void A_required_line_and_an_applying_goal_are_never_listed_as_not_needed()
        {
            var inactive = Real().Observable.Inactive!.Select(i => i.Id).ToList();
            Assert.DoesNotContain("ward.consent", inactive);
            Assert.DoesNotContain("G-S1-IDENTITY", inactive);
            Assert.DoesNotContain("G-SENIOR-NOW", inactive);   // applies but not yet triggered: hidden, not "not needed" (it would give the emergency away)
        }

        [Fact]
        public void A_goal_whose_rule_excludes_the_case_is_listed_with_its_step_and_never_among_the_goals()
        {
            var s = WithFixture();

            var g = s.Observable.Inactive!.Single(i => i.Id == "TEST-G-NOT-HERE");

            Assert.Equal("goal", g.Kind);
            Assert.Equal(Availability.NotApplicable, g.ReasonId);
            Assert.Equal(2, g.Step);
            Assert.DoesNotContain(s.Observable.Goals, x => x.Id == "TEST-G-NOT-HERE");
        }

        [Fact]
        public void A_pending_goal_names_the_goal_it_waits_for_until_that_one_is_done()
        {
            var s = WithFixture();
            Assert.Equal(new[] { "TEST-G-DEADLINE" }, s.Observable.Goals.Single(g => g.Id == "TEST-G-LATER").Waiting);

            foreach (var v in new[] { "ask_identity", "read_wristband", "open_notes", "confirm_identity", "ask_allergies" }) s.Apply(Act(v));

            Assert.Empty(s.Observable.Goals.Single(g => g.Id == "TEST-G-LATER").Waiting);
        }

        [Fact]
        public void A_blocked_action_is_listed_with_its_blocker_and_message_until_the_condition_clears()
        {
            var s = WithFixture();

            var blocked = s.Observable.Unavailable!.Single(u => u.BlockerId == "TEST-B-IDENTITY-FIRST");
            Assert.Contains("ask_allergies", blocked.Verbs);
            Assert.Contains("confirm the patient's identity", blocked.Message);

            foreach (var v in new[] { "ask_identity", "read_wristband", "open_notes", "confirm_identity" }) s.Apply(Act(v));

            Assert.DoesNotContain(s.Observable.Unavailable!, u => u.BlockerId == "TEST-B-IDENTITY-FIRST");
        }

        [Fact]
        public void The_examination_publishes_no_reasons_at_all()
        {
            var s = Real(SessionMode.Assessment);
            Assert.Empty(s.Observable.Inactive!);
            Assert.Empty(s.Observable.Unavailable!);
            Assert.Empty(WithFixture(SessionMode.Assessment).Observable.Inactive!);
        }

        [Fact]
        public void Reasons_do_not_change_the_observable_json_or_its_hash()
        {
            var s = Real();
            Assert.DoesNotContain("inactive", s.Observable.ToJson());
            Assert.DoesNotContain("NOT_DIABETIC", s.Observable.ToJson());
        }

        [Fact]
        public void Every_reason_id_has_its_wording_in_the_training_guide_and_the_other_way_round()
        {
            using var doc = System.Text.Json.JsonDocument.Parse(File.ReadAllText(Path.Combine(TestCase.ContentRoot, "ui", "training_guide.json")));
            var guide = doc.RootElement.GetProperty("inactive_reasons").EnumerateArray()
                .ToDictionary(e => e.GetProperty("id").GetString()!, e => e.GetProperty("reason_id").GetString()!);

            Assert.Equal(guide.OrderBy(kv => kv.Key), Availability.Reasons.OrderBy(kv => kv.Key));
        }

        [Fact]
        public void Every_conditional_goal_in_the_workflow_has_a_named_reason()
        {
            var graph = CaseGraph.Parse(File.ReadAllText(Path.Combine(TestCase.ContentRoot, "workflow", "workflow.json")), "workflow");
            using var doc = System.Text.Json.JsonDocument.Parse(File.ReadAllText(Path.Combine(TestCase.ContentRoot, "workflow", "workflow.json")));
            foreach (var g in doc.RootElement.GetProperty("goals").EnumerateArray())
                if (g.GetProperty("applies_if").ValueKind != System.Text.Json.JsonValueKind.True)
                    Assert.True(Availability.Reasons.ContainsKey(g.GetProperty("id").GetString()!), g.GetProperty("id").GetString());
            Assert.NotEmpty(graph.Goals);
        }
    }
}
