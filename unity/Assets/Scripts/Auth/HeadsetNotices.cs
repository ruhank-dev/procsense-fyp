using System;
using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace ProcSense.VR.Auth
{
    /// <summary>The transport of the optional notice socket. A seam so tests use a scripted one; <see cref="ClientWebSocketTransport"/> is the real thing. Never blocks the caller.</summary>
    public interface INoticeTransport
    {
        /// <summary>Starts connecting (the device token goes in a header). Failure is reported by <see cref="Closed"/>, never thrown.</summary>
        void Open(string url, string deviceToken);
        /// <summary>Next received text message, or false when none is waiting.</summary>
        bool TryReceive(out string message);
        bool Closed { get; }
        void Close();
    }

    public enum NoticeKind { Unknown, Hello, GenerationProgress, Shutdown, Quota }

    /// <summary>
    /// The headset end of the optional server-push socket /ws/headset/{session_id} (P3-NET-12): generation progress, a laptop shutting down, cloud quota used up. Everything is optional: no pairing, a refused or dropped socket or an
    /// unsupported platform all leave <see cref="Connected"/> false and change nothing else; no part of play, scoring or the menus reads this. The owner of the instance calls <see cref="Poll"/> once per frame; it never blocks.
    /// </summary>
    public sealed class HeadsetNotices
    {
        [Serializable] private sealed class NoticeDto { public string kind; public string session_id; public float progress = -1f; public string detail; }

        private readonly INoticeTransport _transport;
        public HeadsetNotices(INoticeTransport transport) { _transport = transport; }

        public bool Connected { get; private set; }
        public bool Shutdown { get; private set; }
        public bool QuotaExhausted { get; private set; }
        /// <summary>0..1 while a generation reports progress, otherwise -1.</summary>
        public float Progress { get; private set; } = -1f;
        public string Detail { get; private set; } = "";
        public int Received { get; private set; }

        /// <summary>The ws:// or wss:// address for a session, or "" when the base address is not acceptable (the https / private-http rules of <see cref="ServiceSettings.IsAcceptable"/>) or the session id is not 1..64 safe characters.</summary>
        public static string SocketUrl(string baseUrl, string sessionId)
        {
            if (!ServiceSettings.IsAcceptable(baseUrl) || string.IsNullOrEmpty(sessionId) || sessionId.Length > 64) return "";
            foreach (var c in sessionId) if (!(char.IsLetterOrDigit(c) || c == '-' || c == '_')) return "";
            var b = baseUrl.TrimEnd('/');
            return (b.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ? "wss://" + b.Substring(8) : "ws://" + b.Substring(7)) + "/ws/headset/" + sessionId;
        }

        /// <summary>Opens the socket only for a paired headset and an acceptable address; returns whether an attempt was made. Safe to skip entirely.</summary>
        public bool Start(string baseUrl, string sessionId, string deviceToken = null)
        {
            deviceToken = deviceToken ?? DeviceTokenStore.Get();
            var url = SocketUrl(baseUrl, sessionId);
            if (url == "" || string.IsNullOrEmpty(deviceToken) || _transport == null) return false;
            try { _transport.Open(url, deviceToken); return true; } catch (Exception) { return false; }
        }

        public void Stop() { try { _transport?.Close(); } catch (Exception) { } Connected = false; }

        /// <summary>Applies every waiting notice. Returns how many were applied.</summary>
        public int Poll()
        {
            int n = 0;
            if (_transport == null) return 0;
            while (_transport.TryReceive(out var text)) { Apply(text); n++; }
            if (_transport.Closed) { Connected = false; Progress = -1f; }
            return n;
        }

        private void Apply(string text)
        {
            NoticeDto dto;
            try { dto = JsonUtility.FromJson<NoticeDto>(text); } catch (Exception) { return; }
            if (dto == null) return;
            Received++;
            switch (Parse(dto.kind))
            {
                case NoticeKind.Hello: Connected = true; break;
                case NoticeKind.GenerationProgress: Progress = dto.progress < 0f ? -1f : Mathf.Clamp01(dto.progress); Detail = dto.detail ?? ""; break;
                case NoticeKind.Shutdown: Shutdown = true; Detail = dto.detail ?? ""; break;
                case NoticeKind.Quota: QuotaExhausted = true; Detail = dto.detail ?? ""; break;
            }
        }

        public static NoticeKind Parse(string kind) => kind switch
        {
            "hello" => NoticeKind.Hello, "generation_progress" => NoticeKind.GenerationProgress, "shutdown" => NoticeKind.Shutdown, "quota" => NoticeKind.Quota, _ => NoticeKind.Unknown,
        };

        /// <summary>A short line for a status chip, or "" when nothing needs saying.</summary>
        public string Status() =>
            Shutdown ? "The laptop is shutting down. Menus and saved scenarios still work." :
            QuotaExhausted ? "The cloud voice quota is used up. Voice uses the menus." :
            Progress >= 0f ? "Preparing a new scenario: " + Mathf.RoundToInt(Progress * 100f) + "%" : "";
    }

    /// <summary>System.Net.WebSockets client on a background task; the main thread only reads a queue. A wss certificate is pinned with <see cref="TlsPin"/>. Any failure just closes it.</summary>
    public sealed class ClientWebSocketTransport : INoticeTransport
    {
        private readonly ConcurrentQueue<string> _inbox = new ConcurrentQueue<string>();
        private readonly CancellationTokenSource _cts = new CancellationTokenSource();
        private volatile bool _closed;
        public bool Closed => _closed;

        public void Open(string url, string deviceToken)
        {
            _closed = false;
            Task.Run(async () =>
            {
                try
                {
                    using (var socket = new ClientWebSocket())
                    {
                        socket.Options.SetRequestHeader("X-ProcSense-Device", deviceToken);
                        if (url.StartsWith("wss://", StringComparison.Ordinal))
                            socket.Options.RemoteCertificateValidationCallback = (s, cert, chain, errors) => cert != null && TlsPin.Decide(TlsPin.Sha256Hex(cert.GetRawCertData())) == PinDecision.Accept;
                        await socket.ConnectAsync(new Uri(url), _cts.Token);
                        var buffer = new byte[4096];
                        var text = new StringBuilder();
                        while (socket.State == WebSocketState.Open && !_cts.IsCancellationRequested)
                        {
                            var r = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), _cts.Token);
                            if (r.MessageType == WebSocketMessageType.Close) break;
                            text.Append(Encoding.UTF8.GetString(buffer, 0, r.Count));
                            if (r.EndOfMessage) { _inbox.Enqueue(text.ToString()); text.Length = 0; }
                        }
                    }
                }
                catch (Exception) { /* optional channel: any failure is just "no notices" */ }
                finally { _closed = true; }
            });
        }

        public bool TryReceive(out string message) => _inbox.TryDequeue(out message);
        public void Close() { try { _cts.Cancel(); } catch (Exception) { } }
    }
}
