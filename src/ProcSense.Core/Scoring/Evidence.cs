using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using ProcSense.Core.Logging;
using ProcSense.Core.Speech;

namespace ProcSense.Core.Scoring
{
    /// <summary>
    /// One piece of evidence about a behaviour. Source is "deterministic" (an action the engine accepted) or "ai" (an objective the semantic evaluator judged in speech). Grade is the graded communication credit 0 to 1 the engine gave the words after every gate
    /// (null for deterministic evidence); Accepted says whether it is evidence FOR THE BEHAVIOUR (a critical objective spoken is graded but never Accepted: only a verified action shows a critical behaviour); Reason says why not. LogRefs point to the log entries that justify it.
    /// </summary>
    public sealed record EvidenceRecord(int Id, string Source, string Behaviour, string Act, string Topic, string Span, double? Grade, IReadOnlyList<int> LogRefs, bool Accepted, string Reason, SpeechJudgement? Judgement = null);

    /// <summary>
    /// Turns actions and judged speech into evidence (p0-scoring-debrief s02, p1-ai-language-s22, ADR-0025). Deterministic evidence comes from the accepted actions in the log, by the mapping in content/assessment/mapping.json.
    /// Speech evidence is the semantic evaluator's judgement, never a score: the engine decides what it is worth. Refused, in this order: unknown objective, not performed by the trainee now (denied, planned, imagined, reported, another actor, claimed as earlier, uncertain, broken off),
    /// no running step, outside the behaviour's window, a grade under the credit floor. A critical objective is graded for its small communication credit but never Accepted as evidence for its behaviour: identity, allergy, consent and handover elements need an action the engine saw.
    /// Only <c>ai_acts</c> can be credited here: nothing in this class can satisfy a behaviour's <c>actions</c>, and a claim of a physical action is only ever a note.
    /// </summary>
    public sealed class EvidenceStore
    {
        public const string CriticalNeedsAction = "CRITICAL_NEEDS_DETERMINISTIC_EVIDENCE", BelowCreditFloor = "BELOW_CREDIT_FLOOR", ContextMismatch = "CONTEXT_MISMATCH", UnknownAct = "UNKNOWN_ACT", NotPerformed = "NOT_PERFORMED", NoRunningStep = "NO_RUNNING_STEP", DuplicateUtterance = "DUPLICATE_UTTERANCE";

        private readonly ScoreTables _t;
        private readonly List<EvidenceRecord> _records = new List<EvidenceRecord>();
        private readonly HashSet<string> _utterances = new HashSet<string>(StringComparer.Ordinal);

        public EvidenceStore(ScoreTables tables) { _t = tables; }

        public IReadOnlyList<EvidenceRecord> Records => _records;

        /// <summary>Adds deterministic evidence for every accepted action in the log that is evidence for a behaviour (one record per behaviour it serves), and submits every accepted <c>speech_evidence</c> entry through <see cref="SubmitUtterance"/>. Idempotent per log entry.</summary>
        public void FromLog(SessionLog log)
        {
            var done = new HashSet<int>(_records.SelectMany(r => r.LogRefs));
            foreach (var e in log.Entries)
            {
                if (!e.Accepted || done.Contains(e.Seq)) continue;
                if (e.Verb == SpeechEvidenceSubsystem.Verb) { SubmitFromEntry(e); continue; }
                AddDeterministic(e.Verb, new[] { e.Seq }, e.Params.TryGetValue("topic", out var topic) ? topic : "");
            }
        }

        /// <summary>Deterministic evidence for an action the simulator accepted: one record per behaviour that lists the verb as an action. The only way a behaviour or a critical objective's physical part is ever shown.</summary>
        public void AddDeterministic(string verb, IReadOnlyList<int> logRefs, string topic = "")
        {
            foreach (var b in _t.Behaviours.Where(x => x.Actions.Contains(verb)))
                _records.Add(new EvidenceRecord(_records.Count, "deterministic", b.Id, verb, topic, "", null, logRefs, true, ""));
        }

