using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using ProcSense.Core.Logging;
using ProcSense.Core.Speech;

namespace ProcSense.Core.Scoring
{
    /// <summary>
    /// One piece of evidence that the trainee showed a behaviour. Source is "deterministic" (an action the engine accepted) or "ai" (a communication act a classifier reported from speech; none exist in P0).
    /// Accepted says whether it counts; Reason says why not. LogRefs point to the log entries that justify it.
    /// </summary>
    public sealed record EvidenceRecord(int Id, string Source, string Behaviour, string Act, string Topic, string Span, double? Confidence, IReadOnlyList<int> LogRefs, bool Accepted, string Reason);

    /// <summary>
    /// Turns actions and (later) classified speech into evidence (p0-scoring-debrief s02). Deterministic evidence comes from the accepted actions in the log, by the mapping in content/assessment/mapping.json.
    /// AI evidence is only ever a suggestion: a critical act is recorded but never credited (identity, allergy, consent and handover critical elements need an action the engine saw), an act under its confidence threshold
    /// is recorded but not credited, and an act that does not belong to the step the trainee is in is rejected with the reason. With no classifier (P0) the store holds deterministic evidence only.
    /// </summary>
    public sealed class EvidenceStore
    {
        public const string CriticalNeedsAction = "CRITICAL_NEEDS_DETERMINISTIC_EVIDENCE", LowConfidence = "LOW_CONFIDENCE", ContextMismatch = "CONTEXT_MISMATCH", UnknownAct = "UNKNOWN_ACT", DuplicateBehaviour = "DUPLICATE_BEHAVIOUR", DuplicateUtterance = "DUPLICATE_UTTERANCE", NoRunningStep = "NO_RUNNING_STEP";

        private readonly ScoreTables _t;
        private readonly List<EvidenceRecord> _records = new List<EvidenceRecord>();
        private readonly HashSet<string> _utterances = new HashSet<string>(StringComparer.Ordinal);

        public EvidenceStore(ScoreTables tables) { _t = tables; }

        public IReadOnlyList<EvidenceRecord> Records => _records;

        /// <summary>Adds deterministic evidence for every accepted action in the log that is evidence for a behaviour (one record per behaviour it serves), and submits every accepted <c>speech_evidence</c> entry through <see cref="Submit(string, string, string, double, IReadOnlyCollection{int}, IReadOnlyList{int})"/> with the workflow steps the engine stamped on it. Safe to call again: it adds only what is new.</summary>
        public void FromLog(SessionLog log)
        {
            var done = new HashSet<int>(_records.SelectMany(r => r.LogRefs));
            foreach (var e in log.Entries)
            {
                if (!e.Accepted || done.Contains(e.Seq)) continue;
                if (e.Verb == SpeechEvidenceSubsystem.Verb) { SubmitFromEntry(e); continue; }
                foreach (var b in _t.Behaviours.Where(x => x.Actions.Contains(e.Verb)))
                    _records.Add(new EvidenceRecord(_records.Count, "deterministic", b.Id, e.Verb, e.Params.TryGetValue("topic", out var topic) ? topic : "", "", null, new[] { e.Seq }, true, ""));
            }
        }

