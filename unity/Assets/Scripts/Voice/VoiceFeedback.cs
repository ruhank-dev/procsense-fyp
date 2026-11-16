using System;
using System.Collections.Generic;
using ProcSense.Core.Content;
using ProcSense.VR.Content;

namespace ProcSense.VR.Voice
{
    /// <summary>One phrase the trainee reads and hears for a voice outcome: the subtitle, the words to speak and the pre-rendered clip of exactly those words (P3-SND-08), or null for live speech.</summary>
    public sealed class VoiceFeedbackLine
    {
        public string Code = "";
        public string Subtitle = "";
        public string Spoken = "";
        public string Clip;
        public bool Retry;
    }

    /// <summary>
    /// The voice feedback table (P3-VOX-07, content/ui/voice_feedback.json, validated by tools/content/validate_ui_guides.py): the phrase for every voice outcome that is not an accepted action (the error taxonomy of
    /// p3-15). The mode decides the extra sentence (practice tips, examination notes); placeholders are filled with the engine's refusal text or the action read back. A missing or invalid file gives the built-in
    /// phrases below, so the trainee is never left without feedback. Presentation only: nothing here applies an action or credits anything.
    /// </summary>
    public sealed class VoiceFeedback
    {
        public const string File = "ui/voice_feedback.json";
        public const string NotUnderstood = "NOT_UNDERSTOOD", Ambiguous = "AMBIGUOUS", WrongAddressee = "WRONG_ADDRESSEE", UnavailableNow = "UNAVAILABLE_NOW", EngineRejected = "ENGINE_REJECTED",
            AsrFailed = "ASR_FAILED", Timeout = "TIMEOUT", NeedsConfirmation = "NEEDS_CONFIRMATION", ServiceOffline = "SERVICE_OFFLINE", SceneLoadFailed = "SCENE_LOAD_FAILED", ContentIntegrity = "CONTENT_INTEGRITY";
        public static readonly string[] Codes = { NotUnderstood, Ambiguous, WrongAddressee, UnavailableNow, EngineRejected, AsrFailed, Timeout, NeedsConfirmation, ServiceOffline, SceneLoadFailed, ContentIntegrity };

        private sealed class Entry { public string Subtitle, Spoken, Clip, PracticeClip, AssessmentClip, Fallback = "", PracticeExtra = "", AssessmentExtra = ""; public bool Retry; }

        private readonly Dictionary<string, Entry> _entries = new Dictionary<string, Entry>();

        /// <summary>True when the phrases came from the content file, false for the built-in fallback.</summary>
        public bool FromFile { get; private set; }

        private static VoiceFeedback _loaded;

        /// <summary>The table (read once); tests may replace it. A missing or invalid file gives <see cref="Defaults"/>.</summary>
        public static VoiceFeedback Loaded
        {
            get
            {
                if (_loaded != null) return _loaded;
                try { _loaded = Parse(UnityContentReader.ForThisPlatform().ReadText(File)); }
                catch (Exception e) { UnityEngine.Debug.LogWarning("[ProcSense] voice feedback table not usable, built-in phrases used: " + e.Message); _loaded = Defaults(); }
                return _loaded;
            }
            set => _loaded = value;
        }

        /// <summary>Reads the table. Throws <see cref="FormatException"/> when it is not the expected schema or an outcome code has no subtitle.</summary>
        public static VoiceFeedback Parse(string json)
        {
            var root = MiniJson.Parse(json) as Dictionary<string, object> ?? throw new FormatException("voice feedback is not an object");
            if (!(root.TryGetValue("schema", out var schema) && (schema as string) == "procsense.voice_feedback/1")) throw new FormatException("voice feedback schema is not procsense.voice_feedback/1");
            if (!(root.TryGetValue("outcomes", out var o) && o is List<object> outcomes)) throw new FormatException("voice feedback has no outcomes");
            var t = new VoiceFeedback { FromFile = true };
            foreach (var item in outcomes)
            {
                if (!(item is Dictionary<string, object> d) || !(d.TryGetValue("code", out var c) && c is string code)) throw new FormatException("an outcome has no code");
                string subtitle = Str(d, "subtitle");
                if (string.IsNullOrWhiteSpace(subtitle)) throw new FormatException(code + " has no subtitle");
                t._entries[code] = new Entry
                {
                    Subtitle = subtitle, Spoken = string.IsNullOrWhiteSpace(Str(d, "spoken")) ? subtitle : Str(d, "spoken"), Clip = ClipPath(d, "clip"), PracticeClip = ClipPath(d, "practice_clip"), AssessmentClip = ClipPath(d, "assessment_clip"),
                    Retry = d.TryGetValue("retry", out var r) && r is bool b && b, Fallback = Str(d, "fallback"), PracticeExtra = Str(d, "practice_extra"), AssessmentExtra = Str(d, "assessment_extra"),
                };
            }
            foreach (var code in Codes) if (!t._entries.ContainsKey(code)) throw new FormatException("no phrase for " + code);
            return t;
        }

