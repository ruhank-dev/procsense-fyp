using System;
using System.Collections;
using ProcSense.VR.Auth;
using UnityEngine;

namespace ProcSense.VR.Voice
{
    /// <summary>
    /// Whether the service is reachable, kept apart from the Unity parts so it is tested without a scene (plan p1-voice-input-s02, FallbackController): the headset asks <c>/health</c> every 30 seconds with a short timeout;
    /// one failed answer makes voice <see cref="Offline"/> at once (the trainee sees a banner and uses the tablet menu), and the next good answer brings it back. Voice must never block play.
    /// </summary>
    public sealed class VoiceHealthState
    {
        public const float IntervalSeconds = 30f;
        public const int TimeoutSeconds = 3;                       // the plan says 2.5 s; whole seconds are what the transport takes
        public const string OfflineBanner = "Voice is offline: use the tablet menu. It will come back by itself.";

        public bool Offline { get; private set; }
        public string Banner => Offline ? OfflineBanner : "";
        public float NextCheckAt { get; private set; }
        public event Action<bool> Changed;

        /// <summary>True when a check is due at <paramref name="now"/> (seconds on any steady clock).</summary>
        public bool Due(float now) => now >= NextCheckAt;

        public void Report(bool reachable, float now)
        {
            NextCheckAt = now + IntervalSeconds;
            if (Offline == !reachable) return;
            Offline = !reachable;
            Changed?.Invoke(Offline);
        }
    }

    /// <summary>Asks the service whether it is there and keeps a <see cref="VoiceHealthState"/>. Put it beside <see cref="PushToTalk"/>.</summary>
    public sealed class VoiceHealth : MonoBehaviour
    {
        public VoiceHealthState State { get; } = new VoiceHealthState();
        public Func<IHttp> Http = () => new UnityHttp();
        public Func<AuthService> Auth = () => AuthService.Current;
        private bool _running;

        public static IEnumerator Check(IHttp http, string baseUrl, Action<bool> done)
        {
            HttpResult answer = default;
            yield return http.Send("GET", baseUrl + "/health", null, null, VoiceHealthState.TimeoutSeconds, r => answer = r);
            done(answer.Ok);
        }

        private void Update()
        {
            var auth = Auth();
            if (_running || auth == null || !auth.IsSignedIn || !State.Due(Time.realtimeSinceStartup)) return;
            StartCoroutine(Run(auth.BaseUrl));
        }

        private IEnumerator Run(string baseUrl)
        {
            _running = true;
            bool reachable = false;
            yield return Check(Http(), baseUrl, ok => reachable = ok);
            State.Report(reachable, Time.realtimeSinceStartup);
            _running = false;
        }
    }
}
