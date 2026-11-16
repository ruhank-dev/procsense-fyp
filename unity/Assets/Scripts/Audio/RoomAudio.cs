using ProcSense.VR.Bridge;
using UnityEngine;

namespace ProcSense.VR.Audio
{
    /// <summary>
    /// The room's background sound and the lab phone's ring (p0-voice-audio-s05; ambience beds and crossfade P3-SND-03). The bed follows where the trainee is (Core's trainee location): the patient
    /// bay (the ward bed, or the emergency department's when the case is set there), the corridor, theatre, and the imaging rooms. A change of room crossfades the old bed into the new one over
    /// <see cref="CrossfadeSeconds"/>, so walking through a door never cuts the sound. The beds are the clips of P3-SND-02 (Resources/Sfx/amb_*), with the older code synthesis (ToneBank) where no
    /// clip exists. They play through the mixer's Ambience group, and duck under a high-priority alarm so the alarm is never masked. The phone rings while Core says it is ringing. One instance for
    /// the whole run, made at start, so no scene needs editing; it reads state and applies nothing.
    /// </summary>
    public sealed class RoomAudio : MonoBehaviour
    {
        public const float BedVolume = .18f, DuckedVolume = .05f, CrossfadeSeconds = 1.5f;

        public static RoomAudio Instance { get; private set; }
        /// <summary>The bed playing now (the one fading in during a crossfade).</summary>
        public AudioSource Bed => _beds[_current];
        /// <summary>The bed fading out during a crossfade (silent otherwise).</summary>
        public AudioSource Outgoing => _beds[1 - _current];
        public AudioSource Ring { get; private set; }
        public string Room { get; private set; } = "";
        /// <summary>0 at the start of a crossfade, 1 when it is over.</summary>
        public float Fade { get; private set; } = 1f;

        /// <summary>The case's setting from the briefing ("ward", "pre-assessment", "emergency department"); the game flow sets it, tests replace it.</summary>
        public static System.Func<string> CaseSetting = () => Object.FindAnyObjectByType<ProcSense.VR.Flow.MenuFlowInstaller>()?.Flow?.Briefing?.Setting ?? "";

        private readonly AudioSource[] _beds = new AudioSource[2];
        private int _current;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Make()
        {
            if (Instance != null) return;
            var go = new GameObject("Room audio");
            DontDestroyOnLoad(go);
            go.AddComponent<RoomAudio>();
        }

        /// <summary>The bed for a Core location id and the case's setting.</summary>
        public static string RoomFor(string location, string setting = "") => location switch
        {
            "theatre" or "theatre_reception" => "ot",
            "ultrasound" or "xray" or "ct" or "mri" => "imaging",
            "corridor" => "corridor",
            "" or null => "",
            _ => setting == "emergency department" ? "ed" : "ward",
        };

        /// <summary>The clip of a bed: the P3-SND-02 recording-style clip when there is one, else the older synthesis.</summary>
        public static AudioClip ClipFor(string room)
        {
            var clip = Resources.Load<AudioClip>("Sfx/amb_" + room);
            if (clip != null) return clip;
            return ToneBank.Room(room == "ot" ? "theatre" : room == "imaging" ? "imaging" : "ward");
        }

        private void Awake()
        {
            Instance = this;
            for (int i = 0; i < 2; i++)
            {
                var bed = gameObject.AddComponent<AudioSource>();
                bed.loop = true; bed.playOnAwake = false; bed.spatialBlend = 0f; bed.volume = 0f;
                AudioDirector.Route(bed, MixGroup.Ambience);
                _beds[i] = bed;
            }
            Ring = gameObject.AddComponent<AudioSource>();
            Ring.loop = true; Ring.playOnAwake = false; Ring.spatialBlend = 0f; Ring.volume = .5f;
            AudioDirector.Route(Ring, MixGroup.Effects);
        }

        private void OnDestroy() { if (Instance == this) Instance = null; }

        /// <summary>Moves to a room's bed: the current bed becomes the outgoing one and the new bed fades in over the crossfade.</summary>
        public void Enter(string room)
        {
            if (room == Room) return;
            Room = room;
            _current = 1 - _current;
            var bed = Bed;
            bed.Stop();
            bed.volume = 0f;
            if (room.Length > 0) { bed.clip = ClipFor(room); bed.time = 0f; bed.Play(); }
            Fade = 0f;
        }

        /// <summary>Advances the crossfade and the alarm duck by dt seconds (Update calls it; tests call it directly).</summary>
        public void Step(float dt)
        {
            float level = AlarmAudioView.HighSounding ? DuckedVolume : BedVolume;
            Fade = Mathf.Min(1f, Fade + dt / CrossfadeSeconds);
            Bed.volume = Room.Length == 0 ? 0f : level * Fade;
            Outgoing.volume = level * (1f - Fade);
            if (Fade >= 1f && Outgoing.isPlaying) Outgoing.Stop();
        }

        private void Update()
        {
            var state = SimulationHost.Instance?.Observable;
            Enter(state == null ? "" : RoomFor(string.IsNullOrEmpty(state.TraineeLocation) ? state.Location : state.TraineeLocation, CaseSetting?.Invoke() ?? ""));
            Step(Time.unscaledDeltaTime);

            bool ringing = state?.Phone?.State == "RINGING";
            if (ringing && !Ring.isPlaying) { Ring.clip = ToneBank.Phone(); Ring.Play(); }
            else if (!ringing && Ring.isPlaying) Ring.Stop();
        }
    }
}
