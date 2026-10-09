using ProcSense.Core.Actions;

namespace ProcSense.Core.Pipeline
{
    /// <summary>One link in the pipeline chain (pattern: chain of responsibility). Subsystems are optional per scenario.</summary>
    public interface ISubsystem
    {
        /// <summary>Position in the fixed pipeline, see PipelineOrder.</summary>
        int Order { get; }

        /// <summary>Called once. Register verbs, take RNG streams (session.Random.Stream(name)) and subscribe to events here.</summary>
        void Init(Session session);

        void OnAction(SimAction action, ActionOutcome outcome);

        /// <summary>Called once per whole 0.5 s step.</summary>
        void OnTick();
    }

    /// <summary>Fixed subsystem order (Implementation Guide B-R11 step 6). Log and Observable are built into Session.</summary>
    public static class PipelineOrder
    {
        public const int Clinical = 10;
        public const int Location = 20;
        public const int Investigations = 30;
        public const int Treatments = 40;
        public const int Equipment = 50;
        public const int Communication = 60;
        public const int Behaviour = 70;
        public const int Staff = 80;
        public const int Physiology = 90;
        public const int Events = 100;
        public const int Scorer = 110;
    }
}
