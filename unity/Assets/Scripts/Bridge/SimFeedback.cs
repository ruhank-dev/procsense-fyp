using System;
using System.Collections.Generic;
using ProcSense.Core;
using ProcSense.Core.Actions;
using ProcSense.Core.Events;
using ProcSense.Core.Workflow;

namespace ProcSense.VR.Bridge
{
    /// <summary>The kinds of the feedback stream (p3-15 SimFeedbackEvent).</summary>
    public enum FeedbackKind { ActionAccepted, ActionRejected, GoalCompleted, GoalMissed, ScoreItem, Alarm, Phase, EndState }

    /// <summary>One thing the trainee should notice (p3-15 SimFeedbackEvent): its kind, the ids involved, a text when there is one, and the engine step.</summary>
    public sealed class SimFeedbackEvent
    {
        public FeedbackKind Kind { get; }
        public IReadOnlyList<string> Ids { get; }
        public string Text { get; }
        public long Step { get; }
        public SimFeedbackEvent(FeedbackKind kind, IReadOnlyList<string> ids, string text, long step) { Kind = kind; Ids = ids; Text = text ?? ""; Step = step; }
        public string Id => Ids.Count > 0 ? Ids[0] : "";
        public override string ToString() => Kind + ":" + string.Join(",", Ids) + (Text.Length > 0 ? " (" + Text + ")" : "") + "@" + Step;
    }

    /// <summary>
    /// The feedback stream (P3-SES-10): one event per thing the trainee should notice, for the training board ticks (TRN-05), the sounds (SND-05), the contextual panels (CTX-06) and the results.
    /// It only reads: actions as the host applied them (accepted or refused, with the trainee text), the gateway's own refusals that never reached the engine (duplicate, practice-only),
    /// new entries of the case graph's history (a goal completed with its score items, a goal missed), clinical events fired by the event director (alarms), the end state once resolved,
    /// and the game-flow phase. Nothing here changes the session. <see cref="Poll"/> runs after every action and every tick; a new session is picked up by itself.
    /// </summary>
    public sealed class SimFeedback
    {
        public event Action<SimFeedbackEvent> Raised;

        /// <summary>The events raised so far in this session (the results and tests read it).</summary>
        public IReadOnlyList<SimFeedbackEvent> History => _history;

        private readonly List<SimFeedbackEvent> _history = new List<SimFeedbackEvent>();
        private Session _session;
        private int _graphSeen;
        private string _endState = "";
        private string _phase;
        private readonly Func<Session> _sessionOf;
        private readonly Func<string> _phaseOf;

        private static SimFeedback _default;
        /// <summary>The app's stream: the running host, the action gateway and the game flow's phase.</summary>
        public static SimFeedback Default
        {
            get
            {
                if (_default != null) return _default;
                _default = new SimFeedback(() => SimulationHost.Instance != null ? SimulationHost.Instance.Session : null,
                                           () => Flow.MenuFlow.Current != null ? Flow.MenuFlow.Current.Phase.ToString() : null);
                ActionGateway.Answered += _default.OnGatewayOutcome;
                return _default;
            }
        }

        public SimFeedback(Func<Session> session, Func<string> phase = null)
        {
            _sessionOf = session ?? (() => null);
            _phaseOf = phase ?? (() => null);
        }

        /// <summary>Connects to a host's action and change events (the app does this once per host; tests call <see cref="OnApplied"/> and <see cref="Poll"/> directly).</summary>
        public void Attach(SimulationHost host)
        {
            if (host == null) return;
            host.Applied += OnApplied;
            host.Changed += _ => Poll();
        }

        /// <summary>An action the host applied: accepted, or refused with the trainee text of its reason.</summary>
        public void OnApplied(SimAction action, ActionOutcome outcome)
        {
            Poll();   // a new session is noticed before its first action is reported
            long step = _session != null ? _session.Clock.Steps : 0;
            var ids = new[] { action.Verb, action.Target };
            if (outcome.Accepted) Raise(new SimFeedbackEvent(FeedbackKind.ActionAccepted, ids, "", step));
            else Raise(new SimFeedbackEvent(FeedbackKind.ActionRejected, new[] { action.Verb, action.Target, outcome.RejectReason }, HintTexts.For(outcome.RejectReason), step));
            Poll();
        }

        /// <summary>The gateway's refusals that never reached the engine (the host reports everything else).</summary>
        public void OnGatewayOutcome(ActionRequest request, GatewayOutcome o)
        {
            if (o.Engine != null || o.Accepted || request == null || request.Action == null) return;
            Raise(new SimFeedbackEvent(FeedbackKind.ActionRejected, new[] { request.Action.Verb, request.Action.Target, o.ReasonCode }, o.HintText, o.EngineStep));
        }

        /// <summary>Looks for what changed since the last call: a new session, new case-graph entries, the end state, the flow phase.</summary>
        public void Poll()
        {
            var s = _sessionOf();
            if (!ReferenceEquals(s, _session))
            {
                _session = s;
                _graphSeen = 0;
                _endState = "";
                _history.Clear();
                if (s != null) s.Events.Subscribe<EventFired>(OnEventFired);   // the bus has no unsubscribe: a finished session is simply no longer polled
            }
            string phase = _phaseOf();
            if (phase != null && phase != _phase)
            {
                _phase = phase;
                Raise(new SimFeedbackEvent(FeedbackKind.Phase, new[] { phase }, "", s != null ? s.Clock.Steps : 0));
            }
            if (s == null) return;
            var graph = s.GraphHistory;
            for (; _graphSeen < graph.Count; _graphSeen++)
            {
                var g = graph[_graphSeen];
                if (g.Kind == "goal_complete")
                {
                    Raise(new SimFeedbackEvent(FeedbackKind.GoalCompleted, new[] { g.Id }, "", g.Step));
                    foreach (var item in g.ScoreItems) Raise(new SimFeedbackEvent(FeedbackKind.ScoreItem, new[] { item, g.Id }, "", g.Step));
                }
                else if (g.Kind == "goal_missed") Raise(new SimFeedbackEvent(FeedbackKind.GoalMissed, new[] { g.Id }, "", g.Step));
            }
            string end = s.EndStates != null ? s.EndStates.Resolved : "";
            if (!string.IsNullOrEmpty(end) && end != _endState)
            {
                _endState = end;
                Raise(new SimFeedbackEvent(FeedbackKind.EndState, new[] { end }, "", s.Clock.Steps));
            }
        }

        private void OnEventFired(EventFired e)
        {
            if (!ReferenceEquals(_sessionOf(), _session)) return;
            Raise(new SimFeedbackEvent(FeedbackKind.Alarm, new[] { e.Event }, "", e.Step));
        }

        private void Raise(SimFeedbackEvent e)
        {
            _history.Add(e);
            Raised?.Invoke(e);
        }
    }
}
