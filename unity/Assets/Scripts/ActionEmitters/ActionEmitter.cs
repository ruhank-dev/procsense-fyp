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
        private readonly SortedDictionary<string, string> _moreParams = new SortedDictionary<string, string>(System.StringComparer.Ordinal);
        private ProcSense.Core.Session _lastSession;

        /// <summary>Raised with Core's answer after each emitted action (views use it for hints).</summary>
        public event Action<ActionOutcome> Fired;

        public float DebounceSeconds
        {
            get => debounceSeconds;
            set => debounceSeconds = float.IsNaN(value) || float.IsInfinity(value) ? 0.35f : Mathf.Max(0f, value);
        }

        public void Configure(string verb, string target = "patient", string paramKey = "", string paramValue = "", string fallbackLabel = "")
        {
            _moreParams.Clear();
            this.verb = verb;
            this.target = target;
            this.paramKey = paramKey;
            this.paramValue = paramValue;
            this.fallbackLabel = fallbackLabel;
            RegisterFallback();   // a runtime-configured emitter has already passed OnEnable
        }

        /// <summary>For an action with several parameters (a tablet form: a test and its tube, the four parts of an SBAR): all of them, replacing any set before.</summary>
        public void ConfigureParams(string verb, IReadOnlyDictionary<string, string> parameters, string target = "patient", string fallbackLabel = "")
        {
            Configure(verb, target, "", "", fallbackLabel);
            foreach (var kv in parameters) _moreParams[kv.Key] = kv.Value;
            RegisterFallback();   // again, now with every parameter
        }

        public SimAction BuildAction(string verbOverride = null)
        {
            var p = new Dictionary<string, string>();
            foreach (var kv in _moreParams) p[kv.Key] = kv.Value;
            if (!string.IsNullOrEmpty(paramKey)) p[paramKey] = paramValue;
            return new SimAction(verbOverride ?? verb, target, Channel.Vr, p);
        }

        /// <summary>Sends the action to Core once; returns Core's answer, or null when the debounce swallowed it.</summary>
        public ActionOutcome Fire(string verbOverride = null)
        {
            var host = SimulationHost.Instance;
            if (host == null || host.Session == null) return null;
            if (_lastSession != host.Session)
            {
                _lastSession = host.Session;
                _lastFired = float.NegativeInfinity;
            }
            // Consume the cooldown only when a session can receive the action. Keep it
            // across enable/disable, but never carry it into a replacement session.
            DebounceSeconds = debounceSeconds;
            if (Time.unscaledTime - _lastFired < debounceSeconds) return null;
            _lastFired = Time.unscaledTime;
            var outcome = host.Apply(BuildAction(verbOverride));
            Fired?.Invoke(outcome);
            return outcome;
        }

        /// <summary>Lets the tablet offer the same action as a button (plan tests T01-5, T03-5).</summary>
        protected void RegisterFallback()
        {
            Fallback.TabletFallbackRegistry.Register(this, fallbackLabel, BuildAction());
        }

        protected void UnregisterFallback() => Fallback.TabletFallbackRegistry.Unregister(this);
    }
}
