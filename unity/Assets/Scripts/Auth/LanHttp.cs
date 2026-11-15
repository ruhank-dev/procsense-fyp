using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ProcSense.VR.Auth
{
    /// <summary>The kinds of request the headset sends to the laptop; each has its own time budget and at most one request in flight (P3-NET-09).</summary>
    public enum RequestKind { Other, Health, Asr, Action, Dialogue, Tts }

    public static class RequestBudgets
    {
        /// <summary>Seconds the headset waits before it gives up and offers the menu (plan p3-18 section 5): ASR 6, spoken action and orders 8, dialogue 10, first TTS sentence 4, health 2. Tuned later with the DC-14/DC-15 measurements.</summary>
        public static int SecondsFor(RequestKind kind, int callerSeconds)
        {
            int budget = kind switch { RequestKind.Health => 2, RequestKind.Asr => 6, RequestKind.Action => 8, RequestKind.Dialogue => 10, RequestKind.Tts => 4, _ => callerSeconds };
            return Math.Max(1, Math.Min(callerSeconds, budget));
        }

        public static RequestKind Classify(string url)
        {
            var path = url ?? "";
            int slash = path.IndexOf("://", StringComparison.Ordinal);
            if (slash >= 0) { int next = path.IndexOf('/', slash + 3); path = next >= 0 ? path.Substring(next) : "/"; }
            if (path.StartsWith("/health", StringComparison.Ordinal) || path.StartsWith("/ai/health", StringComparison.Ordinal)) return RequestKind.Health;
            if (path.StartsWith("/asr", StringComparison.Ordinal)) return RequestKind.Asr;
            if (path.StartsWith("/speech", StringComparison.Ordinal) || path.StartsWith("/orders", StringComparison.Ordinal)) return RequestKind.Action;
            if (path.StartsWith("/dialogue", StringComparison.Ordinal)) return RequestKind.Dialogue;
            if (path.StartsWith("/tts", StringComparison.Ordinal)) return RequestKind.Tts;
            return RequestKind.Other;
        }
    }

    /// <summary>
    /// A wrapper around any <see cref="IHttp"/> for the networked headset (P3-NET-07, 09, 10): adds the paired device token and the version headers, applies the time budget of the request kind,
    /// numbers every request, keeps one request in flight per kind (a newer one makes the older answer "superseded", so a late answer is dropped, never acted on) and retries a failed
    /// idempotent GET once. It never retries a POST: an action proposal is not repeated automatically. The headset never holds a cloud key; the token only proves it was paired.
    /// </summary>
    public sealed class LanHttp : IHttp
    {
        private readonly IHttp _inner;
        private readonly Func<string> _token;
        private readonly Func<string> _content;
        private readonly Dictionary<RequestKind, long> _latest = new Dictionary<RequestKind, long>();
        private long _next;
        public int Dropped { get; private set; }
        public long LastRequestId => _next;

        public LanHttp(IHttp inner, Func<string> token = null, Func<string> content = null) { _inner = inner; _token = token ?? DeviceTokenStore.Get; _content = content ?? (() => ""); }

        /// <summary>The wrapper every client should use at run time.</summary>
        public static IHttp Default() => new LanHttp(new UnityHttp());

        public IEnumerator Send(string method, string url, string jsonBody, IDictionary<string, string> headers, int timeoutSeconds, Action<HttpResult> done)
            => Run(method, url, timeoutSeconds, headers, (h, t, cb) => _inner.Send(method, url, jsonBody, h, t, cb), done);

        public IEnumerator SendBytes(string method, string url, byte[] body, string contentType, IDictionary<string, string> headers, int timeoutSeconds, Action<HttpResult> done)
            => Run(method, url, timeoutSeconds, headers, (h, t, cb) => _inner.SendBytes(method, url, body, contentType, h, t, cb), done);

        private IEnumerator Run(string method, string url, int timeoutSeconds, IDictionary<string, string> headers, Func<IDictionary<string, string>, int, Action<HttpResult>, IEnumerator> call, Action<HttpResult> done)
        {
            var kind = RequestBudgets.Classify(url);
            long id = ++_next;
            if (kind != RequestKind.Other) _latest[kind] = id;
            var merged = Headers(headers, id);
            int timeout = RequestBudgets.SecondsFor(kind, timeoutSeconds);
            HttpResult result = default;
            int attempts = string.Equals(method, "GET", StringComparison.OrdinalIgnoreCase) ? 2 : 1;
            for (int attempt = 0; attempt < attempts; attempt++)
            {
                yield return call(merged, timeout, r => result = r);
                if (!result.NetworkError) break;
            }
            if (kind != RequestKind.Other && _latest[kind] != id) { Dropped++; result = new HttpResult(HttpResult.SupersededStatus, "superseded", true); }
            done(result);
        }

        private Dictionary<string, string> Headers(IDictionary<string, string> given, long id)
        {
            var h = given != null ? new Dictionary<string, string>(given) : new Dictionary<string, string>();
            var token = _token();
            if (!string.IsNullOrEmpty(token)) h["X-ProcSense-Device"] = token;      // never the Authorization header: that one belongs to the signed-in user
            h["X-ProcSense-Build"] = Application.version ?? "";
            h["X-ProcSense-Engine"] = ProcSense.Core.CoreInfo.Version;
            var content = _content();
            if (!string.IsNullOrEmpty(content)) h["X-ProcSense-Content"] = content;
            h["X-Request-Id"] = id.ToString();
            return h;
        }
    }

    /// <summary>The device token the laptop gave this headset when it was paired. Kept on the device only (never in Git, never sent anywhere except to the paired laptop).</summary>
    public static class DeviceTokenStore
    {
        public interface IStorage { string Get(string key); void Set(string key, string value); void Delete(string key); }
        private sealed class Prefs : IStorage
        {
            public string Get(string key) => PlayerPrefs.GetString(key, "");
            public void Set(string key, string value) { PlayerPrefs.SetString(key, value); PlayerPrefs.Save(); }
            public void Delete(string key) { PlayerPrefs.DeleteKey(key); PlayerPrefs.Save(); }
        }

        public const string Key = "procsense.device_token";
        public static IStorage Storage = new Prefs();
        public static string Get() => Storage.Get(Key);
        public static bool IsPaired => !string.IsNullOrEmpty(Get());
        public static void Set(string token) => Storage.Set(Key, token ?? "");
        public static void Clear() => Storage.Delete(Key);
    }
}
