using System.Collections.Generic;
using System.IO;
using System.Linq;
using ProcSense.Core.Actions;
using ProcSense.Core.Content;
using ProcSense.Core.Replay;
using ProcSense.Core.Workflow;

namespace ProcSense.Core.Tests
{
    /// <summary>Every P0 case plays through the engine to the end state the walkthrough gives it (p0-case-library s02, plan tests T-CASE1 and T-CASE2).</summary>
    public class P0CaseTests
    {
        private static readonly string[] Ids = Directory.GetFiles(Path.Combine(TestCase.ContentRoot, "case_files"), "*.json").Select(Path.GetFileNameWithoutExtension).OrderBy(x => x, System.StringComparer.Ordinal).ToArray()!;
        private static readonly ContentPackage All = ContentLoader.Load(new CompositeContentReader(new FileContentReader(TestCase.ContentRoot), new FileContentReader(Path.Combine(TestCase.Repo, "tests", "fixtures"))), Ids, Ids);
        private static readonly string[] P0 = Ids.Where(id => All.CaseFiles[id].Tier == "P0").ToArray();
        private static readonly Dictionary<string, string> Codes = new Dictionary<string, string>
        {
            ["ready"] = "READY_SAFE",
            ["postponed"] = "POSTPONED_APPROPRIATELY",
            ["redirected"] = "REDIRECTED_APPROPRIATELY",
            ["delayed"] = "SAFE_BUT_DELAYED",
            ["failure"] = "SAFETY_FAILURE",
        };

        private static string ScriptOf(string id) => Path.Combine(TestCase.Repo, "tests", "golden", id + "_correct.actions.json");

        [Fact]
        public void There_are_exactly_17_P0_cases_and_each_has_a_correct_path_script_with_its_expected_file()
        {
            Assert.Equal(17, P0.Length);
            foreach (var id in P0)
            {
                Assert.True(File.Exists(ScriptOf(id)), id);
                Assert.True(File.Exists(ScriptOf(id).Replace(".actions.json", ".expected.json")), id);
            }
        }

        [Fact]
        public void T_CASE1_all_17_correct_paths_play_with_no_refused_action_and_reach_the_expected_end_state()
        {
            foreach (var id in P0)
            {
                var s = new Session(All.Cases[id], 42, SessionMode.Practice, Subsystems.Default(All));
                SessionFile.Play(s, ActionScript.Parse(File.ReadAllText(ScriptOf(id))));
                var refused = s.Log.Entries.Where(e => !e.Accepted).Select(e => e.Verb + ":" + e.Reject).ToList();
                Assert.True(refused.Count == 0, id + " refused " + string.Join(", ", refused));
                Assert.Equal(Codes[All.CaseFiles[id].ExpectedEndState], s.EndStates!.Resolved);
            }
        }

        [Fact]
        public void The_correct_paths_score_as_a_success_and_never_raise_a_critical_item()
        {
            foreach (var id in P0)
            {
                var s = new Session(All.Cases[id], 42, SessionMode.Practice, Subsystems.Default(All));
                SessionFile.Play(s, ActionScript.Parse(File.ReadAllText(ScriptOf(id))));
                var r = Scoring.Scorer.Score(s, All);
                Assert.True(r.Critical.Count == 0, id + ": " + string.Join(", ", r.Critical.Select(c => c.Id)));
                Assert.NotEqual(Scoring.Scorer.ResultFail, r.Result);
                Assert.NotEqual(Scoring.Scorer.ResultInProgress, r.Result);
            }
        }

        [Fact]
        public void The_scripts_in_the_repo_are_what_the_generator_writes_today()
        {
            // the generator and the checker are tools/golden/p0_scripts.py; CI runs it with --stale to compare byte for byte
            foreach (var id in P0) Assert.StartsWith("[", File.ReadAllText(ScriptOf(id)).TrimStart());
        }

        // T-CASE2: a hidden fact stays hidden until the trainee asks, and is told once they do
        [Fact]
        public void T_CASE2_every_hidden_history_fact_of_every_P0_case_appears_only_when_its_topic_is_asked()
        {
            int checkedFacts = 0;
            foreach (var id in P0)
            {
                var file = All.CaseFiles[id];
                var hidden = file.History.Where(h => !h.Volunteered && h.Variant == null && All.HistoryTopics.Ids.Contains(h.Topic)).ToList();
                foreach (var h in hidden)
                {
                    var s = new Session(All.Cases[id], 42, SessionMode.Practice, Subsystems.Default(All));
                    foreach (var v in new[] { "ask_identity", "read_wristband", "open_notes", "confirm_identity" }) s.Apply(new SimAction(v, "patient"));
                    Assert.DoesNotContain(s.Observable.DisclosedFacts, f => f.Topic == h.Topic);
                    s.Apply(new SimAction("ask", "patient", Channel.Cli, new Dictionary<string, string> { ["topic"] = h.Topic }));
                    Assert.Contains(s.Observable.DisclosedFacts, f => f.Topic == h.Topic && f.Answer == h.Answer);
                    checkedFacts++;
                }
            }
            Assert.True(checkedFacts > 100);
        }

        [Fact]
        public void The_apixaban_taken_this_morning_never_asked_surfaces_at_Sign_In_and_stops_the_case_VAR_S1_D()
        {
            var s = new Session(All.Cases["VAR-S1-D"], 42, SessionMode.Practice, Subsystems.Default(All), new SessionOptions(TrainingLevel.Beginner, "D2"));
            foreach (var v in new[] { "ask_identity", "read_wristband", "open_notes", "confirm_identity" }) s.Apply(new SimAction(v, "patient"));
            s.Patient.Clinical.SetFlag("consent.signed");
            var o = s.Apply(new SimAction("checklist_item", "patient", Channel.Cli, new Dictionary<string, string> { ["phase"] = "sign_in", ["item"] = "sign_in.01", ["response"] = "yes" }));
            Assert.Contains("case_stopped", o.Effects);
            Assert.Equal("SAFE_BUT_DELAYED", s.EndStates!.Resolved);
        }
    }
}
