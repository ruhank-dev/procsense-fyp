using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ProcSense.Core.Content
{
    /// <summary>
    /// The review-status gate (DT-055, plan test T-CL-2): practice accepts a case at APPROVED_FOR_TRAINING or above, assessment only at APPROVED_FOR_ASSESSMENT, and a DRAFT case is never played.
    /// Test cards that have no case file (the engine's own fixtures) are not content and are not gated. The gate is applied where a session is started from content (the command-line host, the Unity host),
    /// not inside the engine, so a test can still build a session from any case.
    /// </summary>
    public static class ContentGate
    {
        public const string StatusTooLow = "STATUS_TOO_LOW";

        public static bool Allows(string status, SessionMode mode) =>
            mode == SessionMode.Assessment ? status == "APPROVED_FOR_ASSESSMENT" : status == "APPROVED_FOR_TRAINING" || status == "APPROVED_FOR_ASSESSMENT" || status == GeneratedPracticeOnly;

        /// <summary>The status of a generated case: it may be played in practice and never in assessment, and nothing the generator does can raise it (ADR-0006: extended, not loosened).</summary>
        public const string GeneratedPracticeOnly = "GENERATED_PRACTICE_ONLY";

        /// <summary>The status a session in this mode needs.</summary>
        public static string Needs(SessionMode mode) => mode == SessionMode.Assessment ? "APPROVED_FOR_ASSESSMENT" : "APPROVED_FOR_TRAINING";

        /// <summary>Throws a ContentException (STATUS_TOO_LOW) when the case file is not approved enough for the mode.</summary>
        public static void Require(CaseFile file, SessionMode mode)
        {
            if (!Allows(file.Status, mode))
                throw new ContentException(StatusTooLow + ": the case " + file.Id + " is " + file.Status + "; " + mode.ToString().ToLowerInvariant() + " needs " + Needs(mode));
        }
    }

    /// <summary>
    /// Checks the content files against content/manifest.json (plan test T-CL-5): every text file (json, csv, md) must hash to what the manifest says, with line endings normalised the way the manifest was written.
    /// Pictures are not text and are not read here (the manifest check in CI covers them).
    /// </summary>
    public static class ContentManifestCheck
    {
        /// <summary>The version of the content the manifest describes: the packaged content_version when the manifest has one, otherwise the first 12 characters of the content hash.</summary>
        public static string Version(IContentReader reader)
        {
            var root = new JsonObj(MiniJson.Parse(reader.ReadText("manifest.json")), "manifest");
            if (root.Has("content_version") && root.Any("content_version") is string v) return v;
            return (root.Any("content_hash") as string ?? "").Substring(0, 12);
        }

        /// <summary>The paths whose content differs from the manifest, or that the manifest lists but cannot be read. Empty means the content is what the manifest describes.</summary>
        public static IReadOnlyList<string> Verify(IContentReader reader)
        {
            var root = new JsonObj(MiniJson.Parse(reader.ReadText("manifest.json")), "manifest");
            var entries = root.Any("entries") as Dictionary<string, object?> ?? throw new ContentException("manifest.entries: expected an object");
            var bad = new List<string>();
            foreach (var kv in entries.OrderBy(k => k.Key, StringComparer.Ordinal))
            {
                string path = kv.Key;
                if (!(path.EndsWith(".json", StringComparison.Ordinal) || path.EndsWith(".csv", StringComparison.Ordinal) || path.EndsWith(".md", StringComparison.Ordinal))) continue;
                var entry = kv.Value as Dictionary<string, object?> ?? throw new ContentException("manifest.entries." + path + ": expected an object");
                string expected = entry["sha256"] as string ?? "";
                try
                {
                    string text = reader.ReadText(path).Replace("\r\n", "\n");
                    if (Logging.JsonText.Sha256Hex(text) != expected) bad.Add(path);
                }
                catch (Exception e) when (e is System.IO.IOException || e is UnauthorizedAccessException) { bad.Add(path + " (cannot be read)"); }
            }
            return bad;
        }
    }
}
