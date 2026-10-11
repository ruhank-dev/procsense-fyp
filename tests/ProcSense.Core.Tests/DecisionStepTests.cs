using System.Collections.Generic;
using System.Linq;
using ProcSense.Core.Actions;
using ProcSense.Core.Clinical;
using ProcSense.Core.Content;
using ProcSense.Core.Physiology;
using ProcSense.Core.Pipeline;
using ProcSense.Core.Workflow;
using ProcSense.Core.Workflow.Steps;

namespace ProcSense.Core.Tests
{
    /// <summary>Workflow Step 8: deciding, escalating, postponing and redirecting (p0-simulation-workflow-s10, plan tests T08-1 to T08-4).</summary>
    public class DecisionStepTests
    {
        private static readonly ContentPackage Content = TestCase.Package();

        private static CaseFile File_(string id) => ContentLoader.LoadCaseFile(new FileContentReader(TestCase.ContentRoot), id);

        private static SimAction Act(string verb, params (string k, string v)[] p) => new SimAction(verb, "patient", Channel.Cli, p.ToDictionary(x => x.k, x => x.v));

        private static ActionOutcome Do(Session s, string verb, params (string k, string v)[] p) => s.Apply(Act(verb, p));

        private static string Reject(ActionOutcome o) { Assert.False(o.Accepted); return o.RejectReason; }

        private static Session Start(string caseId, out ProblemsSubsystem problems, SessionMode mode = SessionMode.Practice, string? scenario = null)
        {
            var file = File_(caseId);
            problems = new ProblemsSubsystem(Content, file);
            var chain = Subsystems.Default(Content).Where(x => !(x is DecisionSubsystem) && !(x is ProblemsSubsystem) && !(x is DeteriorationSubsystem)).Concat(new ISubsystem[] { new DecisionSubsystem(Content, file), problems, new DeteriorationSubsystem(Content, file) });
            var card = Content.Cases["VAR-S1-A"] with { Scenario = scenario ?? file.Scenario };
            var s = new Session(card, 42, mode, chain);
            foreach (var verb in new[] { "ask_identity", "read_wristband", "open_notes", "confirm_identity" }) Assert.True(Do(s, verb).Accepted, verb);
            Do(s, "ask", ("topic", "pregnancy"));
            return s;
        }

        private static Session Start(string caseId = "VAR-S1-A", SessionMode mode = SessionMode.Practice) => Start(caseId, out _, mode);

        // Orders the two tests the case needs and opens the results, so the engine has the information it asked for.
        private static void OpenNeededResults(Session s)
        {
            Do(s, "order_investigation", ("test", "lft")); Do(s, "take_sample", ("test", "lft"), ("tube", "serum_gold"));
            Do(s, "order_investigation", ("test", "pregnancy")); Do(s, "perform_bedside_test", ("test", "pregnancy"));
            s.Tick(3600);
            Do(s, "view_result", ("test", "lft")); Do(s, "view_result", ("test", "pregnancy"));
        }

        // ---------------------------------------------------------------- declare_outcome

        [Fact]
        public void A_declaration_needs_a_known_outcome()
        {
            var s = Start();
            Assert.Equal(DecisionSubsystem.MissingOutcome, Reject(Do(s, "declare_outcome")));
            Assert.Equal(DecisionSubsystem.UnknownOutcome, Reject(Do(s, "declare_outcome", ("outcome", "FINE"))));
        }

        [Fact]
        public void The_right_declaration_is_credited_and_the_wrong_one_is_a_major_item()
        {
            var s = Start();
            OpenNeededResults(s);
            var ok = Do(s, "declare_outcome", ("outcome", "safe"), ("reason", "everything fits"));
            Assert.Contains("decision:correct", ok.Effects);
            var bad = Do(s, "declare_outcome", ("outcome", "OPTIMISE"));
            Assert.Contains("decision:wrong", bad.Effects);
            Assert.Contains(s.Decision!.History, h => h.Kind == "declared_correct" && h.ScoreItems.Contains("credit.decision_correct"));
            Assert.Contains(s.Decision.History, h => h.Kind == "declared_wrong" && h.ScoreItems.Contains("major.decision_wrong"));
            Assert.Equal(new[] { true, false }, s.Decision.Declared.Select(d => d.Correct));
        }

        [Fact]
        public void Not_declaring_an_emergency_that_is_open_is_a_critical_item()
        {
            var s = Start("STK-S1-A", out var problems);
            problems.Detect("stka.hypo");
            var o = Do(s, "declare_outcome", ("outcome", "SAFE"));
            Assert.Contains("decision:wrong", o.Effects);
            Assert.Contains(s.Decision!.History, h => h.Kind == "declared_wrong" && h.ScoreItems.Contains("critical.emergency_not_declared"));
            Assert.Contains("decision:correct", Do(s, "declare_outcome", ("outcome", "EMERGENCY")).Effects);
        }

