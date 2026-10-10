using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using ProcSense.Core.Actions;
using ProcSense.Core.Content;
using ProcSense.Core.Observation;
using ProcSense.Core.Pipeline;
using ProcSense.Core.Replay;
using ProcSense.Core.Workflow;

namespace ProcSense.Core.Tests
{
    /// <summary>Goals, deadlines, decision points, blockers and hints (p0-simulation-workflow-s05, plan tests T-CG1 to T-CG6).</summary>
    public class CaseGraphTests
    {
        private readonly Xunit.Abstractions.ITestOutputHelper _out;

        public CaseGraphTests(Xunit.Abstractions.ITestOutputHelper output) => _out = output;

        private static CaseGraph Fixture() =>
            CaseGraph.Parse(File.ReadAllText(Path.Combine(TestCase.Repo, "tests", "fixtures", "workflow", "test_workflow.json")), "test_workflow");

        // The default subsystems, but with the given graph in place of the real workflow content.
        private static Session NewSession(CaseGraph graph, SessionMode mode = SessionMode.Practice, ContentPackage? content = null)
        {
            content ??= TestCase.Package();
            var chain = Subsystems.Default(content).Where(s => !(s is ReadinessSubsystem) && !(s is CaseGraphSubsystem))
                .Concat(new ISubsystem[] { new ReadinessSubsystem(graph), new CaseGraphSubsystem(graph) });
            return new Session(content.Cases["VAR-S1-A"], 42, mode, chain);
        }

        private static Session RealSession(SessionMode mode = SessionMode.Practice)
        {
            var content = TestCase.Package();
            return new Session(content.Cases["VAR-S1-A"], 42, mode, Subsystems.Default(content));
        }

        private static SimAction Act(string verb, params (string k, string v)[] p) =>
            new SimAction(verb, "patient", Channel.Cli, p.ToDictionary(x => x.k, x => x.v));

        private static string StateOf(Session s, string goal) => s.Observable.Goals.Single(g => g.Id == goal).State;

        private static void Confirm(Session s)
        {
            foreach (var verb in new[] { "ask_identity", "read_wristband", "open_notes", "confirm_identity" }) Assert.True(s.Apply(Act(verb)).Accepted, verb);
        }

        // T-CG1: the correct skeleton-1 run completes every goal that applies
        [Fact]
        public void The_correct_run_completes_all_the_goals()
        {
            var s = RealSession();
            var script = ActionScript.Parse(File.ReadAllText(Path.Combine(TestCase.Repo, "tests", "golden", "skeleton1_correct.actions.json")));

            SessionFile.Play(s, script);

            var goals = s.Observable.Goals;
            Assert.Equal(new[] { "G-S1-IDENTITY", "G-S1-ALLERGY", "G-S1-REASON", "G-S1-ADULT", "G-FASTING", "G-EXAM-PERMISSION", "G-EXAM-REGIONS", "G-EXAM-AIRWAY", "G-RISK-ASA", "G-RISK-VTE", "G-ANAES-REVIEW", "G-ANAES-PLAN", "G-READY" }, goals.Select(g => g.Id));   // G-SENIOR-NOW only appears when a rigid abdomen is found
            Assert.All(goals, g => Assert.Equal("complete", g.State));
            Assert.All(goals, g => Assert.NotEqual("", g.At));
        }

        // The plan's check: leave out the allergy question and its goal stays pending
        [Fact]
        public void Leaving_out_the_allergy_question_leaves_that_goal_pending()
        {
            var s = RealSession();
            Confirm(s);
            s.Apply(Act("ask_reason_for_admission"));
            s.Apply(Act("ask_responsible_adult"));

            Assert.Equal("pending", StateOf(s, "G-S1-ALLERGY"));
            Assert.Equal("complete", StateOf(s, "G-S1-IDENTITY"));
            Assert.Equal("complete", StateOf(s, "G-S1-REASON"));
        }

        [Fact]
        public void A_goal_that_does_not_apply_to_the_case_is_not_listed()
        {
            var s = NewSession(Fixture());

            Assert.DoesNotContain(s.Observable.Goals, g => g.Id == "TEST-G-NOT-HERE");
            Assert.Equal(2, s.Observable.Goals.Count);
        }

        [Fact]
        public void A_goal_waits_for_the_goal_it_comes_after()
        {
            var s = NewSession(Fixture());

            s.Apply(Act("ask_reason_for_admission"));   // its own condition is true, but TEST-G-DEADLINE is not done
            Assert.Equal("pending", StateOf(s, "TEST-G-LATER"));

            s.Apply(Act("ask_allergies"));             // blocked until identity: so confirm first
            Confirm(s);
            s.Apply(Act("ask_allergies"));

            Assert.Equal("complete", StateOf(s, "TEST-G-DEADLINE"));
            Assert.Equal("complete", StateOf(s, "TEST-G-LATER"));   // completes in the same pass once its prerequisite is done
        }

