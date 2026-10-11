using System.Collections.Generic;
using System.Linq;
using ProcSense.Core.Clinical;
using ProcSense.Core.Content;
using ProcSense.Core.Workflow;
using ProcSense.Core.Workflow.Steps;

namespace ProcSense.Core.Tests
{
    /// <summary>The problem registry, the 7-step ladder and the golden rules (p0-clinical-logic-s14, plan test T-DE1).</summary>
    public class ProblemRegistryTests
    {
        private static readonly ContentPackage Content = TestCase.Package();

        private static CaseFile File_(string id) => ContentLoader.LoadCaseFile(new FileContentReader(TestCase.ContentRoot), id);

        private static ProblemRegistry Reg(string scenario = "S1") => new ProblemRegistry(Content.Problems, scenario);

        [Fact]
        public void The_content_has_six_types_on_seven_rungs()
        {
            var t = Content.Problems;
            Assert.Equal(new[] { "life_threat", "safety_blocker", "time_critical", "pathway_changing", "optimisable", "information_only" }, t.Types.Select(x => x.Id));
            Assert.Equal(new[] { 1, 2, 3, 4, 6, 7 }, t.Types.Select(x => x.LadderRank));
            Assert.Equal(new[] { 1, 2, 2, 2, 3, 4 }, t.Types.Select(x => x.Priority));
            Assert.Equal(7, t.Ladder.Count);
            Assert.Contains("life-threatening", t.Ladder[0].Question);
        }

        // T-DE1: the guide's worked example, ranked as the walkthrough's Part D (the types of the last two differ from the guide's example: ISS-0020)
        [Fact]
        public void T_DE1_STK_S1_A_ranks_P1_to_P4_as_the_walkthrough()
        {
            var reg = ProblemRegistry.FromCase(File_("STK-S1-A"), Content.Problems);
            Assert.Equal(new[] { ("stka.hypo", "life_threat", 1), ("stka.sglt2", "safety_blocker", 2), ("stka.hba1c", "pathway_changing", 3), ("stka.bp", "optimisable", 4) },
                reg.Ordered.Select(p => (p.Id, p.Type, p.Priority)));
        }

        [Theory]
        [InlineData("STK-S1-A")]
        [InlineData("STK-S1-B")]
        [InlineData("STK-S1-C")]
        [InlineData("STK-S1-D")]
        [InlineData("STK-S2-A")]
        [InlineData("STK-S2-B")]
        [InlineData("STK-S2-C")]
        [InlineData("STK-S2-D")]
        public void Every_stacked_case_is_ordered_as_the_case_lists_its_problems(string id)
        {
            var file = File_(id);
            var reg = ProblemRegistry.FromCase(file, Content.Problems);
            Assert.Equal(file.Problems.OrderBy(p => p.Rank).Select(p => p.Id), reg.Ordered.Select(p => p.Id));
            Assert.Equal(Enumerable.Range(1, file.Problems.Count), reg.Ordered.Select(p => p.Priority));
        }

        [Fact]
        public void Equal_priorities_are_ordered_by_ladder_rung_then_by_the_order_found()
        {
            var reg = Reg();
            reg.Register("a", "pathway_changing", "a");
            reg.Register("b", "safety_blocker", "b");
            reg.Register("c", "time_critical", "c");
            reg.Register("d", "safety_blocker", "d");
            Assert.Equal(new[] { "b", "d", "c", "a" }, reg.Ordered.Select(p => p.Id));                    // all default to P2: rung 2, rung 2 (found later), rung 3, rung 4
        }

        [Fact]
        public void A_problem_is_registered_once_and_an_unknown_type_is_refused()
        {
            var reg = Reg();
            var first = reg.Register("x", "optimisable", "x");
            Assert.Same(first, reg.Register("x", "life_threat", "again"));
            Assert.Throws<System.ArgumentException>(() => reg.Register("y", "scary", "y"));
        }

        // ---------------------------------------------------------------- the golden rules (14B.2)

