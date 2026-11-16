using System;
using System.Collections.Generic;
using System.Text;
using ProcSense.Core.Actions;

namespace ProcSense.VR.Bridge
{
    /// <summary>Where an action request came from (p3-15 contracts). The SimAction keeps its own channel, which the engine uses for credit; the source is for the gateway and the logs.</summary>
    public enum ActionSource { Menu, Tablet, Physical, Voice }

    /// <summary>One request to act (p3-15 ActionRequest): an id, the action, its source, the entity it was aimed at, and whether it is a practice-only aid.</summary>
    public sealed class ActionRequest
    {
        public string RequestId { get; }
        public SimAction Action { get; }
        public ActionSource Source { get; }
        public string EntityId { get; }
        /// <summary>A practice-only aid (shown only with practice aids): refused in the examination with PRACTICE_ONLY.</summary>
        public bool PracticeOnly { get; }

        public ActionRequest(SimAction action, ActionSource source, string entityId = "", bool practiceOnly = false, string requestId = null)
        {
            Action = action;
            Source = source;
            EntityId = entityId ?? "";
            PracticeOnly = practiceOnly;
            RequestId = string.IsNullOrEmpty(requestId) ? Guid.NewGuid().ToString("N") : requestId;
        }
    }

    /// <summary>The answer to a request (p3-15 ActionOutcome): accepted or the reason code, the trainee-facing text of a refusal, whether it was a duplicate, and the engine step after it.</summary>
    public sealed class GatewayOutcome
    {
        public string RequestId { get; internal set; } = "";
        public bool Accepted { get; internal set; }
        public string ReasonCode { get; internal set; } = "";
        public string HintText { get; internal set; } = "";
        public bool Duplicate { get; internal set; }
        public long EngineStep { get; internal set; }
        /// <summary>The engine's own outcome (effects), or null when the request never reached the engine.</summary>
        public ActionOutcome Engine { get; internal set; }
    }

    /// <summary>What the gateway sends actions to: the running simulation in the app, a fake in tests.</summary>
    public interface IActionTarget
    {
        ActionOutcome Apply(SimAction action);
        long Step { get; }
        /// <summary>Changes when a new session starts (the gateway then forgets its duplicate window and answered ids).</summary>
        object Epoch { get; }
    }

    /// <summary>
    /// The one door every action goes through (P3-SES-03, the mediator of p3-01): a menu press, the tablet, a physical emitter and a spoken sentence all become an
    /// <see cref="ActionRequest"/> and meet the same rules here before the engine sees them:
    /// 1. a request id is answered once: the same id again (a double click delivered twice, a retried voice call) gets the first answer back, marked duplicate;
    /// 2. the same verb, target and parameters within <see cref="DuplicateWindowSeconds"/> after an accepted one is answered DUPLICATE without reaching the engine when either of the two
    ///    came from speech (the spoken-order rule, generalised across sources: a sentence repeated by the recogniser, or said and then pressed). Two presses of a panel or two touches
    ///    of an object are not judged here: they have their own debounce, and the correct paths repeat actions on purpose (a wristband read again, an observation repeated);
    /// 3. a practice-only aid is refused in the examination (<see cref="ModeGate"/>);
    /// 4. everything else goes to the simulation (SimulationHost.Apply: NO_SESSION, SESSION_PAUSED and every clinical rule stay where they are).
    /// One <see cref="Outcome"/> event carries every answer. The engine decides what is allowed; the gateway only removes repeats and routes.
    /// </summary>
    public sealed class ActionGateway
    {
        public const float DuplicateWindowSeconds = 8f;
        public const string DuplicateCode = "DUPLICATE", PracticeOnlyCode = "PRACTICE_ONLY", InvalidCode = "INVALID_REQUEST";
        private const int RememberedIds = 128;

        /// <summary>
        /// Control verbs that are legitimately sent again within seconds and answered by the engine itself: pause and resume, a second hint (it gives the next tier),
        /// and submission (the engine refuses a second one with its own reason). The duplicate window is for orders and double presses, not for these.
        /// </summary>
        public static readonly HashSet<string> Repeatable = new HashSet<string>(StringComparer.Ordinal)
        { ProcSense.Core.SessionControl.Pause, ProcSense.Core.SessionControl.Resume, ProcSense.Core.ExamControl.RequestHint, ProcSense.Core.ExamControl.SubmitPatient };

        private static readonly Dictionary<SimulationHost, ActionGateway> PerHost = new Dictionary<SimulationHost, ActionGateway>();

        /// <summary>The gateway of a host (one per host, made on first use); the running host's when none is given.</summary>
        public static ActionGateway For(SimulationHost host = null)
        {
            host = host != null ? host : SimulationHost.Instance;
            if (host == null) return Default;
            foreach (var dead in new List<SimulationHost>(PerHost.Keys)) if (dead == null) PerHost.Remove(dead);   // hosts of finished scenes and tests
            if (!PerHost.TryGetValue(host, out var g)) PerHost[host] = g = new ActionGateway(new HostTarget(host), () => UnityEngine.Time.realtimeSinceStartup);
            return g;
        }

        /// <summary>
        /// The drop-in for <c>SimulationHost.Apply</c> (P3-SES-04): sends the action through the host's gateway with its source and returns the engine's outcome, or a refusal with the
        /// gateway's reason when it never reached the engine (DUPLICATE, PRACTICE_ONLY), so callers keep reading Accepted and RejectReason as before.
        /// </summary>
        public static ActionOutcome Apply(SimulationHost host, SimAction action, ActionSource source)
        {
            var o = For(host).Submit(new ActionRequest(action, source));
            if (o.Engine != null) return o.Engine;
            var refused = new ActionOutcome();
            refused.Reject(o.ReasonCode);
            return refused;
        }

