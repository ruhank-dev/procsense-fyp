using System.Collections.Generic;
using System.IO;
using System.Linq;
using ProcSense.Core.Content;
using ProcSense.Core.Flow;

namespace ProcSense.Core.Tests
{
    /// <summary>P3-SCN-08: start-time validation. A case that cannot start is skipped and the reason recorded; a mismatching manifest refuses the whole start; a chosen case is never replaced.</summary>
    public class SessionStarterTests
    {
        private static readonly CaseCatalogue Catalogue = CaseCatalogue.Parse(File.ReadAllText(Path.Combine(TestCase.ContentRoot, "ui", "case_catalogue.json")));
        private static ulong Draw() => 11;
        private static readonly SessionRequest Request = new SessionRequest(SessionMode.Practice, CaseCatalogue.Cholecystectomy, TrainingLevel.Advanced);

        private sealed class Faulty : IContentReader
        {
            private readonly IContentReader _inner = new FileContentReader(TestCase.ContentRoot);
            private readonly HashSet<string> _missing;
            public Faulty(params string[] missing) { _missing = new HashSet<string>(missing); }
            public string ReadText(string path)
            {
                if (_missing.Any(m => path == "case_files/" + m + ".json")) throw new FileNotFoundException(path);
                return _inner.ReadText(path);
            }
        }

        [Fact]
        public void A_healthy_start_gives_the_plan_and_the_loaded_case()
        {
            var r = SessionStarter.Start(Catalogue, Request, new FileContentReader(TestCase.ContentRoot), null, Draw, checkManifest: true);
            Assert.Null(r.Refusal);
            Assert.Empty(r.Substitutions);
            Assert.Contains(r.Plan!.CaseId, r.Load!.Package.CaseFiles.Keys);
        }

        [Fact]
        public void A_case_that_cannot_load_is_skipped_with_its_reason_and_another_is_started()
        {
            var first = SessionStarter.Start(Catalogue, Request, new FileContentReader(TestCase.ContentRoot), null, Draw, checkManifest: false).Plan!.CaseId;
            var r = SessionStarter.Start(Catalogue, Request, new Faulty(first), null, Draw, checkManifest: false);
            Assert.Null(r.Refusal);
            Assert.NotEqual(first, r.Plan!.CaseId);
            Assert.Equal(first, Assert.Single(r.Substitutions).CaseId);
            Assert.NotEmpty(r.Substitutions[0].Reason);
        }

        [Fact]
        public void A_missing_avatar_substitutes_and_says_which_profile()
        {
            var first = SessionStarter.Start(Catalogue, Request, new FileContentReader(TestCase.ContentRoot), null, Draw, checkManifest: false).Plan!;
            string blocked = Catalogue.Find(first.CaseId)!.AvatarProfile;
            var r = SessionStarter.Start(Catalogue, Request, new FileContentReader(TestCase.ContentRoot), null, Draw, p => p != blocked, checkManifest: false);
            Assert.Null(r.Refusal);
            Assert.NotEqual(blocked, Catalogue.Find(r.Plan!.CaseId)!.AvatarProfile);
            Assert.StartsWith(SessionStarter.AvatarMissing, r.Substitutions[0].Reason);
        }

        [Fact]
        public void A_chosen_case_is_never_replaced_and_no_avatar_at_all_gives_no_start()
        {
            var chosen = Request with { CaseId = "SC-5" };
            var r = SessionStarter.Start(Catalogue, chosen, new Faulty("SC-5"), null, Draw, checkManifest: false);
            Assert.Null(r.Plan);
            Assert.Equal(SessionStarter.ChosenCaseFailed, r.Refusal);
            var none = SessionStarter.Start(Catalogue, Request, new FileContentReader(TestCase.ContentRoot), null, Draw, _ => false, checkManifest: false);
            Assert.Null(none.Plan);
            Assert.Equal(SessionStarter.NoStartableCase, none.Refusal);
            Assert.Equal(5, none.Substitutions.Count);
        }

        [Fact]
        public void A_manifest_mismatch_refuses_the_start_instead_of_substituting()
        {
            var tampered = new TamperedManifest();
            var r = SessionStarter.Start(Catalogue, Request, tampered, null, Draw, checkManifest: true);
            Assert.Null(r.Plan);
            Assert.StartsWith(SessionStarter.ContentRefused, r.Refusal);
            Assert.Empty(r.Substitutions);
        }

        private sealed class TamperedManifest : IContentReader
        {
            private readonly IContentReader _inner = new FileContentReader(TestCase.ContentRoot);
            public string ReadText(string path) => path == "history_topics.json" ? _inner.ReadText(path) + " " : _inner.ReadText(path);
        }
    }
}
