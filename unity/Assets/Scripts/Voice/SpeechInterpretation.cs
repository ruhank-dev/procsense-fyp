using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using ProcSense.Core.Actions;
using ProcSense.Core.AIContext;
using ProcSense.Core.Logging;
using ProcSense.Core.Speech;
using ProcSense.VR.Auth;
using ProcSense.VR.Bridge;
using UnityEngine;

namespace ProcSense.VR.Voice
{
    [Serializable] public class InterpretEvidenceDto { public string act, behaviour, span, status, utterance_id; public double confidence; }
    [Serializable] public class InterpretMenuDto { public string behaviour, label; public string[] actions; }
    [Serializable] public class InterpretDto { public string decision, clarify; public InterpretEvidenceDto evidence; public string[] options, points_to, reasons; public InterpretMenuDto[] menu; }

    public enum InterpretDecision { OfferEvidence, Clarify, Abstain, Menu }

    /// <summary>What the interpretation service answered for one utterance, or the safe answer (the menu) when it could not be reached or understood. Nothing in it is credit: an offer is applied to the session as a speech_evidence action and the engine decides.</summary>
    public sealed class InterpretResult
    {
        public InterpretDecision Decision = InterpretDecision.Menu;
        public InterpretEvidenceDto Evidence;
        public string Clarify = "";
        public string[] Options = new string[0];
        public InterpretMenuDto[] Menu = new InterpretMenuDto[0];
        public string[] PointsTo = new string[0];
        public string[] Reasons = new string[0];
        /// <summary>True when the answer came from the service; false when this is the local safe answer (no network, a bad reply, cancelled).</summary>
        public bool FromService;
    }

    /// <summary>Lets the trainee's next utterance, leaving the step or closing the panel drop an answer that has not come back yet.</summary>
    public sealed class InterpretCancellation
    {
        public bool Cancelled { get; private set; }
        public void Cancel() => Cancelled = true;
    }

    /// <summary>
    /// The Unity half of the speech interpretation hook (p1-ai-language-s12, ADR-0022; hook 13 of docs/architecture/unity_ai_hooks.md). It asks the service what an utterance performs, given the engine's own context, and applies an
    /// OFFER_EVIDENCE to the session as one logged <c>speech_evidence</c> action. It never decides credit and never blocks play: any failure (no sign-in, no network, a slow or unreadable answer, a cancelled request) is the menu.
    /// At most one clarification is asked per utterance; a repeated answer for the same utterance is applied once; an answer that arrives after a cancellation is dropped.
    /// </summary>
    public sealed class SpeechInterpretation
    {
        public const string Route = "/interpret/organisations/";
        private readonly HashSet<string> _applied = new HashSet<string>(StringComparer.Ordinal);

        /// <summary>How many clarifications the current utterance has used (the service allows one).</summary>
        public int ClarificationsUsed { get; private set; }

        /// <summary>A new utterance starts: no clarification used yet.</summary>
        public void NewUtterance() => ClarificationsUsed = 0;

        public static InterpretResult SafeMenu() => new InterpretResult { Decision = InterpretDecision.Menu, Reasons = new[] { "UNAVAILABLE" } };

        /// <summary>Reads the service's answer. Never throws: an unreadable or unknown answer is the menu.</summary>
        public static InterpretResult Parse(string body)
        {
            InterpretDto dto = null;
            try { dto = JsonUtility.FromJson<InterpretDto>(body); } catch (ArgumentException) { }
            if (dto == null || string.IsNullOrEmpty(dto.decision)) return new InterpretResult { Reasons = new[] { "UNREADABLE" } };
            var r = new InterpretResult
            {
                FromService = true, Clarify = dto.clarify ?? "", Options = dto.options ?? new string[0], Menu = dto.menu ?? new InterpretMenuDto[0],
                PointsTo = dto.points_to ?? new string[0], Reasons = dto.reasons ?? new string[0]
            };
            switch (dto.decision)
            {
                case "OFFER_EVIDENCE":
                    if (dto.evidence == null || string.IsNullOrEmpty(dto.evidence.act) || string.IsNullOrEmpty(dto.evidence.utterance_id)) return new InterpretResult { Reasons = new[] { "UNREADABLE" } };
                    r.Decision = InterpretDecision.OfferEvidence; r.Evidence = dto.evidence; break;
                case "CLARIFY": r.Decision = r.Clarify.Length > 0 ? InterpretDecision.Clarify : InterpretDecision.Menu; break;
                case "ABSTAIN": r.Decision = InterpretDecision.Abstain; break;
                case "MENU": r.Decision = InterpretDecision.Menu; break;
                default: return new InterpretResult { Reasons = new[] { "UNREADABLE" } };
            }
            return r;
        }

