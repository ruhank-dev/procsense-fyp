using System;
using System.Collections.Generic;
using System.Linq;
using ProcSense.Core.Actions;

namespace ProcSense.VR.Interaction
{
    /// <summary>How a trigger attempt ended.</summary>
    public enum StepResult
    {
        /// <summary>No transition leaves the current state for this trigger (or the socket does not match): nothing sent, nothing changes.</summary>
        NoTransition,
        /// <summary>The transition had no Core action (visual only) and was taken.</summary>
        Moved,
        /// <summary>Core accepted the action and the item moved to the new state.</summary>
        Accepted,
        /// <summary>Core refused (or there was no session): the item stays where it was and the visual reverts (T-IN2).</summary>
        Rejected
    }

    /// <summary>
    /// The state of one item, driven by data. Plain C#, no Unity: a trigger (grab, socket, activate, ui) picks the transition leaving the current state, sends its one action through
    /// <paramref name="send"/> (in the app that is SimulationHost.Apply) and moves only if Core accepts. The machine holds no rules: whether an action is allowed is Core's decision.
    /// </summary>
    public sealed class EquipmentStateMachine
    {
        private readonly EquipmentDefinition _def;

        public EquipmentStateMachine(EquipmentDefinition definition)
        {
            _def = definition ?? throw new ArgumentNullException(nameof(definition));
            State = definition.Initial;
        }

        public string Id => _def.Id;
        public string State { get; private set; }
        public EquipmentDefinition Definition => _def;

        /// <summary>The refusal reason of the last Rejected step, empty otherwise.</summary>
        public string LastReject { get; private set; } = "";

        /// <summary>Raised after every change of state (a view shows the new state's children and material values).</summary>
        public event Action<string, string> StateChanged;

        /// <summary>The transition a trigger would take now, or null. A socket trigger must name the socket the item went into.</summary>
        public Transition Find(Trigger trigger, string socket = "") =>
            _def.From(State).FirstOrDefault(t => t.Trigger == trigger && (trigger != Trigger.Socket || t.Socket == socket)
                                                 && (trigger != Trigger.Activate || t.Socket == "" || t.Socket == socket));

        /// <summary>The action a transition sends (the same one the tablet button sends: T-IN5).</summary>
        public static SimAction BuildAction(Transition t) => new SimAction(t.Verb, t.Target, Channel.Vr, t.Params);

        public StepResult Step(Trigger trigger, Func<SimAction, ActionOutcome> send, string socket = "")
        {
            var t = Find(trigger, socket);
            if (t == null) return StepResult.NoTransition;
            LastReject = "";
            if (t.VisualOnly) { Move(t.To); return StepResult.Moved; }
            return Resolve(t, send?.Invoke(BuildAction(t)));
        }

        /// <summary>
        /// Takes Core's answer to a transition's action: accepted moves the item, anything else leaves it (T-IN2). Also used when the answer comes from the tablet button instead of the VR gesture (T-IN5).
        /// A null answer (no session, or the debounce swallowed the press) changes nothing.
        /// </summary>
        public StepResult Resolve(Transition t, ActionOutcome outcome)
        {
            if (t == null || State != t.From) return StepResult.NoTransition;
            if (outcome == null) { LastReject = "NO_SESSION"; return StepResult.Rejected; }
            if (!outcome.Accepted) { LastReject = outcome.RejectReason; return StepResult.Rejected; }
            LastReject = "";
            Move(t.To);
            return StepResult.Accepted;
        }

        /// <summary>Puts the item back to its first state (a new session).</summary>
        public void Reset()
        {
            LastReject = "";
            Move(_def.Initial);
        }

        /// <summary>The panel buttons that exist in the current state.</summary>
        public IEnumerable<PanelEntry> PanelNow() => _def.Panel.Where(p => p.OnlyIn.Count == 0 || p.OnlyIn.Contains(State));

        private void Move(string to)
        {
            var from = State;
            State = to;
            if (from != to) StateChanged?.Invoke(from, to);
        }
    }
}
