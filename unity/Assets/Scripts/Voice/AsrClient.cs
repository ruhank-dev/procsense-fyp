using System;
using System.Collections;
using ProcSense.VR.Auth;
using UnityEngine;

namespace ProcSense.VR.Voice
{
    [Serializable] public class AsrDto { public bool ok, fallback, low_confidence; public string text, error; public float seconds; }

    /// <summary>What the speech recogniser answered for one clip, or the safe answer (nothing heard) when it could not be reached.</summary>
    public sealed class AsrResult
    {
        public bool Ok;
        public string Text = "";
        /// <summary>The recogniser was not sure: the client says "say that again" and nothing is asked or credited.</summary>
        public bool LowConfidence;
        public bool Unavailable;
        public string Error = "";
        public bool Usable => Ok && !LowConfidence && !string.IsNullOrWhiteSpace(Text);
    }

    /// <summary>
    /// The Unity half of speech recognition (plan p1-voice-input-s01, hook 6 of docs/architecture/unity_ai_hooks.md): sends the push-to-talk clip to the service, which runs the recogniser on the server (the clip is never stored)
    /// and answers the words. Speech recognition stays local to the service (faster-whisper); this class sends nothing to a cloud model. It never throws: a missing sign-in, no network, a refusal or an unreadable answer is
    /// <see cref="AsrResult.Unavailable"/>, and the trainee falls back to the tablet.
    /// </summary>
    public static class AsrClient
    {
        public const string Route = "/asr/organisations/";

        public static AsrResult Parse(string body)
        {
            AsrDto dto = null;
            try { dto = JsonUtility.FromJson<AsrDto>(body); } catch (ArgumentException) { }
            if (dto == null) return new AsrResult { Unavailable = true, Error = "UNREADABLE" };
            if (dto.fallback || !dto.ok) return new AsrResult { Unavailable = dto.fallback, Error = dto.error ?? "" };
            return new AsrResult { Ok = true, Text = (dto.text ?? "").Trim(), LowConfidence = dto.low_confidence };
        }

        public static IEnumerator Transcribe(AuthService auth, byte[] wav, Action<AsrResult> done)
        {
            if (auth == null || !auth.IsSignedIn || wav == null || wav.Length == 0) { done(new AsrResult { Unavailable = true, Error = "NOT_SIGNED_IN" }); yield break; }
            HttpResult answer = default;
            yield return auth.AuthorizedBytes("POST", Route + auth.OrganisationId + "/transcribe", wav, "audio/wav", r => answer = r);
            done(answer.Ok ? Parse(answer.Body) : new AsrResult { Unavailable = true, Error = answer.NetworkError ? "NO_NETWORK" : "HTTP_" + answer.Status });
        }
    }
}