        [Fact]
        public void The_most_dangerous_problem_sets_the_pace_and_makes_the_case_an_emergency()
        {
            var reg = Reg("S1");
            reg.Register("bp", "optimisable", "bp");
            Assert.Equal(CasePace.Postpone, reg.Pace);
            reg.Register("hypo", "life_threat", "hypo");
            Assert.True(reg.Emergency);
            Assert.Equal(CasePace.Emergency, reg.Pace);
            Assert.Equal("hypo", reg.First!.Id);
            reg.Resolve("hypo");
            Assert.False(reg.Emergency);
            Assert.Equal(CasePace.Postpone, reg.Pace);
        }

        [Fact]
        public void In_a_planned_case_problems_add_up_to_postponing_and_several_make_it_certain()
        {
            var reg = Reg("S1");
            Assert.Equal(CasePace.Proceed, reg.Pace);
            reg.Register("info", "information_only", "smoker");
            Assert.Equal(CasePace.Proceed, reg.Pace);                                 // information only never postpones
            reg.Register("a", "optimisable", "a");
            Assert.Equal(CasePace.Postpone, reg.Pace);
            reg.Register("b", "safety_blocker", "b");
            Assert.Equal(CasePace.PostponeCertain, reg.Pace);
        }

        [Fact]
        public void In_an_emergency_case_problems_add_up_to_preparing_in_parallel()
        {
            var reg = Reg("S2");
            reg.Register("a", "optimisable", "a");
            reg.Register("b", "safety_blocker", "b");
            Assert.Equal(CasePace.Parallel, reg.Pace);
        }

        [Fact]
        public void A_new_diagnosis_beats_the_old_plan()
        {
            var reg = Reg("S2");
            reg.Register("opt", "optimisable", "anaemia");
            Assert.Null(reg.NewDiagnosis);
            reg.Register("ectopic", "pathway_changing", "ectopic until proven otherwise");
            Assert.Equal("ectopic", reg.NewDiagnosis!.Id);
        }

        [Fact]
        public void Information_only_problems_are_hidden_while_working_but_the_handover_lists_every_problem()
        {
            var reg = ProblemRegistry.FromCase(File_("STK-S1-A"), Content.Problems);
            Assert.Equal(4, reg.Visible.Count);
            reg.Register("smoker", "information_only", "ex-smoker");
            Assert.DoesNotContain(reg.Visible, p => p.Id == "smoker");
            reg.Resolve("stka.hypo");
            Assert.Equal(5, reg.Handover.Count);                                       // resolved and information-only problems are still handed over
            Assert.Equal(4, reg.Open.Count);
        }

        // ---------------------------------------------------------------- the subsystem

        [Fact]
        public void Detecting_a_life_threat_switches_the_case_to_emergency_mode_and_resolving_it_ends_it()
        {
            var file = File_("STK-S1-A");
            var step = new ProblemsSubsystem(Content, file);
            var chain = Subsystems.Default(Content).Where(x => !(x is ProblemsSubsystem)).Concat(new ProcSense.Core.Pipeline.ISubsystem[] { step });
            var s = new Session(Content.Cases["VAR-S1-A"], 42, SessionMode.Practice, chain);
            var events = new List<WorkflowEvent>();
            s.Events.Subscribe<WorkflowEvent>(events.Add);

            Assert.Null(step.Detect("not.a.problem"));
            Assert.Equal("stka.bp", step.Detect("stka.bp")!.Id);
            Assert.False(s.Patient.Clinical.HasFlag(ProblemsSubsystem.EmergencyFlag));
            step.Detect("stka.hypo");
            Assert.True(s.Patient.Clinical.HasFlag(ProblemsSubsystem.EmergencyFlag));
            Assert.Contains(events, e => e.Name == ProblemsSubsystem.EmergencyEvent && e.Source == "stka.hypo");
            Assert.Equal("stka.hypo", s.Problems!.First!.Id);

            Assert.True(step.Resolve("stka.hypo"));
            Assert.False(s.Patient.Clinical.HasFlag(ProblemsSubsystem.EmergencyFlag));
        }
    }
}