        [Fact]
        public void Practice_shows_the_expected_outcome_and_assessment_never_does()
        {
            Assert.Contains("expected:INVESTIGATE", Do(Start(mode: SessionMode.Practice), "declare_outcome", ("outcome", "SAFE")).Effects);
            Assert.DoesNotContain(Do(Start(mode: SessionMode.Assessment), "declare_outcome", ("outcome", "SAFE")).Effects, e => e.StartsWith("expected:"));
        }

        [Fact]
        public void The_question_is_asked_again_when_information_changes()
        {
            var s = Start();
            Assert.Contains("decision:wrong", Do(s, "declare_outcome", ("outcome", "SAFE")).Effects);       // results still unopened
            OpenNeededResults(s);
            Assert.Contains("decision:correct", Do(s, "declare_outcome", ("outcome", "SAFE")).Effects);
        }

        [Fact]
        public void Identity_must_be_confirmed_before_a_declaration()
        {
            var s = new Session(Content.Cases["VAR-S1-A"], 42, SessionMode.Practice, Subsystems.Default(Content));
            Assert.Equal("IDENTITY_NOT_CONFIRMED", Reject(Do(s, "declare_outcome", ("outcome", "SAFE"))));
        }

        // ---------------------------------------------------------------- escalate

        [Fact]
        public void A_call_needs_a_known_role()
        {
            var s = Start();
            Assert.Equal(DecisionSubsystem.MissingRole, Reject(Do(s, "escalate")));
            Assert.Equal(DecisionSubsystem.UnknownRole, Reject(Do(s, "escalate", ("role", "porter"))));
        }

        [Fact]
        public void A_complete_SBAR_to_the_right_role_is_credited_and_the_senior_responds()
        {
            var s = Start("VAR-S1-PB", out var problems);
            OpenNeededResults(s);
            problems.Detect(File_("VAR-S1-PB").Problems.First(p => p.Type == "pathway_changing").Id);
            var o = Do(s, "escalate", ("role", "senior"), ("s", "Mr X, jaundiced"), ("b", "gallstones"), ("a", "probable duct stone"), ("r", "ERCP before surgery"));
            Assert.Contains("responded:senior", o.Effects);
            Assert.DoesNotContain(o.Effects, e => e.StartsWith("sbar_missing"));
            var h = s.Decision!.History.Single(x => x.Kind == "escalated:senior");
            Assert.Contains("credit.sbar_complete", h.ScoreItems);
            Assert.Contains("credit.right_role", h.ScoreItems);
        }

        [Fact]
        public void An_SBAR_with_a_part_missing_is_a_minor_item_that_names_the_part()
        {
            var s = Start();
            var o = Do(s, "escalate", ("role", "anaesthetist"), ("s", "Mr X"), ("b", "history"), ("a", "  "));
            Assert.Contains("sbar_missing:a,r", o.Effects);
            Assert.Contains("minor.sbar_incomplete", s.Decision!.History.Single(x => x.Kind == "escalated:anaesthetist").ScoreItems);
            Assert.Equal(new[] { "a", "r" }, s.Decision.Escalations.Single().Missing);
        }

        [Fact]
        public void Calling_a_role_the_decision_did_not_call_for_is_noted_without_a_penalty()
        {
            var s = Start();
            OpenNeededResults(s);                                                                  // safe: nobody needs calling
            var o = Do(s, "escalate", ("role", "senior"), ("s", "a"), ("b", "b"), ("a", "c"), ("r", "d"));
            Assert.Contains("role_not_needed", o.Effects);
            Assert.DoesNotContain("credit.right_role", s.Decision!.History.Single(x => x.Kind == "escalated:senior").ScoreItems);
        }

        // ---------------------------------------------------------------- postpone and redirect

        [Fact]
        public void A_postponement_needs_a_reason_and_a_redirect_needs_a_service()
        {
            var s = Start();
            Assert.Equal(DecisionSubsystem.MissingReason, Reject(Do(s, "postpone")));
            Assert.Equal(DecisionSubsystem.MissingService, Reject(Do(s, "redirect", ("service", " "))));
        }

