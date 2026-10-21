namespace ProcSense.Core.Speech
{
    /// <summary>
    /// The two states the ENGINE holds that an objective spoken now depends on (ADR-0023): ANSWER_QUESTION needs a patient question waiting, CLOSED_LOOP needs a critical result waiting for read-back. One definition for the speech-evidence action and for any offline check,
    /// so a service or a client that claims a state the session does not hold gets nothing recorded as evidence.
    /// </summary>
    public static class SpeechStateGate
    {
        public const string NoPendingQuestion = "NO_PENDING_QUESTION", NoReadbackPending = "NO_READBACK_PENDING";

        /// <summary>The reason an objective cannot count in the current state, or null.</summary>
        public static string? Check(string act, bool patientQuestionPending, bool readbackPending)
        {
            if (act == "ANSWER_QUESTION" && !patientQuestionPending) return NoPendingQuestion;
            if (act == "CLOSED_LOOP" && !readbackPending) return NoReadbackPending;
            return null;
        }
    }
}
