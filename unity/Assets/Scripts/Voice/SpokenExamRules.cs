using System;
using System.Collections.Generic;
using ProcSense.Core;
using ProcSense.VR.Bridge;

namespace ProcSense.VR.Voice
{
    /// <summary>
    /// What speaking may do in each mode (P3-VOX-06, decision D3). Speech is allowed in practice and in the examination: the engine decides every action exactly as for the tablet, and the examination keeps every
    /// confirmation. Only presentation differs: no hint or refusal explanation is shown in the examination. Physical and safety-critical actions are never started by speech, whatever the interpreter proposed
    /// (a defence in depth for the catalogue, which already leaves them out); nothing here awards credit and nothing sits behind the speech evaluator.
    /// </summary>
    public static class SpokenExamRules
    {
        /// <summary>Verbs a spoken sentence may never start: they stay gestures or tablet buttons, so speech cannot earn credit for a physical act.</summary>
        public static readonly IReadOnlyCollection<string> CriticalPhysicalVerbs = new HashSet<string>(StringComparer.Ordinal)
        {
            "confirm_identity", "report_mismatch", "read_wristband", "open_notes", "attach_sensor", "detach_sensor",
            "measure_nibp", "measure_temperature", "count_rr", "take_sample", "examine", "murphy_test", "sign_consent",
            "complete_time_out", "sign_ward_checklist", "tick_ward_item", "checklist_item", "use_emergency_route",
        };

        /// <summary>Speaking works whenever a session is running, in practice and in the examination.</summary>
        public static bool Allowed(Session session) => session != null;

        /// <summary>The tablet id (verb or verb:value) names a critical physical action.</summary>
        public static bool IsCriticalPhysical(string actionId)
        {
            if (string.IsNullOrEmpty(actionId)) return false;
            int i = actionId.IndexOf(':');
            var verb = i < 0 ? actionId : actionId.Substring(0, i);
            foreach (var v in CriticalPhysicalVerbs) if (v == verb) return true;
            return false;
        }

        /// <summary>The ids the router may start: the interpreter's proposal without any critical physical action.</summary>
        public static List<string> StartableIds(IEnumerable<string> proposed)
        {
            var ids = new List<string>();
            if (proposed != null) foreach (var id in proposed) if (!IsCriticalPhysical(id)) ids.Add(id);
            return ids;
        }

        /// <summary>The hint text may be shown only in practice.</summary>
        public static string HintToShow(Session session, string text) => session != null && session.PracticeAids ? text ?? "" : "";

        /// <summary>The words shown for an engine refusal: its reason in practice, a neutral line in the examination.</summary>
        public static string RefusalText(Session session, string reason) =>
            session != null && session.PracticeAids ? HintTexts.For(reason) : "that was not allowed now";
    }
}
