using System.Linq;
using ProcSense.Core.Actions;
using ProcSense.Core.Content;
using ProcSense.Core.Pipeline;
using ProcSense.Core.Workflow;
using ProcSense.Core.Workflow.Steps;

namespace ProcSense.Core.Tests
{
    /// <summary>Step fifteen, the WHO Sign Out (p1-theatre-extension-s01, plan tests T15-1 and T-EXT1): the nurse confirms only what the trainee has done; a count discrepancy needs a search, an X-ray and an escalation; the specimen label is read with the name of the patient.</summary>
    public class SignOutTests
    {
        private static readonly ContentPackage Content = TestCase.Package();

        private static SimAction Act(string verb, params (string k, string v)[] p) => new SimAction(verb, "patient", Channel.Cli, p.ToDictionary(x => x.k, x => x.v));

        private static ActionOutcome Do(Session s, string verb, params (string k, string v)[] p) => s.Apply(Act(verb, p));

        private static string Reject(ActionOutcome o) { Assert.False(o.Accepted); return o.RejectReason; }

        // a session whose Time Out is complete: the planned VAR-S1-A, every theatre station checked, the right answers in order
        private static Session AfterTimeOut()
        {
            var file = ContentLoader.LoadCaseFile(new FileContentReader(TestCase.ContentRoot), "VAR-S1-A");
            var chain = Subsystems.Default(Content).Where(x => !(x is OrReadinessSubsystem)).Concat(new ISubsystem[] { new OrReadinessSubsystem(Content, file) });
            var s = new Session(Content.Cases["VAR-S1-A"], 42, SessionMode.Practice, chain);
            foreach (var verb in new[] { "ask_identity", "read_wristband", "open_notes", "confirm_identity" }) Assert.True(Do(s, verb).Accepted, verb);
            s.Patient.Clinical.SetFlag("consent.signed");
            Do(s, "attach_sensor", ("sensor", "spo2"));
            foreach (var st in Content.Or.Stations.Where(x => x.Owner == "trainee")) Do(s, "check_station", ("station", st.Id));
            Say(s, "sign_in.01", "yes"); Say(s, "sign_in.02", "na"); s.Tick(10);
            Say(s, "sign_in.04", "yes"); Say(s, "sign_in.05", "no"); Say(s, "sign_in.06", "no"); Say(s, "sign_in.07", "no");
            s.Tick(10); Say(s, "time_out.02", "yes"); Say(s, "time_out.03", "na"); s.Tick(10); s.Tick(10); s.Tick(10); Say(s, "time_out.07", "na");
            Do(s, "declare_or_ready");
            Assert.True(Do(s, "complete_time_out").Accepted);
            return s;
        }

        private static ActionOutcome Say(Session s, string item, string response) => Do(s, "checklist_item", ("phase", item.Substring(0, item.LastIndexOf('.'))), ("item", item), ("response", response));

        private static void CorrectCounts(Session s) { foreach (var k in new[] { "instruments", "sponges", "needles" }) Assert.True(Do(s, "count_check", ("kind", k), ("result", "correct")).Accepted); }

        private static string Name(Session s) => s.Case.Identity.FullName;

        [Fact]
        public void The_full_correct_sign_out_completes_and_scores_the_credit()
        {
            var s = AfterTimeOut();
            Do(s, "record_procedure");
            CorrectCounts(s);
            Assert.Contains("specimen_label_read", Do(s, "read_specimen_label", ("name", Name(s))).Effects);
            Assert.True(Do(s, "begin_sign_out").Accepted);
            for (int i = 0; i < 6; i++) s.Tick(10);                                                // the nurse confirms her items one after another
            Do(s, "brief_recovery");
            Assert.Contains("confirmed:sign_out.05", Say(s, "sign_out.05", "yes").Effects);
            Assert.Equal("COMPLETE", s.Safety!.PhaseState("sign_out"));
            Assert.Contains(s.Safety.History, h => h.Kind == "sign_out_complete" && h.ScoreItems.Contains("credit.sign_out_complete"));
        }

