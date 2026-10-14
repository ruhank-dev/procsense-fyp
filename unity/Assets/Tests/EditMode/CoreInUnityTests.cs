using System.IO;
using System.Linq;
using NUnit.Framework;
using ProcSense.Core.Content;
using ProcSense.Core.Replay;
using UnityEngine;

namespace ProcSense.Tests.EditMode
{
    /// <summary>The engine inside Unity must give exactly the results it gives on the command line (plan p0-vr-platform-s02 verification).</summary>
    public class CoreInUnityTests
    {
        private static string Repo => Path.GetFullPath(Path.Combine(Application.dataPath, "..", ".."));

        private static ContentPackage Content()
        {
            // Match the CLI golden suite: all case files, plus the three identity fixtures.
            var ids = Directory.GetFiles(Path.Combine(Repo, "content", "case_files"), "*.json")
                .Select(Path.GetFileNameWithoutExtension).OrderBy(id => id, System.StringComparer.Ordinal).ToArray();
            return ContentLoader.Load(
                new CompositeContentReader(new FileContentReader(Path.Combine(Repo, "content")), new FileContentReader(Path.Combine(Repo, "tests", "fixtures"))),
                ids.Concat(new[] { "TEST-ID-MISMATCH", "TEST-ID-MISSING-BAND", "TEST-ID-MISSING-BAND-ALLERGY" }).ToArray(), ids);
        }

        [Test]
        public void Every_golden_run_passes_inside_Unity()
        {
            var content = Content();
            var scripts = Directory.GetFiles(Path.Combine(Repo, "tests", "golden"), "*.actions.json");
            Assert.GreaterOrEqual(scripts.Length, 6, "the golden runs were not found");
            foreach (var script in scripts)
            {
                var diffs = GoldenRun.Check(File.ReadAllText(script), File.ReadAllText(script.Replace(".actions.json", ".expected.json")), content);
                Assert.IsEmpty(diffs, Path.GetFileName(script) + ": " + string.Join(" | ", diffs));
            }
        }

        [Test]
        public void The_skeleton_1_matches_the_command_line_golden_and_rejects_a_changed_hash()
        {
            var expected = File.ReadAllText(Path.Combine(Repo, "tests", "golden", "skeleton1_correct.expected.json"));
            var script = File.ReadAllText(Path.Combine(Repo, "tests", "golden", "skeleton1_correct.actions.json"));
            var content = Content();
            // The reviewed golden file owns the pin; do not duplicate a hash that drifts from it.
            Assert.IsEmpty(GoldenRun.Check(script, expected, content));
            var header = (System.Collections.Generic.Dictionary<string, object>)MiniJson.Parse(expected);
            var hash = (string)header["log_hash"];
            Assert.That(hash, Does.Match("^[0-9a-f]{64}$"));
            var tampered = expected.Replace(hash, new string('0', hash.Length));
            Assert.IsTrue(GoldenRun.Check(script, tampered, content).Any(d => d.StartsWith("log_hash")), "A changed canonical hash must still be rejected");
        }

        [Test]
        public void The_content_loads_with_the_unity_reader_too()
        {
            var reader = new ProcSense.VR.Content.UnityContentReader(Path.Combine(Repo, "content"));

            var package = ContentLoader.Load(reader, "VAR-S1-A");

            Assert.AreEqual("Ayesha Khan", package.Cases["VAR-S1-A"].Identity.FullName);
        }
    }
}
