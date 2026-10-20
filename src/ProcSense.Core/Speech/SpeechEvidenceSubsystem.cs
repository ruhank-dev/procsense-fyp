using System.Globalization;
using System.Linq;
using ProcSense.Core.Actions;
using ProcSense.Core.Pipeline;

namespace ProcSense.Core.Speech
{
    /// <summary>
    /// Takes speech evidence into the session log (p1-ai-language-s10, ADR-0022). The verb <c>speech_evidence</c> carries one interpreted utterance that the interpretation service offered:
    /// <c>act</c>, <c>span</c> (the words that show it), <c>confidence</c> (0 to 1), <c>utterance_id</c> and <c>status</c> (actual, or question for an act that is a question).
    /// The subsystem changes NO simulation state and credits nothing: it checks the shape, then stamps the action with the workflow steps the trainee is in at that moment (<c>steps:2,3</c>, taken from the flow map, not from the caller),
    /// so the log carries the context a replay needs. Whether the evidence counts is decided later, by the same rules every time, in <see cref="Scoring.EvidenceStore"/> (critical acts refused, threshold, window, duplicates).
    /// It is not an action of any behaviour, so it can never satisfy a procedural or physical requirement. ANSWER_QUESTION needs a patient question waiting and CLOSED_LOOP a critical result waiting for read-back in the SESSION (the same definitions as the interpretation context), whatever the caller says.
    /// </summary>
    public sealed class SpeechEvidenceSubsystem : ISubsystem
    {
        public const string Verb = "speech_evidence";
        public const string ActParam = "act", SpanParam = "span", ConfidenceParam = "confidence", UtteranceParam = "utterance_id", StatusParam = "status";
        public const string StepsEffect = "steps:";
        public const string MissingAct = "MISSING_ACT", MissingSpan = "MISSING_SPAN", MissingUtterance = "MISSING_UTTERANCE", BadConfidence = "BAD_CONFIDENCE", BadStatus = "BAD_STATUS";
        public const string NoPendingQuestion = "NO_PENDING_QUESTION", NoReadbackPending = "NO_READBACK_PENDING";
        public const int MaxSpan = 300;

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
            if (!Has(a, SpanParam) || a.Params[SpanParam].Length > MaxSpan) { o.Reject(MissingSpan); return; }
            if (!Has(a, UtteranceParam)) { o.Reject(MissingUtterance); return; }
            if (!a.Params.TryGetValue(ConfidenceParam, out var c) || !double.TryParse(c, NumberStyles.Float, CultureInfo.InvariantCulture, out var conf) || double.IsNaN(conf) || conf < 0 || conf > 1) { o.Reject(BadConfidence); return; }
            if (!a.Params.TryGetValue(StatusParam, out var st) || (st != "actual" && st != "question")) { o.Reject(BadStatus); return; }
            // the two acts that answer something the session must be holding: the engine checks that itself, so a client that claims a waiting question or read-back it was not given gets nothing logged as evidence (ADR-0023)
            if (a.Params[ActParam] == "ANSWER_QUESTION" && !InterpretationContextBuilder.PatientQuestionPending(_s)) { o.Reject(NoPendingQuestion); return; }
            if (a.Params[ActParam] == "CLOSED_LOOP" && !InterpretationContextBuilder.ReadbackPending(_s)) { o.Reject(NoReadbackPending); return; }
            var steps = _s.RunningSteps;
            o.Effects.Add(StepsEffect + string.Join(",", steps.Select(n => n.ToString(CultureInfo.InvariantCulture))));
        }

        public void OnTick() { }

        private static bool Has(SimAction a, string key) => a.Params.TryGetValue(key, out var v) && !string.IsNullOrWhiteSpace(v);
    }
}
