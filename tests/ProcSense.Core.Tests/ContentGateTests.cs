using System.IO;
using System.Linq;
using ProcSense.Core.Content;

namespace ProcSense.Core.Tests
{
    /// <summary>DT-055: the review-status gate (status against mode) and the content manifest check.</summary>
    public class ContentGateTests
    {
        private static readonly string[] Statuses = { "DRAFT", "CLINICAL_REVIEW", "APPROVED_FOR_TRAINING", "APPROVED_FOR_ASSESSMENT" };

        private static CaseFile File_(string id) => ContentLoader.LoadCaseFile(new FileContentReader(TestCase.ContentRoot), id);

        [Theory]
        [InlineData("DRAFT", false, false)]
        [InlineData("CLINICAL_REVIEW", false, false)]
        [InlineData("APPROVED_FOR_TRAINING", true, false)]
        [InlineData("APPROVED_FOR_ASSESSMENT", true, true)]
        public void Status_against_mode(string status, bool practice, bool assessment)
        {
            Assert.Equal(practice, ContentGate.Allows(status, SessionMode.Practice));
            Assert.Equal(assessment, ContentGate.Allows(status, SessionMode.Assessment));
        }

        [Fact]
        public void Every_listed_status_is_known_to_the_gate()
        {
            foreach (var s in Statuses) Assert.Equal(s.StartsWith("APPROVED"), ContentGate.Allows(s, SessionMode.Practice));
        }

        [Fact]
        public void Draft_cases_are_refused_in_both_modes_with_STATUS_TOO_LOW()
        {
            foreach (var id in new[] { "SC-4", "STK-S1-A", "VAR-S1-B" })
            {
                var f = File_(id);
                Assert.Equal("DRAFT", f.Status);
                foreach (var m in new[] { SessionMode.Practice, SessionMode.Assessment })
                    Assert.StartsWith("STATUS_TOO_LOW", Assert.Throws<ContentException>(() => ContentGate.Require(f, m)).Message);
            }
        }

        [Fact]
        public void A_training_case_plays_in_practice_but_not_assessment()
        {
            var f = File_("VAR-S1-A");
            Assert.Equal("APPROVED_FOR_TRAINING", f.Status);
            ContentGate.Require(f, SessionMode.Practice);
            Assert.Throws<ContentException>(() => ContentGate.Require(f, SessionMode.Assessment));
        }

        [Fact]
        public void The_shipped_content_matches_its_manifest()
        {
            Assert.Empty(ContentManifestCheck.Verify(new FileContentReader(TestCase.ContentRoot)));
        }

        [Fact]
        public void A_changed_file_and_a_crlf_copy_are_told_apart()
        {
            var tmp = Path.Combine(Path.GetTempPath(), "ps-gate-" + System.Guid.NewGuid().ToString("N"));
            try
            {
                var man = File.ReadAllText(Path.Combine(TestCase.ContentRoot, "manifest.json"));
                Directory.CreateDirectory(tmp);
                File.WriteAllText(Path.Combine(tmp, "manifest.json"), man);
                var paths = new MiniManifest(man).TextPaths().ToList();
                foreach (var p in paths)
                {
                    var dest = Path.Combine(tmp, p.Replace('/', Path.DirectorySeparatorChar));
                    Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
                    File.WriteAllText(dest, File.ReadAllText(Path.Combine(TestCase.ContentRoot, p)).Replace("\r\n", "\n").Replace("\n", "\r\n"));
                }
                Assert.Empty(ContentManifestCheck.Verify(new FileContentReader(tmp)));
                var victim = Path.Combine(tmp, paths[0].Replace('/', Path.DirectorySeparatorChar));
                File.AppendAllText(victim, " ");
                Assert.Equal(new[] { paths[0] }, ContentManifestCheck.Verify(new FileContentReader(tmp)).ToArray());
            }
            finally { if (Directory.Exists(tmp)) Directory.Delete(tmp, true); }
        }

        private sealed class MiniManifest
        {
            private readonly string _json;
            public MiniManifest(string json) { _json = json; }
            public System.Collections.Generic.IEnumerable<string> TextPaths()
            {
                using var doc = System.Text.Json.JsonDocument.Parse(_json);
                return doc.RootElement.GetProperty("entries").EnumerateObject().Select(p => p.Name)
                    .Where(k => k.EndsWith(".json") || k.EndsWith(".csv") || k.EndsWith(".md")).OrderBy(k => k, System.StringComparer.Ordinal).ToList();
            }
        }
    }
}
