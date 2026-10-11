using System.Collections.Generic;
using System.Linq;
using ProcSense.Core.Actions;
using ProcSense.Core.Content;
using ProcSense.Core.Pipeline;
using ProcSense.Core.Workflow;

namespace ProcSense.Core.Tests
{
    /// <summary>Workflow Step 11: theatre preparation and OR readiness (p0-simulation-workflow-s14, plan tests T11-1, T11-2 and T-SC4).</summary>
    public class OrReadinessTests
    {
        private static readonly ContentPackage Content = TestCase.Package();
        private static OrTables Tables => Content.Or;

        private static CaseFile File_(string id) => ContentLoader.LoadCaseFile(new FileContentReader(TestCase.ContentRoot), id);

        private static SimAction Act(string verb, params (string k, string v)[] p) => new SimAction(verb, "patient", Channel.Cli, p.ToDictionary(x => x.k, x => x.v));

        private static ActionOutcome Do(Session s, string verb, params (string k, string v)[] p) => s.Apply(Act(verb, p));

        private static string Reject(ActionOutcome o) { Assert.False(o.Accepted); return o.RejectReason; }

        private static Session Start(string caseId = "VAR-S1-A", string? scenario = null)
        {
            var file = File_(caseId);
            var chain = Subsystems.Default(Content).Where(x => !(x is OrReadinessSubsystem)).Concat(new ISubsystem[] { new OrReadinessSubsystem(Content, file) });
            var card = Content.Cases["VAR-S1-A"] with { Scenario = scenario ?? file.Scenario };
            return new Session(card, 42, SessionMode.Practice, chain);
        }

        private static void CheckAll(Session s) { foreach (var st in Tables.Stations.Where(x => x.Owner == "trainee")) Assert.True(Do(s, "check_station", ("station", st.Id)).Accepted, st.Id); }

        // ---------------------------------------------------------------- the content

        [Fact]
        public void The_content_has_the_twelve_stations_of_Step_11()
        {
            Assert.Equal(12, Tables.Stations.Count);
            Assert.Equal(new[] { "anaesthetic_machine", "monitors", "suction" }, Tables.Stations.Where(s => s.Critical).Select(s => s.Id));      // the walkthrough's 'seriously concerning' list
            Assert.Equal("team_brief", Tables.Stations.Single(s => !s.Required).Id);
            Assert.Equal("lap_tower", Tables.Faults["SC-18"].Station);
            Assert.Equal(15, Tables.ReplacementDelayMinutes);
        }

        // ---------------------------------------------------------------- checking (L11-01)

        [Fact]
        public void A_check_needs_a_known_station()
        {
            var s = Start();
            Assert.Equal(OrReadinessSubsystem.MissingStation, Reject(Do(s, "check_station")));
            Assert.Equal(OrReadinessSubsystem.UnknownStation, Reject(Do(s, "check_station", ("station", "kettle"))));
        }

        [Fact]
        public void Nothing_shows_until_the_theatre_step_runs_and_then_the_stations_appear_with_their_state()
        {
            var s = Start();
            Assert.DoesNotContain("\"or\"", s.Observable.ToJson());
            var o = Do(s, "check_station", ("station", "suction"));
            Assert.Contains("station:suction:ready", o.Effects);
            var json = s.Observable.ToJson();
            Assert.Contains("\"or\":[", json);
            Assert.Equal("ready", s.Or!.StateOf("suction"));
            Assert.Equal("unchecked", s.Or.StateOf("monitors"));                                // the trainee has not looked yet
            Assert.Equal("ready", s.Or.StateOf("table"));                                       // the theatre staff's word
        }

        // T11-1: every station checked, then declared ready
        [Fact]
        public void T11_1_VAR_S1_A_checks_every_station_and_declares_the_theatre_ready()
        {
            var s = Start();
            CheckAll(s);
            var o = Do(s, "declare_or_ready");
            Assert.Contains("or_ready", o.Effects);
            Assert.True(s.Or!.Ready);
            Assert.Contains(s.Or.History, h => h.Kind == "or_ready" && h.ScoreItems.Contains("credit.or_ready"));
        }

