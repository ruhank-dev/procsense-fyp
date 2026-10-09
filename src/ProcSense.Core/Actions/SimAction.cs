using System;
using System.Collections.Generic;

namespace ProcSense.Core.Actions
{
    public enum Channel { Vr, Tablet, Cli }

    /// <summary>Anything the trainee does. Params are ordinal-sorted so the log is canonical.</summary>
    public sealed class SimAction
    {
        public SimAction(string verb, string target = "", Channel channel = Channel.Cli, IDictionary<string, string>? parameters = null)
        {
            Verb = verb;
            Target = target;
            Channel = channel;
            Params = new SortedDictionary<string, string>(parameters ?? new Dictionary<string, string>(), StringComparer.Ordinal);
        }

        public string Verb { get; }
        public string Target { get; }
        public Channel Channel { get; }
        public SortedDictionary<string, string> Params { get; }
    }

    /// <summary>Result of Session.Apply. Subsystems add effects or call Reject; a rejected action stops the pipeline.</summary>
    public sealed class ActionOutcome
    {
        public bool Accepted { get; private set; } = true;
        public string RejectReason { get; private set; } = "";
        public List<string> Effects { get; } = new List<string>();

        public void Reject(string reason)
        {
            Accepted = false;
            RejectReason = reason;
        }
    }
}
