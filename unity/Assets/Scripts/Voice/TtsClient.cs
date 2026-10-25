using System;
using System.Collections;
using System.Collections.Generic;
using ProcSense.Core.Logging;
using ProcSense.VR.Auth;
using UnityEngine;

namespace ProcSense.VR.Voice
{
    [Serializable] public class TtsLineDto { public string text, audio_b64; }
    [Serializable] public class TtsFallbackDto { public bool ok, fallback; public string error; }

    /// <summary>One sentence of a spoken line: its words and the recorded audio (Ogg Vorbis bytes).</summary>
    public sealed class TtsLine
    {
        public string Text = "";
        public byte[] Audio = new byte[0];
    }

    /// <summary>
    /// The Unity half of speech output (plan p1-voice-input-s04, hook 6): asks the service to speak a line that the dialogue service already released (the voice runs locally on the service: Piper, never a cloud model) and
    /// reads the sentences back. Anything that goes wrong (no sign-in, no network, the voice not installed, a feature switched off) is an empty list: the subtitle is always shown and the trainee reads it.
    /// </summary>
    public static class TtsClient
    {
        public const string Route = "/tts/organisations/";

        /// <summary>The voice of a character: the service's voice table has a female and a male patient, and a voice per staff role.</summary>
        public static string VoiceFor(string teamRole, string sex)
        {
            switch (teamRole)
            {
                case "patient": return string.Equals(sex, "F", StringComparison.OrdinalIgnoreCase) ? "patient_female" : "patient_male";
                case "nurse": return "nurse";
                case "anaesthesia": case "anaesthetist": return "anaesthetist";
                case "surgeon": return "surgeon";
                case "porter": return "porter";
                case "lab": case "laboratory": return "laboratory";
                default: return "team";
            }
        }

        public static string RequestBody(string text, string voice)
        {
            var sb = new System.Text.StringBuilder("{\"text\":");
            JsonText.Quote(sb, text);
            sb.Append(",\"role\":");
            JsonText.Quote(sb, voice);
            return sb.Append('}').ToString();
        }

        /// <summary>The service answers one JSON object per sentence, one per line; a single object with fallback true means no voice.</summary>
        public static List<TtsLine> Parse(string body)
        {
            var lines = new List<TtsLine>();
            if (string.IsNullOrWhiteSpace(body)) return lines;
            foreach (var row in body.Split('\n'))
            {
                if (string.IsNullOrWhiteSpace(row)) continue;
                TtsLineDto dto = null;
                try { dto = JsonUtility.FromJson<TtsLineDto>(row); } catch (ArgumentException) { }
                if (dto == null || string.IsNullOrEmpty(dto.audio_b64)) continue;
                try { lines.Add(new TtsLine { Text = dto.text ?? "", Audio = Convert.FromBase64String(dto.audio_b64) }); } catch (FormatException) { }
            }
            return lines;
        }

        public static IEnumerator Speak(AuthService auth, string text, string voice, Action<List<TtsLine>> done)
        {
            if (auth == null || !auth.IsSignedIn || string.IsNullOrWhiteSpace(text)) { done(new List<TtsLine>()); yield break; }
            HttpResult answer = default;
            yield return auth.Authorized("POST", Route + auth.OrganisationId + "/speak", RequestBody(text, voice), r => answer = r);
            done(answer.Ok ? Parse(answer.Body) : new List<TtsLine>());
        }
    }
}
