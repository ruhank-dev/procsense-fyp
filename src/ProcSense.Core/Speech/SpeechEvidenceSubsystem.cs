using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using ProcSense.Core.Actions;
using ProcSense.Core.Pipeline;

namespace ProcSense.Core.Speech
{
    /// <summary>
    /// Takes the semantic evaluator's judgement into the session log (p1-ai-language-s22, ADR-0025). The verb <c>speech_evidence</c> carries ONE judged objective of one utterance: <c>act</c> (the objective), <c>span</c> (the words that show it), <c>utterance_id</c>,
    /// <c>assertion</c> (performed, negated, intended_later, hypothetical, reported, other_actor, earlier_claim, uncertain, incomplete), <c>actor</c>, <c>form</c>, the three grades <c>relevance</c>, <c>correctness</c>, <c>completeness</c> (0 to 3) and <c>claims_physical</c> (an audit note).
    /// The subsystem changes NO simulation state and credits nothing: it checks the shape, checks the two states the engine itself holds (a waiting patient question for ANSWER_QUESTION, a waiting read-back for CLOSED_LOOP, <see cref="SpeechStateGate"/>), and stamps the action with the workflow steps
    /// the trainee is in at that moment (<c>steps:2,3</c>, from the flow map, not from the caller). Whether and how much it counts is decided later, by the same rules every time, in <see cref="Scoring.EvidenceStore"/>. It is not an action of any behaviour, so it can never satisfy a procedural or physical requirement.
    /// </summary>
    public sealed class SpeechEvidenceSubsystem : ISubsystem
    {
        public const string Verb = "speech_evidence";
        public const string ActParam = "act", SpanParam = "span", UtteranceParam = "utterance_id", AssertionParam = "assertion", ActorParam = "actor", FormParam = "form";
        public const string RelevanceParam = "relevance", CorrectnessParam = "correctness", CompletenessParam = "completeness", ClaimsPhysicalParam = "claims_physical";
        public const string StepsEffect = "steps:";
        public const string MissingAct = "MISSING_ACT", MissingSpan = "MISSING_SPAN", MissingUtterance = "MISSING_UTTERANCE", BadJudgement = "BAD_JUDGEMENT";
        public const string NoPendingQuestion = SpeechStateGate.NoPendingQuestion, NoReadbackPending = SpeechStateGate.NoReadbackPending;
        public const int MaxSpan = 400;      // the longest utterance the evaluation route accepts: the evidence span is words copied from it (DT-247)

        private Session _s = null!;

        public int Order => PipelineOrder.Events + 2;

        public void Init(Session session)
        {
            _s = session;
            session.Verbs.Register(Verb);
        }

        public void OnAction(SimAction a, ActionOutcome o)
        {
            if (a.Verb != Verb) return;
            if (!Has(a, ActParam)) { o.Reject(MissingAct); return; }
            if (!Has(a, UtteranceParam)) { o.Reject(MissingUtterance); return; }
            if (!Has(a, SpanParam) || a.Params[SpanParam].Length > MaxSpan) { o.Reject(MissingSpan); return; }
            if (Judgement(a.Params) == null) { o.Reject(BadJudgement); return; }
            var refusal = SpeechStateGate.Check(a.Params[ActParam], InterpretationContextBuilder.PatientQuestionPending(_s), InterpretationContextBuilder.ReadbackPending(_s));      // a client that claims a waiting question or read-back it was not given gets nothing logged as evidence (ADR-0023)
            if (refusal != null) { o.Reject(refusal); return; }
            o.Effects.Add(StepsEffect + string.Join(",", _s.RunningSteps.Select(n => n.ToString(CultureInfo.InvariantCulture))));
        }

        /// <summary>The judgement an action carries, or null when any part is missing or outside its closed vocabulary or grade range.</summary>
        public static SpeechJudgement? Judgement(IReadOnlyDictionary<string, string> p)
        {
            if (!p.TryGetValue(AssertionParam, out var asr) || !p.TryGetValue(ActorParam, out var actor) || !p.TryGetValue(FormParam, out var form)) return null;
            if (!Grade(p, RelevanceParam, out var r) || !Grade(p, CorrectnessParam, out var c) || !Grade(p, CompletenessParam, out var k)) return null;
            var j = new SpeechJudgement(asr, actor, form, r, c, k, p.TryGetValue(ClaimsPhysicalParam, out var cp) && cp == "true");
            return j.IsValid ? j : null;
        }

        private static bool Grade(IReadOnlyDictionary<string, string> p, string key, out int g) { g = 0; return p.TryGetValue(key, out var v) && int.TryParse(v, NumberStyles.Integer, CultureInfo.InvariantCulture, out g); }

        public void OnTick() { }

        private static bool Has(SimAction a, string key) => a.Params.TryGetValue(key, out var v) && !string.IsNullOrWhiteSpace(v);
    }
}