        // T-CG2: a circle of "after" edges is a load error that lists the circle
        [Fact]
        public void Goals_that_wait_on_each_other_in_a_circle_are_a_load_error_that_lists_the_circle()
        {
            string Goal(string id, string after) => "{\"id\":\"" + id + "\",\"step\":1,\"label\":\"x\",\"hint\":\"x\",\"applies_if\":true,\"complete_if\":true,\"after\":[\"" + after + "\"],\"on_missed\":{\"flags\":[],\"events\":[]},\"score_items\":[],\"refs\":[\"SR-0001\"]}";
            string json = "{\"goals\":[" + Goal("G-A", "G-B") + "," + Goal("G-B", "G-C") + "," + Goal("G-C", "G-A") + "],\"decision_points\":[],\"blockers\":[]}";

            var e = Assert.Throws<ContentException>(() => CaseGraph.Parse(json, "w"));

            Assert.Contains("G-A -> G-B -> G-C -> G-A", e.Message);
        }

        [Fact]
        public void A_goal_that_waits_for_an_unknown_goal_is_a_load_error()
        {
            string json = File.ReadAllText(Path.Combine(TestCase.Repo, "tests", "fixtures", "workflow", "test_workflow.json")).Replace("\"after\": [ \"TEST-G-DEADLINE\" ]", "\"after\": [ \"G-NOPE\" ]");

            var e = Assert.Throws<ContentException>(() => CaseGraph.Parse(json, "w"));

            Assert.Contains("G-NOPE", e.Message);
        }

        // T-CG3: the deadline is exactly on a tick boundary: deterministic
        [Fact]
        public void An_action_one_step_before_the_deadline_completes_the_goal()
        {
            var s = NewSession(Fixture());
            Confirm(s);
            s.Tick(59.5);

            s.Apply(Act("ask_allergies"));

            Assert.Equal("complete", StateOf(s, "TEST-G-DEADLINE"));
            Assert.False(s.Patient.Clinical.HasFlag("missed.test_goal"));
        }

        [Fact]
        public void An_action_at_the_exact_deadline_is_too_late()
        {
            var s = NewSession(Fixture());
            Confirm(s);
            s.Tick(60);   // the clock reaches the deadline

            s.Apply(Act("ask_allergies"));

            Assert.Equal("missed", StateOf(s, "TEST-G-DEADLINE"));
            Assert.True(s.Patient.Clinical.HasFlag("missed.test_goal"));
        }

        [Fact]
        public void A_missed_goal_raises_its_event_and_is_recorded_for_the_scorer()
        {
            var s = NewSession(Fixture());
            var seen = new List<WorkflowEvent>();
            s.Events.Subscribe<WorkflowEvent>(seen.Add);

            s.Tick(61);

            Assert.Equal(new[] { new WorkflowEvent("E-TEST-MISSED", "TEST-G-DEADLINE") }, seen);
            var recorded = s.GraphHistory.Single();
            Assert.Equal("goal_missed", recorded.Kind);
            Assert.Equal(new[] { "SI-TEST-GOAL" }, recorded.ScoreItems);
        }

        [Fact]
        public void The_same_deadline_gives_the_same_result_by_ticks_or_by_a_time_skip()
        {
            var ticked = NewSession(Fixture());
            var skipped = NewSession(Fixture());

            for (int i = 0; i < 120; i++) ticked.Tick(0.5);
            skipped.TimeSkip(1);

            Assert.Equal("missed", StateOf(ticked, "TEST-G-DEADLINE"));
            Assert.Equal("missed", StateOf(skipped, "TEST-G-DEADLINE"));
            Assert.Equal(ticked.Observable.Hash(), skipped.Observable.Hash());
        }

        // T-CG4: a blocker stops the verb and says why
        [Fact]
        public void A_blocked_verb_is_refused_with_the_blocker_and_its_message_in_practice_mode()
        {
            var s = NewSession(Fixture());

            var outcome = s.Apply(Act("ask_allergies"));

            Assert.Equal("BLOCKED:TEST-B-IDENTITY-FIRST", outcome.RejectReason);
            Assert.Contains("confirm the patient's identity", s.Observable.BlockMessage);
            Assert.False(s.Observable.AllergiesKnown);
        }

