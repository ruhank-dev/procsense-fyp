using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ProcSense.Core.Content;
using ProcSense.Core.Replay;
using ProcSense.Core.Workflow;

namespace ProcSense.Core.Tests
{
    /// <summary>Verifying a saved session by replaying it (p1-backend-s09): header version 2, the reasons a session is refused, and that a changed result cannot get through.</summary>
    public class SessionVerifierTests
    {
        private static IContentReader Reader() => new CompositeContentReader(new FileContentReader(TestCase.ContentRoot), new FileContentReader(Path.Combine(TestCase.Repo, "tests", "fixtures")));

        private static readonly string ContentVersion = ContentManifestCheck.Version(Reader());

        private static Session Played(SessionMode mode = SessionMode.Practice, SessionOptions? options = null)
        {
            var content = ContentLoader.Load(Reader(), "VAR-S1-A");
            var session = new Session(content.Cases["VAR-S1-A"], 42, mode, Subsystems.Default(content), options);
            SessionFile.Play(session, ActionScript.Parse(File.ReadAllText(Path.Combine(TestCase.Repo, "tests", "golden", "skeleton1_correct.actions.json"))));
            return session;
        }

        private static string Saved(SessionMeta? meta = null, SessionMode mode = SessionMode.Practice, SessionOptions? options = null) =>
            SessionFile.Write(Played(mode, options), meta ?? new SessionMeta(Guid.NewGuid().ToString(), ContentVersion));

        private static string Sha(string text) => string.Concat(SHA256.Create().ComputeHash(Encoding.UTF8.GetBytes(text)).Select(b => b.ToString("x2")));

        /// <summary>Replaces the header's log hash with the hash of the log text as it now is (what someone who edits a log and wants it to look whole would do).</summary>
        private static string Rehashed(string file)
        {
            int nl = file.IndexOf('\n');
            using var doc = JsonDocument.Parse(file.Substring(0, nl));
            string old = doc.RootElement.GetProperty("header").GetProperty("log_hash").GetString()!;
            return file.Replace(old, Sha(file.Substring(nl + 1)));
        }

        private static string ReplaceFirst(string text, string old, string with) { int i = text.IndexOf(old, StringComparison.Ordinal); return i < 0 ? text : text.Substring(0, i) + with + text.Substring(i + old.Length); }

        private static VerifyResult Verify(string file, byte[]? package = null, bool integrity = false, IContentReader? reader = null) => SessionVerifier.Verify(file, reader ?? Reader(), package, integrity);

        // ---------------------------------------------------------------- header version 2

        [Fact]
        public void A_session_saved_with_meta_has_a_version_2_header_and_the_same_log_hash_as_without()
        {
            var plain = SessionFile.Write(Played());
            var withMeta = Saved(new SessionMeta("5b1d4a52-8a3e-4c2f-9c53-1c0f1f6e9d11", ContentVersion));
            var v1 = SessionFile.HeaderOf(plain);
            var v2 = SessionFile.HeaderOf(withMeta);
            Assert.False(v1.IsVersion2);
            Assert.True(v2.IsVersion2);
            Assert.Equal(("5b1d4a52-8a3e-4c2f-9c53-1c0f1f6e9d11", ContentVersion, CoreInfo.Version, ""), (v2.SessionId, v2.ContentVersion, v2.EngineVersion, v2.ScenarioPackageHash));
            Assert.Equal(v1.LogHash, v2.LogHash);   // the new fields are outside the hash: replay and the golden runs are unchanged
            Assert.Equal(plain.Substring(plain.IndexOf('\n')), withMeta.Substring(withMeta.IndexOf('\n')));   // the log lines are byte for byte the same
        }

        [Fact]
        public void The_header_keeps_the_options_the_session_was_played_with()
        {
            var file = Saved(options: new SessionOptions(TrainingLevel.Advanced, "", true));
            var h = SessionFile.HeaderOf(file);
            Assert.Equal(("advanced", true), (h.Level, h.Noise));
            Assert.True(Verify(file).Ok);   // and the replay plays it the same way
        }

        // ---------------------------------------------------------------- verified

        [Fact]
        public void A_session_that_replays_to_its_hash_is_verified_and_gives_its_score()
        {
            var r = Verify(Saved());
            Assert.True(r.Ok, r.Reason + " " + r.Detail);
            Assert.Equal(r.ExpectedHash, r.LogHash);
            Assert.NotNull(r.Facts);
            Assert.NotEmpty(r.Result);
            Assert.Equal("VAR-S1-A", r.Case);
            using var json = JsonDocument.Parse(r.ToJson(2));
            Assert.True(json.RootElement.GetProperty("ok").GetBoolean());
            Assert.Equal(2, json.RootElement.GetProperty("header_version").GetInt32());
            Assert.Equal("debrief_facts.v1", json.RootElement.GetProperty("facts").GetProperty("schema").GetString());
        }

        [Fact]
        public void A_version_1_file_still_verifies_because_the_new_fields_are_only_checked_when_present()
        {
            Assert.True(Verify(SessionFile.Write(Played())).Ok);
        }

