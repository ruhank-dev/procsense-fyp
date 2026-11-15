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
    }

    /// <summary>
    /// The one door every action goes through (P3-SES-03, the mediator of p3-01): a menu press, the tablet, a physical emitter and a spoken sentence all become an
    /// <see cref="ActionRequest"/> and meet the same rules here before the engine sees them:
    /// 1. a request id is answered once: the same id again (a double click delivered twice, a retried voice call) gets the first answer back, marked duplicate;
    /// 2. the same verb, target and parameters from any source within <see cref="DuplicateWindowSeconds"/> after an accepted one is answered DUPLICATE without reaching the engine
    ///    (the spoken-order rule, generalised);
    /// 3. a practice-only aid is refused in the examination (<see cref="ModeGate"/>);
    /// 4. everything else goes to the simulation (SimulationHost.Apply: NO_SESSION, SESSION_PAUSED and every clinical rule stay where they are).
    /// One <see cref="Outcome"/> event carries every answer. The engine decides what is allowed; the gateway only removes repeats and routes.
    /// </summary>
    public sealed class ActionGateway
    {
        public const float DuplicateWindowSeconds = 8f;
        public const string DuplicateCode = "DUPLICATE", PracticeOnlyCode = "PRACTICE_ONLY", InvalidCode = "INVALID_REQUEST";
        private const int RememberedIds = 128;

        private static ActionGateway _default;
        /// <summary>The app's gateway, sending to <see cref="SimulationHost.Instance"/>.</summary>
        public static ActionGateway Default => _default ?? (_default = new ActionGateway(new HostTarget(), () => UnityEngine.Time.realtimeSinceStartup));

        public event Action<ActionRequest, GatewayOutcome> Outcome;

        private readonly IActionTarget _target;
        private readonly Func<float> _now;
        private readonly Func<bool> _practiceAids;
        private readonly Dictionary<string, GatewayOutcome> _answered = new Dictionary<string, GatewayOutcome>();
        private readonly Queue<string> _answerOrder = new Queue<string>();
        private readonly Dictionary<string, float> _acceptedAt = new Dictionary<string, float>();
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
            if (request == null || request.Action == null || string.IsNullOrEmpty(request.Action.Verb)) return Finish(request, Refused(request, InvalidCode), remember: false);
            if (_answered.TryGetValue(request.RequestId, out var earlier)) return Raise(request, Copy(earlier, duplicate: true));
            if (_inFlight.Contains(request.RequestId)) return Raise(request, Duplicate(request));             // re-entered while the same request is being applied

            string key = Key(request.Action);
            float now = _now();
            if (_acceptedAt.TryGetValue(key, out var at) && now - at < DuplicateWindowSeconds) return Finish(request, Duplicate(request));
            if (request.PracticeOnly && !_practiceAids()) return Finish(request, Refused(request, PracticeOnlyCode));

            _inFlight.Add(request.RequestId);
            ActionOutcome engine;
            try { engine = _target.Apply(request.Action); }
            finally { _inFlight.Remove(request.RequestId); }
            var o = new GatewayOutcome { RequestId = request.RequestId, Accepted = engine.Accepted, ReasonCode = engine.Accepted ? "" : engine.RejectReason, Engine = engine, EngineStep = _target.Step };
            if (!engine.Accepted) o.HintText = HintTexts.For(engine.RejectReason);
            else _acceptedAt[key] = now;
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

        private GatewayOutcome Raise(ActionRequest r, GatewayOutcome o) { Outcome?.Invoke(r, o); return o; }

        /// <summary>The running simulation of the app.</summary>
        private sealed class HostTarget : IActionTarget
        {
            public ActionOutcome Apply(SimAction action)
            {
                var host = SimulationHost.Instance;
                if (host != null) return host.Apply(action);
                var o = new ActionOutcome();
                o.Reject("NO_SESSION");
                return o;
            }

            public long Step { get { var h = SimulationHost.Instance; return h != null && h.Session != null ? h.Session.Clock.Steps : 0; } }
        }
    }
}