        private static ActionGateway _default;
        /// <summary>A gateway for when no host exists yet (every request is answered NO_SESSION).</summary>
        public static ActionGateway Default => _default ?? (_default = new ActionGateway(new HostTarget(null), () => UnityEngine.Time.realtimeSinceStartup));

        public event Action<ActionRequest, GatewayOutcome> Outcome;
        /// <summary>Every answer of every gateway (the feedback stream listens here).</summary>
        public static event Action<ActionRequest, GatewayOutcome> Answered;
        private object _epoch;

        private readonly IActionTarget _target;
        private readonly Func<float> _now;
        private readonly Func<bool> _practiceAids;
        private readonly Dictionary<string, GatewayOutcome> _answered = new Dictionary<string, GatewayOutcome>();
        private readonly Queue<string> _answerOrder = new Queue<string>();
        private readonly Dictionary<string, (float at, ActionSource source)> _acceptedAt = new Dictionary<string, (float, ActionSource)>();
        private readonly HashSet<string> _inFlight = new HashSet<string>();

        public ActionGateway(IActionTarget target, Func<float> now, Func<bool> practiceAids = null)
        {
            _target = target ?? throw new ArgumentNullException(nameof(target));
            _now = now ?? throw new ArgumentNullException(nameof(now));
            _practiceAids = practiceAids ?? (() => ModeGate.PracticeAids);
        }

        /// <summary>Forgets the duplicate window and the answered ids (a new session starts).</summary>
        public void Reset() { _answered.Clear(); _answerOrder.Clear(); _acceptedAt.Clear(); _inFlight.Clear(); }

        public GatewayOutcome Submit(ActionRequest request)
        {
            if (!ReferenceEquals(_target.Epoch, _epoch)) { Reset(); _epoch = _target.Epoch; }   // a new session: nothing from the last one counts as a duplicate
            if (request == null || request.Action == null || string.IsNullOrEmpty(request.Action.Verb)) return Finish(request, Refused(request, InvalidCode), remember: false);
            if (_answered.TryGetValue(request.RequestId, out var earlier)) return Raise(request, Copy(earlier, duplicate: true));
            if (_inFlight.Contains(request.RequestId)) return Raise(request, Duplicate(request));             // re-entered while the same request is being applied

            string key = Key(request.Action);
            float now = _now();
            if (!Repeatable.Contains(request.Action.Verb) && _acceptedAt.TryGetValue(key, out var last) && now - last.at < DuplicateWindowSeconds
                && (request.Source == ActionSource.Voice || last.source == ActionSource.Voice)) return Finish(request, Duplicate(request));
            if (request.PracticeOnly && !_practiceAids()) return Finish(request, Refused(request, PracticeOnlyCode));

            _inFlight.Add(request.RequestId);
            ActionOutcome engine;
            try { engine = _target.Apply(request.Action); }
            finally { _inFlight.Remove(request.RequestId); }
            var o = new GatewayOutcome { RequestId = request.RequestId, Accepted = engine.Accepted, ReasonCode = engine.Accepted ? "" : engine.RejectReason, Engine = engine, EngineStep = _target.Step };
            if (!engine.Accepted) o.HintText = HintTexts.For(engine.RejectReason);
            else _acceptedAt[key] = (now, request.Source);
            return Finish(request, o);
        }

        /// <summary>The duplicate key: verb, target and the sorted parameters (the source does not matter: the same order by voice and by tablet is one order).</summary>
        public static string Key(SimAction a)
        {
            var sb = new StringBuilder(a.Verb).Append('|').Append(a.Target);
            foreach (var p in a.Params) sb.Append('|').Append(p.Key).Append('=').Append(p.Value);
            return sb.ToString();
        }

        private GatewayOutcome Duplicate(ActionRequest r) => new GatewayOutcome { RequestId = r.RequestId, Accepted = false, ReasonCode = DuplicateCode, Duplicate = true, EngineStep = _target.Step, HintText = "Already done a moment ago." };

        private GatewayOutcome Refused(ActionRequest r, string code) => new GatewayOutcome { RequestId = r != null ? r.RequestId : "", Accepted = false, ReasonCode = code, EngineStep = _target.Step, HintText = HintTexts.For(code) };

        private static GatewayOutcome Copy(GatewayOutcome o, bool duplicate) => new GatewayOutcome
        { RequestId = o.RequestId, Accepted = o.Accepted, ReasonCode = o.ReasonCode, HintText = o.HintText, Duplicate = duplicate, EngineStep = o.EngineStep, Engine = o.Engine };

        private GatewayOutcome Finish(ActionRequest r, GatewayOutcome o, bool remember = true)
        {
            if (remember && r != null)
            {
                _answered[r.RequestId] = o;
                _answerOrder.Enqueue(r.RequestId);
                while (_answerOrder.Count > RememberedIds) _answered.Remove(_answerOrder.Dequeue());
            }
            return Raise(r, o);
        }

        private GatewayOutcome Raise(ActionRequest r, GatewayOutcome o) { Outcome?.Invoke(r, o); Answered?.Invoke(r, o); return o; }

        /// <summary>A simulation host of the app (none: every request is answered NO_SESSION).</summary>
        private sealed class HostTarget : IActionTarget
        {
            private readonly SimulationHost _host;
            public HostTarget(SimulationHost host) { _host = host; }

            public ActionOutcome Apply(SimAction action)
            {
                if (_host != null) return _host.Apply(action);
                var o = new ActionOutcome();
                o.Reject("NO_SESSION");
                return o;
            }

            public long Step => _host != null && _host.Session != null ? _host.Session.Clock.Steps : 0;
            public object Epoch => _host != null ? _host.Session : null;
        }
    }
}
