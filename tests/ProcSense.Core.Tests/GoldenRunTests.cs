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
        public void The_expected_golden_runs_exist()
        {
            var names = Names();
            foreach (var n in new[] { "pause_and_time_skip", "skeleton1_bloods_first", "skeleton1_closed_question", "skeleton1_correct", "wristband_mismatch_resolved", "wristband_missing_allergy", "wristband_missing_applied" })
                Assert.Contains(n, names);
            Assert.Equal(142 + 4 * TestCase.CombinationIds.Count, names.Length);                                           // the 7 above and four paths (correct, novice, dangerous, edge) for each of the 17 P0 cases (p0-evaluation-performance-s02), VAR-S1-A in assessment mode (s04), and the variant paths B2 and C2 (DT-093, DT-193) plus all four paths of each of the 16 P1 cases (p1-case-library-extended-s03, s04)
        }

        [Fact]
        public void Every_golden_run_passes()
        {
            var ids = Directory.GetFiles(Path.Combine(TestCase.ContentRoot, "case_files"), "*.json").Select(Path.GetFileNameWithoutExtension).OrderBy(x => x, System.StringComparer.Ordinal).Select(x => x!).ToArray();
            var content = ContentLoader.Load(new CompositeContentReader(new FileContentReader(TestCase.ContentRoot), new FileContentReader(Path.Combine(TestCase.Repo, "tests", "fixtures"))), ids.Concat(new[] { "TEST-ID-MISMATCH", "TEST-ID-MISSING-BAND", "TEST-ID-MISSING-BAND-ALLERGY" }).ToArray(), ids);
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
