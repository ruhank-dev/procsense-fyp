using System.Collections.Generic;
using System.Linq;
using ProcSense.Core.Actions;
using ProcSense.Core.Clinical;
using ProcSense.Core.Content;
using ProcSense.Core.Pipeline;
using ProcSense.Core.Workflow;
using ProcSense.Core.Workflow.Steps;

namespace ProcSense.Core.Tests
{
    /// <summary>The five outcomes and the master decision (p0-clinical-logic-s13, plan tests T-CG7 and T-CG8).</summary>
    public class DecisionEngineTests
    {
        private static readonly ContentPackage Content = TestCase.Package();
        private static readonly DecisionEngine Engine = new DecisionEngine(Content.Decision);

        private static CaseFile File_(string id) => ContentLoader.LoadCaseFile(new FileContentReader(TestCase.ContentRoot), id);

        private static SimAction Act(string verb, params (string k, string v)[] p) => new SimAction(verb, "patient", Channel.Cli, p.ToDictionary(x => x.k, x => x.v));

        private static ActionOutcome Do(Session s, string verb, params (string k, string v)[] p) => s.Apply(Act(verb, p));

        // A patient with nothing wrong, then one thing changed at a time.
        private static DecisionInput Calm(string scenario = "S1", IReadOnlyList<Problem>? open = null, int? news2 = 0, double sbp = 124, double spo2 = 98, string acvpu = "A",
            string[]? flags = null, bool pregnancyPositive = false, bool missing = false, bool fastingBreach = false, int asa = 1) =>
            new DecisionInput(scenario, open ?? new List<Problem>(), news2, sbp, spo2, acvpu, flags ?? new string[0], pregnancyPositive, missing, fastingBreach, asa);

        private static ProblemRegistry Reg(string scenario = "S1") => new ProblemRegistry(Content.Problems, scenario);

        // ---------------------------------------------------------------- the content

        [Fact]
        public void The_content_has_five_outcomes_and_six_ordered_checks()
        {
            Assert.Equal(new[] { "SAFE", "INVESTIGATE", "OPTIMISE", "REVIEW", "EMERGENCY" }, Content.Decision.Outcomes.Select(o => o.Code));
            Assert.Equal(new[] { "emergency", "information_missing", "diagnosis_changed", "fixable_problem", "affects_anaesthesia", "safe" }, Content.Decision.Checks.Select(c => c.Id));
            Assert.Equal((7, 90, 91, true), (Content.Decision.Signs.News2Min, Content.Decision.Signs.SbpMax, Content.Decision.Signs.Spo2Max, Content.Decision.Signs.NotAlert));
            Assert.Equal("postpone", Content.Decision.Pace["S1"]);
            Assert.Equal("parallel", Content.Decision.Pace["S2"]);
        }

        // ---------------------------------------------------------------- the checks, one at a time

        [Fact]
        public void Nothing_wrong_is_safe_to_continue()
        {
            var r = Engine.Evaluate(Calm());
            Assert.Equal(("SAFE", "safe"), (r.Outcome, r.Check));
            Assert.Empty(r.Evidence);
        }

        [Theory]
        [InlineData("news2", "news2>=7")]
        [InlineData("bp", "top_bp<=90")]
        [InlineData("oxygen", "oxygen<=91")]
        [InlineData("confusion", "not_alert")]
        [InlineData("anaphylaxis", "event.anaphylaxis")]
        [InlineData("chest pain", "symptom.chest_pain")]
        public void Each_emergency_sign_gives_EMERGENCY_with_its_evidence(string which, string evidence)
        {
            var input = which switch
            {
                "news2" => Calm(news2: 7),
                "bp" => Calm(sbp: 90),
                "oxygen" => Calm(spo2: 91),
                "confusion" => Calm(acvpu: "C"),
                "anaphylaxis" => Calm(flags: new[] { "event.anaphylaxis" }),
                _ => Calm(flags: new[] { "symptom.chest_pain" }),
            };
            var r = Engine.Evaluate(input);
            Assert.Equal("EMERGENCY", r.Outcome);
            Assert.Contains(evidence, r.Evidence);
        }

        [Fact]
        public void The_edge_of_each_emergency_sign_is_not_an_emergency()
        {
            Assert.Equal("SAFE", Engine.Evaluate(Calm(news2: 6, sbp: 91, spo2: 92)).Outcome);
        }

