using System.Collections.Generic;
using System.Linq;
using ProcSense.Core.Actions;
using ProcSense.Core.Content;
using ProcSense.Core.Pipeline;
using ProcSense.Core.Workflow;
using ProcSense.Core.Workflow.Steps;

namespace ProcSense.Core.Tests
{
    /// <summary>The end-state resolver and facts that surface later (p0-simulation-workflow-s17, plan tests T-CG7, T-CG8 and T02-WT1).</summary>
    public class EndStateTests
    {
        private static readonly ContentPackage Content = TestCase.Package();
        private static EndStateTables Tables => Content.EndStates;

        private static CaseFile File_(string id) => ContentLoader.LoadCaseFile(new FileContentReader(TestCase.ContentRoot), id);

        private static SimAction Act(string verb, params (string k, string v)[] p) => new SimAction(verb, "patient", Channel.Cli, p.ToDictionary(x => x.k, x => x.v));

        private static ActionOutcome Do(Session s, string verb, params (string k, string v)[] p) => s.Apply(Act(verb, p));

        private static Session Start(string caseId, out ProblemsSubsystem problems, string? scenario = null)
        {
            var file = File_(caseId);
            problems = new ProblemsSubsystem(Content, file);
            var chain = Subsystems.Default(Content).Where(x => !(x is DecisionSubsystem) && !(x is ProblemsSubsystem) && !(x is OrReadinessSubsystem) && !(x is EndStateSubsystem))
                .Concat(new ISubsystem[] { new DecisionSubsystem(Content, file), problems, new OrReadinessSubsystem(Content, file), new EndStateSubsystem(Content, file) });
            var card = Content.Cases["VAR-S1-A"] with { Scenario = scenario ?? file.Scenario };
            var s = new Session(card, 42, SessionMode.Practice, chain);
            foreach (var verb in new[] { "ask_identity", "read_wristband", "open_notes", "confirm_identity" }) Assert.True(Do(s, verb).Accepted, verb);
            return s;
        }

        private static Session Start(string caseId, string? scenario = null) => Start(caseId, out _, scenario);

        private static ActionOutcome Say(Session s, string item, string response) => Do(s, "checklist_item", ("phase", item.Substring(0, item.LastIndexOf('.'))), ("item", item), ("response", response));

        // a run that reaches ready for incision with nothing wrong
        private static void ReadyRun(Session s, bool declareTooEarly = false)
        {
            s.Patient.Clinical.SetFlag("consent.signed");
            Do(s, "attach_sensor", ("sensor", "spo2"));
            if (declareTooEarly) Do(s, "declare_or_ready");                                       // the theatre was not ready yet
            foreach (var st in Content.Or.Stations.Where(x => x.Owner == "trainee")) Do(s, "check_station", ("station", st.Id));
            Say(s, "sign_in.01", "yes"); Say(s, "sign_in.02", "na"); s.Tick(10);
            Say(s, "sign_in.04", "yes"); Say(s, "sign_in.05", "no"); Say(s, "sign_in.06", "no"); Say(s, "sign_in.07", "no");
            s.Tick(10); Say(s, "time_out.02", "yes"); Say(s, "time_out.03", "na"); s.Tick(10); s.Tick(10); s.Tick(10); Say(s, "time_out.07", "na");
            Do(s, "declare_or_ready");
            Assert.True(Do(s, "complete_time_out").Accepted);
        }

        // ---------------------------------------------------------------- the content

        [Fact]
        public void The_content_has_the_five_end_states_the_delay_items_and_the_surface_later_fact()
        {
            Assert.Equal(new[] { "READY_SAFE", "POSTPONED_APPROPRIATELY", "REDIRECTED_APPROPRIATELY", "SAFE_BUT_DELAYED", "SAFETY_FAILURE" }, Tables.States.Select(s => s.Code));
            Assert.Contains("major.or_fault_ignored", Tables.DelayItems);
            Assert.Equal("VAR-S1-D", Tables.SurfaceLater.Single().Case);
            Assert.Equal("D2", Tables.SurfaceLater.Single().Variant);
        }

        // ---------------------------------------------------------------- one fixture per end state

        [Fact]
        public void READY_SAFE_a_clean_run_to_ready_for_incision()
        {
            var s = Start("VAR-S1-A");
            Assert.Equal("", s.EndStates!.Resolved);                                            // the case goes on
            ReadyRun(s);
            Assert.Equal("READY_SAFE", s.EndStates.Resolved);
            Assert.True(s.Patient.Clinical.HasFlag("endstate.resolved"));
        }

        [Fact]
        public void SAFE_BUT_DELAYED_the_same_run_with_an_avoidable_delay()
        {
            var s = Start("VAR-S1-A");
            ReadyRun(s, declareTooEarly: true);                                                 // declared the theatre ready when it was not
            Assert.Equal("SAFE_BUT_DELAYED", s.EndStates!.Resolved);
        }

