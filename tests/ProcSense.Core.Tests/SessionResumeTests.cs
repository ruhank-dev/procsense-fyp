using System.IO;
using System.Linq;
using ProcSense.Core.Actions;
using ProcSense.Core.Content;
using ProcSense.Core.Flow;
using ProcSense.Core.Replay;
using ProcSense.Core.Workflow;

namespace ProcSense.Core.Tests
{
    /// <summary>P3-SES-09 (pure part): an interrupted session resumes exactly where it stopped, or is refused with a fixed code.</summary>
    public class SessionResumeTests
    {
        private static readonly FileContentReader Reader = new FileContentReader(TestCase.ContentRoot);
        private static ContentPackage Content => ContentLoader.Load(Reader, new[] { "VAR-S1-A" }, new[] { "VAR-S1-A" });

        private static string Saved(SessionMode mode = SessionMode.Practice, int actions = 8)
        {
            var content = Content;
            var s = new Session(content.Cases["VAR-S1-A"], 42, mode, Subsystems.Default(content));
            SessionFile.Play(s, ActionScript.Parse(File.ReadAllText(Path.Combine(TestCase.Repo, "tests", "golden", "VAR-S1-A_correct.actions.json"))).Take(actions));
            return SessionFile.Write(s, new SessionMeta("11111111-2222-3333-4444-555555555555", ContentManifestCheck.Version(Reader)));
        }

        [Fact]
        public void A_saved_session_resumes_with_the_same_hash_and_can_carry_on()
        {
            string text = Saved();
            var offer = SessionResume.Open(text, Reader);
            Assert.True(offer.CanResume, offer.Reason);
            Assert.Equal("VAR-S1-A", offer.CaseId);
            Assert.Equal(8, offer.ActionsSaved);
            Assert.Equal(SessionFile.HeaderOf(text).LogHash, offer.Session!.Log.Hash());
            Assert.True(offer.Session.Apply(new SimAction("ask_identity")).Accepted || offer.Session.LastRejection.Length > 0);   // it is a live session: it answers
            Assert.True(offer.Session.Check(new SimAction("no_such_verb")).Accepted == false);                                         // and Check works on it
        }

        [Fact]
        public void An_engine_or_content_change_or_a_tampered_log_is_refused_with_a_code()
        {
            string text = Saved();
            Assert.Equal(VerifyReason.EngineVersionUnsupported, SessionResume.Open(text.Replace("\"engine_version\":\"" + CoreInfo.Version + "\"", "\"engine_version\":\"9.9.9\""), Reader).Reason);
            Assert.Equal(VerifyReason.ContentVersionMismatch, SessionResume.Open(text.Replace(ContentManifestCheck.Version(Reader), "000000000000"), Reader).Reason);
            Assert.Equal(VerifyReason.HashMismatch, SessionResume.Open(text.Replace("\"verb\":\"ask_identity\"", "\"verb\":\"read_wristband\""), Reader).Reason);
            Assert.Equal(VerifyReason.LogMalformed, SessionResume.Open("not a session", Reader).Reason);
            Assert.False(SessionResume.Open(text, Reader).Session == null);
        }

        [Fact]
        public void A_case_that_the_mode_no_longer_allows_is_not_resumed()
        {
            string text = Saved(SessionMode.Assessment);                       // saved while approved; the case is then training-only (lowered in memory)
            var offer = SessionResume.Open(text, new TrainingOnlyReader(Reader, "VAR-S1-A"), false);
            Assert.False(offer.CanResume);
            Assert.Equal(VerifyReason.CaseNotAllowedForMode, offer.Reason);
        }
    }
}
