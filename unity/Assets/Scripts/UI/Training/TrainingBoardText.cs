using System.Collections.Generic;
using System.Linq;
using ProcSense.Core;
using ProcSense.Core.Observation;
using ProcSense.VR.Bridge;

namespace ProcSense.VR.UI.Training
{
    /// <summary>One line of the board: the step, its state as the engine has it (done, active, open, waiting, missed, not_needed) and a short note (why it waits or is not needed).</summary>
    public sealed class BoardStep
    {
        public string Id = "", Kind = "", Label = "", State = "", Note = "";
    }

    public sealed class BoardSection
    {
        public string Id = "", Title = "";
        public List<BoardStep> Steps = new List<BoardStep>();
        public int Done => Steps.Count(s => s.State == "done");
        public int Counted => Steps.Count(s => s.State != "not_needed");
    }

    /// <summary>What the board shows. <see cref="Visible"/> is false in the examination: no goal list, no objective, no reasons.</summary>
    public sealed class BoardModel
    {
        public bool Visible;
        public string Objective = "", ObjectiveId = "", Progress = "", Feedback = "";
        public List<BoardSection> Sections = new List<BoardSection>();
        public List<string> Blocked = new List<string>();
        public IEnumerable<BoardStep> AllSteps => Sections.SelectMany(s => s.Steps);
    }

    /// <summary>
    /// The training board's content (P3-TRN-03) from the running session only: the engine's goals (with what each waits for), its scenario stages, the ward checklist ticks and the lines it lists as
    /// not needed with the reason id (P3-TRN-02), the last refusal and the active blockers. The guide (content/ui/training_guide.json) gives the order, section, how-to and the words of each reason.
    /// Nothing here decides completion. In the examination (<see cref="ModeGate.PracticeAids"/> off) the model is empty and invisible.
    /// </summary>
    public static class TrainingBoardText
    {
        public static BoardModel Build(Session s, TrainingGuide guide)
        {
            var m = new BoardModel();
            if (s == null || guide == null || !s.PracticeAids) return m;
            m.Visible = true;
            var o = s.Observable;
            string scenario = s.Case.Scenario;

            var rows = new Dictionary<string, BoardStep>();
            foreach (var g in o.Goals)
            {
                string state = g.State == "complete" ? "done" : g.State == "missed" ? "missed" : g.Waiting.Count > 0 ? "waiting" : "open";
                string note = state == "waiting" ? "First: " + string.Join(", ", g.Waiting.Select(id => guide.Find(id)?.Label ?? id)) : state == "missed" ? "Missed at " + g.At : "";
                rows[g.Id] = new BoardStep { Id = g.Id, Kind = "goal", Label = g.Label, State = state, Note = note };
            }
            foreach (var st in s.FlowStages)
            {
                string state = st.State == "done" ? "done" : st.State == "skipped" ? "not_needed" : st.State == "active" ? "active" : "open";
                string note = st.State == "suspended" ? "Paused while an emergency is handled" : st.State == "skipped" ? "Skipped for this case" : "";
                rows[st.Id] = new BoardStep { Id = st.Id, Kind = "stage", Label = st.Title, State = state, Note = note };
            }
            var ward = s.Ward;
            if (ward != null)
                foreach (var item in ward.Items)
                {
                    bool ticked = ward.Ticked.Contains(item.Id);
                    bool holds = ticked && ward.Verified(item.Id);
                    rows["ward." + item.Id] = new BoardStep
                    {
                        Id = "ward." + item.Id, Kind = "ward_item", Label = item.Label, State = holds ? "done" : "open",
                        Note = ticked && !holds ? "Ticked, but the check does not hold yet" : "",
                    };
                }
            foreach (var x in o.Inactive ?? new InactiveGoal[0])
            {
                var entry = guide.Find(x.Id);
                if (!rows.TryGetValue(x.Id, out var row)) rows[x.Id] = row = new BoardStep { Id = x.Id, Kind = x.Kind, Label = entry?.Label ?? x.Id };
                row.State = "not_needed";
                row.Note = "Not needed: " + guide.ReasonText(x.ReasonId);
            }

            // the guide gives the order and the section; a step the engine does not publish (a goal not triggered yet) is never shown
            foreach (var (id, title) in guide.Sections)
            {
                var section = new BoardSection { Id = id, Title = title };
                foreach (var e in guide.Entries.Where(e => e.Section == id && (e.Scenarios.Count == 0 || e.Scenarios.Contains(scenario))).OrderBy(e => e.Order))
                    if (rows.TryGetValue(e.Id, out var row)) section.Steps.Add(row);
                if (section.Steps.Count > 0) m.Sections.Add(section);
            }

            var next = m.AllSteps.FirstOrDefault(r => r.Kind == "goal" && r.State == "open")
                       ?? m.AllSteps.FirstOrDefault(r => r.State == "active")
                       ?? m.AllSteps.FirstOrDefault(r => r.State == "open" || r.State == "waiting");
            m.ObjectiveId = next?.Id ?? "";
            m.Objective = next != null ? next.Label : "Everything on the board is done: hand the patient over or submit when ready.";

            int goalsDone = o.Goals.Count(g => g.State == "complete"), goals = o.Goals.Count;
            int done = m.AllSteps.Count(r => r.State == "done"), counted = m.AllSteps.Count(r => r.State != "not_needed");
            m.Progress = "Goals " + goalsDone + " of " + goals + "  |  Steps " + done + " of " + counted;

            if (o.BlockMessage.Length > 0) m.Feedback = o.BlockMessage;
            else if (o.LastRejection.Length > 0) m.Feedback = HintTexts.For(o.LastRejection);
            else if (!string.IsNullOrEmpty(o.Hint)) m.Feedback = o.Hint;
            m.Blocked = (o.Unavailable ?? new UnavailableAction[0]).Select(u => u.Message).Where(t => t.Length > 0).Distinct().ToList();
            return m;
        }

        /// <summary>The how-to of a step in the input mode the trainee uses ("menu" or "voice"), with its explanation.</summary>
        public static string HowTo(TrainingGuide guide, string stepId, string inputMode)
        {
            var e = guide?.Find(stepId);
            if (e == null) return "";
            string how = inputMode == "voice"
                ? e.HowVoice + (e.VoiceExample.Length > 0 ? "\nSay: \"" + e.VoiceExample + "\"" : "")
                : e.HowMenu;
            return how + (e.Explanation.Length > 0 ? "\nWhy: " + e.Explanation : "");
        }

        public static string Mark(string state)
        {
            switch (state)
            {
                case "done": return "[done]";
                case "active": return "[now]";
                case "waiting": return "[wait]";
                case "missed": return "[missed]";
                case "not_needed": return "[n/a]";
                default: return "[ ]";
            }
        }
    }
}