        // The plan's fixture: variant B2 (readings 188/114 and 186/112): declare OPTIMISE, postpone, and the end state is a success
        [Fact]
        public void VAR_S1_B2_declare_OPTIMISE_and_postpone_ends_POSTPONED_APPROPRIATELY()
        {
            var s = Start("VAR-S1-B", out var problems);
            s.Patient.Clinical.SetFlag("variant.B2");
            OpenNeededResults(s);
            problems.Detect(File_("VAR-S1-B").Problems.Single().Id);
            Assert.Contains("decision:correct", Do(s, "declare_outcome", ("outcome", "OPTIMISE"), ("reason", "BP 188/114 on repeat")).Effects);
            var o = Do(s, "postpone", ("reason", "BP above 180/110: refer for control"));
            Assert.Contains("end_state:POSTPONED_APPROPRIATELY", o.Effects);
            Assert.Equal("POSTPONED_APPROPRIATELY", s.Decision!.EndState);
            Assert.Contains(s.Decision.History, h => h.ScoreItems.Contains("credit.postponed_appropriately"));
        }

        [Fact]
        public void Postponing_a_case_that_should_go_ahead_is_a_major_item_and_ends_safe_but_delayed()
        {
            var s = Start("VAR-S1-B");                                                              // without the B2 variant this patient is ready
            var o = Do(s, "postpone", ("reason", "just in case"));
            Assert.Contains("end_state:SAFE_BUT_DELAYED", o.Effects);
            Assert.Contains(s.Decision!.History, h => h.ScoreItems.Contains("major.postponed_unnecessarily"));
        }

        [Fact]
        public void Postponing_while_an_emergency_is_open_is_a_critical_safety_failure()
        {
            var s = Start("STK-S1-A", out var problems);
            problems.Detect("stka.hypo");
            var o = Do(s, "postpone", ("reason", "come back tomorrow"));
            Assert.Contains("end_state:SAFETY_FAILURE", o.Effects);
            Assert.Contains(s.Decision!.History, h => h.ScoreItems.Contains("critical.postponed_in_emergency"));
        }

        [Fact]
        public void Redirecting_the_ectopic_case_ends_REDIRECTED_APPROPRIATELY()
        {
            var s = Start("VAR-S2-B", out var problems);
            problems.Detect(File_("VAR-S2-B").Problems.First(p => p.Type == "life_threat").Id);
            var o = Do(s, "redirect", ("service", "gynaecology"));
            Assert.Contains("end_state:REDIRECTED_APPROPRIATELY", o.Effects);
            Assert.Contains(s.Decision!.History, h => h.ScoreItems.Contains("credit.redirected_appropriately"));
        }

        [Fact]
        public void Redirecting_a_case_that_should_not_be_redirected_ends_safe_but_delayed()
        {
            var s = Start("VAR-S1-B");
            Assert.Contains("end_state:SAFE_BUT_DELAYED", Do(s, "redirect", ("service", "medicine")).Effects);
        }

        [Fact]
        public void The_case_can_only_end_once()
        {
            var s = Start("VAR-S1-B");
            Assert.True(Do(s, "postpone", ("reason", "x")).Accepted);
            Assert.Equal(DecisionSubsystem.AlreadyEnded, Reject(Do(s, "redirect", ("service", "y"))));
            Assert.Equal(DecisionSubsystem.AlreadyEnded, Reject(Do(s, "postpone", ("reason", "z"))));
        }

        [Fact]
        public void An_end_action_raises_the_end_state_event()
        {
            var s = Start("VAR-S1-B");
            var events = new List<WorkflowEvent>();
            s.Events.Subscribe<WorkflowEvent>(events.Add);
            Do(s, "postpone", ("reason", "x"));
            Assert.Contains(events, e => e.Name == DecisionSubsystem.EndStateEvent);
        }

        // ---------------------------------------------------------------- treatments stay with the nurse (T08-1, T08-2, T08-4 and E8-B)

        [Fact]
        public void The_trainee_has_no_verb_to_give_a_treatment()
        {
            var s = Start();
            foreach (var verb in new[] { "administer", "give", "give_medication", "prescribe" }) Assert.Equal("UNKNOWN_VERB", Reject(Do(s, verb, ("class", "TR-PARACETAMOL"))));
        }

        [Fact]
        public void T08_4_an_IV_treatment_without_a_line_is_refused()
        {
            var s = Start();
            Assert.Equal("IV_ACCESS_REQUIRED", Reject(Do(s, "request_fluids", ("type", "TR-IV-FLUIDS"))));
        }

        [Fact]
        public void E8_B_a_dehydrated_emergency_patient_gets_worse_until_fluids_are_requested_and_given()
        {
            var s = Start("SC-17");
            double hr0 = s.Patient.Physiological.Vitals.HeartRate;
            s.Tick(30 * 60);
            Assert.True(s.Patient.Physiological.Vitals.HeartRate > hr0);                          // heart rate rising: the deterioration clock
            Assert.False(s.Patient.Clinical.HasFlag(DeteriorationSubsystem.TreatedFlag));
        }
    }
}
