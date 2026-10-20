using System;
using System.Collections.Generic;
using System.Linq;
using ProcSense.Core.AIContext;
using ProcSense.Core.Content;
using ProcSense.Core.Scoring;
using ProcSense.Core.Workflow.Steps;

namespace ProcSense.Core.Speech
{
    /// <summary>
    /// Builds the <see cref="InterpretationContext"/> from the session's truth (p1-ai-language-s10, ADR-0022). Read-only: it has no way back into the session.
    /// An act is eligible only if a behaviour that lists it as an AI act has a step window that contains one of the steps the trainee is in (the same rule <see cref="EvidenceStore.Submit(string, string, string, double, IReadOnlyCollection{int}, IReadOnlyList{int})"/> applies);
    /// with no running step nothing is eligible, never "everything".
    /// </summary>
    public static class InterpretationContextBuilder
    {
        public const int MaxTurns = 4, MaxTurnChars = 300;

        public static InterpretationContext Build(Session s, ContentPackage content, IReadOnlyList<ContextTurn> turns, string addressee)
        {
            var tables = content.Scoring;
            var steps = s.RunningSteps;
            var store = new EvidenceStore(tables);
            store.FromLog(s.Log);
            var accepted = store.Records.Where(r => r.Source == "ai" && r.Accepted).Select(r => r.Behaviour).Distinct().OrderBy(x => x, StringComparer.Ordinal).ToList();
            var kept = turns.Skip(Math.Max(0, turns.Count - MaxTurns)).Select(t => new ContextTurn(t.Speaker, t.Text.Length > MaxTurnChars ? t.Text.Substring(0, MaxTurnChars) : t.Text)).ToList();
            return new InterpretationContext(s.Case.Id, s.Case.Scenario, s.Mode.ToString().ToLowerInvariant(), steps, EligibleFor(tables, steps), accepted,
                PatientQuestionPending(s), ReadbackPending(s), kept, "trainee", addressee);
        }

        /// <summary>A patient question is waiting for an answer: the consent discussion has a question pending. The one definition the context and the speech-evidence check share (DT-232: no other engine event opens a question yet).</summary>
        public static bool PatientQuestionPending(Session s) => s.Consent != null && s.Consent.Pending.Count > 0;

        /// <summary>A critical result is waiting to be acknowledged, so a read-back can be given. The one definition the context and the speech-evidence check share.</summary>
        public static bool ReadbackPending(Session s) => s.Patient.Clinical.HasFlag(ResultReviewSubsystem.CriticalFlag);

        /// <summary>The acts a behaviour window allows at the given steps, in the order of the mapping file; an act listed by two behaviours appears once per behaviour whose window contains a step.</summary>
        public static IReadOnlyList<EligibleAct> EligibleFor(ScoreTables tables, IReadOnlyCollection<int> steps)
        {
            var list = new List<EligibleAct>();
            foreach (var b in tables.Behaviours)
            {
                if (!b.Steps.Any(steps.Contains)) continue;
                foreach (var act in b.AiActs) list.Add(new EligibleAct(act, b.Id, tables.CriticalActs.Contains(act)));
            }
            return list;
        }
    }
}
