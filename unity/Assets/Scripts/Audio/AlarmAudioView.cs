using ProcSense.Core.Actions;
using ProcSense.VR.Bridge;
using ProcSense.VR.UI.Monitor;
using ProcSense.VR.Views;
using UnityEngine;

namespace ProcSense.VR.Audio
{
    /// <summary>
    /// The monitor's alarm sound and its cuff pump (p0-voice-audio-s05), from the monitor's alarms (which follow Core's display zones): the worst alarm still sounding plays
    /// its priority's burst every repeat time; an acknowledged alarm is quiet until its silence ends or it gets worse; nothing sounds without an alarm. Spatial, at the
    /// monitor. The monitor adds it to itself, so no scene needs editing.
    /// </summary>
    [RequireComponent(typeof(MonitorView))]
    public sealed class AlarmAudioView : MonoBehaviour
    {
        /// <summary>True while any monitor is sounding a high-priority alarm (the room ambience ducks under it).</summary>
        public static bool HighSounding { get; private set; }
        private static int _highCount;

        public AudioSource Source { get; private set; }
        public AudioSource CuffSource { get; private set; }
        /// <summary>The priority now sounding ("" for none), and the bursts played so far (tests).</summary>
        public string Playing { get; private set; } = "";
        public int Bursts { get; private set; }

        private MonitorView _monitor;
        private float _next;
        private bool _high;
        private SimulationHost _host;

        private void Awake()
        {
            _monitor = GetComponent<MonitorView>();
            Source = MakeSource("Alarm sound", 1f);
            CuffSource = MakeSource("Cuff sound", .6f);
        }

        private AudioSource MakeSource(string name, float volume)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var src = go.AddComponent<AudioSource>();
            src.playOnAwake = false;
            src.spatialBlend = 1f;
            src.rolloffMode = AudioRolloffMode.Logarithmic;
            src.minDistance = 1f;
            src.maxDistance = 15f;
            src.volume = volume;
            return src;
        }

        private void OnEnable() => Attach(SimulationHost.Instance);
        private void OnDisable() { Attach(null); SetHigh(false); Playing = ""; }

        private void Attach(SimulationHost host)
        {
            if (_host != null) _host.Applied -= OnApplied;
            _host = host;
            if (_host != null) _host.Applied += OnApplied;
        }

        private void OnApplied(SimAction action, ActionOutcome outcome)
        {
            if (action.Verb == "measure_nibp" && outcome.Accepted && CuffSource != null) { CuffSource.clip = ToneBank.Cuff(); CuffSource.Play(); }
        }

        private void Update()
        {
            if (!ReferenceEquals(_host, SimulationHost.Instance)) Attach(SimulationHost.Instance);
            var sounding = _monitor.Alarms?.Sounding;
            string now = sounding?.Priority.Id ?? "";
            SetHigh(now == "high");
            if (now != Playing) { Playing = now; _next = 0; if (now.Length == 0) Source.Stop(); }
            if (sounding == null || Time.unscaledTime < _next) return;
            Source.clip = ToneBank.Alarm(sounding.Priority.Id, sounding.Priority.Pulses);
            Source.Play();
            Bursts++;
            _next = Time.unscaledTime + sounding.Priority.RepeatSeconds;
        }

        private void SetHigh(bool high)
        {
            if (high == _high) return;
            _high = high;
            _highCount += high ? 1 : -1;
            HighSounding = _highCount > 0;
        }
    }
}
