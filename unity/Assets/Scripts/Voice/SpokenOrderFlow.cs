using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ProcSense.Core.Actions;
using ProcSense.Core.AIContext;
using ProcSense.Core.Observation;
using ProcSense.VR.Auth;
using ProcSense.VR.Bridge;
using UnityEngine;

namespace ProcSense.VR.Voice
{
    /// <summary>A short, identifier-free description of the scene for the order interpreter: where the trainee is, what has been ordered or requested and how it stands, the transport, the last turns. Facts the trainee could see; nothing hidden, no name, no date of birth.</summary>
    public static class SpokenOrderContext
    {
        public const int MaxChars = 900;

        public static string Build(ObservableState s, IReadOnlyList<ContextTurn> turns)
        {
            var sb = new StringBuilder("Practice session. ");
            if (s != null)
            {
                sb.Append("Location: ").Append(s.Location).Append(". Identity ").Append(s.IdentityConfirmed ? "confirmed" : "not confirmed").Append(". Allergies ").Append(s.AllergiesKnown ? "known" : "not yet asked").Append(". ");
                if (s.Investigations != null && s.Investigations.Count > 0)
                    sb.Append("Investigations: ").Append(string.Join(", ", s.Investigations.Take(8).Select(i => i.Label + " (" + i.State + ")"))).Append(". ");
                if (s.Orders != null && s.Orders.Count > 0)
                    sb.Append("Treatments: ").Append(string.Join(", ", s.Orders.Take(8).Select(o => o.Label + " (" + o.State + ")"))).Append(". ");
                if (s.Transport != null && !string.IsNullOrEmpty(s.Transport.State)) sb.Append("Transport: ").Append(s.Transport.State).Append(string.IsNullOrEmpty(s.Transport.To) ? "" : " to " + s.Transport.To).Append(". ");
                if (s.News2 != null && s.News2.Count > 0) sb.Append("NEWS2 ").Append(s.News2[s.News2.Count - 1].Total).Append(". ");
            }
            if (turns != null && turns.Count > 0)
                sb.Append("Recent turns: ").Append(string.Join(" | ", turns.Skip(Math.Max(0, turns.Count - 4)).Select(t => t.Speaker + ": " + t.Text)));
            var text = sb.ToString().Trim();
            return text.Length > MaxChars ? text.Substring(0, MaxChars) : text;
        }
    }

    public enum OrderPhase { Idle, Proposed, NeedsDetail, Clarifying, Notice }

    /// <summary>
    /// The spoken medical orders of the VR trainee (ADR-0028, 27 Oct 2026), from the words to the engine: <see cref="Offer"/> sends the sentence to the order route (Gemma Cloud first, the offline grammar as the fallback; the route says which answered);
    /// a proposal waits for the trainee's own yes (a button or the spoken word, <see cref="Confirm"/>, <see cref="AnswerBySpeech"/>), a missing detail is chosen from the closed list the content allows (<see cref="Choose"/>), a clarifying question is answered by
    /// saying more or picking an option (<see cref="PickOption"/>). Only a yes that the route accepts releases the action named by the signed single-use ticket; it is then applied with <see cref="SimulationHost.Apply"/>, the one way Unity changes the
    /// simulation, so the engine validates and scores a spoken order exactly as any other action and its refusal is shown to the trainee. Nothing is sent to the engine on a hearing, an expired or used ticket releases nothing, and the same order is not applied twice
    /// within <see cref="DuplicateWindowSeconds"/>. Practice only. Applies no credit.
    /// </summary>
    public sealed class SpokenOrderFlow : MonoBehaviour
    {
        public const float DuplicateWindowSeconds = 8f;
        public const float ProposalSeconds = 60f;            // the ticket's own life on the server
        public const float NoticeSeconds = 12f;              // how long the outcome of an order stays on the panel
        public Func<AuthService> Auth = () => AuthService.Current;
        public Func<SimulationHost> Host = () => SimulationHost.Instance;
        public TeamConversation Talk;
        /// <summary>Replaced by tests; the real ones call the service.</summary>
        public Func<AuthService, string, string, Action<OrderHearing>, IEnumerator> Hearer = (auth, text, context, done) => SpokenOrderClient.Hear(auth, text, context, done);
        public Func<AuthService, string, string, Action<OrderConfirmation>, IEnumerator> Confirmer = (auth, ticket, spoken, done) => SpokenOrderClient.Confirm(auth, ticket, spoken, done);
        public Func<AuthService, string, string, string, Action<OrderHearing>, IEnumerator> Detailer = (auth, ticket, param, value, done) => SpokenOrderClient.Details(auth, ticket, param, value, done);
        /// <summary>Raised whenever what the panel shows changes.</summary>
        public event Action Changed;

