using System;
using System.Collections.Generic;
using System.Linq;

namespace ProcSense.Core.Content
{
    /// <summary>A case the mode may not play: its id, its review status and why it was left out.</summary>
    public sealed record ExcludedCase(string Id, string Status, string Reason);

    /// <summary>What <see cref="ContentMode.Load"/> made: the package with only the cases the mode may play, the cases left out, and the version of the content that was checked.</summary>
    public sealed record ModeLoad(ContentPackage Package, IReadOnlyList<ExcludedCase> Excluded, string ContentVersion);

    public static class ContentMode
    {
        private static bool Exists(IContentReader reader, string path)
        {
            try { reader.ReadText(path); return true; }
            catch (System.IO.FileNotFoundException) { return false; }
            catch (System.IO.DirectoryNotFoundException) { return false; }
        }

        public const string IntegrityFailed = "CONTENT_HASH_MISMATCH";

        /// <summary>
        /// Loads content for a session mode (plan tests T-CL-2 and T-CL-5, Implementation Guide B-M11 step 4). The content is first checked against its manifest and refused if any file differs;
        /// then each requested case that has a case file is kept only if its review status is enough for the mode (practice: APPROVED_FOR_TRAINING or above; assessment: APPROVED_FOR_ASSESSMENT),
        /// and otherwise listed as excluded, never loaded. A card with no case file (an engine test fixture) is not content and is not gated.
        /// </summary>
        public static ModeLoad Load(IContentReader reader, SessionMode mode, bool checkManifest, params string[] caseIds)
        {
            string version = "unchecked";
            if (checkManifest)
            {
                var bad = ContentManifestCheck.Verify(reader);
                if (bad.Count > 0)
                    throw new ContentException(IntegrityFailed + ": " + string.Join(", ", bad.Take(5)) + (bad.Count > 5 ? " and " + (bad.Count - 5).ToString(System.Globalization.CultureInfo.InvariantCulture) + " more" : "") + " do not match the content manifest");
                version = ContentManifestCheck.Version(reader);
            }

            var cards = new List<string>();
            var files = new List<string>();
            var excluded = new List<ExcludedCase>();
            foreach (var id in caseIds)
            {
                CaseFile? file = Exists(reader, "case_files/" + id + ".json") ? ContentLoader.LoadCaseFile(reader, id) : null;
                if (file != null && !ContentGate.Allows(file.Status, mode))
                {
                    excluded.Add(new ExcludedCase(id, file.Status, ContentGate.StatusTooLow + ": " + mode.ToString().ToLowerInvariant() + " needs " + ContentGate.Needs(mode)));
                    continue;
                }
                if (file != null) files.Add(id);
                if (Exists(reader, "cases/" + id + ".json") || file != null) cards.Add(id);           // a case file with no hand-made card is the patient itself (p0-evaluation-performance-s04 fixed this: such cases were left out)
                else throw new ContentException("the case '" + id + "' has neither a card nor a case file");
            }
            return new ModeLoad(ContentLoader.Load(reader, cards, files), excluded, version);
        }
    }
}
