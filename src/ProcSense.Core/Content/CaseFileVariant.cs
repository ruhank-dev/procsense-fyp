using System;
using System.Collections.Generic;
using System.Linq;

namespace ProcSense.Core.Content
{
    /// <summary>
    /// The case file as one session plays it (DT-193). A case file holds the facts of every variant (VAR-S1-B's B2 readings of 188/114, VAR-S1-C's C2 results, VAR-S1-D's last-dose answers).
    /// A session plays one path: the base case, or one variant. Items with no variant always apply; "base" items apply to the base path only; an item of another variant never applies; an item of the
    /// session's variant applies and replaces the base item it corrects (a C2 result replaces the base result of the same test). Observations marked "variant" replace the "initial" ones when a variant is played.
    /// With no variant the result is the base path, so a session that plays the base case sees nothing of any variant.
    /// </summary>
    public static class CaseFileVariant
    {
        public const string VariantCondition = "variant";

        public static CaseFile ForVariant(this CaseFile f, string variant)
        {
            variant ??= "";
            bool Applies(string? v) => v == null || (v == "base" ? variant.Length == 0 : v == variant);

            var results = f.Results.Where(r => Applies(r.Variant)).ToList();
            var corrected = new HashSet<string>(results.Where(r => r.Variant == variant && variant.Length > 0).Select(r => Clinical.InvestigationTables.BaseName(r.Test)), StringComparer.OrdinalIgnoreCase);
            results = results.Where(r => r.Variant != null || !corrected.Contains(Clinical.InvestigationTables.BaseName(r.Test))).ToList();

            bool playsVariantReadings = variant.Length > 0 && f.Observations.Any(o => o.Condition == VariantCondition);
            var observations = f.Observations
                .Where(o => Applies(o.Variant))
                .Where(o => playsVariantReadings ? o.Condition != "initial" : o.Condition != VariantCondition)
                .ToList();

            if (results.Count == f.Results.Count && observations.Count == f.Observations.Count
                && f.History.All(h => Applies(h.Variant)) && f.Problems.All(p => Applies(p.Variant)) && f.Decisions.All(d => Applies(d.Variant))) return f;

            return f with
            {
                Results = results,
                Observations = observations,
                History = f.History.Where(h => Applies(h.Variant)).ToList(),
                Problems = f.Problems.Where(p => Applies(p.Variant)).ToList(),
                Decisions = f.Decisions.Where(d => Applies(d.Variant)).ToList(),
            };
        }

        /// <summary>The readings a variant starts with (the earliest "variant" observation), or null when the variant has none.</summary>
        public static CaseFileObservation? VariantStart(this CaseFile f, string variant) =>
            variant.Length == 0 ? null : f.ForVariant(variant).Observations.Where(o => o.Condition == VariantCondition).OrderBy(o => o.Time).FirstOrDefault();
    }
}
