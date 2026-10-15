using System.Collections.Generic;

namespace ProcSense.Core.Actions
{
    /// <summary>The verbs the engine understands. Subsystems register theirs in Init; anything else is UNKNOWN_VERB.</summary>
    public sealed class VerbRegistry
    {
        public const string UnknownVerb = "UNKNOWN_VERB";

        private readonly Dictionary<string, (bool identity, bool patient)> _verbs = new Dictionary<string, (bool, bool)>();

        /// <summary>requiresIdentity: tests, medicines and transfers, which are rejected until identity is confirmed. requiresPatient: actions aimed at the patient, which are refused while the patient is in another place.</summary>
        public void Register(string verb, bool requiresIdentity = false, bool requiresPatient = false) => _verbs[verb] = (requiresIdentity, requiresPatient);

        public bool IsKnown(string verb) => _verbs.ContainsKey(verb);

        /// <summary>Every registered verb (for tests that prove a verb does not exist, for example one that would switch the mode).</summary>
        public IReadOnlyCollection<string> Known => _verbs.Keys;

        public bool RequiresIdentity(string verb) => _verbs.TryGetValue(verb, out var r) && r.identity;

        public bool RequiresPatient(string verb) => _verbs.TryGetValue(verb, out var r) && r.patient;
    }
}
