using System.Collections.Generic;
using System.Linq;
using ProcSense.Core.Actions;
using ProcSense.Core.Content;
using ProcSense.Core.Pipeline;
using ProcSense.Core.Workflow;
using ProcSense.Core.Workflow.Steps;

namespace ProcSense.Core.Tests
{
    /// <summary>The theatre extension after Sign Out (p1-theatre-extension-s02 and s03, walkthrough C.22 and C.24): the intra-operative training events and the handover to the recovery nurse, both ProcSense educational extensions.</summary>
    public class TheatreExtensionTests
    {
        private static readonly ContentPackage Content = TestCase.Package();

        private static SimAction Act(string verb, params (string k, string v)[] p) => new SimAction(verb, "patient", Channel.Cli, p.ToDictionary(x => x.k, x => x.v));

        private static ActionOutcome Do(Session s, string verb, params (string k, string v)[] p) => s.Apply(Act(verb, p));

        private static ActionOutcome Say(Session s, string item, string response) => Do(s, "checklist_item", ("phase", item.Substring(0, item.LastIndexOf('.'))), ("item", item), ("response", response));

        // a session whose Time Out is complete (the planned VAR-S1-A, every theatre station checked, the right answers in order)
        private static Session AfterTimeOut(string caseId = "VAR-S1-A", ulong seed = 42, SessionMode mode = SessionMode.Practice)
        {
            var file = ContentLoader.LoadCaseFile(new FileContentReader(TestCase.ContentRoot), caseId);
            var chain = Subsystems.Default(Content).Where(x => !(x is OrReadinessSubsystem)).Concat(new ISubsystem[] { new OrReadinessSubsystem(Content, file) });
            var s = new Session(Content.Cases[caseId], seed, mode, chain);
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

        // ... and one whose Sign Out is complete too, with every concern briefed
        private static Session AfterSignOut()
        {
            var s = AfterTimeOut();
            Assert.True(Do(s, "ask_allergies").Accepted);
            s.Patient.Clinical.SetFlag("news2.computed");                                                // the observations were taken and scored
            Do(s, "record_procedure");
            foreach (var k in new[] { "instruments", "sponges", "needles" }) Assert.True(Do(s, "count_check", ("kind", k), ("result", "correct")).Accepted);
            Assert.True(Do(s, "read_specimen_label", ("name", s.Case.Identity.FullName)).Accepted);
            Assert.True(Do(s, "begin_sign_out").Accepted);
            for (int i = 0; i < 6; i++) s.Tick(10);
            Do(s, "brief_recovery");
            Say(s, "sign_out.05", "yes");
            Assert.Equal("COMPLETE", s.Safety!.PhaseState("sign_out"));
            return s;
        }

        private static string Reject(ActionOutcome o) { Assert.False(o.Accepted); return o.RejectReason; }

        private static ActionOutcome Hand(Session s, string section, string items) => Do(s, RecoveryHandoverSubsystem.HandoverVerb, ("section", section), ("items", items));

        private static void HandOverEverything(Session s)
        {
            foreach (var section in new[] { "s", "b", "a", "r" })
                Assert.True(Hand(s, section, string.Join(",", s.RecoveryHandover!.Required().Where(i => i.Section == section).Select(i => i.Id))).Accepted, section);
        }

        // ---- the handover to the recovery nurse

        [Fact]
        public void The_extension_content_loads_and_the_items_are_sourced()
        {
            Assert.Equal(12, Content.RecoveryHandover.Items.Count);
            Assert.Equal(4, Content.IntraOp.Events.Count);
            Assert.All(Content.IntraOp.Events, e => { Assert.Equal("ProcSense educational extension", e.Label); Assert.True(e.PracticeOnly); Assert.Equal("awaiting_clinician", e.Approval); });
        }

        [Fact]
        public void The_recovery_handover_waits_for_the_sign_out()
        {
            var s = AfterTimeOut();
            Assert.Equal(RecoveryHandoverSubsystem.SignOutNotComplete, Reject(Hand(s, "s", "identity")));
            Assert.Equal(RecoveryHandoverSubsystem.SignOutNotComplete, Reject(Do(s, RecoveryHandoverSubsystem.FinishVerb)));
        }

        [Fact]
        public void A_complete_recovery_handover_is_credited_once()
        {
            var s = AfterSignOut();
            HandOverEverything(s);
            var done = Do(s, RecoveryHandoverSubsystem.FinishVerb);
            Assert.Contains("recovery_handover_complete", done.Effects);
            Assert.True(s.RecoveryHandover!.Complete);
            Assert.Contains(s.RecoveryHandover.History, h => h.Kind == "recovery_handover_complete" && h.ScoreItems.Contains("credit.recovery_handover_complete"));
            Assert.Equal(RecoveryHandoverSubsystem.AlreadyHandedOver, Reject(Do(s, RecoveryHandoverSubsystem.FinishVerb)));
        }

        [Fact]
        public void The_trainee_cannot_hand_over_what_they_do_not_know_yet()
        {
            var s = AfterTimeOut();
            Do(s, "record_procedure");
            foreach (var k in new[] { "instruments", "sponges", "needles" }) Do(s, "count_check", ("kind", k), ("result", "correct"));
            Do(s, "read_specimen_label", ("name", s.Case.Identity.FullName));
            Do(s, "begin_sign_out");
            for (int i = 0; i < 6; i++) s.Tick(10);
            Assert.False(Do(s, "begin_sign_out").Accepted);
            // the recovery briefing has not been given: the plans are not known, and the Sign Out is not complete either
            Assert.Equal(RecoveryHandoverSubsystem.SignOutNotComplete, Reject(Hand(s, "a", "antibiotic_plan")));
        }

        [Fact]
        public void Leaving_out_the_antibiotic_plan_is_a_major_omission_and_the_recovery_nurse_asks_once()
        {
            var s = AfterSignOut();
            foreach (var section in new[] { "s", "b", "a", "r" })
                Hand(s, section, string.Join(",", s.RecoveryHandover!.Required().Where(i => i.Section == section && i.Id != "antibiotic_plan").Select(i => i.Id)));
            var first = Do(s, RecoveryHandoverSubsystem.FinishVerb);
            Assert.Contains("missing:antibiotic_plan", first.Effects);
            Assert.Contains("recovery_nurse_asks:antibiotic_plan", first.Effects);
            Assert.Contains(s.RecoveryHandover!.History, h => h.Kind == "recovery_handover_incomplete" && h.ScoreItems.Contains("major.recovery_handover_omitted"));
            var again = Do(s, RecoveryHandoverSubsystem.FinishVerb);                                   // left it out a second time after being asked
            Assert.Contains(s.RecoveryHandover.History.SelectMany(h => h.ScoreItems), x => x == "major.recovery_handover_repeat_omission");
            Assert.Contains("missing:antibiotic_plan", again.Effects);
            Assert.True(Hand(s, "a", "antibiotic_plan").Accepted);                                   // giving it now finishes the handover
            Assert.Contains("recovery_handover_complete", Do(s, RecoveryHandoverSubsystem.FinishVerb).Effects);
        }

        [Fact]
        public void Leaving_out_the_allergies_is_a_critical_omission()
        {
            var s = AfterSignOut();
            foreach (var section in new[] { "s", "b", "a", "r" })
                Hand(s, section, string.Join(",", s.RecoveryHandover!.Required().Where(i => i.Section == section && i.Id != "allergies").Select(i => i.Id)));
            Do(s, RecoveryHandoverSubsystem.FinishVerb);
            Assert.Contains(s.RecoveryHandover!.History.SelectMany(h => h.ScoreItems), x => x == "critical.recovery_handover_omitted");
        }

        [Fact]
        public void The_recovery_handover_is_scored_with_the_session()
        {
            var s = AfterSignOut();
            HandOverEverything(s);
            Do(s, RecoveryHandoverSubsystem.FinishVerb);
            Assert.Contains("credit.recovery_handover_complete", s.EndStates!.AllItems());
        }

        // ---- the intra-operative events

        [Fact]
        public void An_intra_operative_event_waits_for_the_time_out_and_plays_once()
        {
            var early = new Session(Content.Cases["VAR-S1-A"], 42, SessionMode.Practice, Subsystems.Default(Content));
            foreach (var verb in new[] { "ask_identity", "read_wristband", "open_notes", "confirm_identity" }) Do(early, verb);
            Assert.Equal(IntraOpSubsystem.TimeOutNotDone, Reject(Do(early, IntraOpSubsystem.BeginVerb)));
            var s = AfterTimeOut();
            var o = Do(s, IntraOpSubsystem.BeginVerb);
            Assert.True(o.Accepted);
            Assert.Contains("label:ProcSense educational extension", o.Effects);
            Assert.True(s.IntraOp!.Pending);
            Assert.Equal(IntraOpSubsystem.AlreadyPlayed, Reject(Do(s, IntraOpSubsystem.BeginVerb)));
        }

        [Fact]
        public void A_teamwork_answer_is_credited_a_wrong_one_is_a_minor_item()
        {
            var s = AfterTimeOut();
            Do(s, IntraOpSubsystem.BeginVerb);
            var ev = s.IntraOp!.Event!;
            Assert.Equal(IntraOpSubsystem.MissingResponse, Reject(Do(s, IntraOpSubsystem.RespondVerb)));
            Assert.Equal(IntraOpSubsystem.UnknownResponse, Reject(Do(s, IntraOpSubsystem.RespondVerb, ("response", "nope"))));
            Assert.True(Do(s, IntraOpSubsystem.RespondVerb, ("response", ev.Responses.First(r => r.Correct).Id)).Accepted);
            Assert.Contains(s.IntraOp.History, h => h.Kind == "intraop_response" && h.ScoreItems.Contains("credit.intraop_response"));
            Assert.False(s.IntraOp.Pending);
            Assert.Equal(IntraOpSubsystem.NoEventPending, Reject(Do(s, IntraOpSubsystem.RespondVerb, ("response", ev.Responses[0].Id))));

            var w = AfterTimeOut();
            Do(w, IntraOpSubsystem.BeginVerb);
            Do(w, IntraOpSubsystem.RespondVerb, ("response", w.IntraOp!.Event!.Responses.First(r => !r.Correct).Id));
            Assert.Contains(w.IntraOp.History.SelectMany(h => h.ScoreItems), x => x == "minor.intraop_wrong_response");
        }

        [Fact]
        public void Freezing_is_a_major_item_once_the_time_has_run_out()
        {
            var s = AfterTimeOut();
            Do(s, IntraOpSubsystem.BeginVerb);
            int deadline = s.IntraOp!.Event!.DeadlineSteps;
            s.Tick(0.5 * (deadline - 1));                                                                // a step is half a second
            Assert.True(s.IntraOp.Pending);
            s.Tick(1.0);
            Assert.False(s.IntraOp.Pending);
            Assert.Contains(s.IntraOp.History.SelectMany(h => h.ScoreItems), x => x == "major.intraop_no_response");
            Assert.Contains("major.intraop_no_response", s.EndStates!.AllItems());
        }

        [Fact]
        public void The_event_is_chosen_by_the_seed_from_those_that_apply_and_never_in_assessment()
        {
            var ids = new HashSet<string>();
            for (ulong seed = 1; seed <= 12; seed++)
            {
                var s = AfterTimeOut("VAR-S1-A", seed);
                Do(s, IntraOpSubsystem.BeginVerb);
                Assert.Contains("S1", s.IntraOp!.Event!.AppliesTo);
                ids.Add(s.IntraOp.Event.Id);
                var again = AfterTimeOut("VAR-S1-A", seed);
                Do(again, IntraOpSubsystem.BeginVerb);
                Assert.Equal(s.IntraOp.Event.Id, again.IntraOp!.Event!.Id);             // a replay plays the same event
            }
            Assert.True(ids.Count > 1);
            var assessed = AfterTimeOut("VAR-S1-A", 42, SessionMode.Assessment);
            Assert.Equal(IntraOpSubsystem.NotInAssessment, Reject(Do(assessed, IntraOpSubsystem.BeginVerb)));
        }
    }
}