        [Fact]
        public void A_positive_pregnancy_test_with_shock_is_an_emergency_and_without_shock_is_not()
        {
            Assert.Contains("positive_pregnancy_with_shock", Engine.Evaluate(Calm(pregnancyPositive: true, sbp: 88)).Evidence);
            Assert.Equal("SAFE", Engine.Evaluate(Calm(pregnancyPositive: true)).Outcome);
        }

        [Fact]
        public void An_open_life_threat_problem_is_an_emergency_even_with_normal_numbers()
        {
            var reg = Reg(); reg.Register("hypo", "life_threat", "Hypoglycaemia");
            var r = Engine.Evaluate(Calm(open: reg.Open));
            Assert.Equal("EMERGENCY", r.Outcome);
            Assert.Equal(new[] { "hypo" }, r.Evidence);
        }

        [Fact]
        public void A_result_the_case_needs_that_nobody_opened_gives_INVESTIGATE()
        {
            var r = Engine.Evaluate(Calm(missing: true));
            Assert.Equal(("INVESTIGATE", "information_missing"), (r.Outcome, r.Check));
        }

        [Fact]
        public void A_problem_that_changes_the_pathway_gives_REVIEW()
        {
            var reg = Reg(); reg.Register("duct", "pathway_changing", "Probable duct stone");
            var r = Engine.Evaluate(Calm(open: reg.Open));
            Assert.Equal(("REVIEW", "diagnosis_changed", "duct"), (r.Outcome, r.Check, r.Evidence.Single()));
        }

        [Theory]
        [InlineData("S1", "postpone")]
        [InlineData("S2", "parallel")]
        public void A_fixable_problem_gives_OPTIMISE_and_the_scenarios_pace(string scenario, string pace)
        {
            var reg = Reg(scenario); reg.Register("bp", "optimisable", "BP 182/112");
            var r = Engine.Evaluate(Calm(scenario, reg.Open));
            Assert.Equal(("OPTIMISE", pace), (r.Outcome, r.Pace));
        }

        [Fact]
        public void A_fasting_breach_or_ASA_3_gives_REVIEW_by_the_anaesthetist()
        {
            Assert.Equal(("REVIEW", "affects_anaesthesia"), (Engine.Evaluate(Calm(fastingBreach: true)).Outcome, Engine.Evaluate(Calm(fastingBreach: true)).Check));
            Assert.Contains("asa>=3", Engine.Evaluate(Calm(asa: 3)).Evidence);
            Assert.Equal("SAFE", Engine.Evaluate(Calm(asa: 2)).Outcome);
        }

        [Fact]
        public void The_checks_run_in_order_so_an_emergency_beats_everything_else()
        {
            var reg = Reg(); reg.Register("a", "optimisable", "a"); reg.Register("b", "pathway_changing", "b");
            Assert.Equal("EMERGENCY", Engine.Evaluate(Calm(open: reg.Open, news2: 9, missing: true, fastingBreach: true, asa: 4)).Outcome);
            Assert.Equal("INVESTIGATE", Engine.Evaluate(Calm(open: reg.Open, missing: true, fastingBreach: true)).Outcome);
            Assert.Equal("diagnosis_changed", Engine.Evaluate(Calm(open: reg.Open, fastingBreach: true)).Check);
            reg.Resolve("b");
            Assert.Equal("OPTIMISE", Engine.Evaluate(Calm(open: reg.Open, fastingBreach: true)).Outcome);
        }

        // ---------------------------------------------------------------- real cases

        // T-CG7: VAR-S1-PB, a probable stone in the bile duct
        [Fact]
        public void T_CG7_VAR_S1_PB_is_REVIEW_for_the_duct_stone_pathway()
        {
            var file = File_("VAR-S1-PB");
            var reg = ProblemRegistry.FromCase(file, Content.Problems);
            var r = Engine.Evaluate(Calm(open: reg.Open));
            Assert.Equal("REVIEW", r.Outcome);
            Assert.Equal(file.Problems.Where(p => p.Type == "pathway_changing").Select(p => p.Id), r.Evidence);
        }

        // T-CG8: VAR-S2-B, a possible ruptured ectopic pregnancy
        [Fact]
        public void T_CG8_VAR_S2_B_is_EMERGENCY_and_the_case_ends_redirected()
        {
            var file = File_("VAR-S2-B");
            var reg = ProblemRegistry.FromCase(file, Content.Problems);
            Assert.Equal("EMERGENCY", Engine.Evaluate(Calm("S2", reg.Open)).Outcome);
            Assert.Equal("redirected", file.ExpectedEndState);
        }

