using System;
using System.Collections.Generic;
using System.Linq;
using ProcSense.Core.Actions;
using ProcSense.VR.ActionEmitters;
using UnityEngine;

namespace ProcSense.VR.Interaction
{
    /// <summary>A concrete emitter for one transition or panel entry: the same class of object a button uses, so the tablet fallback (T-IN5) and the VR gesture send the identical action.</summary>
    public sealed class TransitionEmitter : ActionEmitter
    {
        private void OnEnable() => RegisterFallback();
        private void OnDisable() => UnregisterFallback();
    }

    /// <summary>
    /// Puts an <see cref="EquipmentStateMachine"/> on a prefab. The trainee's gestures call <see cref="Socketed"/>, <see cref="Activated"/>, <see cref="Grabbed"/> and <see cref="Pressed"/>; each sends its one
    /// action through an emitter (which registers the tablet fallback) and the item shows the new state only when Core accepts. A tablet press of the same action moves the item too.
    /// Presentation only: no clinical rule is decided here.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class EquipmentController : MonoBehaviour
    {
        [Serializable]
        public sealed class StateVisual { public string state = ""; public GameObject[] show = Array.Empty<GameObject>(); }

        [SerializeField] private string itemId = "";
        [SerializeField] private List<StateVisual> visuals = new List<StateVisual>();

        private EquipmentStateMachine _machine;
        private readonly Dictionary<Transition, TransitionEmitter> _emitters = new Dictionary<Transition, TransitionEmitter>();
        private bool _stepping;

        public string ItemId { get => itemId; set => itemId = value; }
        public EquipmentStateMachine Machine => _machine;
        public string State => _machine?.State ?? "";
        public List<StateVisual> Visuals => visuals;

        /// <summary>Raised when a step ends, with its result (a view plays the snap haptic on Accepted or Moved and a short refusal cue on Rejected).</summary>
        public event Action<StepResult> StepEnded;

        /// <summary>Builds the machine for this item from its definition and creates one emitter per action transition.</summary>
        public void Bind(EquipmentDefinition definition)
        {
            if (_machine != null) _machine.StateChanged -= ShowState;
            foreach (var e in _emitters.Values) if (e != null) Destroy(e);
            _emitters.Clear();
            _machine = new EquipmentStateMachine(definition); _itemId = definition.Id;
            _machine.StateChanged += ShowState;
            foreach (var t in definition.Transitions.Where(t => !t.VisualOnly))
            {
                var emitter = gameObject.AddComponent<TransitionEmitter>();
                emitter.ConfigureParams(t.Verb, t.Params, t.Target, t.Label);
                var transition = t;
                emitter.Fired += outcome => { if (!_stepping) End(_machine.Resolve(transition, outcome)); };   // a tablet press
                _emitters[t] = emitter;
            }
            ShowState(null, _machine.State);
        }

        public void Socketed(string socket) => Run(Trigger.Socket, socket);
        public void Activated(string socket = "") => Run(Trigger.Activate, socket);
        public void Grabbed() => Run(Trigger.Grab);
        public void UiPressed() => Run(Trigger.Ui);

        /// <summary>A poke-panel entry pressed: sends its action (the panel does not move the item; Core's state shows through the item's views).</summary>
        public ActionOutcome Pressed(PanelEntry entry)
        {
            var host = Bridge.SimulationHost.Instance;
            return host == null ? null : Bridge.ActionGateway.Apply(host, new SimAction(entry.Verb, entry.Target, Channel.Vr, entry.Params), Bridge.ActionSource.Physical);
        }

        private static ActionOutcome Send(SimAction a) { var host = Bridge.SimulationHost.Instance; return host == null ? null : Bridge.ActionGateway.Apply(host, a, Bridge.ActionSource.Physical); }

        private void Run(Trigger trigger, string socket = "")
        {
            if (_machine == null) return;
            var t = _machine.Find(trigger, socket);
            if (t == null) return;
            _stepping = true;
            try
            {
                End(_machine.Step(trigger, a => _emitters.TryGetValue(t, out var e) ? e.Fire() : Send(a), socket));
            }
            finally { _stepping = false; }
        }

        private void End(StepResult result) => StepEnded?.Invoke(result);

        private string _itemId = "";

        private void ShowState(string from, string to)
        {
            if (from != null && _itemId == "ecg_machine" && to == "RECORDED") ProcSense.VR.Audio.SoundPlayer.Instance?.Play("eq_ecg_print");   // the trace prints
            foreach (var v in visuals)
                foreach (var go in v.show)
                    if (go != null) go.SetActive(v.state == to);
        }
    }
}
