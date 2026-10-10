using System;
using System.IO;
using System.Linq;
using ProcSense.Core.Actions;
using ProcSense.Core.Content;
using ProcSense.Core.Observation;
using ProcSense.Core.Pipeline;
using ProcSense.Core.Replay;
using ProcSense.Core.Workflow;

namespace ProcSense.Core.Tests
{
    /// <summary>The two scenario master maps run as data: parallel tracks, joins, skipped stages and emergency interrupts (p0-simulation-workflow-s06, EXP-WF-1).</summary>
    public class FlowMapTests
    {
        private readonly Xunit.Abstractions.ITestOutputHelper _out;

        public FlowMapTests(Xunit.Abstractions.ITestOutputHelper output) => _out = output;

        private static FlowMaps RealMaps() => FlowMaps.Parse(File.ReadAllText(Path.Combine(TestCase.Repo, "content", "flow", "flow_maps.json")), "flow_maps");

        private static string Flag(string name) => "{\"in\":[\"" + name + "\",{\"var\":\"flags\"}]}";

        private static string Stage(string id, string track, string after, string done, string extra = "")
        {
            int n = int.Parse(id.Substring(3));
            return "{\"id\":\"" + id + "\",\"stage\":" + n + ",\"title\":\"Stage " + id + "\",\"track\":\"" + track + "\",\"steps\":[1],\"after\":[" + after + "],\"applies_if\":true,\"start_if\":true,"
                + "\"done_if\":" + Flag(done) + ",\"interrupt\":false,\"suspends\":[],\"refs\":[\"SR-1800\"]" + extra + "}";
        }

        private static string Interrupt(string id, string startFlag, string doneFlag) =>
            "{\"id\":\"" + id + "\",\"stage\":" + int.Parse(id.Substring(3)) + ",\"title\":\"Stage " + id + "\",\"track\":\"interrupt\",\"steps\":[8],\"after\":[],\"applies_if\":true,\"start_if\":" + Flag(startFlag)
            + ",\"done_if\":" + Flag(doneFlag) + ",\"interrupt\":true,\"suspends\":[\"patient\"],\"refs\":[\"SR-1800\"]}";

        // A small map that shows every mechanism with flags only: a patient line of 1, 2, 3 (skipped unless branch.abnormal), 4, a theatre track that runs from the start,
        // a join at stage 6 and two interrupts.
        private static string FixtureJson() =>
            "{\"maps\":[{\"scenario\":\"S1\",\"stages\":["
            + Stage("S1.01", "patient", "", "a1") + ","
            + Stage("S1.02", "patient", "\"S1.01\"", "a2") + ","
            + Stage("S1.03", "patient", "\"S1.02\"", "a3").Replace("\"applies_if\":true", "\"applies_if\":" + Flag("branch.abnormal")) + ","
            + Stage("S1.04", "patient", "\"S1.02\",\"S1.03\"", "a4") + ","
            + Stage("S1.05", "theatre", "", "t5") + ","
            + Stage("S1.06", "patient", "\"S1.04\",\"S1.05\"", "a6") + ","
            + Interrupt("S1.18", "event.deterioration", "r18") + ","
            + Interrupt("S1.19", "event.emergency", "r19")
            + "]}]}";

        private static Session Fixture(SessionMode mode = SessionMode.Practice)
        {
            var content = TestCase.Package();
            var maps = FlowMaps.Parse(FixtureJson(), "fixture");
            var chain = Subsystems.Default(content).Where(x => !(x is FlowSubsystem)).Concat(new ISubsystem[] { new FlowSubsystem(maps) });
            return new Session(content.Cases["VAR-S1-A"], 42, mode, chain);
        }

        // Flags are only looked at after an accepted action, so a test sets them and then lets one second pass.
        private static void Raise(Session s, params string[] flags)
        {
            foreach (var f in flags) s.Patient.Clinical.SetFlag(f);
            Assert.True(s.Apply(new SimAction("wait", "patient", Channel.Cli, new System.Collections.Generic.Dictionary<string, string>())).Accepted);
        }

        private static string State(Session s, string id) => s.FlowStages.Single(x => x.Id == id).State;

        private static string[] Running(Session s) => s.Observable.Flow.Select(x => x.Id + ":" + x.State).ToArray();

        // ---------------------------------------------------------------- the real maps