        [Fact]
        public void The_nurse_does_not_confirm_what_was_not_done_and_the_phase_waits_for_the_trainee()
        {
            var s = AfterTimeOut();
            Assert.True(Do(s, "begin_sign_out").Accepted);
            s.Tick(10);
            Assert.Equal("stopped", s.Safety!.ItemState("sign_out.01"));                              // the procedure was not recorded
            Assert.Equal("STOPPED", s.Safety.PhaseState("sign_out"));
            Do(s, "record_procedure");
            s.Tick(10); s.Tick(10);
            Assert.Equal("confirmed", s.Safety.ItemState("sign_out.01"));                             // asked again once the state is right
        }

        [Fact]
        public void A_count_discrepancy_needs_a_search_an_x_ray_and_an_escalation_before_the_counts_are_complete()
        {
            var s = AfterTimeOut();
            Assert.Contains("search_required", Do(s, "count_check", ("kind", "sponges"), ("result", "discrepancy")).Effects);
            Do(s, "count_check", ("kind", "instruments"), ("result", "correct")); Do(s, "count_check", ("kind", "needles"), ("result", "correct"));
            Assert.False(s.Patient.Clinical.HasFlag("signout.counts_complete"));
            Do(s, "search_for_item");
            Do(s, "request_xray");
            Assert.False(s.Patient.Clinical.HasFlag("signout.counts_complete"));
            var o = Do(s, "escalate_count");
            Assert.Contains("discrepancy_resolved", o.Effects);
            Assert.True(s.Patient.Clinical.HasFlag("signout.counts_complete"));
            Assert.Contains(s.Safety!.History, h => h.Kind == "count_discrepancy_handled" && h.ScoreItems.Contains("credit.count_discrepancy_handled"));
        }

        [Fact]
        public void The_steps_of_a_discrepancy_need_a_discrepancy_and_a_count_needs_a_known_kind()
        {
            var s = AfterTimeOut();
            Assert.Equal(SafetyChecklistSubsystem.NoDiscrepancy, Reject(Do(s, "search_for_item")));
            Assert.Equal(SafetyChecklistSubsystem.UnknownKind, Reject(Do(s, "count_check", ("kind", "gloves"), ("result", "correct"))));
            Assert.Equal(SafetyChecklistSubsystem.UnknownResult, Reject(Do(s, "count_check", ("kind", "needles"), ("result", "maybe"))));
        }

        [Fact]
        public void A_specimen_label_read_with_the_wrong_name_is_a_critical_error_and_the_right_name_is_accepted()
        {
            var s = AfterTimeOut();
            var wrong = Do(s, "read_specimen_label", ("name", "Somebody Else"));
            Assert.Contains("specimen_name_mismatch", wrong.Effects);
            Assert.False(s.Patient.Clinical.HasFlag("signout.specimen_read"));
            Assert.Contains(s.Safety!.History, h => h.Kind == "specimen_name_wrong" && h.ScoreItems.Contains("critical.specimen_name_wrong"));
            Assert.Equal(SafetyChecklistSubsystem.MissingName, Reject(Do(s, "read_specimen_label")));
            Assert.Contains("specimen_label_read", Do(s, "read_specimen_label", ("name", Name(s).ToUpperInvariant())).Effects);
        }

        [Fact]
        public void An_equipment_fault_in_the_readiness_log_has_to_be_reported_before_the_nurse_confirms_the_equipment_item()
        {
            var s = AfterTimeOut();
            s.Patient.Clinical.SetFlag("or.fault_logged");
            Do(s, "record_procedure"); CorrectCounts(s); Do(s, "read_specimen_label", ("name", Name(s)));
            Assert.True(Do(s, "begin_sign_out").Accepted);
            for (int i = 0; i < 5; i++) s.Tick(10);
            Assert.Equal("stopped", s.Safety!.ItemState("sign_out.04"));                              // a fault is in the log and nobody reported it
            Do(s, "report_equipment_problem");
            for (int i = 0; i < 3; i++) s.Tick(10);
            Assert.Equal("confirmed", s.Safety.ItemState("sign_out.04"));
        }

        [Fact]
        public void The_sign_out_verbs_need_Time_Out_first()
        {
            var s = new Session(Content.Cases["VAR-S1-A"], 42, SessionMode.Practice, Subsystems.Default(Content));
            foreach (var verb in new[] { "ask_identity", "read_wristband", "open_notes", "confirm_identity" }) Do(s, verb);
            foreach (var verb in new[] { "record_procedure", "search_for_item", "brief_recovery", "report_equipment_problem" }) Assert.Equal(SafetyChecklistSubsystem.TimeOutFirst, Reject(Do(s, verb)));
        }
    }
}
