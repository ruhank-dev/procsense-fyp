using System;
using System.Collections.Generic;
using System.Linq;
using ProcSense.Core.Content;
using ProcSense.Core.Random;

namespace ProcSense.Core.Flow
{
    /// <summary>What the picker chose, and why a filter was relaxed when the pool was too small (the reason is logged, never hidden).</summary>
    public sealed record ScenarioPick(string CaseId, ulong Seed, int PoolSize, string? Relaxed);

    /// <summary>
    /// P3-SCN-03/04 (pure): mode + procedure + level + seed + recent case ids give a case id from the allowed pool. The pool is the catalogue's cases of the procedure whose review status the mode
    /// allows (ContentGate; so never a draft and never below the mode's status). A beginner in practice is not given a stacked case unless that leaves nothing; the last three cases are not
    /// repeated unless that leaves nothing. The draw is SimRandom(seed) over the pool in id order, so the same inputs always give the same case, and a different seed gives a different draw.
    /// </summary>
    public static class ScenarioPicker
    {
        public const int NoRepeat = 3;

        public static ScenarioPick? Pick(CaseCatalogue catalogue, SessionMode mode, string procedure, TrainingLevel level, ulong seed, IReadOnlyList<string>? recent = null)
        {
            var pool = catalogue.Entries.Where(e => e.Procedure == procedure && ContentGate.Allows(e.Status, mode)).ToList();
            if (pool.Count == 0) return null;
            string? relaxed = null;

            if (mode == SessionMode.Practice && level == TrainingLevel.Beginner)
            {
                var easy = pool.Where(e => !e.Stacked).ToList();
                if (easy.Count > 0) pool = easy; else relaxed = "beginner pool empty: stacked cases allowed";
            }
            if (recent != null && recent.Count > 0)
            {
                var last = new HashSet<string>(recent.Skip(Math.Max(0, recent.Count - NoRepeat)), StringComparer.Ordinal);
                var fresh = pool.Where(e => !last.Contains(e.Id)).ToList();
                if (fresh.Count > 0) pool = fresh; else relaxed = (relaxed == null ? "" : relaxed + "; ") + "pool too small to avoid the last " + NoRepeat + " cases";
            }
            var rng = new SimRandom(seed);
            return new ScenarioPick(pool[rng.NextInt(pool.Count)].Id, seed, pool.Count, relaxed);
        }

        /// <summary>"Retry" reuses the case and the seed (same events, same hash) (P3-SCN-09).</summary>
        public static ScenarioPick Retry(ScenarioPick previous) => previous;

        /// <summary>"New scenario" draws a new seed from the previous one and picks again, not returning the case just played when another is allowed (P3-SCN-09).</summary>
        public static ScenarioPick? NewScenario(CaseCatalogue catalogue, SessionMode mode, string procedure, TrainingLevel level, ScenarioPick previous, IReadOnlyList<string>? recent = null)
        {
            ulong next = new SimRandom(previous.Seed).Stream("new-scenario").NextULong();
            var played = new List<string>(recent ?? Array.Empty<string>()) { previous.CaseId };
            return Pick(catalogue, mode, procedure, level, next, played);
        }
    }
}