        private void SubmitFromEntry(LogEntry e)
        {
            var steps = new List<int>();
            var effect = e.Effects.FirstOrDefault(x => x.StartsWith(SpeechEvidenceSubsystem.StepsEffect, StringComparison.Ordinal));
            if (effect != null)
                foreach (var part in effect.Substring(SpeechEvidenceSubsystem.StepsEffect.Length).Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
                    if (int.TryParse(part, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n)) steps.Add(n);
            string P(string key) => e.Params.TryGetValue(key, out var v) ? v : "";
            var judgement = SpeechEvidenceSubsystem.Judgement(e.Params);
            if (judgement == null) return;                                              // the subsystem already refuses a malformed judgement; nothing to record
            SubmitUtterance(P(SpeechEvidenceSubsystem.UtteranceParam), P(SpeechEvidenceSubsystem.ActParam), P(SpeechEvidenceSubsystem.SpanParam), judgement, steps, new[] { e.Seq });
        }

        /// <summary>One judged objective of an utterance: the same utterance id and objective are judged once (a repeat is refused as DUPLICATE_UTTERANCE and credits nothing); several objectives of one utterance each count once.</summary>
        public EvidenceRecord SubmitUtterance(string utterance, string act, string span, SpeechJudgement judgement, IReadOnlyCollection<int> steps, IReadOnlyList<int> logRefs)
        {
            if (utterance.Length > 0 && !_utterances.Add(utterance + "|" + act))
            {
                var dup = new EvidenceRecord(_records.Count, "ai", "", act, "", span, 0, logRefs, false, DuplicateUtterance, judgement);
                _records.Add(dup);
                return dup;
            }
            return Submit(act, "", span, judgement, steps, logRefs);
        }

        /// <summary>A judged objective from speech, checked against ALL the workflow steps the trainee is in (parallel tracks can run several). The behaviour is the one that lists the objective and whose step window contains a running step (CLARIFY and ESCALATE_SPEECH belong to two behaviours);
        /// a critical objective belongs to the behaviour named in <c>critical_act_evidence</c>.</summary>
        public EvidenceRecord Submit(string act, string topic, string span, SpeechJudgement judgement, IReadOnlyCollection<int> steps, IReadOnlyList<int> logRefs)
        {
            bool critical = _t.CriticalActs.Contains(act);
            var candidates = critical && _t.CriticalEvidence.TryGetValue(act, out var ce) ? _t.Behaviours.Where(b => b.Id == ce.Behaviour).ToList() : _t.Behaviours.Where(b => b.AiActs.Contains(act)).ToList();
            var behaviour = candidates.FirstOrDefault(b => b.Steps.Any(steps.Contains)) ?? candidates.FirstOrDefault();
            string bid = behaviour?.Id ?? "";
            EvidenceRecord Refuse(string reason) => new EvidenceRecord(_records.Count, "ai", bid, act, topic, span, 0, logRefs, false, reason, judgement);
            EvidenceRecord record;
            if (behaviour == null) record = Refuse(UnknownAct);
            else if (!judgement.IsValid) record = Refuse(UnknownAct + ": the judgement is malformed");
            else if (!judgement.PerformedNow) record = Refuse(NotPerformed + ":" + judgement.Assertion + (judgement.Actor == "trainee" ? "" : ":" + judgement.Actor));
            else if (steps.Count == 0) record = Refuse(NoRunningStep);
            else if (!behaviour.Steps.Any(steps.Contains)) record = Refuse(ContextMismatch + ": " + act + " belongs to steps " + string.Join(",", behaviour.Steps) + ", the trainee is in " + string.Join(",", steps));
            else
            {
                double grade = _t.Communication.Credit(judgement.Relevance, judgement.Correctness, judgement.Completeness);
                if (grade <= 0) record = Refuse(BelowCreditFloor);
                else record = new EvidenceRecord(_records.Count, "ai", behaviour.Id, act, topic, span, grade, logRefs, !critical, critical ? CriticalNeedsAction : "", judgement);
            }
            _records.Add(record);
            return record;
        }

        /// <summary>The evidence that counts for a behaviour.</summary>
        public IReadOnlyList<EvidenceRecord> Credited(string behaviour) => _records.Where(r => r.Accepted && r.Behaviour == behaviour).ToList();

        /// <summary>Behaviours with no evidence at all so far (the debrief lists them as not shown).</summary>
        public IReadOnlyList<string> NotShown() => _t.Behaviours.Where(b => b.Actions.Count + b.AiActs.Count > 0 && Credited(b.Id).Count == 0).Select(b => b.Id).ToList();
    }
}
