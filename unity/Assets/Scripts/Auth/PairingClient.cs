using System;
using System.Collections;
using UnityEngine;

namespace ProcSense.VR.Auth
{
    public enum PairOutcome { Paired, NotRequired, WrongCode, Locked, Offline, Failed }
    public enum CompatState { Unknown, Ok, EngineMismatch, ApiMismatch }

    /// <summary>The headset side of pairing with the laptop (P3-NET-07) and of the version check (P3-NET-10). Pure logic over <see cref="IHttp"/>, so it is tested without a network.</summary>
    public static class PairingClient
    {
        [Serializable] private sealed class ExchangeIn { public string code; public string device_name; }
        [Serializable] private sealed class ExchangeOut { public string device_id; public string device_token; public string tls_fingerprint; }
        [Serializable] private sealed class HealthDto { public int api_version; public string[] engine_versions; public bool lan_profile; }

        public const int ApiVersion = 1;

        public static IEnumerator Exchange(IHttp http, string baseUrl, string code, string deviceName, Action<PairOutcome> done)
        {
            HttpResult answer = default;
            var body = JsonUtility.ToJson(new ExchangeIn { code = code, device_name = deviceName ?? "headset" });
            bool tls = TlsPin.AppliesTo(baseUrl);
            if (tls) { TlsPin.PairingWindow = true; TlsPin.ResetState(); TlsPin.PairingWindow = true; }
            yield return http.Send("POST", baseUrl.TrimEnd('/') + "/pairing/exchange", body, null, 6, r => answer = r);
            if (tls) TlsPin.PairingWindow = false;
            if (answer.NetworkError) { done(PairOutcome.Offline); yield break; }
            switch (answer.Status)
            {
                case 404: done(PairOutcome.NotRequired); yield break;       // a server that is not in the LAN profile needs no pairing
                case 403: done(PairOutcome.WrongCode); yield break;
                case 429: done(PairOutcome.Locked); yield break;
            }
            if (!answer.Ok) { done(PairOutcome.Failed); yield break; }
            var dto = JsonUtility.FromJson<ExchangeOut>(answer.Body);
            if (dto == null || string.IsNullOrEmpty(dto.device_token)) { done(PairOutcome.Failed); yield break; }
            if (tls)
            {
                // P3-NET-15: the fingerprint the laptop reports must be the certificate this very connection showed, then it is pinned.
                var reported = TlsPin.Normalize(dto.tls_fingerprint);
                if (reported.Length != 64 || reported != TlsPin.Observed) { done(PairOutcome.Failed); yield break; }
                TlsPin.Set(reported);
            }
            DeviceTokenStore.Set(dto.device_token);
            done(PairOutcome.Paired);
        }

        /// <summary>Compares the laptop's /health with this headset's engine: a laptop that does not list this engine version, or speaks another API version, means the AI routes stay off and the menus keep working.</summary>
        public static IEnumerator CheckCompatibility(IHttp http, string baseUrl, Action<CompatState> done)
        {
            HttpResult answer = default;
            yield return http.Send("GET", baseUrl.TrimEnd('/') + "/health", null, null, 2, r => answer = r);
            done(answer.Ok ? Evaluate(answer.Body) : CompatState.Unknown);
        }

        public static CompatState Evaluate(string healthJson)
        {
            HealthDto dto;
            try { dto = JsonUtility.FromJson<HealthDto>(healthJson); } catch (Exception) { return CompatState.Unknown; }
            if (dto == null) return CompatState.Unknown;
            if (dto.api_version != 0 && dto.api_version != ApiVersion) return CompatState.ApiMismatch;
            if (dto.engine_versions != null && dto.engine_versions.Length > 0 && Array.IndexOf(dto.engine_versions, ProcSense.Core.CoreInfo.Version) < 0) return CompatState.EngineMismatch;
            return CompatState.Ok;
        }

        /// <summary>The line shown in the banner for a state, or "" when there is nothing to say.</summary>
        public static string Banner(CompatState state) => state switch
        {
            CompatState.EngineMismatch => "The laptop server does not support this version of the app. Voice is off; the menus still work.",
            CompatState.ApiMismatch => "The laptop server speaks a different API version. Voice is off; the menus still work.",
            _ => "",
        };
    }

    /// <summary>The digits of the pairing code as the player types them on the keypad. No Unity types, so it is tested directly.</summary>
    public sealed class PairingEntry
    {
        public const int Length = 6;
        private readonly System.Text.StringBuilder _digits = new System.Text.StringBuilder();
        public string Code => _digits.ToString();
        public bool Complete => _digits.Length == Length;
        public void Press(int digit) { if (digit >= 0 && digit <= 9 && _digits.Length < Length) _digits.Append((char)('0' + digit)); }
        public void Back() { if (_digits.Length > 0) _digits.Length--; }
        public void Clear() => _digits.Length = 0;
        public string Display => Code.PadRight(Length, '_');
    }
}