        public OrderPhase Phase { get; private set; } = OrderPhase.Idle;
        public OrderHearing Current { get; private set; }
        /// <summary>The line the panel shows: the read-back, the question, or the outcome of the last order.</summary>
        public string Message { get; private set; } = "";
        /// <summary>Which system read the last sentence (also when it was not an order), for the badge of the panel: the trainee and the team see whether Gemma Cloud or the offline grammar is responding.</summary>
        public string InterpreterText { get; private set; } = "";
        public int Heard { get; private set; }
        public int Applied { get; private set; }
        public IReadOnlyList<string> AppliedVerbs => _appliedVerbs;
        public bool Pending => Phase == OrderPhase.Proposed || Phase == OrderPhase.NeedsDetail;

        private readonly List<string> _appliedVerbs = new List<string>();
        private readonly Dictionary<string, float> _recent = new Dictionary<string, float>();
        private float _proposedAt, _noticeAt;
        private bool _busy;

        private void Update()
        {
            if (Pending && Time.realtimeSinceStartup - _proposedAt > ProposalSeconds) Expire();
            else if (Phase == OrderPhase.Notice && Time.realtimeSinceStartup - _noticeAt > NoticeSeconds) Clear();
        }

        private void Raise() => Changed?.Invoke();

        private void Set(OrderPhase phase, string message)
        {
            Phase = phase;
            Message = message;
            if (phase == OrderPhase.Notice) _noticeAt = Time.realtimeSinceStartup;
            if (phase == OrderPhase.Idle) Current = null;
            Raise();
        }

        private void Expire() { Set(OrderPhase.Notice, "That order timed out. Say it again if you still want it."); }

        public void Clear() { Set(OrderPhase.Idle, ""); }

        /// <summary>Sends the sentence to the order route. <paramref name="handled"/> is true when the order panel has taken it over (a proposal to confirm, a question to answer, or "the simulator has no such order"), false when it was not an order and the ordinary speech path should answer.</summary>
        public IEnumerator Offer(string text, Action<bool> handled)
        {
            var host = Host();
            if (host == null || host.Session == null || !ModeGate.PracticeAids || string.IsNullOrWhiteSpace(text)) { handled(false); yield break; }
            Heard++;
            OrderHearing h = null;
            string context = SpokenOrderContext.Build(host.Session.Observable, Talk != null ? Talk.Turns : null);
            yield return Hearer(Auth(), text.Trim(), context, r => h = r);
            if (h == null || !h.FromService)
            {
                InterpreterText = "Orders unavailable: " + (h != null ? h.Reason : "NO_ANSWER") + ". Use the tablet.";
                Raise();
                handled(false);
                yield break;
            }
            InterpreterText = h.InterpreterText;
            if (h.IsProposal) { Present(h); handled(true); yield break; }
            if (h.IsClarification)
            {
                Current = h;
                _proposedAt = Time.realtimeSinceStartup;
                Set(OrderPhase.Clarifying, string.IsNullOrWhiteSpace(h.ReadBack) ? "Which one do you mean? Say it again or choose." : h.ReadBack);
                handled(true);
                yield break;
            }
            if (h.IsUnsupported) { Current = h; Set(OrderPhase.Notice, h.ReadBack); handled(true); yield break; }
            Raise();                                                                      // not an order: only the badge changes
            handled(false);
        }

        private void Present(OrderHearing h)
        {
            Current = h;
            _proposedAt = Time.realtimeSinceStartup;
            bool needs = h.Needs.Count > 0;
            Set(needs ? OrderPhase.NeedsDetail : OrderPhase.Proposed, needs ? h.Needs[0].Question : h.ReadBack);
        }

        /// <summary>The trainee chose one of the alternatives of a clarifying question: it is offered as a new sentence, so it goes through the same route, the same read-back and the same yes.</summary>
        public IEnumerator PickOption(string label, Action<bool> handled)
        {
            if (Phase != OrderPhase.Clarifying) { handled(false); yield break; }
            Clear();
            yield return Offer(label, handled);
        }

