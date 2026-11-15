using System;
using System.Collections.Generic;
using System.Linq;

namespace ProcSense.Core.Observation
{
    /// <summary>
    /// A goal or ward checklist line that is not needed for this patient (P3-TRN-02, DT-298): its id, its kind ("goal" or "ward_item", the latter as "ward.&lt;item&gt;"), the reason id
    /// (the key of the wording in content/ui/training_guide.json <c>inactive_reasons</c>) and the walkthrough step it belongs to. Decided by the engine from its own state, never by the view.
    /// </summary>
    public sealed record InactiveGoal(string Id, string Kind, string ReasonId, int Step);

    /// <summary>An action the engine will refuse right now because a readiness blocker holds (P3-TRN-02): the blocker id, the verbs it stops and the content's message. Practice mode only.</summary>
    public sealed record UnavailableAction(string BlockerId, IReadOnlyList<string> Verbs, string Message);

    /// <summary>Why something is unavailable, worked out from the authoritative state. Published in practice mode only: in the examination nothing tells the trainee what is left or why.</summary>
    public static class Availability
    {
        /// <summary>The ward checklist is walkthrough step 10.</summary>
        public const int WardStep = 10;

        /// <summary>Reason when an item is optional and its content names no specific reason (for example hair removal).</summary>
        public const string NotRequired = "NOT_REQUIRED";

        /// <summary>Reason for a conditional goal whose content names no specific reason.</summary>
        public const string NotApplicable = "NOT_APPLICABLE";

        /// <summary>
        /// Reason ids per goal or ward item. They are the ids of content/ui/training_guide.json <c>inactive_reasons</c>; a Core test keeps both lists equal, so a new conditional goal
        /// needs its wording in the guide in the same change.
        /// </summary>
        public static readonly IReadOnlyDictionary<string, string> Reasons = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["G-S1-ADULT"] = "NOT_SCENARIO_S1",
            ["G-EXAM-REGIONS"] = "NO_EXAM_REGIONS",
            ["G-EXAM-AIRWAY"] = "NO_AIRWAY_ITEMS",
            ["ward.site_marking"] = "SITE_MARKING_NOT_APPLICABLE",
            ["ward.stockings"] = "STOCKINGS_NOT_PRESCRIBED",
            ["ward.cbg"] = "NOT_DIABETIC",
        };

        public static string ReasonFor(string id, string fallback) => Reasons.TryGetValue(id, out var r) ? r : fallback;

        internal static IReadOnlyList<InactiveGoal> Inactive(Session s)
        {
            var list = new List<InactiveGoal>();
            if (s.Tracker != null) list.AddRange(s.Tracker.NotApplicable());
            var ward = s.WardStep;
            if (ward != null)
            {
                var needed = new HashSet<string>(ward.Needed().Select(i => i.Id), StringComparer.Ordinal);
                foreach (var item in ward.Items)
                    if (!needed.Contains(item.Id))
                    {
                        string id = "ward." + item.Id;
                        list.Add(new InactiveGoal(id, "ward_item", ReasonFor(id, NotRequired), WardStep));
                    }
            }
            return list;
        }

        internal static IReadOnlyList<UnavailableAction> Blocked(Session s)
        {
            var list = new List<UnavailableAction>();
            if (s.Graph.Blockers.Count == 0) return list;
            var state = Workflow.RuleState.Build(s);
            foreach (var b in s.Graph.Blockers)
                if (b.BlockedIf.Test(state)) list.Add(new UnavailableAction(b.Id, b.CheckAt, b.Message));
            return list;
        }
    }
}
