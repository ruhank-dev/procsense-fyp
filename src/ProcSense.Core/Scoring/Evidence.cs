using System;
using System.Collections.Generic;
using System.Linq;
using ProcSense.Core.Logging;

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
        public const string CriticalNeedsAction = "CRITICAL_NEEDS_DETERMINISTIC_EVIDENCE", LowConfidence = "LOW_CONFIDENCE", ContextMismatch = "CONTEXT_MISMATCH", UnknownAct = "UNKNOWN_ACT";

        private readonly ScoreTables _t;
        private readonly List<EvidenceRecord> _records = new List<EvidenceRecord>();

        public EvidenceStore(ScoreTables tables) { _t = tables; }

        public IReadOnlyList<EvidenceRecord> Records => _records;

        /// <summary>Adds deterministic evidence for every accepted action in the log that is evidence for a behaviour (one record per behaviour it serves). Safe to call again: it adds only what is new.</summary>
        public void FromLog(SessionLog log)
        {
            var done = new HashSet<int>(_records.Where(r => r.Source == "deterministic").SelectMany(r => r.LogRefs));
            foreach (var e in log.Entries)
            {
                if (!e.Accepted || done.Contains(e.Seq)) continue;
                foreach (var b in _t.Behaviours.Where(x => x.Actions.Contains(e.Verb)))
                    _records.Add(new EvidenceRecord(_records.Count, "deterministic", b.Id, e.Verb, e.Params.TryGetValue("topic", out var topic) ? topic : "", "", null, new[] { e.Seq }, true, ""));
            }
        }

        /// <summary>A classified act from speech. <paramref name="step"/> is the workflow step number the trainee is in, used for the context check.</summary>
        public EvidenceRecord Submit(string act, string topic, string span, double confidence, int step, IReadOnlyList<int> logRefs)
        {
            var behaviour = _t.Behaviours.FirstOrDefault(b => b.AiActs.Contains(act));
            EvidenceRecord record;
            if (behaviour == null) record = new EvidenceRecord(_records.Count, "ai", "", act, topic, span, confidence, logRefs, false, UnknownAct);
            else if (_t.CriticalActs.Contains(act)) record = new EvidenceRecord(_records.Count, "ai", behaviour.Id, act, topic, span, confidence, logRefs, false, CriticalNeedsAction);
            else if (confidence < _t.ThresholdFor(act)) record = new EvidenceRecord(_records.Count, "ai", behaviour.Id, act, topic, span, confidence, logRefs, false, LowConfidence);
            else if (!behaviour.Steps.Contains(step)) record = new EvidenceRecord(_records.Count, "ai", behaviour.Id, act, topic, span, confidence, logRefs, false, ContextMismatch + ": " + act + " belongs to steps " + string.Join(",", behaviour.Steps) + ", not " + step.ToString(System.Globalization.CultureInfo.InvariantCulture));
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
