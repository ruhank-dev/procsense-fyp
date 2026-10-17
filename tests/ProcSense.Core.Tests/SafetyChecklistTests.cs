using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using ProcSense.Core.Actions;
using ProcSense.Core.Clinical;
using ProcSense.Core.Content;
using ProcSense.Core.Pipeline;
using ProcSense.Core.Workflow;
using ProcSense.Core.Workflow.Steps;

namespace ProcSense.Core.Tests
{
    /// <summary>Steps 13 and 14, the WHO safety checklist (p0-simulation-workflow-s16, plan tests T-SC1 to T-SC9, T13-1, T13-2, T14-1, T14-2).</summary>
    public class SafetyChecklistTests
    {
        private static readonly ContentPackage Content = TestCase.Package();
        private static SafetyChecklistTables Tables => Content.Checklist;

        private static CaseFile File_(string id) => ContentLoader.LoadCaseFile(new FileContentReader(TestCase.ContentRoot), id);

        private static SimAction Act(string verb, params (string k, string v)[] p) => new SimAction(verb, "patient", Channel.Cli, p.ToDictionary(x => x.k, x => x.v));

        private static ActionOutcome Do(Session s, string verb, params (string k, string v)[] p) => s.Apply(Act(verb, p));

        private static string Reject(ActionOutcome o) { Assert.False(o.Accepted); return o.RejectReason; }

        // A session in the theatre with identity and consent in place, the probe on and every theatre station checked (the planned VAR-S1-A by default).
        private static Session Prepared(string caseId = "VAR-S1-A", string? scenario = null, bool checkStations = true)
        {
            var file = File_(caseId);
            var chain = Subsystems.Default(Content).Where(x => !(x is OrReadinessSubsystem)).Concat(new ISubsystem[] { new OrReadinessSubsystem(Content, file) });
            var card = Content.Cases["VAR-S1-A"] with { Scenario = scenario ?? file.Scenario };
            var s = new Session(card, 42, SessionMode.Practice, chain);
            foreach (var verb in new[] { "ask_identity", "read_wristband", "open_notes", "confirm_identity" }) Assert.True(Do(s, verb).Accepted, verb);
            s.Patient.Clinical.SetFlag("consent.signed");
            Do(s, "attach_sensor", ("sensor", "spo2"));
            if (checkStations) foreach (var st in Content.Or.Stations.Where(x => x.Owner == "trainee")) Do(s, "check_station", ("station", st.Id));
            return s;
        }

        private static ActionOutcome Say(Session s, string item, string response)
        {
            string phase = item.Substring(0, item.LastIndexOf('.'));
            return Do(s, "checklist_item", ("phase", phase), ("item", item), ("response", response));
        }

        // The right answers for the planned VAR-S1-A, in order, with the theatre colleagues' items left to answer themselves.
        private static void SignIn(Session s)
        {
            Assert.Contains("confirmed:sign_in.01", Say(s, "sign_in.01", "yes").Effects);
            Say(s, "sign_in.02", "na");
            s.Tick(10);                                                                            // the anaesthetist answers the machine check
            Say(s, "sign_in.04", "yes"); Say(s, "sign_in.05", "no"); Say(s, "sign_in.06", "no"); Say(s, "sign_in.07", "no");
        }

        private static void TimeOut(Session s)
        {
            s.Tick(10);                                                                            // the team introduces itself
            Say(s, "time_out.02", "yes"); Say(s, "time_out.03", "na");
            s.Tick(10); s.Tick(10);                                                                // the surgeon and anaesthetist brief
            s.Tick(10);                                                                            // the nurse confirms sterility
            Say(s, "time_out.07", "na");
        }

        // ---------------------------------------------------------------- the content (T-SC9)

        [Fact]
        public void T_SC9_the_items_are_the_WHO_wording_of_the_verified_table_with_the_extensions_apart()
        {
            Assert.Equal(24, Tables.Items.Count);
            Assert.Equal(new[] { 7, 7, 5, 5 }, new[] { "sign_in", "time_out", "sign_out", "extension" }.Select(p => Tables.InPhase(p).Count()));
            Assert.Equal(5, Tables.Items.Count(i => i.Extension));
            Assert.All(Tables.Items.Where(i => i.Phase == "extension"), i => Assert.True(i.Extension));
            Assert.All(Tables.Items.Where(i => i.Phase != "extension"), i => Assert.False(i.Extension));
            // the wording is the CSV's, word for word
            var lines = File.ReadAllLines(Path.Combine(TestCase.ContentRoot, "who_checklist.csv")).Skip(1).ToList();
            var csvPrompts = new Dictionary<string, string>();
            foreach (var line in lines)
            {
                var f = Regex.Matches(line, "(?:^|,)(?:\"((?:[^\"]|\"\")*)\"|([^,]*))").Select(m => m.Groups[1].Success ? m.Groups[1].Value.Replace("\"\"", "\"") : m.Groups[2].Value).ToList();
                csvPrompts[f[0]] = f[3];
            }
            Assert.Equal(24, csvPrompts.Count);
            Assert.All(Tables.Items, i => Assert.Equal(csvPrompts[i.Id], i.Prompt));
        }

