using System.Collections.Generic;
using ProcSense.Core.Pipeline;

namespace ProcSense.Core.Workflow
{
    /// <summary>The one place that lists which subsystems a normal session runs. Workflow steps add theirs here.</summary>
    public static class Subsystems
    {
        public static IEnumerable<ISubsystem> Default() => new List<ISubsystem>();
    }
}
