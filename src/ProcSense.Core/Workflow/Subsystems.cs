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
            new ReadinessSubsystem(content.Workflow),
            new IdentitySubsystem(content),
            new HistorySubsystem(content),
            new ObservationsSubsystem(content),
            new CaseGraphSubsystem(content.Workflow),
        };
    }
}
