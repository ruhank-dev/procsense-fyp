using System;
using System.Collections.Generic;
using ProcSense.Core.Content;

namespace ProcSense.Core.Flow
{
    /// <summary>A case that was drawn but could not be started, and why (logged and shown, never hidden).</summary>
    public sealed record StartSubstitution(string CaseId, string Reason);

    /// <summary>What starting a session gave: the plan, the content loaded for it, every case skipped on the way, or the reason nothing could be started.</summary>
    public sealed record StartResult(SessionPlan? Plan, ModeLoad? Load, IReadOnlyList<StartSubstitution> Substitutions, string? Refusal);

    /// <summary>
    /// Start-time validation (P3-SCN-08, pure): the drawn case is loaded through ContentMode (manifest check on the device, review-status gate), its avatar profile must be available, and when the case
    /// cannot start the next case is drawn and the reason is recorded. A manifest mismatch is not a case problem (every file is suspect), so it refuses the start with CONTENT_REFUSED instead of
    /// substituting. A case chosen by id (retry, replay) is never replaced: it starts or the start is refused.
    /// </summary>
    public static class SessionStarter
    {
        public const string ContentRefused = "CONTENT_REFUSED", NoStartableCase = "NO_STARTABLE_CASE", ChosenCaseFailed = "CHOSEN_CASE_FAILED";
        public const string AvatarMissing = "AVATAR_MISSING";

        public static StartResult Start(CaseCatalogue catalogue, SessionRequest request, IContentReader reader, IReadOnlyList<string>? recent, Func<ulong> drawSeed,
            Func<string, bool>? avatarAvailable = null, bool checkManifest = true, int maxTries = 5)
        {
            int hintLimit = HintLimitOf(reader);
            var skipped = new List<StartSubstitution>();
            var avoid = new List<string>(recent ?? Array.Empty<string>());
            var current = request;
            for (int attempt = 0; attempt < maxTries; attempt++)
            {
                var planned = SessionPlanner.Plan(catalogue, current, avoid, drawSeed);
                if (planned.Plan == null) return new StartResult(null, null, skipped, skipped.Count > 0 ? NoStartableCase : planned.Refusal);
                var plan = planned.Plan;
                string? problem = null;
                ModeLoad? load = null;
                try
                {
                    load = ContentMode.Load(reader, plan.Mode, checkManifest, plan.CaseId);
                    if (load.Excluded.Count > 0) problem = load.Excluded[0].Reason;
                }
                catch (ContentException e) when (e.Message.StartsWith(ContentMode.IntegrityFailed, StringComparison.Ordinal))
                {
                    return new StartResult(null, null, skipped, ContentRefused + ": " + e.Message);
                }
                catch (Exception e) when (e is ContentException || e is System.IO.IOException) { problem = e.Message; }

                var entry = catalogue.Find(plan.CaseId);
                if (problem == null && avatarAvailable != null && entry != null && !avatarAvailable(entry.AvatarProfile)) problem = AvatarMissing + ": " + entry.AvatarProfile;
                if (problem == null) return new StartResult(plan with { Options = plan.Options with { HintLimit = hintLimit } }, load, skipped, null);

                skipped.Add(new StartSubstitution(plan.CaseId, problem));
                if (request.CaseId != null) return new StartResult(null, null, skipped, ChosenCaseFailed);
                avoid.Add(plan.CaseId);
                current = request with { Seed = null };                          // draw again; the failed case is kept out of the pool
            }
            return new StartResult(null, null, skipped, NoStartableCase);
        }

        /// <summary>The hint limit of content/scoring/grades.json (P3-EXM-01), or the default 3 when the package has no grades file.</summary>
        public static int HintLimitOf(IContentReader reader)
        {
            string text;
            try { text = reader.ReadText("scoring/grades.json"); }
            catch (Exception e) when (e is System.IO.FileNotFoundException || e is System.IO.DirectoryNotFoundException || e is KeyNotFoundException) { return ExamControl.DefaultHintLimit; }
            return GradeTable.Parse(text).HintLimit;
        }
    }
}
