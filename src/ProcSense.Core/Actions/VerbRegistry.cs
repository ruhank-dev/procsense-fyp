using System.Collections.Generic;

namespace ProcSense.Core.Actions
{
    /// <summary>The verbs the engine understands. Subsystems register theirs in Init; anything else is UNKNOWN_VERB.</summary>
    public sealed class VerbRegistry
    {
        public const string UnknownVerb = "UNKNOWN_VERB";

        private readonly HashSet<string> _verbs = new HashSet<string>();

        public void Register(string verb) => _verbs.Add(verb);

        public bool IsKnown(string verb) => _verbs.Contains(verb);
    }
}