        [Fact]
        public void In_assessment_mode_the_block_is_logged_but_the_reason_is_not_shown()
        {
            var s = NewSession(Fixture(), SessionMode.Assessment);

            var outcome = s.Apply(Act("ask_allergies"));

            Assert.Equal("BLOCKED:TEST-B-IDENTITY-FIRST", outcome.RejectReason);
            Assert.Contains("BLOCKED:TEST-B-IDENTITY-FIRST", s.Log.ToJsonl());
            Assert.Equal("", s.Observable.BlockMessage);
        }

        [Fact]
        public void A_blocker_lets_the_verb_through_once_the_condition_is_clear()
        {
            var s = NewSession(Fixture());
            Confirm(s);

            Assert.True(s.Apply(Act("ask_allergies")).Accepted);
        }

        [Fact]
        public void A_blocker_without_a_message_is_refused()
        {
            string json = File.ReadAllText(Path.Combine(TestCase.Repo, "tests", "fixtures", "workflow", "test_workflow.json"))
                .Replace("\"message\": \"Test fixture: confirm the patient's identity before asking about allergies.\"", "\"message\": \"  \"");

            var e = Assert.Throws<ContentException>(() => CaseGraph.Parse(json, "w"));

            Assert.Contains("must say why", e.Message);
        }

        // T-CG5: a decision is logged with the expected and the chosen option
        [Fact]
        public void A_decision_card_appears_when_triggered_and_never_shows_the_expected_option()
        {
            var s = NewSession(Fixture());
            Assert.Empty(s.Observable.Decisions);

            s.Apply(Act("introduce_self"));

            var card = s.Observable.Decisions.Single();
            Assert.Equal("TEST-DP-CHOICE", card.Id);
            Assert.Equal(new[] { "proceed", "ask_first" }, card.Options.Select(o => o.Id));
            Assert.DoesNotContain("expected", s.Observable.ToJson());
        }

        [Fact]
        public void Choosing_an_option_records_expected_against_chosen_and_applies_its_changes()
        {
            var s = NewSession(Fixture());
            var enabled = new List<WorkflowEvent>();
            s.Events.Subscribe<WorkflowEvent>(enabled.Add);
            s.Apply(Act("introduce_self"));

            var outcome = s.Apply(Act("decide", ("dp", "TEST-DP-CHOICE"), ("option", "ask_first")));

            Assert.True(outcome.Accepted);
            Assert.Contains("decision:TEST-DP-CHOICE chose:ask_first expected:ask_first as_expected", outcome.Effects);
            Assert.Contains("score:SI-TEST-ASK", outcome.Effects);
            Assert.True(s.Patient.Clinical.HasFlag("chose.ask_first"));
            Assert.Equal(new[] { new WorkflowEvent("E-TEST-ENABLED", "TEST-DP-CHOICE") }, enabled);
            Assert.Empty(s.Observable.Decisions);
            Assert.Contains("expected:ask_first", s.Log.ToJsonl());
        }

        [Fact]
        public void Choosing_the_other_option_is_recorded_as_not_expected()
        {
            var s = NewSession(Fixture());
            s.Apply(Act("introduce_self"));

            var outcome = s.Apply(Act("decide", ("dp", "TEST-DP-CHOICE"), ("option", "proceed")));

            Assert.Contains("decision:TEST-DP-CHOICE chose:proceed expected:ask_first not_expected", outcome.Effects);
        }

        [Fact]
        public void A_decision_can_only_be_made_once_while_it_is_open_and_with_a_real_option()
        {
            var s = NewSession(Fixture());

            Assert.Equal("DECISION_NOT_OPEN", s.Apply(Act("decide", ("dp", "TEST-DP-CHOICE"), ("option", "proceed"))).RejectReason);   // not triggered yet
            s.Apply(Act("introduce_self"));
            Assert.Equal("UNKNOWN_OPTION", s.Apply(Act("decide", ("dp", "TEST-DP-CHOICE"), ("option", "nope"))).RejectReason);
            Assert.True(s.Apply(Act("decide", ("dp", "TEST-DP-CHOICE"), ("option", "proceed"))).Accepted);
            Assert.Equal("DECISION_NOT_OPEN", s.Apply(Act("decide", ("dp", "TEST-DP-CHOICE"), ("option", "ask_first"))).RejectReason);   // already decided
            Assert.Equal("DECISION_NOT_OPEN", s.Apply(Act("decide", ("dp", "NO-SUCH-DP"), ("option", "proceed"))).RejectReason);
        }

