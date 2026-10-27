using System;
using System.Collections.Generic;
using System.Linq;
using ProcSense.Core.Actions;
using ProcSense.Core.Observation;
using ProcSense.VR.Bridge;
using UnityEngine;

namespace ProcSense.VR.Audio
{
    /// <summary>Where the rendered voice clips come from (by line id); null when a line has no audio.</summary>
    public interface IVoiceClips
    {
        AudioClip Find(string lineId);
    }

    /// <summary>The clips copied into the project by tools/voice/copy_to_unity.py (Resources/Voice/&lt;line id&gt;).</summary>
    public sealed class ResourceVoiceClips : IVoiceClips
    {
        public AudioClip Find(string lineId) => Resources.Load<AudioClip>("Voice/" + lineId);
    }

    /// <summary>
    /// Plays the voice lines Core's events call for (p0-voice-audio-s03): each from the speaking character's position (a spatial AudioSource from a small pool, at the speaker's head when the scene names it), each
    /// with its subtitle, one at a time from a queue so nobody talks over anybody. The interrupt policy: a patient line in progress is let finish its sentence; an urgent staff line (a Core event such as the lab's
    /// critical call) goes to the front of the queue and cuts a non-urgent staff line short. A line with no audio file is shown as a subtitle only, for as long as reading it takes, with a warning in the log, never
    /// silently dropped. Not placed in a scene yet: the speakers' head transforms come with the characters (p0-characters).
    /// </summary>
    public sealed class VoicePlayer : MonoBehaviour
    {
        public const int PoolSize = 4;
        public const float WordsPerSecond = 2.6f;

        public SubtitleView Subtitles;
        public IVoiceClips Clips = new ResourceVoiceClips();
        public readonly Dictionary<string, Transform> Heads = new Dictionary<string, Transform>(StringComparer.Ordinal);

        public AudioCatalog Catalog { get; private set; }
        public VoiceLine Current { get; private set; }
        public IReadOnlyList<VoiceLine> Queue => _queue;
        public IReadOnlyList<string> Played => _played;
        public IReadOnlyList<string> Missing => _missing;
        public event Action<VoiceLine> Started;

        private readonly List<VoiceLine> _queue = new List<VoiceLine>();
        private readonly List<string> _played = new List<string>();
        private readonly List<string> _missing = new List<string>();
        private readonly List<AudioSource> _pool = new List<AudioSource>();
        private AudioSource _playing;
        private float _left;
        private SimulationHost _host;
        private ObservableState _before;

        private void Awake()
        {
            for (int i = 0; i < PoolSize; i++)
            {
                var go = new GameObject("Voice source " + i);
                go.transform.SetParent(transform, false);
                var src = go.AddComponent<AudioSource>();
                src.playOnAwake = false;
                src.spatialBlend = 1f;
                src.rolloffMode = AudioRolloffMode.Linear;
                src.maxDistance = 12f;
                _pool.Add(src);
            }
            if (Subtitles == null) Subtitles = gameObject.AddComponent<SubtitleView>();
        }

        private void OnEnable() => Attach(SimulationHost.Instance);

        private void OnDisable() => Attach(null);

        /// <summary>Listens to this host's actions and state (null to stop).</summary>
        public void Attach(SimulationHost host)
        {
            if (_host != null) { _host.Applied -= OnApplied; _host.Changed -= OnChanged; }
            _host = host;
            if (_host == null) return;
            Catalog ??= AudioCatalog.Parse(ProcSense.VR.Content.UnityContentReader.ForThisPlatform().ReadText(AudioCatalog.File));
            _host.Applied += OnApplied;
            _host.Changed += OnChanged;
            _before = _host.Observable;
        }

        private string CaseId => _host?.Session?.Case.Id ?? "";

        private void OnApplied(SimAction a, ActionOutcome o)
        {
            foreach (var l in Catalog.For(CaseId, a, o)) Say(l);
        }

        private void OnChanged(ObservableState s)
        {
            foreach (var l in Catalog.ForState(CaseId, _before, s)) Say(l);
            _before = s;
        }

        /// <summary>Queues a line under the interrupt policy and starts it if nothing is playing.</summary>
        public void Say(VoiceLine line)
        {
            if (line.Urgent)
            {
                int at = _queue.FindIndex(q => !q.Urgent);
                _queue.Insert(at < 0 ? _queue.Count : at, line);
                if (Current != null && !Current.Urgent && Current.Speaker != "patient") Stop();     // the patient finishes the sentence; other staff talk is cut short
            }
            else _queue.Add(line);
            if (Current == null) Next();
        }

        /// <summary>Moves playback on by this much time (the update loop calls it with the frame time).</summary>
        public void Tick(float seconds)
        {
            Subtitles.Tick(seconds);
            if (Current == null) { Next(); return; }
            _left -= seconds;
            if (_left <= 0f) { Stop(); Next(); }
        }

        private void Update() => Tick(Time.deltaTime);

        private void Stop()
        {
            if (_playing != null) _playing.Stop();
            _playing = null;
            Current = null;
        }

        private void Next()
        {
            if (_queue.Count == 0) return;
            var line = _queue[0];
            _queue.RemoveAt(0);
            Current = line;
            _played.Add(line.Id);
            var clip = Clips?.Find(line.Id);
            float reading = Mathf.Max(SubtitleView.MinSeconds, line.Text.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries).Length / WordsPerSecond);
            if (clip != null)
            {
                _playing = _pool.FirstOrDefault(p => !p.isPlaying) ?? _pool[0];
                _playing.transform.position = Heads.TryGetValue(line.Speaker, out var head) && head != null ? head.position : transform.position;
                _playing.clip = clip;
                _playing.Play();
                _left = clip.length;
            }
            else
            {
                _missing.Add(line.Id);
                Debug.LogWarning("[ProcSense] voice line " + line.Id + " has no audio: subtitle only");
                _left = reading;
            }
            Subtitles.Show(line.Speaker, line.Text, Mathf.Max(_left, reading));
            Started?.Invoke(line);
        }
    }
}