        /// <summary>The request body: the engine's context, the words, the utterance id and the clarifications used.</summary>
        public static string RequestBody(InterpretationContext context, string utterance, string utteranceId, int clarificationsUsed)
        {
            var sb = new StringBuilder("{\"context\":").Append(context.ToJson()).Append(",\"utterance\":");
            JsonText.Quote(sb, utterance);
            sb.Append(",\"utterance_id\":");
            JsonText.Quote(sb, utteranceId);
            return sb.Append(",\"clarifications_used\":").Append(clarificationsUsed.ToString(CultureInfo.InvariantCulture)).Append('}').ToString();
        }

        /// <summary>
        /// Asks the service. <paramref name="done"/> always runs exactly once, unless the request was cancelled before the answer came (then it does not run: the late answer is dropped).
        /// </summary>
        public IEnumerator Interpret(AuthService auth, SimulationHost host, string utterance, string utteranceId, IReadOnlyList<ContextTurn> turns, string addressee, InterpretCancellation cancel, Action<InterpretResult> done)
        {
            if (auth == null || !auth.IsSignedIn || host == null || host.Session == null || host.Content == null) { if (cancel == null || !cancel.Cancelled) done(SafeMenu()); yield break; }
            var context = InterpretationContextBuilder.Build(host.Session, host.Content, turns ?? new List<ContextTurn>(), addressee);
            HttpResult answer = default;
            yield return auth.Authorized("POST", Route + auth.OrganisationId, RequestBody(context, utterance, utteranceId, ClarificationsUsed), r => answer = r);
            if (cancel != null && cancel.Cancelled) yield break;
            var result = answer.Ok ? Parse(answer.Body) : SafeMenu();
            if (result.Decision == InterpretDecision.Clarify) ClarificationsUsed++;
            done(result);
        }

        /// <summary>The engine action for an offer: <c>speech_evidence</c> with the words, the confidence and the utterance id. The engine stamps the steps and decides whether it counts.</summary>
        public static SimAction ToAction(InterpretEvidenceDto e) => new SimAction(SpeechEvidenceSubsystem.Verb, "", Channel.Vr, new Dictionary<string, string>
        {
            [SpeechEvidenceSubsystem.ActParam] = e.act, [SpeechEvidenceSubsystem.SpanParam] = e.span ?? "", [SpeechEvidenceSubsystem.ConfidenceParam] = e.confidence.ToString("0.###", CultureInfo.InvariantCulture),
            [SpeechEvidenceSubsystem.UtteranceParam] = e.utterance_id, [SpeechEvidenceSubsystem.StatusParam] = string.IsNullOrEmpty(e.status) ? "actual" : e.status
        });

        /// <summary>Applies an offer to the session once per utterance id. Returns true if an action was sent. A decision other than an offer, a repeated utterance or a missing session sends nothing.</summary>
        public bool Apply(SimulationHost host, InterpretResult result)
        {
            if (host == null || host.Session == null || result == null || result.Decision != InterpretDecision.OfferEvidence || result.Evidence == null) return false;
            if (!_applied.Add(result.Evidence.utterance_id)) return false;
            host.Apply(ToAction(result.Evidence));
            return true;
        }
    }
}
