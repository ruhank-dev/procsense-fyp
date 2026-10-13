using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using ProcSense.Core.Content;
using ProcSense.Core.Logging;
using ProcSense.Core.Scoring;
using ProcSense.Core.Workflow;

namespace ProcSense.Core.Replay
{
    /// <summary>Why a session was not verified. Fixed codes: a server stores them and a person can look them up.</summary>
    public static class VerifyReason
    {
        public const string LogMalformed = "LOG_MALFORMED";
        public const string CaseUnknown = "CASE_UNKNOWN";
        public const string EngineVersionUnsupported = "ENGINE_VERSION_UNSUPPORTED";
        public const string ContentVersionMismatch = "CONTENT_VERSION_MISMATCH";
        public const string ContentHashMismatch = "CONTENT_HASH_MISMATCH";
        public const string CaseNotAllowedForMode = "CASE_NOT_ALLOWED_FOR_MODE";
        public const string PackageMissing = "PACKAGE_MISSING";
        public const string PackageLoaderMissing = "PACKAGE_LOADER_MISSING";
        public const string HashMismatch = "HASH_MISMATCH";
    }

    /// <summary>The verdict on a saved session: whether it replays to its own hash with this engine and this content, and if so what it scored. `Facts` (the debrief facts, with every score item) is set only when Ok.</summary>
    public sealed record VerifyResult(bool Ok, string Reason, string Detail, string Case, string Mode, string ContentVersion, string EngineVersion, string LogHash, string ExpectedHash,
                                      string EndState, string Result, double ProcessTotal, DebriefFacts? Facts)
    {
        public static VerifyResult Refused(string reason, string detail, SessionHeader? h = null, string actualHash = "") =>
            new VerifyResult(false, reason, detail, h?.Case ?? "", h?.Mode ?? "", h?.ContentVersion ?? "", h?.EngineVersion ?? "", actualHash, h?.LogHash ?? "", "", "", 0, null);

        /// <summary>{ok, reason, detail, case, mode, content_version, engine_version, header_version, expected_hash, log_hash, end_state, result, process_total, facts} in a fixed key order.</summary>
        public string ToJson(int headerVersion)
        {
            var sb = new StringBuilder("{\"ok\":").Append(Ok ? "true" : "false");
            void Text(string key, string value) { sb.Append(",\"").Append(key).Append("\":"); JsonText.Quote(sb, value); }
            Text("reason", Reason); Text("detail", Detail); Text("case", Case); Text("mode", Mode); Text("content_version", ContentVersion); Text("engine_version", EngineVersion);
            sb.Append(",\"header_version\":").Append(headerVersion.ToString(CultureInfo.InvariantCulture));
            Text("expected_hash", ExpectedHash); Text("log_hash", LogHash); Text("end_state", EndState); Text("result", Result);
            sb.Append(",\"process_total\":").Append(ProcessTotal.ToString("0.####", CultureInfo.InvariantCulture));
            sb.Append(",\"facts\":").Append(Facts == null ? "null" : Facts.ToJson());
            return sb.Append('}').ToString();
        }
    }

