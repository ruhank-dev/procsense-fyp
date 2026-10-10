using System;
using System.Linq;
using ProcSense.Core.Actions;
using ProcSense.Core.Clinical;
using ProcSense.Core.Content;
using ProcSense.Core.Pipeline;

namespace ProcSense.Core.Workflow.Steps
{
    /// <summary>
    /// Works out the fasting status (rule R-FAST-01) as soon as the trainee has asked about both the last food and the last drink, judged at the planned time of anaesthesia.
    /// It sets the flags the rules read: fasting.breach for a planned (S1) patient who has not fasted long enough, which opens the decision point DP-S1-FAST, and fasting.rsi for an emergency (S2)
    /// patient with a full stomach, who gets a full-stomach anaesthetic plan, not a cancellation. What the patient took comes from the case file (history intake), never from the wording.
    /// </summary>
    public sealed class FastingSubsystem : ISubsystem
    {
        public const string BreachFlag = "fasting.breach";
        public const string RsiFlag = "fasting.rsi";

        private readonly ContentPackage _content;
        private readonly CaseFile? _fixed;
        private CaseFile? _file;
        private Session _s = null!;
        private bool _done;

        /// <param name="caseFile">Test hook: use this case file instead of the one named like the case card.</param>
        public FastingSubsystem(ContentPackage content, CaseFile? caseFile = null) { _content = content; _fixed = caseFile; }

        public int Order => PipelineOrder.Clinical + 1;

        public void Init(Session session)
        {
            _s = session;
            _file = _fixed ?? (_content.CaseFiles.TryGetValue(session.Case.Id, out var f) ? f : null);
        }

        public void OnAction(SimAction a, ActionOutcome o)
        {
            if (_done || !o.Accepted || _file == null) return;
            var clinical = _s.Patient.Clinical;
            if (!clinical.IsRevealed("last_food") || !clinical.IsRevealed("last_drink")) return;

            var pres = _file.Presentation;
            bool emergency = !pres.SurgeryTime.HasValue;
            double toAnaesthesia = emergency ? 0 : Math.Max(0, (pres.SurgeryTime!.Value - pres.TimeNow).TotalHours);
            var result = Fasting.Assess(_content.Fasting, IntakeOf("last_food"), IntakeOf("last_drink"), toAnaesthesia, emergency);
            _done = true;
            clinical.Fasting = result;
            if (result.HoursUntilSafe > 0) clinical.FastingSafeFrom = Observation.ObservableBuilder.Hms(_s.Clock.ClockTime + TimeSpan.FromHours(result.HoursUntilSafe)).Substring(0, 5);
            if (result.Status == "breach" && !emergency) clinical.SetFlag(BreachFlag);
            if (result.Plan == "rsi") clinical.SetFlag(RsiFlag);
            o.Effects.Add("fasting:" + result.Status);
        }

        public void OnTick() { }

        private Intake? IntakeOf(string topic)
        {
            var h = _file!.History.FirstOrDefault(x => x.Topic == topic);
            return h?.Intake == null ? null : new Intake(h.Intake.Kind, h.Intake.HoursBeforeNow);
        }
    }
}
