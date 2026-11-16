using System;
using System.Collections.Generic;
using ProcSense.Core.Content;
using ProcSense.Core.Replay;
using ProcSense.Core.Workflow;

namespace ProcSense.Core.Flow
{
    /// <summary>What the menu shows for a saved, interrupted session: whether it can be resumed, why not, and when it can the live session replayed to its last action.</summary>
    public sealed record ResumeOffer(bool CanResume, string Reason, string CaseId, string Mode, int ActionsSaved, Session? Session, ContentPackage? Content = null);

    /// <summary>
    /// Resume (P3-SES-09, pure part): a saved log is replayed from the start to its last action, so the session continues exactly where it stopped (same state, same hash). It is offered only when the engine
    /// version, the content version and the log hash all agree and the case is still allowed for the mode; otherwise the reason is a fixed code and the menu offers Discard only. Saving is
    /// SessionFile.Write (the log is flushed on pause or quit by the Unity side).
    /// </summary>
    public static class SessionResume
    {
        public const string Resumable = "";

        public static ResumeOffer Open(string savedText, IContentReader reader, bool checkIntegrity = true)
        {
            SessionHeader header;
            try { header = SessionFile.HeaderOf(savedText.Replace("\r\n", "\n")); }
            catch (Exception e) when (e is ContentException || e is FormatException || e is InvalidCastException || e is KeyNotFoundException || e is ArgumentException)
            { return Refuse(VerifyReason.LogMalformed, "", ""); }

            if (header.EngineVersion.Length > 0 && header.EngineVersion != CoreInfo.Version) return Refuse(VerifyReason.EngineVersionUnsupported, header.Case, header.Mode);
            var mode = string.Equals(header.Mode, "assessment", StringComparison.OrdinalIgnoreCase) ? SessionMode.Assessment : SessionMode.Practice;
            ModeLoad load;
            try
            {
                load = ContentMode.Load(reader, mode, checkIntegrity, header.Case);
                if (header.ContentVersion.Length > 0 && checkIntegrity && load.ContentVersion != header.ContentVersion) return Refuse(VerifyReason.ContentVersionMismatch, header.Case, header.Mode);
            }
            catch (ContentException) { return Refuse(VerifyReason.ContentHashMismatch, header.Case, header.Mode); }
            if (load.Excluded.Count > 0) return Refuse(VerifyReason.CaseNotAllowedForMode, header.Case, header.Mode);

            var replay = SessionFile.Replay(savedText.Replace("\r\n", "\n"), load.Package, () => Subsystems.Default(load.Package));
            if (!replay.Match) return Refuse(VerifyReason.HashMismatch, header.Case, header.Mode);
            replay.Session.Checker = new Session.CheckSource(load.Package, () => Subsystems.Default(load.Package));
            return new ResumeOffer(true, Resumable, header.Case, header.Mode, replay.Session.Log.Entries.Count, replay.Session, load.Package);
        }

        private static ResumeOffer Refuse(string reason, string caseId, string mode) => new ResumeOffer(false, reason, caseId, mode, 0, null);
    }
}
