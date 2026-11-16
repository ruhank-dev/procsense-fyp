using System;
using System.Collections.Generic;
using System.Text;
using ProcSense.Core;
using ProcSense.Core.Actions;

namespace ProcSense.VR.Flow
{
    /// <summary>
    /// The two spoken flow intents (P3-VOX-08, P3-VOX-13): asking for a hint and submitting the patient to theatre. A phrase maps to exactly the action the button sends (same verb, target, channel and
    /// parameters), so the log, the score and the replay hash are identical whichever way it was asked. Only these fixed phrases are accepted: nothing is guessed, and a phrase that is not here goes
    /// to the normal spoken-order interpreter, which decides for itself. Submitting by voice still needs the confirmation the button asks for (MenuFlow.Submit arms first).
    /// </summary>
    public static class FlowVoiceIntents
    {
        public enum Intent { None, Hint, Submit }

        private static readonly HashSet<string> HintPhrases = new HashSet<string>(StringComparer.Ordinal)
        {
            "hint", "a hint", "give me a hint", "i need a hint", "can i have a hint", "request a hint", "help", "help me",
        };

        private static readonly HashSet<string> SubmitPhrases = new HashSet<string>(StringComparer.Ordinal)
        {
            "submit the patient", "submit patient", "submit the patient to theatre", "submit the patient to the theatre", "submit the patient to ot", "submit patient to ot",
            "submit patient to theatre", "send the patient to theatre", "send the patient to the theatre", "send the patient to ot",
        };

        /// <summary>The words of an utterance in lower case with punctuation and extra spaces removed.</summary>
        public static string Normalise(string utterance)
        {
            if (utterance == null) return "";
            var sb = new StringBuilder(utterance.Length);
            foreach (char c in utterance.ToLowerInvariant()) sb.Append(char.IsLetterOrDigit(c) ? c : ' ');
            return string.Join(" ", sb.ToString().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries));
        }

        public static Intent Classify(string utterance)
        {
            string text = Normalise(utterance);
            if (HintPhrases.Contains(text)) return Intent.Hint;
            if (SubmitPhrases.Contains(text)) return Intent.Submit;
            return Intent.None;
        }

        /// <summary>The action the menu button for the same thing sends.</summary>
        public static SimAction ActionFor(Intent intent)
        {
            switch (intent)
            {
                case Intent.Hint: return new SimAction(ExamControl.RequestHint, "", Channel.Vr);
                case Intent.Submit: return new SimAction(ExamControl.SubmitPatient, "", Channel.Vr, new Dictionary<string, string>());
                default: return null;
            }
        }

        public static bool TryMap(string utterance, out SimAction action)
        {
            var intent = Classify(utterance);
            action = ActionFor(intent);
            return action != null;
        }

        /// <summary>Runs a phrase through the flow: a hint now, a submission armed and then confirmed by saying it again. Returns false when the phrase is not a flow intent.</summary>
        public static bool Handle(MenuFlow flow, string utterance)
        {
            switch (Classify(utterance))
            {
                case Intent.Hint: flow.Hint(); return true;
                case Intent.Submit: flow.Submit(); return true;
                default: return false;
            }
        }
    }
}