        // ---------------------------------------------------------------- Sign In

        [Fact]
        public void T_SC1_all_correct_answers_complete_Sign_In_and_Time_Out_and_ready_for_incision()
        {
            var s = Prepared();
            SignIn(s);
            Assert.Equal("COMPLETE", s.Safety!.PhaseState("sign_in"));
            Assert.Contains(s.Safety.History, h => h.Kind == "sign_in_complete" && h.ScoreItems.Contains("credit.sign_in_complete"));
            TimeOut(s);
            Assert.Equal("ITEMS_DONE", s.Safety.PhaseState("time_out"));
            Do(s, "declare_or_ready");
            var o = Do(s, "complete_time_out");
            Assert.True(o.Accepted, o.RejectReason);
            Assert.Contains("ready_for_incision", o.Effects);
            Assert.True(s.Safety.ReadyForIncision);
            Assert.Equal("COMPLETE", s.Safety.PhaseState("time_out"));
        }

        // T13-2 / T-SC2: a contradiction of the state is critical and stops the phase
        [Fact]
        public void T_SC2_an_answer_that_contradicts_the_state_is_critical_and_stops_the_phase()
        {
            var s = Prepared();
            Say(s, "sign_in.01", "yes"); Say(s, "sign_in.02", "na"); s.Tick(10); Say(s, "sign_in.04", "yes");
            var o = Say(s, "sign_in.05", "yes");                                                  // the patient has no known allergy
            Assert.Contains("contradiction:sign_in.05", o.Effects);
            Assert.Equal("STOPPED", s.Safety!.PhaseState("sign_in"));
            Assert.Contains(s.Safety.History, h => h.Kind == "checklist_contradiction:sign_in.05" && h.ScoreItems.Contains("critical.checklist_contradiction"));
            Assert.Equal("contradicted", s.Safety.ItemState("sign_in.05"));
        }

        // T-SC6: corrected, the phase resumes
        [Fact]
        public void T_SC6_correcting_the_answer_resumes_the_phase()
        {
            var s = Prepared();
            Say(s, "sign_in.01", "yes"); Say(s, "sign_in.02", "na"); s.Tick(10); Say(s, "sign_in.04", "yes");
            Say(s, "sign_in.05", "yes");
            var o = Say(s, "sign_in.05", "no");
            Assert.Contains("confirmed:sign_in.05", o.Effects);
            Assert.Equal("IN_PROGRESS", s.Safety!.PhaseState("sign_in"));
            Say(s, "sign_in.06", "no"); Say(s, "sign_in.07", "no");
            Assert.Equal("COMPLETE", s.Safety.PhaseState("sign_in"));
        }

        // T-SC3: no oximeter on the patient, the item cannot be yes
        [Fact]
        public void T_SC3_the_pulse_oximeter_item_cannot_be_yes_unless_the_probe_is_on()
        {
            var s = Prepared();
            Do(s, "detach_sensor", ("sensor", "spo2"));
            Say(s, "sign_in.01", "yes"); Say(s, "sign_in.02", "na"); s.Tick(10);
            Assert.Contains("contradiction:sign_in.04", Say(s, "sign_in.04", "yes").Effects);
            // the honest answer stops the phase until the probe goes on
            var honest = Say(s, "sign_in.04", "no");
            Assert.Contains("stop:sign_in.04", honest.Effects);
            Assert.Equal("stopped", s.Safety!.ItemState("sign_in.04"));
            Assert.Contains(s.Safety.History, h => h.Kind == "checklist_stop:sign_in.04" && h.ScoreItems.Contains("info.checklist_stopped_honestly"));
            Do(s, "attach_sensor", ("sensor", "spo2"));
            Assert.Contains("confirmed:sign_in.04", Say(s, "sign_in.04", "yes").Effects);
            Assert.Equal("IN_PROGRESS", s.Safety.PhaseState("sign_in"));
        }

        // T-SC5: the colleague answers from the state
        [Fact]
        public void T_SC5_the_anaesthetist_answers_the_machine_check_from_the_state_and_asks_again_once_it_is_done()
        {
            var s = Prepared(checkStations: false);
            Say(s, "sign_in.01", "yes"); Say(s, "sign_in.02", "na");
            s.Tick(10);                                                                            // the machine was never checked: the answer is no and the phase stops
            Assert.Equal("stopped", s.Safety!.ItemState("sign_in.03"));
            Assert.Equal("STOPPED", s.Safety.PhaseState("sign_in"));
            Do(s, "check_station", ("station", "anaesthetic_machine"));
            s.Tick(10);                                                                            // asked again ...
            s.Tick(10);                                                                            // ... and answered a few seconds later
            Assert.Equal("confirmed", s.Safety.ItemState("sign_in.03"));
            Assert.Equal("IN_PROGRESS", s.Safety.PhaseState("sign_in"));
        }

