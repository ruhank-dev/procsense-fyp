using System.Collections.Generic;

namespace ProcSense.VR.Bridge
{
    /// <summary>Short on-screen hints for the reasons Core gives when it refuses an action. Interface wording only; nothing clinical lives here.</summary>
    public static class HintTexts
    {
        private static readonly Dictionary<string, string> Texts = new Dictionary<string, string>
        {
            { "IDENTITY_NOT_CONFIRMED", "Identity not confirmed" },
            { "IDENTITY_CHECK_INCOMPLETE", "First ask her to say her name and date of birth, read the wristband and open the notes" },
            { "ST1_MISMATCH", "Something does not match - stop and report it" },
            { "SPOKEN_ANSWER_UNCLEAR", "Her answer was not clear enough to rely on" },
            { "WRISTBAND_MISSING", "There is no wristband on her wrist" },
            { "VERIFY_IDENTITY_FIRST", "Check who she is another way first (ask her, open the notes)" },
            { "CUFF_NOT_ATTACHED", "Attach the blood-pressure cuff first" },
            { "CYCLE_IN_PROGRESS", "The cuff is still measuring" },
            { "UNKNOWN_TOPIC", "She cannot answer that" },
            { "TOPIC_NOT_IN_CASE", "Nothing to ask about that for this patient" },
            { "MISSING_SENSOR", "Choose a sensor" },
            { "UNKNOWN_SENSOR", "That sensor is not available" },
            { "SESSION_PAUSED", "The session is paused" },
        };

        /// <summary>The hint for a Core rejection reason ("" for none); unknown reasons are shown as the code so they are never hidden.</summary>
        public static string For(string reason)
        {
            if (string.IsNullOrEmpty(reason)) return "";
            return Texts.TryGetValue(reason, out var text) ? text : reason;
        }
    }
}