        // T-CG7: a probable bile-duct stone is postponed and the end state is a success
        [Fact]
        public void POSTPONED_APPROPRIATELY_T_CG7_VAR_S1_PB()
        {
            var s = Start("VAR-S1-PB", out var problems);
            Do(s, "ask", ("topic", "pregnancy"));
            Do(s, "order_investigation", ("test", "lft")); Do(s, "take_sample", ("test", "lft"), ("tube", "serum_gold"));
            Do(s, "order_investigation", ("test", "pregnancy")); Do(s, "perform_bedside_test", ("test", "pregnancy"));
            s.Tick(3600); Do(s, "view_result", ("test", "lft")); Do(s, "view_result", ("test", "pregnancy"));
            problems.Detect(File_("VAR-S1-PB").Problems.First(p => p.Type == "pathway_changing").Id);
            Assert.Contains("decision:correct", Do(s, "declare_outcome", ("outcome", "REVIEW")).Effects);
            Do(s, "postpone", ("reason", "probable duct stone: senior review and the biliary pathway first"));
            Assert.Equal("POSTPONED_APPROPRIATELY", s.EndStates!.Resolved);
        }

        // T-CG8: the ectopic pregnancy case is an emergency and is redirected
        [Fact]
        public void REDIRECTED_APPROPRIATELY_T_CG8_VAR_S2_B()
        {
            var s = Start("VAR-S2-B", out var problems);
            problems.Detect(File_("VAR-S2-B").Problems.First(p => p.Type == "life_threat").Id);
            Assert.Equal("EMERGENCY", s.Decision!.Current.Outcome);
            Do(s, "redirect", ("service", "gynaecology"));
            Assert.Equal("REDIRECTED_APPROPRIATELY", s.EndStates!.Resolved);
        }

        [Fact]
        public void SAFETY_FAILURE_postponing_while_an_emergency_is_open()
        {
            var s = Start("STK-S1-A", out var problems);
            problems.Detect("stka.hypo");
            Do(s, "postpone", ("reason", "come back tomorrow"));
            Assert.Equal("SAFETY_FAILURE", s.EndStates!.Resolved);
        }

        [Fact]
        public void SAFETY_FAILURE_any_critical_item_makes_the_end_of_the_case_a_failure()
        {
            var s = Start("VAR-S1-A");
            Do(s, "tick_ward_item", ("item", "consent"));                                       // ticked consent that was never given: critical
            Assert.Equal("", s.EndStates!.Resolved);                                            // the case has not ended yet
            ReadyRun(s);
            Assert.Equal("SAFETY_FAILURE", s.EndStates.Resolved);
        }

        [Fact]
        public void SAFETY_FAILURE_the_checklist_skipped_repeatedly_stops_the_case()
        {
            var s = Start("VAR-S1-A");
            Say(s, "sign_in.04", "yes"); Say(s, "sign_in.04", "yes");
            s.Tick(1);                                                                           // a refused action does not reach the resolver; the next step of time does
            Assert.Equal("SAFETY_FAILURE", s.EndStates!.Resolved);
        }

        [Fact]
        public void The_end_state_is_resolved_once_and_the_event_is_raised_once()
        {
            var s = Start("VAR-S1-B");
            s.Patient.Clinical.SetFlag("variant.B2");
            var events = new List<WorkflowEvent>();
            s.Events.Subscribe<WorkflowEvent>(events.Add);
            Do(s, "postpone", ("reason", "x"));
            Do(s, "ask", ("topic", "complaint"));
            Assert.Equal(1, events.Count(e => e.Name == EndStateSubsystem.Event));
            Assert.Equal("POSTPONED_APPROPRIATELY", s.EndStates!.Resolved);
        }

        // ---------------------------------------------------------------- facts that surface later (T02-WT1)

        [Fact]
        public void T02_WT1_VAR_S1_D_b_the_apixaban_never_asked_about_surfaces_at_Sign_In_and_the_case_stops()
        {
            var s = Start("VAR-S1-D");
            s.Patient.Clinical.SetFlag("variant.D2");                                           // the patient who took it this morning
            s.Patient.Clinical.SetFlag("consent.signed");
            var o = Say(s, "sign_in.01", "yes");                                                // Sign In begins; the anaesthetist asks about the blood thinner
            Assert.Contains("surfaced:s1d.apixaban_this_morning", o.Effects);
            Assert.Contains("case_stopped", o.Effects);
            Assert.Contains(s.EndStates!.History, h => h.Kind == "surfaced_late:s1d.apixaban_this_morning" && h.ScoreItems.Contains("major.hidden_fact_surfaced_late"));
            Assert.Equal("SAFE_BUT_DELAYED", s.EndStates.Resolved);
            Assert.Equal(SafetyChecklistSubsystem.CaseStopped, Say(s, "sign_in.02", "na").RejectReason);
        }

        [Fact]
        public void A_trainee_who_asked_about_the_medicines_has_nothing_surface()
        {
            var s = Start("VAR-S1-D");
            s.Patient.Clinical.SetFlag("variant.D2");
            s.Patient.Clinical.SetFlag("consent.signed");
            Do(s, "ask", ("topic", "medicines"));
            var o = Say(s, "sign_in.01", "yes");
            Assert.DoesNotContain(o.Effects, e => e.StartsWith("surfaced:"));
            Assert.Equal("", s.EndStates!.Resolved);
        }

        [Fact]
        public void The_other_variant_has_nothing_hidden_to_surface()
        {
            var s = Start("VAR-S1-D");                                                          // D1: three days ago, no variant flag for D2
            s.Patient.Clinical.SetFlag("consent.signed");
            Assert.DoesNotContain(Say(s, "sign_in.01", "yes").Effects, e => e.StartsWith("surfaced:"));
        }
    }
}