        // T11-2: a declaration with something wrong
        [Fact]
        public void T11_2_declaring_ready_with_stations_unchecked_is_a_major_item_and_time_out_stays_blocked()
        {
            var s = Start();
            Do(s, "check_station", ("station", "table"));
            var o = Do(s, "declare_or_ready");
            Assert.Contains(o.Effects, e => e.StartsWith("not_ready:") && e.Contains("suction"));
            Assert.False(s.Or!.Ready);
            var h = s.Or.History.Single(x => x.Kind == "declared_not_ready");
            Assert.Contains("major.or_declared_ready_not_ready", h.ScoreItems);
            Assert.Equal(3, h.ScoreItems.Count(i => i == "major.or_critical_station_unchecked"));     // machine, monitors and suction were never looked at
            Assert.Contains(Content.Workflow.Blockers, b => b.Id == "B-OR" && b.CheckAt.Contains("complete_time_out"));
        }

        // ---------------------------------------------------------------- faults (L11-02, E11-A) and T-SC4

        [Fact]
        public void T_SC4_the_insufflator_fault_of_SC_18_shows_when_the_tower_is_checked_and_a_replacement_comes_after_the_delay()
        {
            var s = Start("SC-18");
            Assert.Equal("unchecked", s.Or!.StateOf("lap_tower"));
            var o = Do(s, "check_station", ("station", "lap_tower"));
            Assert.Contains("station:lap_tower:fault", o.Effects);
            Assert.Contains("call:senior", o.Effects);
            var r = Do(s, "report_fault", ("station", "lap_tower"));
            Assert.Contains("replacement_in_minutes:15", r.Effects);
            Assert.Equal("replacing", s.Or.StateOf("lap_tower"));
            s.Tick(14 * 60);
            Assert.Equal("replacing", s.Or.StateOf("lap_tower"));
            s.Tick(60);
            Assert.Equal("ready", s.Or.StateOf("lap_tower"));
            Assert.Contains(s.Or.History, h => h.Kind == "fault_reported" && h.ScoreItems.Contains("credit.or_fault_reported"));
        }

        [Fact]
        public void E11_A_an_insufflator_fault_that_is_ignored_is_a_major_item_and_the_theatre_is_not_ready()
        {
            var s = Start("SC-18");
            CheckAll(s);                                                                       // the red panel is there to see
            Assert.Equal("fault", s.Or!.StateOf("lap_tower"));
            Do(s, "declare_or_ready");
            Assert.False(s.Or.Ready);
            Assert.Contains("major.or_fault_ignored", s.Or.History.Single(x => x.Kind == "declared_not_ready").ScoreItems);
        }

        [Fact]
        public void After_reporting_and_replacing_the_theatre_can_be_declared_ready()
        {
            var s = Start("SC-18");
            CheckAll(s);
            Do(s, "report_fault", ("station", "lap_tower"));
            Do(s, "declare_or_ready");
            Assert.False(s.Or!.Ready);                                                         // still being replaced
            s.Tick(15 * 60);
            Do(s, "declare_or_ready");
            Assert.True(s.Or.Ready);
        }

        [Fact]
        public void Reporting_a_station_with_no_fault_finds_nothing()
        {
            var s = Start();
            Assert.Contains("no_fault_found:suction", Do(s, "report_fault", ("station", "suction")).Effects);
        }

        [Fact]
        public void A_fault_that_comes_from_an_event_stops_a_theatre_that_was_ready()
        {
            var s = Start();
            CheckAll(s);
            Do(s, "declare_or_ready");
            Assert.True(s.Or!.Ready);
            s.Events.Publish(new WorkflowEvent("E-OR-FAULT", "suction"));
            Assert.Equal("fault", s.Or.StateOf("suction"));
            Assert.False(s.Or.Ready);
        }

        // ---------------------------------------------------------------- the substitute theatre

        [Fact]
        public void An_emergency_case_may_use_a_substitute_theatre_and_a_planned_one_may_not()
        {
            var s = Start("SC-18", "S2");
            CheckAll(s);
            Assert.True(Do(s, "use_substitute_theatre").Accepted);
            Assert.Equal("ready", s.Or!.StateOf("lap_tower"));
            Assert.Equal(OrReadinessSubsystem.NotAllowed, Reject(Do(Start("SC-18", "S1"), "use_substitute_theatre")));
        }
    }
}
