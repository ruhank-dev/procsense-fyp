using System.IO;
using System.Text;
using ProcSense.Core.Actions;
using ProcSense.Core.Content;
using ProcSense.Core.Replay;
using ProcSense.Core.Workflow;

namespace ProcSense.Core.Tests
{
    /// <summary>The server's verifier replays a session of a generated case from the package the log names (p1-live-scenario-generation-s10, p1-backend-s09).</summary>
    public class ScenarioPackageVerifyTests
    {
        private static readonly string FixturePath = Path.Combine(TestCase.Repo, "tests", "fixtures", "generated", "GEN-S1-0003-001.package.json");
        private static readonly byte[] FixtureBytes = File.ReadAllBytes(FixturePath);
        private static readonly IContentReader Real = new FileContentReader(TestCase.ContentRoot);

        private static string PlayedLog()
        {
            var package = new ScenarioPackageReader(Encoding.UTF8.GetString(FixtureBytes));
            var content = ContentLoader.Load(new CompositeContentReader(package, Real), package.CaseId);
            var script = ActionScript.Parse(File.ReadAllText(Path.Combine(TestCase.Repo, "tests", "golden", "VAR-S1-G_correct.actions.json")));
            var session = new Session(content.Cases[package.CaseId], 42, SessionMode.Practice, Subsystems.Default(content), new SessionOptions(TrainingLevel.Beginner, ""));
            SessionFile.Play(session, script);
            return SessionFile.Write(session, new SessionMeta("0b6f6d0c-5a43-4d57-9d9d-0d3b1d8a0c11", ContentManifestCheck.Version(Real), package.PackageHash));
        }

        [Fact]
        public void A_session_of_a_generated_case_verifies_from_the_package_it_names()
        {
            var r = SessionVerifier.Verify(PlayedLog(), Real, FixtureBytes);
            Assert.True(r.Ok, r.Reason + ": " + r.Detail);
            Assert.Equal("GEN-S1-0003-001", r.Case);
            Assert.NotNull(r.Facts!.Generated);
            Assert.Equal("VAR-S1-G", r.Facts.Generated!.Spine);
            Assert.Equal(new ScenarioPackageReader(Encoding.UTF8.GetString(FixtureBytes)).PackageHash, r.Facts.Generated.PackageHash);
        }

        [Fact]
        public void The_same_package_with_windows_line_endings_still_verifies()
        {
            var crlf = Encoding.UTF8.GetBytes(Encoding.UTF8.GetString(FixtureBytes).Replace("\n", "\r\n"));
            Assert.True(SessionVerifier.Verify(PlayedLog(), Real, crlf).Ok);
        }

        [Fact]
        public void No_package_or_another_package_is_package_missing_and_never_a_pass()
        {
            string log = PlayedLog();
            Assert.Equal(VerifyReason.PackageMissing, SessionVerifier.Verify(log, Real, null).Reason);
            var other = Encoding.UTF8.GetBytes(Encoding.UTF8.GetString(FixtureBytes).Replace("\"seed\": 3", "\"seed\": 4"));
            Assert.NotEqual(FixtureBytes, other);
            Assert.Equal(VerifyReason.PackageMissing, SessionVerifier.Verify(log, Real, other).Reason);
        }

        [Fact]
        public void A_package_that_says_it_is_approved_is_refused_even_when_its_hash_matches()
        {
            string text = Encoding.UTF8.GetString(FixtureBytes).Replace("\"status\": \"GENERATED_PRACTICE_ONLY\",\n  \"request_id\"", "\"status\": \"APPROVED_FOR_TRAINING\",\n  \"request_id\"");
            Assert.NotEqual(Encoding.UTF8.GetString(FixtureBytes), text);
            var bytes = Encoding.UTF8.GetBytes(text);
            string log = PlayedLog().Replace(new ScenarioPackageReader(Encoding.UTF8.GetString(FixtureBytes)).PackageHash, SessionVerifierHash(text));
            Assert.False(SessionVerifier.Verify(log, Real, bytes).Ok);
        }

        private static string SessionVerifierHash(string text) => ProcSense.Core.Logging.JsonText.Sha256Hex(text.Replace("\r\n", "\n"));
    }
}