        [Fact]
        public void STK_S1_A_goes_from_EMERGENCY_to_OPTIMISE_to_SAFE_as_each_problem_is_dealt_with()
        {
            var reg = ProblemRegistry.FromCase(File_("STK-S1-A"), Content.Problems);
            var seen = new List<string> { Engine.Evaluate(Calm(open: reg.Open)).Outcome };
            foreach (var id in new[] { "stka.hypo", "stka.hba1c", "stka.sglt2", "stka.bp" })
            {
                reg.Resolve(id);
                seen.Add(Engine.Evaluate(Calm(open: reg.Open)).Outcome);
            }
            Assert.Equal(new[] { "EMERGENCY", "OPTIMISE", "OPTIMISE", "SAFE", "SAFE" }, seen);                // HbA1c and the SGLT2 inhibitor are fixable; the blood pressure below 180/110 is information only
        }

        [Fact]
        public void The_emergency_stacked_cases_start_as_EMERGENCY_or_a_fixable_pace_never_SAFE()
        {
            foreach (var id in new[] { "STK-S2-A", "STK-S2-B", "STK-S2-C", "STK-S2-D", "STK-S1-B", "STK-S1-C", "STK-S1-D" })
            {
                var file = File_(id);
                var reg = ProblemRegistry.FromCase(file, Content.Problems);
                var r = Engine.Evaluate(Calm(file.Scenario, reg.Open));
                Assert.NotEqual("SAFE", r.Outcome);
                Assert.NotEmpty(r.Evidence);
            }
        }

        // ---------------------------------------------------------------- the subsystem in a session

        private static Session Start(string caseId = "VAR-S1-A") => Start(caseId, out _);

        private static Session Start(string caseId, out ProblemsSubsystem problems)
        {
            var file = File_(caseId);
            problems = new ProblemsSubsystem(Content, file);
            var chain = Subsystems.Default(Content).Where(x => !(x is DecisionSubsystem) && !(x is ProblemsSubsystem)).Concat(new ISubsystem[] { new DecisionSubsystem(Content, file), problems });
            var s = new Session(Content.Cases["VAR-S1-A"], 42, SessionMode.Practice, chain);
            foreach (var verb in new[] { "ask_identity", "read_wristband", "open_notes", "confirm_identity" }) Assert.True(Do(s, verb).Accepted, verb);
            Do(s, "ask", ("topic", "pregnancy"));
            return s;
        }

        [Fact]
        public void In_a_session_the_expected_outcome_follows_the_information_and_each_change_is_kept()
        {
            var s = Start();
            Assert.Equal("INVESTIGATE", s.Decision!.Current.Outcome);                        // VAR-S1-A needs the liver tests and the pregnancy test opened
            Do(s, "order_investigation", ("test", "lft")); Do(s, "take_sample", ("test", "lft"), ("tube", "serum_gold"));
            Do(s, "order_investigation", ("test", "pregnancy")); Do(s, "perform_bedside_test", ("test", "pregnancy"));
            s.Tick(3600);
            Do(s, "view_result", ("test", "lft")); Do(s, "view_result", ("test", "pregnancy"));
            Assert.Equal("SAFE", s.Decision.Current.Outcome);
            Assert.Equal(new[] { "decision_expected:INVESTIGATE", "decision_expected:SAFE" }, s.Decision.History.Select(h => h.Kind).Distinct());
        }

        [Fact]
        public void A_fall_in_blood_pressure_makes_the_expected_outcome_EMERGENCY()
        {
            var s = Start();
            s.Patient.Physiological.Vitals = s.Patient.Physiological.Vitals with { SystolicBp = 84 };
            Do(s, "ask", ("topic", "complaint"));
            Assert.Equal("EMERGENCY", s.Decision!.Current.Outcome);
            Assert.Contains(s.Decision.History, h => h.Kind == "decision_expected:EMERGENCY");
        }

        [Fact]
        public void A_problem_the_trainee_finds_changes_the_expected_outcome()
        {
            var s = Start("STK-S1-A", out var problems);
            problems.Detect("stka.hypo");
            Assert.Equal("EMERGENCY", s.Decision!.Current.Outcome);
            Assert.Contains("stka.hypo", s.Decision.Current.Evidence);
        }

        [Fact]
        public void The_expected_outcome_is_never_part_of_what_the_trainee_observes()
        {
            var s = Start();
            Assert.DoesNotContain("INVESTIGATE", s.Observable.ToJson());
            Assert.DoesNotContain("decision_expected", s.Observable.ToJson());
        }
    }
}