        [Fact]
        public void A_decision_whose_expected_option_does_not_exist_is_a_load_error()
        {
            string json = File.ReadAllText(Path.Combine(TestCase.Repo, "tests", "fixtures", "workflow", "test_workflow.json")).Replace("\"expected\": [ \"ask_first\" ]", "\"expected\": [ \"nope\" ]");

            var e = Assert.Throws<ContentException>(() => CaseGraph.Parse(json, "w"));

            Assert.Contains("'nope' is not one of the options", e.Message);
        }

        // Hints: practice mode only
        [Fact]
        public void The_hint_follows_the_next_goal_in_practice_mode()
        {
            var s = RealSession();
            Assert.Contains("full name and date of birth", s.Observable.Hint);

            Confirm(s);

            Assert.Contains("allergies", s.Observable.Hint);
        }

        [Fact]
        public void Assessment_mode_shows_no_goals_and_no_hints()
        {
            var s = RealSession(SessionMode.Assessment);
            Confirm(s);

            Assert.Empty(s.Observable.Goals);
            Assert.Equal("", s.Observable.Hint);
        }

        [Fact]
        public void The_hint_is_empty_when_every_goal_is_done()
        {
            var s = RealSession();
            var script = ActionScript.Parse(File.ReadAllText(Path.Combine(TestCase.Repo, "tests", "golden", "skeleton1_correct.actions.json")));
            SessionFile.Play(s, script);

            Assert.Equal("", s.Observable.Hint);
        }

        // The graph changes nothing about what a replay produces
        [Fact]
        public void A_session_with_goals_replays_to_the_same_hash()
        {
            var content = TestCase.Package();
            var s = RealSession();
            Confirm(s);
            s.Apply(Act("ask_allergies"));

            var result = SessionFile.Replay(SessionFile.Write(s), content, () => Subsystems.Default(content));

            Assert.True(result.Match);
            Assert.Equal(s.Observable.Hash(), result.Session.Observable.Hash());
        }

        // T-CG6: 200 goals. The plan's budget is 0.3 ms per Apply on a quiet Release build; the guard is looser so a busy runner cannot fail it.
        [Fact]
        public void Two_hundred_goals_are_cheap_per_action()
        {
            var goals = new StringBuilder();
            for (int i = 0; i < 200; i++)
            {
                if (i > 0) goals.Append(',');
                string key = i % 4 == 0 ? "allergies_known" : i % 4 == 1 ? "identity_confirmed" : i % 4 == 2 ? "nurse" : "location";
                goals.Append("{\"id\":\"G-P" + i + "\",\"step\":1,\"label\":\"g\",\"hint\":\"h\",\"applies_if\":true,\"complete_if\":{\"==\":[{\"var\":\"" + key + "\"},\"never\"]},\"after\":[],\"on_missed\":{\"flags\":[],\"events\":[]},\"score_items\":[],\"refs\":[\"SR-0001\"]}");
            }
            var graph = CaseGraph.Parse("{\"goals\":[" + goals + "],\"decision_points\":[],\"blockers\":[]}", "perf");
            var s = NewSession(graph);
            for (int i = 0; i < 100; i++) s.Apply(Act("wait"));

            double best = double.MaxValue;
            for (int run = 0; run < 5; run++)
            {
                var clock = Stopwatch.StartNew();
                for (int i = 0; i < 200; i++) s.Apply(Act("wait"));
                best = Math.Min(best, clock.Elapsed.TotalMilliseconds / 200);
            }

            _out.WriteLine(best.ToString("F4") + " ms per action with 200 goals");
            Assert.True(best < 2.0, best.ToString("F4") + " ms per action with 200 goals");
        }

        [Fact]
        public void A_goal_is_only_re_evaluated_when_a_key_it_reads_changed()
        {
            var read = Logic_ReadKeys("{\"and\":[{\"==\":[{\"var\":\"identity_confirmed\"},true]},{\"in\":[\"x\",{\"var\":\"topics\"}]},{\"some\":[{\"var\":\"flags\"},{\"==\":[{\"var\":\"\"},1]}]}]}");

            Assert.Equal(new[] { "flags", "identity_confirmed", "topics" }, read);
            Assert.Null(Logic_ReadKeys("{\"var\":\"\"}"));          // reads the whole data: cannot be narrowed
            Assert.Null(Logic_ReadKeys("{\"var\":{\"var\":\"k\"}}")); // a computed name: cannot be narrowed
            Assert.Equal(new[] { "a" }, Logic_ReadKeys("{\"some\":[{\"var\":\"a\"},{\"==\":[{\"var\":\"inner\"},1]}]}"));   // the test reads the item, not the data
        }

        private static IReadOnlyList<string>? Logic_ReadKeys(string json) => Rules.Logic.Compile(MiniJson.Parse(json)).ReadKeys;
    }
}
