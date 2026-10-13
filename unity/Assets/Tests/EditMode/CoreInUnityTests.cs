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

        private static ContentPackage Content() => ContentLoader.Load(
            new CompositeContentReader(new FileContentReader(Path.Combine(Repo, "content")), new FileContentReader(Path.Combine(Repo, "tests", "fixtures"))),
            "VAR-S1-A", "TEST-ID-MISMATCH", "TEST-ID-MISSING-BAND", "TEST-ID-MISSING-BAND-ALLERGY");

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
        public void The_skeleton_1_log_hash_is_the_one_pinned_for_the_command_line()
        {
            var expected = File.ReadAllText(Path.Combine(Repo, "tests", "golden", "skeleton1_correct.expected.json"));

            Assert.That(expected, Does.Contain("\"log_hash\": \"dcec6b447e525659b33bb1a6ceeb9f50c68ab74a4ac159c250604de7efbb06cd\""));
            Assert.IsEmpty(GoldenRun.Check(File.ReadAllText(Path.Combine(Repo, "tests", "golden", "skeleton1_correct.actions.json")), expected, Content()));
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
