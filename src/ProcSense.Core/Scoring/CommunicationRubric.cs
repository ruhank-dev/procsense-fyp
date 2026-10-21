using System;
using ProcSense.Core.Content;

namespace ProcSense.Core.Scoring
{
    /// <summary>
    /// The graded communication credit (p1-ai-language-s22, ADR-0025; content/scoring/rubric.json "communication", EXPERIMENTAL design values, SR-2865). The semantic evaluator grades what a performed objective was on three bounded levels
    /// (relevance, correctness, completeness, 0 to <see cref="GradeLevels"/>); this deterministic formula turns them into a share of the objective's weight. The model never produces a number of points.
    /// <code>grade = 0 when relevance = 0 or correctness = 0, else relevance/n * correctness/n * (floor + (1 - floor) * completeness/n)</code>; a grade under <see cref="CreditFloor"/> earns nothing.
    /// An ordinary objective weighs <see cref="OrdinaryWeight"/> (the one credit point a behaviour already carries); a critical objective only <see cref="CriticalWeight"/>, because its physical part is earned by verified actions and never by speech.
    /// </summary>
    public sealed record CommunicationRubric(int GradeLevels, double CompletenessFloor, double CreditFloor, double OrdinaryWeight, double CriticalWeight)
    {
        public static readonly CommunicationRubric Default = new CommunicationRubric(3, 0.4, 0.1, 1.0, 0.25);

        /// <summary>The grade of the words, 0 to 1, before the floor.</summary>
        public double Grade(int relevance, int correctness, int completeness)
        {
            if (relevance <= 0 || correctness <= 0) return 0;
            double n = GradeLevels;
            return Math.Min(1.0, relevance / n) * Math.Min(1.0, correctness / n) * (CompletenessFloor + (1 - CompletenessFloor) * Math.Min(1.0, Math.Max(0, completeness) / n));
        }

        /// <summary>The grade that counts: the grade, or 0 when it is under the credit floor.</summary>
        public double Credit(int relevance, int correctness, int completeness)
        {
            double g = Grade(relevance, correctness, completeness);
            return g >= CreditFloor ? g : 0;
        }

        internal static CommunicationRubric Parse(JsonObj o)
        {
            o.Bool("provisional"); o.Str("ref"); o.Str("note");
            var r = new CommunicationRubric((int)o.Int("grade_levels"), o.Num("completeness_floor"), o.Num("credit_floor"), o.Num("ordinary_weight"), o.Num("critical_weight"));
            o.Finish();
            if (r.GradeLevels < 1 || r.CompletenessFloor < 0 || r.CompletenessFloor > 1 || r.CreditFloor < 0 || r.CreditFloor > 1 || r.OrdinaryWeight < 0 || r.CriticalWeight < 0)
                throw new ContentException("scoring/rubric.communication: a value is out of range");
            return r;
        }
    }

    /// <summary>The behaviour a critical objective is spoken for and the verified actions that earn its physical part (content/assessment/mapping.json critical_act_evidence). Speech is never one of them.</summary>
    public sealed record CriticalEvidence(string Behaviour, System.Collections.Generic.IReadOnlyList<string> Actions);
}