    /// <summary>
    /// Checks a saved session by playing it again (p1-backend-s09): the same engine, the same content and the log must come out with the hash the header states. A modified headset cannot send a result the log does not produce.
    /// What this proves: the score follows from the recorded actions under these rules. It cannot prove that a person made the actions.
    /// </summary>
    public static class SessionVerifier
    {
        /// <param name="packageBytes">The generated scenario package the header names, when it names one.</param>
        /// <param name="checkIntegrity">Check every content file against the content manifest first (switch off only for developers).</param>
        public static VerifyResult Verify(string text, IContentReader reader, byte[]? packageBytes = null, bool checkIntegrity = true)
        {
            SessionHeader header;
            try { header = SessionFile.HeaderOf(text.Replace("\r\n", "\n")); }
            catch (Exception e) when (IsMalformed(e)) { return VerifyResult.Refused(VerifyReason.LogMalformed, "the first line is not a session header", null); }

            if (header.EngineVersion.Length > 0 && header.EngineVersion != CoreInfo.Version)
                return VerifyResult.Refused(VerifyReason.EngineVersionUnsupported, "the log was made with engine " + header.EngineVersion + "; this is " + CoreInfo.Version, header);

            if (header.ScenarioPackageHash.Length > 0)
            {
                if (packageBytes == null || Sha256(packageBytes) != header.ScenarioPackageHash)
                    return VerifyResult.Refused(VerifyReason.PackageMissing, "the log names a generated scenario package that was not given, or whose bytes do not match its hash", header);
                return VerifyResult.Refused(VerifyReason.PackageLoaderMissing, "this engine cannot load a generated scenario package yet (p1-live-scenario-generation-s10)", header);
            }

            if (header.ContentVersion.Length > 0)
            {
                string have;
                try { have = ContentManifestCheck.Version(reader); }
                catch (Exception e) when (e is System.IO.IOException || e is ContentException) { have = "(no manifest)"; }
                if (have != header.ContentVersion) return VerifyResult.Refused(VerifyReason.ContentVersionMismatch, "the log used content " + header.ContentVersion + "; this content is " + have, header);
            }
            if (checkIntegrity)
            {
                var bad = ContentManifestCheck.Verify(reader);
                if (bad.Count > 0) return VerifyResult.Refused(VerifyReason.ContentHashMismatch, bad.Count + " content file(s) differ from the manifest, first: " + bad[0], header);
            }

            // the log text itself must be what the header's hash covers: the replay below uses only the actions, so without this a changed effect or result in the text would go unnoticed
            string normalised = text.Replace("\r\n", "\n");
            if (JsonText.Sha256Hex(normalised.Substring(normalised.IndexOf('\n') + 1)) != header.LogHash)
                return VerifyResult.Refused(VerifyReason.HashMismatch, "the log text does not match the hash in its header", header);

            try
            {
                ContentPackage content;
                try { content = ContentLoader.Load(reader, header.Case); }
                catch (System.IO.IOException) { return VerifyResult.Refused(VerifyReason.CaseUnknown, "the content has no case " + header.Case, header); }
                var mode = (SessionMode)Enum.Parse(typeof(SessionMode), header.Mode, true);
                if (!content.Cases.ContainsKey(header.Case)) return VerifyResult.Refused(VerifyReason.CaseUnknown, "the content has no case " + header.Case, header);
                if (content.CaseFiles.TryGetValue(header.Case, out var file) && !ContentGate.Allows(file.Status, mode))
                    return VerifyResult.Refused(VerifyReason.CaseNotAllowedForMode, "the case " + header.Case + " is " + file.Status + "; " + header.Mode + " needs " + ContentGate.Needs(mode), header);

                var replay = SessionFile.Replay(text.Replace("\r\n", "\n"), content, () => Subsystems.Default(content));
                if (!replay.Match) return VerifyResult.Refused(VerifyReason.HashMismatch, "the log does not replay to the hash in its header", header, replay.ActualHash);

                var facts = DebriefFacts.Build(replay.Session, content);
                double total = facts.Categories.Sum(c => c.Weighted);
                return new VerifyResult(true, "", "", header.Case, header.Mode, header.ContentVersion, header.EngineVersion.Length > 0 ? header.EngineVersion : CoreInfo.Version, replay.ActualHash,
                                        header.LogHash, facts.EndState, facts.Result, total, facts);
            }
            catch (Exception e) when (IsMalformed(e))
            {
                return VerifyResult.Refused(VerifyReason.LogMalformed, e.Message, header);
            }
        }

        private static bool IsMalformed(Exception e) =>
            e is ContentException || e is ArgumentException || e is FormatException || e is InvalidCastException || e is KeyNotFoundException || e is OverflowException || e is IndexOutOfRangeException;

        private static string Sha256(byte[] bytes)
        {
            using (var sha = SHA256.Create())
                return string.Concat(sha.ComputeHash(bytes).Select(b => b.ToString("x2", CultureInfo.InvariantCulture)));
        }
    }
}
