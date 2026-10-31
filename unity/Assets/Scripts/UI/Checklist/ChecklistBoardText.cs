using System.Collections.Generic;
using System.Linq;
using ProcSense.Core;
using ProcSense.Core.Content;

namespace ProcSense.VR.UI.Checklist
{
    /// <summary>One line of the WHO board: the question as content words it, who answers it, its state, and for a stop the reason and what fixes it.</summary>
    public sealed class BoardRow
    {
        public string Id = "", Phase = "", Prompt = "", AnsweredBy = "", State = "", Reason = "";
        public bool Extension;
        public bool Npc => AnsweredBy == "npc";
    }

    /// <summary>
    /// The WHO checklist board's rows (p0-vr-interaction-ui-s08), from the content table (content/checklist/safety_checklist.json, in its order field) and Core's checklist state
    /// (<see cref="ProcSense.Core.Workflow.Steps.SafetyChecklistSubsystem"/>): Sign In and Time Out (Sign Out is P1), each item pending, confirmed, stopped or contradicted, the phase state, and the ProcSense
    /// extensions in a section of their own. A stop shows the content's "if no" (what fixes it). No wording is written here.
    /// </summary>
    public static class ChecklistBoardText
    {
        public static readonly string[] BoardPhases = { "sign_in", "time_out" };

        public static IReadOnlyList<BoardRow> Rows(Session s, ContentPackage content, string phase)
        {
            var safety = s?.Safety;
            return content.Checklist.InPhase(phase).Select(i =>
            {
                string state = safety?.ItemState(i.Id) ?? "pending";
                return new BoardRow
                {
                    Id = i.Id, Phase = i.Phase, Prompt = i.Prompt, AnsweredBy = i.AnsweredBy, Extension = i.Extension, State = state,
                    Reason = state == "stopped" || state == "contradicted" ? (i.IfNo.Length > 0 ? i.IfNo : "Resolve this before going on") : "",
                };
            }).ToList();
        }

        public static string PhaseState(Session s, string phase) => s?.Safety?.PhaseState(phase) ?? "NOT_STARTED";

        public static string Mark(string state)
        {
            switch (state)
            {
                case "confirmed": return "[done]";
                case "stopped": return "[STOP]";
                case "contradicted": return "[STOP]";
                default: return "[ ]";
            }
        }

        public static string WhoLabel(string answeredBy) => answeredBy == "npc" ? "Team" : "You";
    }
}
