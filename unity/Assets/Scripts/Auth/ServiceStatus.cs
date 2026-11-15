using System;
using System.Collections;
using UnityEngine;

namespace ProcSense.VR.Auth
{
    /// <summary>How the headset's link to the laptop server stands (P3-API-03, P3-NET-08): green, amber or red, with the words the chip shows.</summary>
    public enum ConnectionLevel { Connected, Degraded, Offline }

    /// <summary>
    /// The connection model, without Unity (P3-API-03, P3-NET-08): from the answers of the laptop's <c>/health</c> (reachable, API and engine versions) and <c>/ai/health</c> (the model host up,
    /// cloud routing), plus whether this headset is paired, it works out one state: <see cref="ConnectionLevel.Connected"/> ("Laptop connected · Cloud AI ready"),
    /// <see cref="ConnectionLevel.Degraded"/> ("Reconnecting…", "Cloud AI unavailable · local fallback", a version mismatch) or <see cref="ConnectionLevel.Offline"/>
    /// ("Laptop not found"). The polling interval backs off while the laptop is away and comes back to the short interval as soon as it answers, so a stopped laptop shows within
    /// 5 s and a restarted one within 10 s. Voice needs the laptop; the menus never do.
    /// </summary>
    public sealed class ServiceStatusModel
    {
        public const float HealthyInterval = 3f, TimeoutSeconds = 2f;
        public static readonly float[] Backoff = { 1f, 2f, 4f };

        public bool Reachable { get; private set; }
        public bool Paired { get; private set; }
        public CompatState Compat { get; private set; } = CompatState.Unknown;
        public bool CloudReady { get; private set; }
        public bool ModelConfigured { get; private set; }
        public int Failures { get; private set; }
        public bool Known { get; private set; }
        public float NextCheckAt { get; private set; }
        public event Action Changed;

        public ConnectionLevel Level =>
            !Known ? ConnectionLevel.Degraded :
            !Reachable ? (Failures <= 1 ? ConnectionLevel.Degraded : ConnectionLevel.Offline) :
            Compat == CompatState.ApiMismatch || Compat == CompatState.EngineMismatch || !CloudReady ? ConnectionLevel.Degraded : ConnectionLevel.Connected;

        /// <summary>True when voice can be used (the laptop answers and speaks this app's versions).</summary>
        public bool VoiceAvailable => Reachable && Compat != CompatState.ApiMismatch && Compat != CompatState.EngineMismatch;

        /// <summary>The chip's words for the current state.</summary>
        public string Text
        {
            get
            {
                if (!Known) return "Looking for the laptop…";
                if (!Reachable) return Failures <= 1 ? "Reconnecting…" : "Laptop not found · play in menu mode";
                if (Compat == CompatState.ApiMismatch || Compat == CompatState.EngineMismatch) return "Laptop needs an update · voice off";
                if (!CloudReady) return "Cloud AI unavailable · local fallback";
                return "Laptop connected · Cloud AI ready";
            }
        }

        public bool Due(float now) => now >= NextCheckAt;

        /// <summary>One round of answers: <paramref name="health"/> null when /health did not answer; <paramref name="ai"/> null when /ai/health did not.</summary>
        public void Report(string health, string ai, bool paired, float now)
        {
            bool reachable = health != null;
            var compat = reachable ? PairingClient.Evaluate(health) : CompatState.Unknown;
            bool cloud = false, configured = false;
            if (ai != null) ReadAi(ai, out cloud, out configured);
            Failures = reachable ? 0 : Failures + 1;
            NextCheckAt = now + (reachable ? HealthyInterval : Backoff[Math.Min(Failures, Backoff.Length) - 1]);
            bool changed = !Known || reachable != Reachable || compat != Compat || cloud != CloudReady || paired != Paired || (!reachable && Failures == 2);
            Known = true; Reachable = reachable; Compat = compat; CloudReady = reachable && cloud; ModelConfigured = configured; Paired = paired;
            if (changed) Changed?.Invoke();
        }

        [Serializable] private sealed class Llm { public bool up; public bool configured; }
        [Serializable] private sealed class AiHealth { public Llm llm; }

        private static void ReadAi(string json, out bool up, out bool configured)
        {
            up = configured = false;
            try { var a = JsonUtility.FromJson<AiHealth>(json); if (a != null && a.llm != null) { up = a.llm.up; configured = a.llm.configured; } }
            catch (Exception) { }
        }
    }

    /// <summary>Polls the laptop (the address of <see cref="ServerEndpoint.Current"/>) and keeps a <see cref="ServiceStatusModel"/> (P3-NET-08). One per app; the chip and the voice banner read <see cref="Instance"/>.</summary>
    public sealed class ServiceStatus : MonoBehaviour
    {
        public static ServiceStatus Instance { get; private set; }
        public ServiceStatusModel Model { get; } = new ServiceStatusModel();
        public Func<IHttp> Http = LanHttp.Default;
        public Func<string> BaseUrl = () => ServerEndpoint.Current;
        private bool _running;

        /// <summary>The app's status (made on first use, kept across scenes).</summary>
        public static ServiceStatus Ensure()
        {
            if (Instance != null) return Instance;
            var go = new GameObject("Service status");
            if (Application.isPlaying) DontDestroyOnLoad(go);
            return go.AddComponent<ServiceStatus>();
        }

        private void Awake() { if (Instance != null && Instance != this) { Destroy(this); return; } Instance = this; }
        private void OnDestroy() { if (Instance == this) Instance = null; }

        private void Update()
        {
            if (_running || !Model.Due(Time.realtimeSinceStartup)) return;
            string url = BaseUrl();
            if (string.IsNullOrEmpty(url)) return;
            StartCoroutine(Poll(url));
        }

        /// <summary>One round: /health, then /ai/health when the laptop answered.</summary>
        public IEnumerator Poll(string baseUrl)
        {
            _running = true;
            string root = baseUrl.TrimEnd('/');
            HttpResult health = default, ai = default;
            var http = Http();
            yield return http.Send("GET", root + "/health", null, null, (int)ServiceStatusModel.TimeoutSeconds, r => health = r);
            if (health.Ok) yield return http.Send("GET", root + "/ai/health", null, null, (int)ServiceStatusModel.TimeoutSeconds, r => ai = r);
            Model.Report(health.Ok ? health.Body : null, health.Ok && ai.Ok ? ai.Body : null, DeviceTokenStore.IsPaired, Time.realtimeSinceStartup);
            _running = false;
        }
    }
}
