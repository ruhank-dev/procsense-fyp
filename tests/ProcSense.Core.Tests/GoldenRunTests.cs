using System.IO;
using System.Linq;
using ProcSense.Core.Content;
using ProcSense.Core.Replay;

namespace ProcSense.Core.Tests
{
    public class GoldenRunTests
    {
        private static readonly string Dir = Path.Combine(TestCase.Repo, "tests", "golden");

        private static string[] Names() =>
            Directory.GetFiles(Dir, "*.actions.json").Select(f => Path.GetFileName(f).Replace(".actions.json", "")).OrderBy(n => n).ToArray();

        private static string Script(string name) => File.ReadAllText(Path.Combine(Dir, name + ".actions.json"));
        private static string Expected(string name) => File.ReadAllText(Path.Combine(Dir, name + ".expected.json"));

        [Fact]
        public void The_three_skeleton_1_golden_runs_exist()
        {
            Assert.Equal(new[] { "skeleton1_bloods_first", "skeleton1_closed_question", "skeleton1_correct" }, Names());
        }

        [Fact]
        public void Every_golden_run_passes()
        {
            var content = TestCase.Package();
            foreach (var name in Names())
                Assert.True(GoldenRun.Check(Script(name), Expected(name), content).Count == 0,
                    name + ": " + string.Join(" | ", GoldenRun.Check(Script(name), Expected(name), content)));
        }

        // Proof the check is real: change one action and the hashes and facts must stop matching.
        [Fact]
        public void Changing_one_action_breaks_the_golden_run()
        {
            var changed = Script("skeleton1_correct").Replace("{ \"t\": 106, \"verb\": \"summarise\" },", "");

            var diffs = GoldenRun.Check(changed, Expected("skeleton1_correct"), TestCase.Package());

            Assert.Contains(diffs, d => d.StartsWith("log_hash"));
            Assert.Contains(diffs, d => d.StartsWith("facts.trust"));
        }

        [Fact]
        public void A_tampered_expected_hash_is_caught()
        {
            var hash = MiniJson.Parse(Expected("skeleton1_correct")) is System.Collections.Generic.Dictionary<string, object?> d ? (string)d["log_hash"]! : "";
            var tampered = Expected("skeleton1_correct").Replace(hash, new string('0', hash.Length));

            Assert.Contains(GoldenRun.Check(Script("skeleton1_correct"), tampered, TestCase.Package()), d => d.StartsWith("log_hash"));
        }

        // The expected file is a human-reviewed record: its vitals must be the walkthrough's Scene 3 values held in the case file.
        [Fact]
        public void The_correct_run_ends_with_the_walkthrough_scene_values()
        {
            var facts = (System.Collections.Generic.Dictionary<string, object?>)((System.Collections.Generic.Dictionary<string, object?>)MiniJson.Parse(Expected("skeleton1_correct"))!)["facts"]!;
            var vitals = (System.Collections.Generic.Dictionary<string, object?>)facts["vitals"]!;
            var b = TestCase.Card().BaselineVitals;

            Assert.Equal(true, facts["identity_confirmed"]);
            Assert.Equal(16, ((System.Collections.Generic.List<object?>)facts["disclosed_topics"]!).Count);
            Assert.Equal(b.Acvpu, vitals["acvpu"]);
            Assert.Equal(b.PainScore.ToString(System.Globalization.CultureInfo.InvariantCulture), vitals["pain_score"]);
            Assert.Equal(9, vitals.Count);
        }

        [Fact]
        public void Bloods_first_is_rejected_once_and_the_closed_question_arms_the_risk()
        {
            var bloods = Expected("skeleton1_bloods_first");
            Assert.Contains("\"reason\": \"IDENTITY_NOT_CONFIRMED\"", bloods);
            Assert.Contains("risk.wrong_blood", Expected("skeleton1_closed_question"));
        }
    }
}