        // EXP-WF-1: both 20-stage maps are data. Parsing compiles every rule through the same JsonLogic evaluator, so a stage that needed its own code could not load.
        [Fact]
        public void Both_real_maps_load_with_twenty_stages_each_and_need_no_custom_code()
        {
            var maps = RealMaps();
            Assert.Equal(new[] { "S1", "S2" }, maps.Maps.Select(m => m.Scenario));
            foreach (var m in maps.Maps)
            {
                Assert.Equal(Enumerable.Range(1, 20), m.Stages.Select(x => x.Number));
                Assert.Equal(20, m.Stages.Select(x => x.Id).Distinct().Count());
            }
            int stages = maps.Maps.Sum(m => m.Stages.Count);
            _out.WriteLine($"EXP-WF-1: {stages} stages encoded as data, 0 stages needed custom code.");
            Assert.Equal(40, stages);
        }

        // Every walkthrough step 1 to 14 is worked through by some stage of each map (step 15 belongs to the P1 theatre extension but is mapped too).
        [Fact]
        public void Every_step_is_covered_by_a_stage_in_each_map()
        {
            foreach (var m in RealMaps().Maps)
            {
                var covered = m.Stages.SelectMany(x => x.Steps).ToHashSet();
                Assert.All(Enumerable.Range(1, 15), step => Assert.Contains(step, covered));
            }
        }

        [Fact]
        public void The_real_maps_have_the_parallel_tracks_the_plan_describes()
        {
            var s1 = RealMaps().For("S1")!.Stages;
            var s2 = RealMaps().For("S2")!.Stages;
            Assert.Equal("theatre", s1.Single(x => x.Id == "S1.12").Track);
            Assert.Empty(s1.Single(x => x.Id == "S1.12").After);                     // S1: theatre preparation runs from the start, before the patient arrives
            Assert.Contains("S1.12", s1.Single(x => x.Id == "S1.13").After);         // Time Out waits for it
            Assert.Equal("optimisation", s2.Single(x => x.Id == "S2.08").Track);
            Assert.Equal(new[] { "S2.05" }, s2.Single(x => x.Id == "S2.08").After);  // S2: optimisation starts once the tests are sent and runs while results are pending
            Assert.Equal(new[] { "S2.07" }, s2.Single(x => x.Id == "S2.09").After);  // consent does not wait for optimisation
            foreach (var m in new[] { s1, s2 })
                Assert.All(m.Where(x => x.Number is 18 or 19), x => { Assert.True(x.Interrupt); Assert.Equal(new[] { "patient" }, x.Suspends); });
        }

        // The correct skeleton-1 run finishes stages 1 to 4 with real facts (wristband, identity, allergies, history topics, sensors and readings), and the theatre track is already running.
        [Fact]
        public void The_correct_run_moves_the_patient_line_to_investigations_while_the_theatre_track_runs()
        {
            var content = TestCase.Package();
            var s = new Session(content.Cases["VAR-S1-A"], 42, SessionMode.Practice, Subsystems.Default(content));
            Assert.Contains("S1.01:active", Running(s));
            Assert.Contains("S1.12:active", Running(s));                             // parallel from minute 0

            SessionFile.Play(s, ActionScript.Parse(File.ReadAllText(Path.Combine(TestCase.Repo, "tests", "golden", "skeleton1_correct.actions.json"))));

            Assert.All(new[] { "S1.01", "S1.02", "S1.03", "S1.04" }, id => Assert.Equal("done", State(s, id)));
            Assert.Equal(new[] { "S1.05:active", "S1.12:active" }, Running(s));
            Assert.Equal("waiting", State(s, "S1.13"));                              // Time Out cannot start before Sign In and a ready theatre
            Assert.Equal(new[] { "stage_start", "stage_start", "stage_done", "stage_start", "stage_done" }, s.FlowHistory.Take(5).Select(x => x.Kind));
        }

        // ---------------------------------------------------------------- mechanics (fixture map)

        [Fact]
        public void The_patient_line_and_the_theatre_track_run_at_the_same_time()
        {
            var s = Fixture();
            Assert.Equal(new[] { "S1.01:active", "S1.05:active" }, Running(s));
            Raise(s, "a1");
            Assert.Equal(new[] { "S1.02:active", "S1.05:active" }, Running(s));
            Raise(s, "t5");                                                          // the theatre finishes while the patient is still being assessed
            Assert.Equal("done", State(s, "S1.05"));
            Assert.Equal(new[] { "S1.02:active" }, Running(s));
        }