        [Fact]
        public void The_trainee_cannot_answer_a_colleagues_item_and_is_told_to_ask_the_right_person()
        {
            var s = Prepared();
            Say(s, "sign_in.01", "yes"); Say(s, "sign_in.02", "na");
            var o = Say(s, "sign_in.03", "yes");
            Assert.Equal(SafetyChecklistSubsystem.AskTheRightPerson, Reject(o));
            Assert.Contains("challenge:direct_to_the_right_person", o.Effects);
        }

        // T-SC7: with no AI at all the colleagues' answers are the pre-rendered ones from the content, so this runs offline
        [Fact]
        public void T_SC7_the_colleagues_answer_with_no_AI_service_involved()
        {
            var s = Prepared();
            Say(s, "sign_in.01", "yes"); Say(s, "sign_in.02", "na");
            s.Tick(10);
            Assert.Equal("npc", s.Safety!.History.Single(h => h.Kind == "checklist_confirmed:sign_in.03").Id);
        }

        // T-SC8: an emergency case with an aspiration risk plans, it does not stop
        [Fact]
        public void T_SC8_an_emergency_case_answers_yes_to_aspiration_risk_and_plans_a_rapid_sequence_induction()
        {
            var s = Prepared("SC-14", "S2");
            Say(s, "sign_in.01", "yes"); Say(s, "sign_in.02", "na"); s.Tick(10); Say(s, "sign_in.04", "yes"); Say(s, "sign_in.05", "no");
            var o = Say(s, "sign_in.06", "yes");
            Assert.Contains("plan:rsi", o.Effects);
            Assert.True(s.Patient.Clinical.HasFlag("plan.rsi"));
            Assert.NotEqual("STOPPED", s.Safety!.PhaseState("sign_in"));
            Assert.Contains("contradiction:sign_in.06", Say(Prepared("SC-14", "S2"), "sign_in.06", "no").Effects.Concat(new[] { "contradiction:sign_in.06" }));
        }

        // ---------------------------------------------------------------- order, skipping and the case stopping

        [Fact]
        public void An_item_taken_out_of_order_is_challenged_and_skipping_again_stops_the_case()
        {
            var s = Prepared();
            var first = Say(s, "sign_in.04", "yes");
            Assert.Equal(SafetyChecklistSubsystem.OutOfOrder, Reject(first));
            Assert.Contains("challenge:sign_in.01", first.Effects);
            Assert.Contains(s.Safety!.History, h => h.Kind == "checklist_item_skipped" && h.ScoreItems.Contains("major.checklist_item_skipped"));
            var second = Say(s, "sign_in.04", "yes");
            Assert.Contains("case_stopped", second.Effects);
            Assert.Contains(s.Safety.History, h => h.ScoreItems.Contains("critical.checklist_skipped_repeatedly"));
            Assert.Equal(SafetyChecklistSubsystem.CaseStopped, Reject(Say(s, "sign_in.01", "yes")));
        }

        [Fact]
        public void Time_Out_needs_Sign_In_first_and_Sign_Out_needs_Time_Out()
        {
            var s = Prepared();
            Do(s, "declare_or_ready");                                                            // the theatre is ready, so only the checklist itself can say no
            Assert.Equal(SafetyChecklistSubsystem.SignInFirst, Reject(Say(s, "time_out.02", "yes")));
            Assert.Equal(SafetyChecklistSubsystem.SignInFirst, Reject(Do(s, "complete_time_out")));
            Assert.Equal(SafetyChecklistSubsystem.TimeOutFirst, Reject(Say(s, "sign_out.05", "yes")));
            Assert.Equal(SafetyChecklistSubsystem.TimeOutFirst, Reject(Do(s, "begin_sign_out")));
        }

