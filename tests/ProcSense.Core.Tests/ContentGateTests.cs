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
            foreach (var id in new[] { "SC-4", "VAR-S1-B" })
            {
                var f = File_(id);
                Assert.Equal("DRAFT", f.Status);
                foreach (var m in new[] { SessionMode.Practice, SessionMode.Assessment })
                    Assert.StartsWith("STATUS_TOO_LOW", Assert.Throws<ContentException>(() => ContentGate.Require(f, m)).Message);
            }
        }

        [Fact]
        public void The_shipped_playable_cases_are_owner_attested_for_assessment_and_the_drafts_are_not()
        {
            var files = System.IO.Directory.GetFiles(System.IO.Path.Combine(TestCase.ContentRoot, "case_files"), "*.json").Select(p => File_(System.IO.Path.GetFileNameWithoutExtension(p)!)).ToList();
            Assert.Equal(63, files.Count(f => f.Status == "APPROVED_FOR_ASSESSMENT"));
            Assert.All(files.Where(f => f.Status == "APPROVED_FOR_ASSESSMENT"), f =>
            {
                Assert.Contains("ADR-0032", f.Approval!.By);
                Assert.Contains("Owner", f.Approval.By);
                Assert.Contains("no clinician is named and no signature is claimed", f.Approval.By);
            });
            Assert.Equal(new[] { "SC-4", "VAR-S1-B" }, files.Where(f => f.Status == "DRAFT").Select(f => f.Id).OrderBy(x => x, System.StringComparer.Ordinal).ToArray());
        }

        [Fact]
        public void A_non_P0_case_approved_for_assessment_needs_the_recorded_owner_attestation()
        {
            string text = System.IO.File.ReadAllText(System.IO.Path.Combine(TestCase.ContentRoot, "case_files", "SC-1.json"));
            Assert.NotNull(CaseFileLoader.Load(text, "SC-1"));                                                                       // the approval cites ADR-0032: accepted
            string stripped = text.Replace("ADR-0032", "ADR-xxxx");
            Assert.Contains("owner attestation citing ADR-0032", Assert.Throws<ContentException>(() => CaseFileLoader.Load(stripped, "SC-1")).Message);
        }

        [Fact]
        public void A_training_case_plays_in_practice_but_not_assessment()
        {
            var f = File_("VAR-S1-A") with { Status = "APPROVED_FOR_TRAINING" };   // the shipped case is approved for assessment (P3-SCN-07), so it is lowered here
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