        [Fact]
        public void A_stage_that_waits_on_two_tracks_starts_only_when_both_are_finished()
        {
            var s = Fixture();
            Raise(s, "a1", "a2", "a4", "branch.abnormal", "a3");
            Assert.Equal("waiting", State(s, "S1.06"));                              // the patient line is ready, the theatre is not
            Raise(s, "t5");
            Assert.Equal("active", State(s, "S1.06"));
        }

        [Fact]
        public void A_stage_that_does_not_apply_is_skipped_and_the_line_moves_on()
        {
            var s = Fixture();
            Raise(s, "a1", "a2");
            Assert.Equal("skipped", State(s, "S1.03"));
            Assert.Equal("active", State(s, "S1.04"));
            Assert.Contains("stage_skipped", s.FlowHistory.Select(x => x.Kind));
        }

        [Fact]
        public void An_abnormal_branch_stage_runs_when_the_branch_applies()
        {
            var s = Fixture();
            Raise(s, "branch.abnormal", "a1", "a2");
            Assert.Equal("active", State(s, "S1.03"));
            Assert.Equal("waiting", State(s, "S1.04"));
        }

        // T-CG9 equivalent: an emergency stops the patient line and returns to the stage that was running
        [Fact]
        public void An_emergency_interrupt_stops_the_patient_line_and_hands_it_back_to_the_stage_that_was_running()
        {
            var s = Fixture();
            Raise(s, "a1", "a2");
            Assert.Equal("active", State(s, "S1.04"));

            Raise(s, "event.deterioration");
            Assert.Equal(new[] { "S1.18:active", "S1.04:suspended", "S1.05:active" }, Running(s));   // the theatre track is not stopped

            Raise(s, "a4");                                                          // finished in the world, but a stopped stage cannot finish
            Assert.Equal("suspended", Running(s).Single(x => x.StartsWith("S1.04")).Split(':')[1]);

            Raise(s, "t5");                                                          // the theatre carries on during the emergency
            Assert.Equal("done", State(s, "S1.05"));

            Raise(s, "r18");                                                         // resolved: control returns, and the line carries on from where it was
            Assert.Equal("done", State(s, "S1.18"));
            Assert.Equal("done", State(s, "S1.04"));
            Assert.Equal("active", State(s, "S1.06"));
            Assert.Equal(new[] { "interrupt_raised", "interrupt_resolved" }, s.FlowHistory.Where(x => x.Id == "S1.18").Select(x => x.Kind));
        }

        [Fact]
        public void An_emergency_during_a_deterioration_is_handled_first_and_the_line_waits_for_both()
        {
            var s = Fixture();
            Raise(s, "a1", "event.deterioration");
            Raise(s, "event.emergency");
            Assert.Equal(new[] { "S1.18:active", "S1.19:active", "S1.02:suspended", "S1.05:active" }, Running(s));

            Raise(s, "r19");
            Assert.Equal("suspended", Running(s).Single(x => x.StartsWith("S1.02")).Split(':')[1]);   // the deterioration is still open
            Raise(s, "r18", "a2");
            Assert.Equal("done", State(s, "S1.02"));
        }

        // The trainee is not told what is left in assessment mode; the engine still tracks it for the scorer
        [Fact]
        public void Assessment_mode_shows_the_trainee_no_flow_but_the_engine_still_tracks_it()
        {
            var s = Fixture(SessionMode.Assessment);
            Raise(s, "a1");
            Assert.Empty(s.Observable.Flow);
            Assert.Equal("done", State(s, "S1.01"));
            Assert.Equal("active", State(s, "S1.02"));
        }

        // A session with no map for its scenario simply has no flow
        [Fact]
        public void A_scenario_without_a_map_has_no_flow()
        {
            var content = TestCase.Package();
            var chain = Subsystems.Default(content).Where(x => !(x is FlowSubsystem)).Concat(new ISubsystem[] { new FlowSubsystem(FlowMaps.Empty) });
            var s = new Session(content.Cases["VAR-S1-A"], 42, SessionMode.Practice, chain);
            Assert.Empty(s.FlowStages);
            Assert.Empty(s.Observable.Flow);
        }

        [Fact]
        public void The_same_actions_give_the_same_flow_every_time()
        {
            string Run()
            {
                var content = TestCase.Package();
                var s = new Session(content.Cases["VAR-S1-A"], 42, SessionMode.Practice, Subsystems.Default(content));
                SessionFile.Play(s, ActionScript.Parse(File.ReadAllText(Path.Combine(TestCase.Repo, "tests", "golden", "skeleton1_correct.actions.json"))));
                return s.Observable.Hash() + "|" + string.Join(",", s.FlowHistory.Select(x => x.Kind + x.Id + x.Step));
            }
            Assert.Equal(Run(), Run());
        }