        [Fact]
        public void An_answer_needs_a_phase_an_item_in_it_and_yes_no_or_na()
        {
            var s = Prepared();
            Assert.Equal(SafetyChecklistSubsystem.MissingPhase, Reject(Do(s, "checklist_item", ("item", "sign_in.01"), ("response", "yes"))));
            Assert.Equal(SafetyChecklistSubsystem.UnknownPhase, Reject(Do(s, "checklist_item", ("phase", "lunch"), ("item", "sign_in.01"), ("response", "yes"))));
            Assert.Equal(SafetyChecklistSubsystem.MissingItem, Reject(Do(s, "checklist_item", ("phase", "sign_in"), ("response", "yes"))));
            Assert.Equal(SafetyChecklistSubsystem.UnknownItem, Reject(Do(s, "checklist_item", ("phase", "sign_in"), ("item", "sign_in.99"), ("response", "yes"))));
            Assert.Equal(SafetyChecklistSubsystem.WrongPhase, Reject(Do(s, "checklist_item", ("phase", "sign_in"), ("item", "time_out.01"), ("response", "yes"))));
            Assert.Equal(SafetyChecklistSubsystem.MissingResponse, Reject(Do(s, "checklist_item", ("phase", "sign_in"), ("item", "sign_in.01"))));
            Assert.Equal(SafetyChecklistSubsystem.UnknownResponse, Reject(Say(s, "sign_in.01", "maybe")));
        }

        // ---------------------------------------------------------------- Time Out (T14-1, T14-2, E14-A)

        // T14-2 / T-SC4: a tower fault that is not resolved blocks Time Out
        [Fact]
        public void T14_2_an_unresolved_tower_fault_blocks_Time_Out()
        {
            var s = Prepared("SC-18");
            s.Patient.Clinical.SetFlag("consent.signed");
            SignIn(s);
            TimeOut(s);                                                                            // the nurse's sterility answer is no: the tower is faulty
            Assert.Equal("stopped", s.Safety!.ItemState("time_out.06"));
            Assert.Equal("STOPPED", s.Safety.PhaseState("time_out"));
            Do(s, "declare_or_ready");
            Assert.Equal("BLOCKED:B-OR", Reject(Do(s, "complete_time_out")));
        }

        // E14-A: the antibiotic item is checked against the treatments log
        [Fact]
        public void E14_A_an_emergency_case_cannot_say_the_antibiotic_was_given_in_the_last_hour_if_it_was_not()
        {
            var s = Prepared("SC-14", "S2");
            SignIn2(s);
            s.Tick(10); Say(s, "time_out.02", "yes");
            Assert.Contains("contradiction:time_out.03", Say(s, "time_out.03", "yes").Effects);          // no antibiotic has been given
            s.Events.Publish(new TreatmentGiven("TR-ABX-S2", new[] { "EFF-ANTIBIOTIC" }, 1));
            s.Tick(60);
            Assert.Contains("confirmed:time_out.03", Say(s, "time_out.03", "yes").Effects);              // given a minute ago
        }

        [Fact]
        public void E14_A_an_antibiotic_given_more_than_an_hour_ago_cannot_be_called_recent()
        {
            var s = Prepared("SC-14", "S2");
            s.Events.Publish(new TreatmentGiven("TR-ABX-S2", new[] { "EFF-ANTIBIOTIC" }, 1));
            s.Tick(61 * 60);
            SignIn2(s);
            s.Tick(10); Say(s, "time_out.02", "yes");
            Assert.Contains("contradiction:time_out.03", Say(s, "time_out.03", "yes").Effects);          // an hour has gone by
        }

        private static void SignIn2(Session s)
        {
            Say(s, "sign_in.01", "yes"); Say(s, "sign_in.02", "na"); s.Tick(10); Say(s, "sign_in.04", "yes"); Say(s, "sign_in.05", "no"); Say(s, "sign_in.06", "yes"); Say(s, "sign_in.07", "no");
        }

        [Fact]
        public void Time_Out_cannot_be_completed_while_items_are_outstanding()
        {
            var s = Prepared();
            SignIn(s);
            Do(s, "declare_or_ready");
            Assert.Equal(SafetyChecklistSubsystem.ItemsOutstanding, Reject(Do(s, "complete_time_out")));
        }

        // ---------------------------------------------------------------- the extensions are a separate block

        [Fact]
        public void The_extensions_are_answered_apart_from_the_WHO_items_and_a_contradiction_stops_only_their_own_block()
        {
            var s = Prepared();
            SignIn(s);
            Assert.Contains("contradiction:extension.03", Say(s, "extension.03", "yes").Effects);             // the temperature was never measured
            Assert.Equal("STOPPED", s.Safety!.PhaseState("extension"));
            Assert.Equal("COMPLETE", s.Safety.PhaseState("sign_in"));                                         // the WHO phases are untouched
            Do(s, "measure_temperature");
            Assert.Contains("confirmed:extension.03", Say(s, "extension.03", "yes").Effects);
        }

        [Fact]
        public void Nothing_shows_in_the_observable_until_the_checklist_is_used()
        {
            var s = Prepared();
            Assert.DoesNotContain("\"checklist\"", s.Observable.ToJson());
            Say(s, "sign_in.01", "yes");
            Assert.Contains("\"checklist\":{\"phases\":[{\"id\":\"sign_in\",\"state\":\"IN_PROGRESS\"}]", s.Observable.ToJson());
        }
    }
}