        [Fact]
        public void The_same_session_gives_the_same_verdict_every_time()
        {
            var file = Saved();
            Assert.Equal(Verify(file).ToJson(2), Verify(file).ToJson(2));
        }

        [Fact]
        public void With_the_integrity_check_on_the_content_must_match_its_manifest()
        {
            Assert.True(Verify(Saved(), integrity: true).Ok);
        }

        // ---------------------------------------------------------------- a changed result does not get through (EXP-BE-5)

        [Fact]
        public void A_log_edited_to_remove_a_score_effect_is_refused_whether_or_not_the_hash_is_fixed_up()
        {
            var file = Saved();
            var edited = ReplaceFirst(file, "\"score:", "\"info:");
            Assert.NotEqual(file, edited);
            Assert.Equal(VerifyReason.HashMismatch, Verify(edited).Reason);                  // the header hash no longer matches
            Assert.Equal(VerifyReason.HashMismatch, Verify(Rehashed(edited)).Reason);        // and fixing the hash does not help: the replay does not produce this log
        }

        [Fact]
        public void A_changed_action_with_a_fixed_hash_is_refused_when_the_log_no_longer_follows_from_the_actions()
        {
            var file = Saved();
            var edited = Rehashed(ReplaceFirst(file, "\"accepted\":true", "\"accepted\":false"));
            Assert.Equal(VerifyReason.HashMismatch, Verify(edited).Reason);
        }

        [Fact]
        public void A_session_that_claims_other_content_or_another_engine_is_refused_with_the_reason()
        {
            Assert.Equal(VerifyReason.ContentVersionMismatch, Verify(Saved(new SessionMeta(Guid.NewGuid().ToString(), "000000000000-practice"))).Reason);
            var other = Saved().Replace("\"engine_version\":\"" + CoreInfo.Version + "\"", "\"engine_version\":\"9.9.9\"");
            var r = Verify(other);
            Assert.Equal(VerifyReason.EngineVersionUnsupported, r.Reason);
            Assert.Contains("9.9.9", r.Detail);
        }

        [Fact]
        public void A_training_only_case_is_not_verified_as_an_assessment()
        {
            var r = Verify(Saved(mode: SessionMode.Assessment));
            Assert.Equal(VerifyReason.CaseNotAllowedForMode, r.Reason);
            Assert.Contains("APPROVED_FOR_ASSESSMENT", r.Detail);
        }

        [Fact]
        public void An_unknown_case_and_a_malformed_log_are_refused_without_crashing()
        {
            Assert.Equal(VerifyReason.CaseUnknown, Verify(Saved().Replace("\"case\":\"VAR-S1-A\"", "\"case\":\"NO-SUCH-CASE\"")).Reason);
            foreach (var text in new[] { "", "not json", "{\"nothing\":{}}\n", "{\"header\":{\"case\":\"VAR-S1-A\"}}\n", "{\"header\":{\"case\":\"VAR-S1-A\",\"seed\":\"x\",\"mode\":\"practice\",\"log_hash\":\"ab\"}}\nnot a log line\n" })
                Assert.Contains(Verify(text).Reason, new[] { VerifyReason.LogMalformed, VerifyReason.CaseUnknown, VerifyReason.HashMismatch });
            Assert.Equal(VerifyReason.LogMalformed, Verify("not json").Reason);
        }

        [Fact]
        public void A_generated_scenario_needs_its_package_and_a_matching_hash_that_is_not_a_package_is_refused()
        {
            byte[] package = Encoding.UTF8.GetBytes("{\"schema\":\"scenario_package.v1\"}");
            string hash = Sha(Encoding.UTF8.GetString(package));
            var file = Saved(new SessionMeta(Guid.NewGuid().ToString(), ContentVersion, hash));
            Assert.Equal(VerifyReason.PackageMissing, Verify(file).Reason);
            Assert.Equal(VerifyReason.PackageMissing, Verify(file, Encoding.UTF8.GetBytes("{\"something\":\"else\"}")).Reason);
            var unreadable = Verify(file, package);
            Assert.Equal(VerifyReason.PackageMissing, unreadable.Reason);
            Assert.Contains("cannot be read", unreadable.Detail);            // the hash matches but the bytes are not a scenario package; the loader itself is tested in ScenarioPackageVerifyTests
        }

        [Fact]
        public void Changed_content_is_refused_by_the_integrity_check()
        {
            var changed = new ChangedReader(Reader(), "fasting/fasting.json", t => t + " ");
            Assert.Equal(VerifyReason.ContentHashMismatch, SessionVerifier.Verify(Saved(), changed, null, true).Reason);
        }

        private sealed class ChangedReader : IContentReader
        {
            private readonly IContentReader _inner; private readonly string _path; private readonly Func<string, string> _change;
            public ChangedReader(IContentReader inner, string path, Func<string, string> change) { _inner = inner; _path = path; _change = change; }
            public string ReadText(string relativePath) => relativePath == _path ? _change(_inner.ReadText(relativePath)) : _inner.ReadText(relativePath);
        }
    }
}
