using System.Collections.Generic;
using System.Linq;

namespace ProcSense.Core.Speech
{
    /// <summary>
    /// What the semantic evaluator judged about ONE objective in ONE utterance (p1-ai-language-s22, ADR-0025): who did it, when (the assertion), in which form, and three bounded grades. The model's words only; whether and how much it counts is decided by the engine
    /// from this plus its own state (<see cref="Scoring.EvidenceStore"/>). <see cref="ClaimsPhysical"/> is an audit note that the speech claimed or required a physical or system action: it is never evidence that the action happened.
    /// </summary>
    public sealed record SpeechJudgement(string Assertion, string Actor, string Form, int Relevance, int Correctness, int Completeness, bool ClaimsPhysical)
    {
        public static readonly IReadOnlyList<string> Assertions = new[] { "performed", "negated", "intended_later", "hypothetical", "reported", "other_actor", "earlier_claim", "uncertain", "incomplete" };
        public static readonly IReadOnlyList<string> Actors = new[] { "trainee", "other", "unclear" };
        public static readonly IReadOnlyList<string> Forms = new[] { "statement", "question", "request" };
        public const int MaxGrade = 3;

        /// <summary>The trainee performs the communication in this utterance: the only judgement that can earn a communication credit. Computed here, never taken from the caller.</summary>
        public bool PerformedNow => Assertion == "performed" && Actor == "trainee";

        public bool IsValid => Assertions.Contains(Assertion) && Actors.Contains(Actor) && Forms.Contains(Form) && InRange(Relevance) && InRange(Correctness) && InRange(Completeness);

        private static bool InRange(int g) => g >= 0 && g <= MaxGrade;
    }
}
