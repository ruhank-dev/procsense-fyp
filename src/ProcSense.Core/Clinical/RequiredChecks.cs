using System;
using System.Collections.Generic;
using System.Linq;

namespace ProcSense.Core.Clinical
{
    /// <summary>
    /// What is true of the patient and the session when a treatment is judged. Each required check reads one of these: identity and allergies are derived state, indication, timing, order and
    /// documentation are sets of treatment ids the workflow fills in as the trainee acts, restrictions are the patient's conditions (tags such as kidney_injury) and Done holds the
    /// prerequisites in place (iv_access).
    /// </summary>
    public sealed class TreatmentContext
    {
        public bool IdentityConfirmed { get; set; }
        public bool AllergiesKnown { get; set; }
        public ISet<string> Indicated { get; } = new HashSet<string>(StringComparer.Ordinal);
        public ISet<string> TimingChecked { get; } = new HashSet<string>(StringComparer.Ordinal);
        public ISet<string> Ordered { get; } = new HashSet<string>(StringComparer.Ordinal);
        public ISet<string> Documented { get; } = new HashSet<string>(StringComparer.Ordinal);
        public ISet<string> Restrictions { get; } = new HashSet<string>(StringComparer.Ordinal);
        public ISet<string> Done { get; } = new HashSet<string>(StringComparer.Ordinal);
    }

    /// <summary>One check that did not pass: which one, how serious (from the check list), the treatment, and a short reason (for restriction, the condition that applies).</summary>
    public sealed record CheckItem(string Treatment, string Check, string Severity, string Reason);

    /// <summary>
    /// Allowed is false only when a prerequisite is missing (RejectReason names it, for example IV_ACCESS_REQUIRED). Otherwise the treatment may go ahead and every check that failed is an
    /// item, so the scorer can mark it (a critical item in assessment mode). Items follow the order of the check list.
    /// </summary>
    public sealed record CheckReport(string Treatment, bool Allowed, string RejectReason, IReadOnlyList<CheckItem> Items)
    {
        public bool HasCritical => Items.Any(i => i.Severity == "critical");
    }

    /// <summary>
    /// Evaluates the checks a treatment lists (and only those). The "before" phase is judged when the nurse is asked to give it, the "after" phase when it has been given. Nothing is
    /// implied in code: a check that is not on the treatment's own list is never judged.
    /// </summary>
    public static class RequiredChecks
    {
        public static CheckReport Evaluate(TreatmentTables tables, TreatmentDef treatment, TreatmentContext context, string phase)
        {
            if (phase != "before" && phase != "after") throw new ArgumentException("phase must be 'before' or 'after'");
            if (phase == "before")
            {
                foreach (var need in treatment.Requires)
                    if (!context.Done.Contains(need))
                        return new CheckReport(treatment.Id, false, tables.Prerequisites.First(p => p.Id == need).RejectReason, new List<CheckItem>());
            }
            var items = new List<CheckItem>();
            foreach (var kind in tables.Checks)
            {
                if (kind.Phase != phase || !treatment.RequiredChecks.Contains(kind.Id)) continue;
                if (kind.Id == "restriction")
                {
                    foreach (var r in treatment.RestrictedBy)
                        if (context.Restrictions.Contains(r.Tag)) items.Add(new CheckItem(treatment.Id, kind.Id, kind.Severity, r.Tag));
                }
                else if (!Passes(kind.Id, treatment.Id, context)) items.Add(new CheckItem(treatment.Id, kind.Id, kind.Severity, "missing"));
            }
            return new CheckReport(treatment.Id, true, "", items);
        }

        private static bool Passes(string check, string treatment, TreatmentContext c) => check switch
        {
            "identity" => c.IdentityConfirmed,
            "allergy_known" => c.AllergiesKnown,
            "indication" => c.Indicated.Contains(treatment),
            "timing" => c.TimingChecked.Contains(treatment),
            "order" => c.Ordered.Contains(treatment),
            "documentation" => c.Documented.Contains(treatment),
            _ => throw new InvalidOperationException("no rule for the check '" + check + "': add it to RequiredChecks"),
        };
    }
}