        /// <summary>The built-in phrases (no tips), for when the file cannot be read or is invalid.</summary>
        public static VoiceFeedback Defaults()
        {
            var t = new VoiceFeedback();
            void Add(string code, string text, bool retry, string fallback = "") => t._entries[code] = new Entry { Subtitle = text, Spoken = text, Retry = retry, Fallback = fallback };
            Add(NotUnderstood, "Sorry, I did not understand that. Could you say it again?", true);
            Add(Ambiguous, "I am not sure which one you mean. Could you be more specific?", true);
            Add(WrongAddressee, "That is not something this person can help with.", true);
            Add(UnavailableNow, "That is not possible right now.", false);
            Add(EngineRejected, "{reason_text}", false, "That cannot be done yet.");
            Add(AsrFailed, "I could not hear you clearly. Please try again, or use the tablet.", true);
            Add(Timeout, "That took too long. Please try again, or use the tablet.", true);
            Add(NeedsConfirmation, "Please confirm: {action_text}. Say yes to go ahead, or no to cancel.", false);
            Add(ServiceOffline, "Voice is not available right now. You can keep going with the tablet.", false);
            Add(SceneLoadFailed, "The scenario could not be loaded. Please go back to the menu and try again.", false);
            Add(ContentIntegrity, "The scenario content did not pass its check, so it cannot be used. Please tell the supervisor.", false);
            return t;
        }

        /// <summary>The phrase for an outcome in this mode. <paramref name="reasonText"/> fills {reason_text} (the outcome's fallback when empty); <paramref name="actionText"/> fills {action_text}. An unknown code is NOT_UNDERSTOOD.</summary>
        public VoiceFeedbackLine Resolve(string code, bool practice, string reasonText = null, string actionText = null)
        {
            if (code == null || !_entries.TryGetValue(code, out var e)) { code = NotUnderstood; e = _entries[NotUnderstood]; }
            string reason = string.IsNullOrWhiteSpace(reasonText) ? (string.IsNullOrWhiteSpace(e.Fallback) ? "That cannot be done yet." : e.Fallback) : Sentence(reasonText);
            string Fill(string s) => s.Replace("{reason_text}", reason).Replace("{action_text}", actionText ?? "");
            string extra = practice ? e.PracticeExtra : e.AssessmentExtra;
            string Plus(string s) => string.IsNullOrWhiteSpace(extra) ? s : s + " " + extra;
            return new VoiceFeedbackLine { Code = code, Subtitle = Plus(Fill(e.Subtitle)), Spoken = Plus(Fill(e.Spoken)), Clip = ClipFor(e, extra, practice), Retry = e.Retry };
        }

        /// <summary>
        /// The clip whose words equal <see cref="VoiceFeedbackLine.Spoken"/> (P3-SND-08): the phrase alone when the mode adds nothing, otherwise the clip rendered with the mode's extra sentence. Null when that
        /// clip does not exist, so the words go to live speech instead of a clip that says less than the subtitle.
        /// </summary>
        private static string ClipFor(Entry e, string extra, bool practice) => string.IsNullOrWhiteSpace(extra) ? e.Clip : practice ? e.PracticeClip : e.AssessmentClip;

        private static string ClipPath(Dictionary<string, object> d, string key) => string.IsNullOrWhiteSpace(Str(d, key)) ? null : Str(d, key);

        /// <summary>The outcome for a failed interpreter call (the reason the client recorded), or null when the call itself did not fail.</summary>
        public static string CodeForReason(string reason)
        {
            switch (reason ?? "")
            {
                case "NO_NETWORK": case "NOT_SIGNED_IN": case "HTTP_502": case "HTTP_503": return ServiceOffline;
                case "HTTP_408": case "HTTP_504": case "TIMEOUT": return Timeout;
                case "UNREADABLE": return NotUnderstood;
                default: return null;
            }
        }

        private static string Sentence(string s)
        {
            s = s.Trim();
            if (s.Length == 0) return s;
            s = char.ToUpperInvariant(s[0]) + s.Substring(1);
            return s.EndsWith(".") || s.EndsWith("?") || s.EndsWith("!") ? s : s + ".";
        }

        private static string Str(Dictionary<string, object> d, string key) => d.TryGetValue(key, out var v) && v is string s ? s : "";
    }
}
