using ProcSense.Core.Actions;
using ProcSense.Core.Content;
using ProcSense.Core.Pipeline;

namespace ProcSense.Core.Workflow.Steps
{
    /// <summary>
    /// Starts a variant with its own readings (DT-193): VAR-S1-B's B2 patient arrives at 188/114, not at the base case's 172/104. It runs first, before physiology takes its baselines from the patient's vitals.
    /// A session with no variant, or a variant whose case file has no readings of its own, changes nothing. The rest of a variant (results, history, problems, decisions) is chosen by CaseFileVariant.ForVariant.
    /// </summary>
    public sealed class VariantSubsystem : ISubsystem
    {
        private readonly ContentPackage _content;

        public VariantSubsystem(ContentPackage content) { _content = content; }

        public int Order => PipelineOrder.Clinical - 1;

        public void Init(Session session)
        {
            if (session.Variant.Length == 0 || !_content.CaseFiles.TryGetValue(session.Case.Id, out var file)) return;
            var start = file.VariantStart(session.Variant);
            if (start == null) return;
            session.Patient.Physiological.Vitals = new Vitals(start.HeartRate, start.HeartRhythm, start.SystolicBp, start.DiastolicBp, start.RespiratoryRate, start.Spo2, start.Oxygen, start.TemperatureC, start.Acvpu, start.PainScore);
        }

        public void OnAction(SimAction a, ActionOutcome o) { }

        public void OnTick() { }
    }
}