        // ---------------------------------------------------------------- loading errors name the place

        private static string Fault(string json)
        {
            var e = Assert.Throws<ContentException>(() => FlowMaps.Parse(json, "flow"));
            return e.Message;
        }

        private static string OneStage(string stageJson) => "{\"maps\":[{\"scenario\":\"S1\",\"stages\":[" + stageJson + "]}]}";

        [Fact]
        public void An_unknown_field_is_refused_with_its_place()
        {
            Assert.Contains("flow.maps[0].stages[0].colour: unknown field", Fault(OneStage(Stage("S1.01", "patient", "", "a1", ",\"colour\":\"red\""))));
        }

        [Fact]
        public void A_stage_that_waits_on_a_stage_that_does_not_exist_is_refused()
        {
            Assert.Contains("stage 'S1.09' does not exist", Fault(OneStage(Stage("S1.01", "patient", "\"S1.09\"", "a1"))));
        }

        [Fact]
        public void A_stage_that_waits_on_a_later_stage_is_refused_so_no_circle_can_exist()
        {
            var json = "{\"maps\":[{\"scenario\":\"S1\",\"stages\":[" + Stage("S1.01", "patient", "\"S1.02\"", "a1") + "," + Stage("S1.02", "patient", "", "a2") + "]}]}";
            Assert.Contains("comes later in the map", Fault(json));
        }

        [Fact]
        public void Stages_out_of_order_or_duplicated_are_refused()
        {
            Assert.Contains("out of order", Fault("{\"maps\":[{\"scenario\":\"S1\",\"stages\":[" + Stage("S1.02", "patient", "", "a2") + "," + Stage("S1.01", "patient", "", "a1") + "]}]}"));
            Assert.Contains("duplicate stage 'S1.01'", Fault("{\"maps\":[{\"scenario\":\"S1\",\"stages\":[" + Stage("S1.01", "patient", "", "a1") + "," + Stage("S1.01", "patient", "", "a1") + "]}]}"));
        }

        [Fact]
        public void A_step_outside_one_to_fifteen_is_refused()
        {
            Assert.Contains("must be 1 to 15", Fault(OneStage(Stage("S1.01", "patient", "", "a1").Replace("\"steps\":[1]", "\"steps\":[16]"))));
            Assert.Contains("needs at least one step", Fault(OneStage(Stage("S1.01", "patient", "", "a1").Replace("\"steps\":[1]", "\"steps\":[]"))));
        }

        [Fact]
        public void An_interrupt_must_stop_something_and_only_an_interrupt_may()
        {
            Assert.Contains("must stop at least one track", Fault(OneStage(Interrupt("S1.18", "x", "y").Replace("\"suspends\":[\"patient\"]", "\"suspends\":[]"))));
            Assert.Contains("only an interrupt stage suspends", Fault(OneStage(Stage("S1.01", "patient", "", "a1").Replace("\"suspends\":[]", "\"suspends\":[\"patient\"]"))));
        }

        [Fact]
        public void A_stage_whose_number_does_not_match_its_id_or_scenario_is_refused()
        {
            Assert.Contains("does not match the id", Fault(OneStage(Stage("S1.01", "patient", "", "a1").Replace("\"stage\":1", "\"stage\":2"))));
            Assert.Contains("must look like S1.07", Fault(OneStage(Stage("S2.01", "patient", "", "a1"))));
        }

        [Fact]
        public void A_stage_without_a_source_register_id_is_refused()
        {
            Assert.Contains("needs at least one entry", Fault(OneStage(Stage("S1.01", "patient", "", "a1").Replace("\"refs\":[\"SR-1800\"]", "\"refs\":[]"))));
            Assert.Contains("expected a register id", Fault(OneStage(Stage("S1.01", "patient", "", "a1").Replace("SR-1800", "nope"))));
        }

        [Fact]
        public void A_rule_that_is_not_valid_JsonLogic_is_refused_with_its_place()
        {
            Assert.Contains("done_if", Fault(OneStage(Stage("S1.01", "patient", "", "a1").Replace(Flag("a1"), "{\"nonsense\":[1]}"))));
        }
    }
}
