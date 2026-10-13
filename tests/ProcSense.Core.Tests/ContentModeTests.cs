using System;
using System.IO;
using System.Linq;
using ProcSense.Core.Content;

namespace ProcSense.Core.Tests
{
    /// <summary>Loading content for a session mode (p0-case-library-s01): tests T-CL-2 (a case below the mode is excluded), T-CL-4 (a schema violation names its path) and T-CL-5 (a hash mismatch refuses the load).</summary>
    public class ContentModeTests
    {
        private static IContentReader Real() => new CompositeContentReader(new FileContentReader(TestCase.ContentRoot), new FileContentReader(Path.Combine(TestCase.Repo, "tests", "fixtures")));

        /// <summary>Hands out the real content, except that one file's text is changed.</summary>
        private sealed class ChangedReader : IContentReader
        {
            private readonly IContentReader _inner;
            private readonly string _path;
            private readonly Func<string, string> _change;
            public ChangedReader(IContentReader inner, string path, Func<string, string> change) { _inner = inner; _path = path; _change = change; }
            public string ReadText(string relativePath) => relativePath == _path ? _change(_inner.ReadText(relativePath)) : _inner.ReadText(relativePath);
        }

        [Fact]
        public void T_CL_2_practice_plays_an_approved_for_training_case_and_leaves_a_draft_out()
        {
            var load = ContentMode.Load(Real(), SessionMode.Practice, true, "VAR-S1-A", "VAR-S1-B");
            Assert.Contains("VAR-S1-A", load.Package.Cases.Keys);
            Assert.Contains("VAR-S1-A", load.Package.CaseFiles.Keys);
            Assert.DoesNotContain("VAR-S1-B", load.Package.CaseFiles.Keys);
            var draft = Assert.Single(load.Excluded);
            Assert.Equal("VAR-S1-B", draft.Id);
            Assert.Equal("DRAFT", draft.Status);
            Assert.Contains("STATUS_TOO_LOW", draft.Reason);
        }

        [Fact]
        public void T_CL_2_assessment_needs_approved_for_assessment_so_nothing_below_it_is_loaded()
        {
            var load = ContentMode.Load(Real(), SessionMode.Assessment, true, "VAR-S1-A");
            Assert.Empty(load.Package.Cases);
            Assert.Empty(load.Package.CaseFiles);
            var left = Assert.Single(load.Excluded);
            Assert.Equal("APPROVED_FOR_TRAINING", left.Status);
            Assert.Contains("needs APPROVED_FOR_ASSESSMENT", left.Reason);
        }

        [Fact]
        public void A_card_with_no_case_file_is_an_engine_fixture_and_is_not_gated()
        {
            var load = ContentMode.Load(Real(), SessionMode.Assessment, true, "TEST-ID-MISMATCH");
            Assert.Contains("TEST-ID-MISMATCH", load.Package.Cases.Keys);
            Assert.Empty(load.Excluded);
        }

        [Fact]
        public void A_case_with_neither_a_card_nor_a_case_file_is_an_error()
        {
            Assert.Throws<ContentException>(() => ContentMode.Load(Real(), SessionMode.Practice, true, "NO-SUCH-CASE"));
        }

        [Fact]
        public void T_CL_5_a_changed_content_file_refuses_the_load_and_names_it()
        {
            var changed = new ChangedReader(Real(), "fasting/fasting.json", text => text + " ");
            var e = Assert.Throws<ContentException>(() => ContentMode.Load(changed, SessionMode.Practice, true, "VAR-S1-A"));
            Assert.Contains(ContentMode.IntegrityFailed, e.Message);
            Assert.Contains("fasting/fasting.json", e.Message);
        }

        [Fact]
        public void The_content_version_comes_from_the_manifest_and_the_check_can_be_left_off_for_a_developer()
        {
            var load = ContentMode.Load(Real(), SessionMode.Practice, true, "VAR-S1-A");
            Assert.Matches("^[0-9a-f]{12}", load.ContentVersion);
            var changed = new ChangedReader(Real(), "fasting/fasting.json", text => text + " ");
            var unchecked_ = ContentMode.Load(changed, SessionMode.Practice, false, "VAR-S1-A");
            Assert.Equal("unchecked", unchecked_.ContentVersion);
        }

        [Fact]
        public void T_CL_4_a_schema_violation_in_a_case_file_names_the_field()
        {
            var changed = new ChangedReader(Real(), "case_files/VAR-S1-A.json", text => text.Replace("\"synthetic\": true", "\"synthetic\": \"yes\""));
            var e = Assert.Throws<ContentException>(() => ContentLoader.LoadCaseFile(changed, "VAR-S1-A"));
            Assert.Contains("synthetic", e.Message);
        }
    }
}
