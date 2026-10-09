using System.Collections.Generic;

namespace ProcSense.Core.Actions
{
    /// <summary>The verbs the engine understands. Subsystems register theirs in Init; anything else is UNKNOWN_VERB.</summary>
    public sealed class VerbRegistry
    {
        public const string UnknownVerb = "UNKNOWN_VERB";

        private readonly Dictionary<string, bool> _verbs = new Dictionary<string, bool>();

        /// <summary>requiresIdentity: tests, medicines and transfers, which are rejected until identity is confirmed.</summary>
        public void Register(string verb, bool requiresIdentity = false) => _verbs[verb] = requiresIdentity;

        public bool IsKnown(string verb) => _verbs.ContainsKey(verb);

        public bool RequiresIdentity(string verb) => _verbs.TryGetValue(verb, out var r) && r;
    }
}
