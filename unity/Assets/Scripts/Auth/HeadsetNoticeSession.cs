using System;
using UnityEngine;

namespace ProcSense.VR.Auth
{
    public enum NoticeLink { Offline, Reconnecting, Connected }

    /// <summary>
    /// Keeps the optional notice socket alive for one session: Begin when a paired headset has a session id, End on session end or sign-out, a fresh transport with exponential backoff (2 s doubling to 30 s) after a drop. Never throws, never blocks, and
    /// nothing in play reads it; an unpaired headset or an unusable address stays <see cref="NoticeLink.Offline"/> and never tries. Time is passed in so tests need no clock.
    /// </summary>
    public sealed class HeadsetNoticeSession
    {
        public const float FirstDelay = 2f, MaxDelay = 30f;
        private readonly Func<INoticeTransport> _factory;
        private HeadsetNotices _notices;
        private string _baseUrl, _sessionId, _token;
        private bool _active, _gaveUp;
        private float _nextAttempt, _delay = FirstDelay;

        public HeadsetNoticeSession(Func<INoticeTransport> factory) { _factory = factory; }

        public bool Active => _active;
        public int Attempts { get; private set; }
        public float NextDelay => _delay;
        public HeadsetNotices Notices => _notices;
        public NoticeLink Link => !_active || _notices == null ? NoticeLink.Offline : _notices.Connected ? NoticeLink.Connected : NoticeLink.Reconnecting;

        /// <summary>Starts (or restarts for a new session id). Returns false, staying offline, when unpaired or the address is not acceptable.</summary>
        public bool Begin(string baseUrl, string sessionId, float now, string deviceToken = null)
        {
            End();
            try
            {
                _token = deviceToken ?? DeviceTokenStore.Get();
                if (HeadsetNotices.SocketUrl(baseUrl, sessionId) == "" || string.IsNullOrEmpty(_token) || _factory == null) return false;
            }
            catch (Exception) { return false; }
            _baseUrl = baseUrl; _sessionId = sessionId; _active = true; _gaveUp = false; _delay = FirstDelay; Attempts = 0;
            Connect(now);
            return true;
        }

        public void End()
        {
            _active = false;
            try { _notices?.Stop(); } catch (Exception) { }
            _notices = null;
        }

        /// <summary>Call once per frame.</summary>
        public void Tick(float now)
        {
            if (!_active || _notices == null) return;
            try
            {
                _notices.Poll();
                if (_notices.Shutdown) { _gaveUp = true; }                                  // the laptop said it is going away: do not hammer it
                if (_notices.Connected) { _delay = FirstDelay; _nextAttempt = now + FirstDelay; return; }
                if (!_gaveUp && _closedSeen && now >= _nextAttempt) { Connect(now); }
            }
            catch (Exception) { }
        }

        private bool _closedSeen;

        private void Connect(float now)
        {
            try { _notices?.Stop(); } catch (Exception) { }
            INoticeTransport t = null;
            try { t = _factory(); } catch (Exception) { }
            _notices = new HeadsetNotices(new Watch(t, this));
            Attempts++;
            _closedSeen = false;
            _nextAttempt = now + _delay;
            _delay = Mathf.Min(_delay * 2f, MaxDelay);
            if (!_notices.Start(_baseUrl, _sessionId, _token)) { _closedSeen = true; }
        }

        // Wraps a transport so the session learns when it closed, without changing the transport seam.
        private sealed class Watch : INoticeTransport
        {
            private readonly INoticeTransport _t; private readonly HeadsetNoticeSession _s;
            public Watch(INoticeTransport t, HeadsetNoticeSession s) { _t = t; _s = s; }
            public void Open(string url, string token) { if (_t == null) throw new InvalidOperationException("no transport"); _t.Open(url, token); }
            public bool TryReceive(out string m) { if (_t == null) { m = null; return false; } return _t.TryReceive(out m); }
            public bool Closed { get { var c = _t == null || _t.Closed; if (c) { _s._closedSeen = true; } return c; } }
            public void Close() { _t?.Close(); }
        }

        /// <summary>The one banner line to show, most important first, or "" when nothing needs saying. Allocation-free for repeated identical input except progress text (cached by percent by the view).</summary>
        public string Line()
        {
            var pin = TlsPin.Banner();
            if (pin.Length > 0) return pin;
            if (_notices != null) { var s = _notices.Status(); if (s.Length > 0) return s; }
            return Link == NoticeLink.Connected ? "Connected to the laptop" : Link == NoticeLink.Reconnecting ? "Reconnecting to the laptop..." : "Laptop notices offline";
        }
    }
}
