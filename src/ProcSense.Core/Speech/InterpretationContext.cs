using System.Collections.Generic;
using System.Text;
using ProcSense.Core.AIContext;
using ProcSense.Core.Logging;

namespace ProcSense.Core.Speech
{
    /// <summary>One communication act that may be offered as evidence right now, with the behaviour it would count for.</summary>
    public sealed record EligibleAct(string Act, string Behaviour, bool Critical);

    /// <summary>
    /// What the interpretation service is told about ONE trainee utterance (p1-ai-language-s10, ADR-0022): an immutable, read-only projection of the session made by <see cref="InterpretationContextBuilder"/>.
    /// It holds the case and mode, the workflow steps the trainee is in, the acts that are eligible at those steps (the behaviour windows of content/assessment/mapping.json), the behaviours already credited from speech,
    /// whether the patient has a question waiting and whether a critical result waits for acknowledgement (the engine's own flags, nothing guessed), and the last few turns. No hidden fact, expected answer, score or hint is in it.
    /// </summary>
    public sealed record InterpretationContext(
        string Case, string Scenario, string Mode, IReadOnlyList<int> Steps, IReadOnlyList<EligibleAct> Eligible, IReadOnlyList<string> Accepted,
        bool PatientQuestionPending, bool ReadbackPending, IReadOnlyList<ContextTurn> LastTurns, string Speaker, string Addressee)
    {
        /// <summary>The context as JSON, in a fixed key order (what the service receives).</summary>
        public string ToJson()
        {
            var sb = new StringBuilder("{");
            Str(sb, "case", Case); sb.Append(',');
            Str(sb, "scenario", Scenario); sb.Append(',');
            Str(sb, "mode", Mode); sb.Append(",\"steps\":[").Append(string.Join(",", Steps)).Append("],\"eligible\":[");
            for (int i = 0; i < Eligible.Count; i++)
            {
                if (i > 0) sb.Append(',');
                sb.Append('{'); Str(sb, "act", Eligible[i].Act); sb.Append(','); Str(sb, "behaviour", Eligible[i].Behaviour);
                sb.Append(",\"critical\":").Append(Eligible[i].Critical ? "true" : "false").Append('}');
            }
            sb.Append("],\"accepted\":[");
            for (int i = 0; i < Accepted.Count; i++) { if (i > 0) sb.Append(','); JsonText.Quote(sb, Accepted[i]); }
            sb.Append("],\"patient_question_pending\":").Append(PatientQuestionPending ? "true" : "false");
            sb.Append(",\"readback_pending\":").Append(ReadbackPending ? "true" : "false").Append(",\"last_turns\":[");
            for (int i = 0; i < LastTurns.Count; i++)
            {
                if (i > 0) sb.Append(',');
                sb.Append('{'); Str(sb, "speaker", LastTurns[i].Speaker); sb.Append(','); Str(sb, "text", LastTurns[i].Text); sb.Append('}');
            }
            sb.Append("],"); Str(sb, "speaker", Speaker); sb.Append(','); Str(sb, "addressee", Addressee);
            return sb.Append('}').ToString();
        }

        private static void Str(StringBuilder sb, string key, string value) { JsonText.Quote(sb, key); sb.Append(':'); JsonText.Quote(sb, value); }
    }
}
