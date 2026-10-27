using System.Collections.Generic;

namespace ProcSense.VR.Bridge
{
    /// <summary>Short on-screen hints for the reasons Core gives when it refuses an action. Interface wording only; nothing clinical lives here.</summary>
    public static class HintTexts
    {
        private static readonly Dictionary<string, string> Texts = new Dictionary<string, string>
        {
            { "IDENTITY_NOT_CONFIRMED", "Identity not confirmed" },
            { "ORDER_PENDING", "That is already requested and waiting" },
            { "MISSING_MODE", "Choose how the patient is moved" },
            { "UNKNOWN_LOCATION", "The simulator does not know that place" },
            { "ALREADY_THERE", "The patient is already there" },
            { "MISSING_DEVICE", "Choose the oxygen device" },
            { "NOT_A_HOME_MEDICINE", "The patient has not named that medicine" },
            { "ALREADY_HELD", "That medicine is already held" },
            { "UNKNOWN_ROLE", "The simulator only escalates to the senior doctor and the anaesthetist" },
            { "IV_ACCESS_REQUIRED", "Intravenous access is needed first" },
            { "ALREADY_IN_PLACE", "That is already in place" },
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
            { "NO_CALL", "Nobody is calling" },
            { "CALL_NOT_ANSWERED", "Answer the phone first" },
            { "CALL_IDENTITY_FIRST", "Confirm the patient's name and date of birth with the caller first" },
            { "CALL_READ_BACK_FIRST", "Read the values back to the caller first" },
            { "WRONG_IDENTITY_ON_CALL", "That is not the patient the caller named" },
            { "MISSING_VALUES", "Say the values back" },
            { "MISSING_ACTION", "Choose what you will do about the critical result" },
            { "UNKNOWN_ACTION", "Choose one of the actions offered" },
            { "MISSING_NOTE", "Write a note of the call and what you will do" },
            { "NOT_VIEWED", "Open the result first" },
            { "VIEW_FIRST", "Open the result first" },
            { "NOT_READY", "The result is not ready yet" },
            { "NOTHING_TO_INTERPRET", "There is nothing to interpret in that result" },
            { "UNKNOWN_INTERPRETATION", "Choose one of the interpretations offered" },
            { "USE_INTERPRET_IMAGING", "Interpret a scan with the imaging choices" },
            { "MISSING_QUERY", "Type a name, record number or date of birth" },
            { "MISSING_DOCUMENT", "Choose a document" },
            { "UNKNOWN_DOCUMENT", "That document is not in this record" },
            { "NO_IMAGE", "There is no picture with this report" },
            { "NOT_DELIVERED", "That report has not been delivered yet" },
            { "ALREADY_FILED", "That report is already in the folder" },
            { "NOT_A_PRINTOUT", "Only a printed result goes in the report pocket" },
            { "NOT_IN_FOLDER", "That document is not in the folder" },
            { "ASK_THE_RIGHT_PERSON", "The team answers that one: ask the right person" },
            { "ITEM_OUT_OF_ORDER", "Take the checklist in order" },
            { "SIGN_IN_FIRST", "Finish the Sign In first" },
            { "CASE_STOPPED", "The case is stopped: resolve the problem first" },
            { "PHASE_COMPLETE", "That part of the checklist is already complete" },
            { "ITEMS_OUTSTANDING", "Some checklist items are still outstanding" },
            { "NOT_ALL_TICKED", "Some ward checklist items are not ticked yet" },
            { "ALREADY_SIGNED", "That is already signed" },
        };

        /// <summary>The hint for a Core rejection reason ("" for none); unknown reasons are shown as the code so they are never hidden.</summary>
        public static string For(string reason)
        {
            if (string.IsNullOrEmpty(reason)) return "";
            return Texts.TryGetValue(reason, out var text) ? text : reason;
        }
    }
}
