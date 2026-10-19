using System.Collections.Generic;
using ProcSense.Core.Content;
using ProcSense.Core.Pipeline;
using ProcSense.Core.Workflow.Steps;

namespace ProcSense.Core.Workflow
{
    /// <summary>The one place that lists which subsystems a normal session runs. Workflow steps add theirs here.</summary>
    public static class Subsystems
    {
        public static IEnumerable<ISubsystem> Default(ContentPackage content) => new List<ISubsystem>
        {
            new VariantSubsystem(content),
            new ReadinessSubsystem(content.Workflow),
            new Location.TransportSubsystem(content),
            new IdentitySubsystem(content),
            new HistorySubsystem(content),
            new ObservationsSubsystem(content),
            new ExaminationSubsystem(content),
            new FastingSubsystem(content),
            new RiskSubsystem(content),
            new InvestigationsSubsystem(content),
            new ResultReviewSubsystem(content),
            new Clinical.MedicineWorkflowSubsystem(content),
            new News2Subsystem(content),
            new Physiology.PhysiologySubsystem(content),
            new Behaviour.BehaviourSubsystem(content),
            new SepsisSubsystem(content),
            new ClashSubsystem(content),
            new DecisionSubsystem(content),
            new ConsentSubsystem(content),
            new WardChecklistSubsystem(content),
            new OrReadinessSubsystem(content),
            new HandoverSubsystem(content),
            new SafetyChecklistSubsystem(content),
            new RecoveryHandoverSubsystem(content),
            new IntraOpSubsystem(content),
            new EndStateSubsystem(content),
            new Physiology.DeteriorationSubsystem(content),
            new ProblemsSubsystem(content),
            new CaseGraphSubsystem(content.Workflow),
            new FlowSubsystem(content.FlowMaps),
            new Events.EventDirector(content),
            new Diagnostics.DiagnosticsLoop(content),
            new Records.RecordsSubsystem(content),
            new Speech.SpeechEvidenceSubsystem(),
        };
    }
}