        /// <summary>A detail the engine needs (the oxygen device, how the patient is moved) was chosen from the closed list: the route puts it into the signed action and returns a new ticket to confirm.</summary>
        public IEnumerator Choose(string param, string value)
        {
            if (Phase != OrderPhase.NeedsDetail || Current == null || _busy) yield break;
            _busy = true;
            OrderHearing h = null;
            yield return Detailer(Auth(), Current.Ticket, param, value, r => h = r);
            _busy = false;
            if (h == null || !h.IsProposal) { Set(OrderPhase.Notice, h != null && !string.IsNullOrEmpty(h.ReadBack) ? h.ReadBack : "That choice did not work. Say the order again."); yield break; }
            h.Interpreter = Current.Interpreter; h.InterpreterModel = Current.InterpreterModel; h.ReportedModel = Current.ReportedModel; h.FallbackReason = Current.FallbackReason;       // the details step asks no model: the reader of the sentence stays the same
            Present(h);
        }

        public IEnumerator Reject() { return Decide("no"); }
        public IEnumerator Confirm() { return Decide("yes"); }

        /// <summary>The trainee says yes or no. Returns through <paramref name="consumed"/> whether the words answered the pending proposal; anything else is a new sentence for the ordinary path.</summary>
        public IEnumerator AnswerBySpeech(string spoken, Action<bool> consumed)
        {
            if (!Pending || Current == null || string.IsNullOrWhiteSpace(spoken)) { consumed(false); yield break; }
            if (Phase == OrderPhase.NeedsDetail) { consumed(false); yield break; }       // a detail is chosen by button; a sentence meanwhile is a new sentence
            OrderConfirmation c = null;
            yield return Confirmer(Auth(), Current.Ticket, spoken.Trim(), r => c = r);
            if (c == null || c.Open || !c.FromService) { consumed(false); yield break; }
            consumed(true);
            Finish(c);
        }

        private IEnumerator Decide(string spoken)
        {
            if (!Pending || Current == null || _busy) yield break;
            if (Phase == OrderPhase.NeedsDetail && spoken == "yes") yield break;          // nothing to confirm before the detail is chosen
            _busy = true;
            OrderConfirmation c = null;
            yield return Confirmer(Auth(), Current.Ticket, spoken, r => c = r);
            _busy = false;
            if (c == null || !c.FromService) { Set(OrderPhase.Notice, "Could not reach the order service. Nothing was ordered. Use the tablet."); yield break; }
            Finish(c);
        }

        private void Finish(OrderConfirmation c)
        {
            var shown = Current;
            if (c.Cancelled) { Set(OrderPhase.Notice, "Cancelled. Nothing was ordered."); return; }
            if (!c.Released) { Set(OrderPhase.Notice, c.Reason == "TICKET_EXPIRED" ? "That order timed out. Say it again if you still want it." : "That order is no longer open. Say it again."); return; }
            if (c.NoEngineVerb) { Set(OrderPhase.Notice, "Noted: " + (shown != null ? shown.Label : "") + ". The simulator has no action for this one."); return; }
            Apply(c, shown);
        }

        private void Apply(OrderConfirmation c, OrderHearing shown)
        {
            var host = Host();
            if (host == null || host.Session == null) { Set(OrderPhase.Notice, "No session is running."); return; }
            string key = c.Verb + "|" + string.Join(",", c.Params.OrderBy(p => p.Key, StringComparer.Ordinal).Select(p => p.Key + "=" + p.Value));
            if (_recent.TryGetValue(key, out var at) && Time.realtimeSinceStartup - at < DuplicateWindowSeconds) { Set(OrderPhase.Notice, "Already sent a moment ago."); return; }
            _recent[key] = Time.realtimeSinceStartup;
            var action = new SimAction(c.Verb, "patient", Channel.Vr, c.Params.Count == 0 ? null : new Dictionary<string, string>(c.Params));
            var outcome = host.Apply(action);                                                 // the engine decides: a refusal comes back with its reason and nothing changes
            string label = shown != null ? shown.Label : c.Verb;
            if (outcome.Accepted)
            {
                Applied++;
                _appliedVerbs.Add(c.Verb);
                Set(OrderPhase.Notice, "Sent to the team: " + label + ".");
            }
            else
            {
                _recent.Remove(key);                                                          // a refused order may be tried again
                Set(OrderPhase.Notice, "The simulator did not take it: " + HintTexts.For(outcome.RejectReason) + ".");
            }
        }
    }
}
