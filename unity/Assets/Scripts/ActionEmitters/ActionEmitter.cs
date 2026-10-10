using System;
using System.Collections.Generic;
using ProcSense.Core.Actions;
using ProcSense.VR.Bridge;
using UnityEngine;

namespace ProcSense.VR.ActionEmitters
{
    /// <summary>
    /// Turns something the trainee does in VR (grab, poke, socket, button) into one logged SimAction. Emitters hold no rules:
    /// whether the action is allowed is Core's decision. Select events can repeat, so a short debounce drops accidental doubles.
    /// </summary>
    public abstract class ActionEmitter : MonoBehaviour
    {
        [SerializeField] protected string verb = "";
        [SerializeField] protected string target = "patient";
        [SerializeField] protected string paramKey = "";
        [SerializeField] protected string paramValue = "";
        [SerializeField] protected string fallbackLabel = "";
        [SerializeField] private float debounceSeconds = 0.35f;

        private float _lastFired = float.NegativeInfinity;

        /// <summary>Raised with Core's answer after each emitted action (views use it for hints).</summary>
        public event Action<ActionOutcome> Fired;

        public float DebounceSeconds { get => debounceSeconds; set => debounceSeconds = value; }

        public void Configure(string verb, string target = "patient", string paramKey = "", string paramValue = "", string fallbackLabel = "")
        {
            this.verb = verb;
            this.target = target;
            this.paramKey = paramKey;
            this.paramValue = paramValue;
            this.fallbackLabel = fallbackLabel;
            RegisterFallback();   // a runtime-configured emitter has already passed OnEnable
        }

        public SimAction BuildAction(string verbOverride = null)
        {
            var p = new Dictionary<string, string>();
            if (!string.IsNullOrEmpty(paramKey)) p[paramKey] = paramValue;
            return new SimAction(verbOverride ?? verb, target, Channel.Vr, p);
        }

        /// <summary>Sends the action to Core once; returns Core's answer, or null when the debounce swallowed it.</summary>
        public ActionOutcome Fire(string verbOverride = null)
        {
            if (Time.unscaledTime - _lastFired < debounceSeconds) return null;
            _lastFired = Time.unscaledTime;
            var host = SimulationHost.Instance;
            if (host == null) return null;
            var outcome = host.Apply(BuildAction(verbOverride));
            Fired?.Invoke(outcome);
            return outcome;
        }

        /// <summary>Lets the tablet offer the same action as a button (plan tests T01-5, T03-5).</summary>
        protected void RegisterFallback()
        {
            if (!string.IsNullOrEmpty(fallbackLabel) && !string.IsNullOrEmpty(verb))
                Fallback.TabletFallbackRegistry.Register(fallbackLabel, BuildAction());
        }
    }
}
