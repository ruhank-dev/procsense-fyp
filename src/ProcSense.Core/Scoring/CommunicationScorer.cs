using System;
using System.Collections.Generic;
using System.Linq;

namespace ProcSense.Core.Scoring
{
    /// <summary>
    /// One behaviour's communication credit (p1-ai-language-s22, ADR-0025): the objective that earned it, the three grades the evaluator gave, the grade the engine computed, the weight, the credit (grade x weight) and, for a critical objective, whether the verified simulator action
    /// that earns its physical part is on record. PhysicalVerified is information, not a score: the physical credit is the engine's own credit item for the action, and speech can never add to it.
    /// </summary>
    public sealed record CommunicationItem(string Behaviour, string Label, string Act, bool Critical, double Grade, double Weight, double Credit, int Relevance, int Correctness, int Completeness, string Span, int EvidenceId, bool ClaimsPhysical, bool PhysicalVerified);

    /// <summary>
    /// Turns the evidence store into communication credit. Per behaviour the BEST graded performance counts once (repeating an utterance cannot add up); an ordinary objective weighs the rubric's ordinary weight, a critical objective only its critical weight.
    /// What was claimed about physical actions is carried as a note: a claim never makes anything verified, and a critical behaviour's physical part is true only when the engine itself accepted one of its actions.
    /// </summary>
    public static class CommunicationScorer
    {
        public static IReadOnlyList<CommunicationItem> Collect(EvidenceStore store, ScoreTables tables, IReadOnlyDictionary<string, double> behaviourWeights)
        {
            var items = new List<CommunicationItem>();
            foreach (var b in tables.Behaviours)
            {
                var best = store.Records.Where(r => r.Source == "ai" && r.Behaviour == b.Id && (r.Grade ?? 0) > 0).OrderByDescending(r => r.Grade).ThenBy(r => r.Id).FirstOrDefault();
                if (best == null) continue;
                bool critical = tables.CriticalActs.Contains(best.Act);
                double w = (critical ? tables.Communication.CriticalWeight : tables.Communication.OrdinaryWeight) * (behaviourWeights.TryGetValue(b.Id, out var bw) ? bw : 1.0);
                bool verified = critical && tables.CriticalEvidence.TryGetValue(best.Act, out var ev) &&
                                store.Records.Any(r => r.Source == "deterministic" && r.Accepted && r.Behaviour == ev.Behaviour && ev.Actions.Contains(r.Act));
                var j = best.Judgement!;
                items.Add(new CommunicationItem(b.Id, b.Label, best.Act, critical, Math.Round(best.Grade!.Value, 4), w, Math.Round(best.Grade.Value * w, 4), j.Relevance, j.Correctness, j.Completeness, best.Span, best.Id, j.ClaimsPhysical, verified));
            }
            return items;
        }
    }
}
