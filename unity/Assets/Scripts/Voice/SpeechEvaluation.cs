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
    [Serializable] public class JudgedActDto { public string objective, assertion, actor, recipient, form, evidence, rationale; public bool performed_now; public int relevance, correctness, completeness; }
    [Serializable] public class EvaluateMenuDto { public string behaviour, label; public string[] actions; }
    [Serializable] public class EvaluateDto { public string decision, utterance_id, physical_action; public JudgedActDto[] acts; public bool claims_physical_action, critical_topic; public EvaluateMenuDto[] menu; public string[] reasons; }

    public enum EvaluateDecision { Evaluated, Unavailable }

    /// <summary>What the evaluation service answered for one utterance, or the safe answer (unavailable, the menu stays) when it could not be reached or understood. Nothing in it is credit and nothing in it is a score: the judged objectives are applied to the session as logged actions and the engine decides what they are worth.</summary>
    public sealed class EvaluateResult
    {
        public EvaluateDecision Decision = EvaluateDecision.Unavailable;
        public string UtteranceId = "";
        public JudgedActDto[] Acts = new JudgedActDto[0];
        public bool ClaimsPhysicalAction, CriticalTopic;
        public EvaluateMenuDto[] Menu = new EvaluateMenuDto[0];
        public string[] Reasons = new string[0];
        /// <summary>True when the answer came from the service; false when this is the local safe answer (no network, a bad reply, cancelled).</summary>
        public bool FromService;
    }

    /// <summary>Lets the trainee's next utterance, leaving the step or closing the panel drop an answer that has not come back yet.</summary>
    public sealed class EvaluateCancellation
    {
        public bool Cancelled { get; private set; }
        public void Cancel() => Cancelled = true;
    }

    /// <summary>
    /// The Unity half of the speech evaluation hook (p1-ai-language-s22, ADR-0025; hook 13 of docs/architecture/unity_ai_hooks.md). It asks the service what an utterance does, given the engine's own context, and applies every judged objective to the session as one logged
    /// <c>speech_evidence</c> action. It never decides credit, never scores and never blocks play: any failure (no sign-in, no network, a slow or unreadable answer, a cancelled request) is the safe answer, the menu of verified actions stays open and no speech credit follows.
    /// The words of the utterance are not part of the action (only the evidence span the model copied); a physical-action claim is carried as a note and can never make a physical action count: only the simulator's own accepted actions do.
    /// A repeated answer for the same utterance is applied once; an answer that arrives after a cancellation is dropped.
    /// </summary>
    public sealed class SpeechEvaluation
    {
        public const string Route = "/evaluate/organisations/";
        private readonly HashSet<string> _applied = new HashSet<string>(StringComparer.Ordinal);

        public static EvaluateResult SafeUnavailable() => new EvaluateResult { Decision = EvaluateDecision.Unavailable, Reasons = new[] { "UNAVAILABLE" } };

        /// <summary>Reads the service's answer. Never throws: an unreadable or unknown answer is the safe answer.</summary>
        public static EvaluateResult Parse(string body)
        {
            EvaluateDto dto = null;
            try { dto = JsonUtility.FromJson<EvaluateDto>(body); } catch (ArgumentException) { }
            if (dto == null || string.IsNullOrEmpty(dto.decision)) return new EvaluateResult { Reasons = new[] { "UNREADABLE" } };
            var r = new EvaluateResult { FromService = true, UtteranceId = dto.utterance_id ?? "", Menu = dto.menu ?? new EvaluateMenuDto[0], Reasons = dto.reasons ?? new string[0] };
            switch (dto.decision)
            {
                case "EVALUATED":
                    var acts = new List<JudgedActDto>();
                    foreach (var a in dto.acts ?? new JudgedActDto[0])
                    {
                        if (a == null || string.IsNullOrEmpty(a.objective)) return new EvaluateResult { Reasons = new[] { "UNREADABLE" } };
                        acts.Add(a);
                    }
                    r.Decision = EvaluateDecision.Evaluated; r.Acts = acts.ToArray(); r.ClaimsPhysicalAction = dto.claims_physical_action; r.CriticalTopic = dto.critical_topic; break;
                case "UNAVAILABLE": r.Decision = EvaluateDecision.Unavailable; break;
                default: return new EvaluateResult { Reasons = new[] { "UNREADABLE" } };
            }
            return r;
        }

        /// <summary>The request body: the engine's context, the words and the utterance id.</summary>
        public static string RequestBody(InterpretationContext context, string utterance, string utteranceId)
        {
            var sb = new StringBuilder("{\"context\":").Append(context.ToJson()).Append(",\"utterance\":");
            JsonText.Quote(sb, utterance);
            sb.Append(",\"utterance_id\":");
            JsonText.Quote(sb, utteranceId);
            return sb.Append('}').ToString();
        }

        /// <summary>Asks the service. <paramref name="done"/> always runs exactly once, unless the request was cancelled before the answer came (then it does not run: the late answer is dropped).</summary>
        public IEnumerator Evaluate(AuthService auth, SimulationHost host, string utterance, string utteranceId, IReadOnlyList<ContextTurn> turns, string addressee, EvaluateCancellation cancel, Action<EvaluateResult> done)
        {
            if (auth == null || !auth.IsSignedIn || host == null || host.Session == null || host.Content == null) { if (cancel == null || !cancel.Cancelled) done(SafeUnavailable()); yield break; }
            var context = InterpretationContextBuilder.Build(host.Session, host.Content, turns ?? new List<ContextTurn>(), addressee);
            HttpResult answer = default;
            yield return auth.Authorized("POST", Route + auth.OrganisationId, RequestBody(context, utterance, utteranceId), r => answer = r);
            if (cancel != null && cancel.Cancelled) yield break;
            done(answer.Ok ? Parse(answer.Body) : SafeUnavailable());
        }

        /// <summary>The engine action for one judged objective: <c>speech_evidence</c> with the objective, the evidence span, the assertion, actor, form and the three grades. The engine stamps the steps and decides whether and how much it counts.</summary>
        public static SimAction ToAction(string utteranceId, JudgedActDto a, bool claimsPhysical) => new SimAction(SpeechEvidenceSubsystem.Verb, "", Channel.Vr, new Dictionary<string, string>
        {
            [SpeechEvidenceSubsystem.ActParam] = a.objective, [SpeechEvidenceSubsystem.SpanParam] = string.IsNullOrWhiteSpace(a.evidence) ? a.objective : a.evidence, [SpeechEvidenceSubsystem.UtteranceParam] = utteranceId,
            [SpeechEvidenceSubsystem.AssertionParam] = a.assertion ?? "", [SpeechEvidenceSubsystem.ActorParam] = a.actor ?? "", [SpeechEvidenceSubsystem.FormParam] = a.form ?? "",
            [SpeechEvidenceSubsystem.RelevanceParam] = a.relevance.ToString(CultureInfo.InvariantCulture), [SpeechEvidenceSubsystem.CorrectnessParam] = a.correctness.ToString(CultureInfo.InvariantCulture),
            [SpeechEvidenceSubsystem.CompletenessParam] = a.completeness.ToString(CultureInfo.InvariantCulture), [SpeechEvidenceSubsystem.ClaimsPhysicalParam] = claimsPhysical ? "true" : "false"
        });

        /// <summary>Applies every judged objective of an answer to the session, once per utterance id. Returns how many actions were sent. An unavailable answer, a repeated utterance or a missing session sends nothing.</summary>
        public int Apply(SimulationHost host, EvaluateResult result)
        {
            if (host == null || host.Session == null || result == null || result.Decision != EvaluateDecision.Evaluated || result.Acts.Length == 0 || string.IsNullOrEmpty(result.UtteranceId)) return 0;
            if (!_applied.Add(result.UtteranceId)) return 0;
            foreach (var a in result.Acts) host.Apply(ToAction(result.UtteranceId, a, result.ClaimsPhysicalAction));
            return result.Acts.Length;
        }
    }
}