        private void SubmitFromEntry(LogEntry e)
        {
            var steps = new List<int>();
            var effect = e.Effects.FirstOrDefault(x => x.StartsWith(SpeechEvidenceSubsystem.StepsEffect, StringComparison.Ordinal));
            if (effect != null)
                foreach (var part in effect.Substring(SpeechEvidenceSubsystem.StepsEffect.Length).Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
                    if (int.TryParse(part, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n)) steps.Add(n);
            string utterance = e.Params.TryGetValue(SpeechEvidenceSubsystem.UtteranceParam, out var u) ? u : "";
            double confidence = e.Params.TryGetValue(SpeechEvidenceSubsystem.ConfidenceParam, out var c) && double.TryParse(c, NumberStyles.Float, CultureInfo.InvariantCulture, out var d) ? d : 0;
            string act = e.Params.TryGetValue(SpeechEvidenceSubsystem.ActParam, out var a) ? a : "", span = e.Params.TryGetValue(SpeechEvidenceSubsystem.SpanParam, out var sp) ? sp : "";
            SubmitUtterance(utterance, act, span, confidence, steps, new[] { e.Seq });
        }

        /// <summary>One interpreted utterance: the same utterance id is judged once (a repeat is refused as DUPLICATE_UTTERANCE and credits nothing), then <see cref="Submit(string, string, string, double, IReadOnlyCollection{int}, IReadOnlyList{int})"/> decides. The path of a logged <c>speech_evidence</c> action and of the headless <c>evidence-check</c> command.</summary>
        public EvidenceRecord SubmitUtterance(string utterance, string act, string span, double confidence, IReadOnlyCollection<int> steps, IReadOnlyList<int> logRefs)
        {
            if (utterance.Length > 0 && !_utterances.Add(utterance))
            {
                var dup = new EvidenceRecord(_records.Count, "ai", "", act, "", span, confidence, logRefs, false, DuplicateUtterance);
                _records.Add(dup);
                return dup;
            }
            return Submit(act, "", span, confidence, steps, logRefs);
        }

        /// <summary>A classified act from speech. <paramref name="step"/> is the workflow step number the trainee is in, used for the context check.</summary>
        public EvidenceRecord Submit(string act, string topic, string span, double confidence, int step, IReadOnlyList<int> logRefs) => Submit(act, topic, span, confidence, new[] { step }, logRefs);

        /// <summary>
        /// A classified act from speech, judged against ALL the workflow steps the trainee is in (parallel tracks can run several). The behaviour is the one that lists the act and whose step window contains a running step
        /// (CLARIFY and ESCALATE_SPEECH belong to two behaviours); with none, the first behaviour that lists it names the reason. Refused, in this order: unknown act, critical act, low confidence, outside every window, a behaviour already credited from speech.
        /// Only <c>ai_acts</c> can be credited here: nothing in this method can satisfy a behaviour's <c>actions</c>.
        /// </summary>
        public EvidenceRecord Submit(string act, string topic, string span, double confidence, IReadOnlyCollection<int> steps, IReadOnlyList<int> logRefs)
        {
            var candidates = _t.Behaviours.Where(b => b.AiActs.Contains(act)).ToList();
            var behaviour = candidates.FirstOrDefault(b => b.Steps.Any(steps.Contains)) ?? candidates.FirstOrDefault();
            EvidenceRecord record;
            if (behaviour == null) record = new EvidenceRecord(_records.Count, "ai", "", act, topic, span, confidence, logRefs, false, UnknownAct);
            else if (_t.CriticalActs.Contains(act)) record = new EvidenceRecord(_records.Count, "ai", behaviour.Id, act, topic, span, confidence, logRefs, false, CriticalNeedsAction);
            else if (confidence < _t.ThresholdFor(act)) record = new EvidenceRecord(_records.Count, "ai", behaviour.Id, act, topic, span, confidence, logRefs, false, LowConfidence);
            else if (steps.Count == 0) record = new EvidenceRecord(_records.Count, "ai", behaviour.Id, act, topic, span, confidence, logRefs, false, NoRunningStep);
            else if (!behaviour.Steps.Any(steps.Contains)) record = new EvidenceRecord(_records.Count, "ai", behaviour.Id, act, topic, span, confidence, logRefs, false, ContextMismatch + ": " + act + " belongs to steps " + string.Join(",", behaviour.Steps) + ", not " + string.Join(",", steps.Select(n => n.ToString(CultureInfo.InvariantCulture))));
            else if (_records.Any(r => r.Source == "ai" && r.Accepted && r.Behaviour == behaviour.Id)) record = new EvidenceRecord(_records.Count, "ai", behaviour.Id, act, topic, span, confidence, logRefs, false, DuplicateBehaviour);
            else record = new EvidenceRecord(_records.Count, "ai", behaviour.Id, act, topic, span, confidence, logRefs, true, "");
            _records.Add(record);
            return record;
        }

        /// <summary>The evidence that counts for a behaviour.</summary>
        public IReadOnlyList<EvidenceRecord> Credited(string behaviour) => _records.Where(r => r.Accepted && r.Behaviour == behaviour).ToList();

        /// <summary>Behaviours with no evidence at all so far (the debrief lists them as not shown).</summary>
        public IReadOnlyList<string> NotShown() => _t.Behaviours.Where(b => b.Actions.Count + b.AiActs.Count > 0 && Credited(b.Id).Count == 0).Select(b => b.Id).ToList();
    }
}
