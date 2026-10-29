using ProcSense.VR.Bridge;
using UnityEngine;

namespace ProcSense.VR.Audio
{
    /// <summary>
    /// The room's background sound and the lab phone's ring (p0-voice-audio-s05). The bed follows where the trainee is (Core's trainee location): the ward bay and corridor,
    /// the imaging rooms, theatre. It ducks under a high-priority alarm so the alarm is never masked. The phone rings while Core says it is ringing. One instance for the
    /// whole run, made at start, so no scene needs editing; it reads state and applies nothing.
    /// </summary>
    public sealed class RoomAudio : MonoBehaviour
    {
        public const float BedVolume = .18f, DuckedVolume = .05f;

        public static RoomAudio Instance { get; private set; }
        public AudioSource Bed { get; private set; }
        public AudioSource Ring { get; private set; }
        public string Room { get; private set; } = "";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Make()
        {
            if (Instance != null) return;
            var go = new GameObject("Room audio");
            DontDestroyOnLoad(go);
            go.AddComponent<RoomAudio>();
        }

        /// <summary>The bed for a Core location id.</summary>
        public static string RoomFor(string location) => location switch
        {
            "theatre" or "theatre_reception" => "theatre",
            "ultrasound" or "xray" or "ct" or "mri" => "imaging",
            "" or null => "",
            _ => "ward",
        };

        private void Awake()
        {
            Instance = this;
            Bed = gameObject.AddComponent<AudioSource>();
            Bed.loop = true; Bed.playOnAwake = false; Bed.spatialBlend = 0f; Bed.volume = BedVolume;
            Ring = gameObject.AddComponent<AudioSource>();
            Ring.loop = true; Ring.playOnAwake = false; Ring.spatialBlend = 0f; Ring.volume = .5f;
        }

        private void OnDestroy() { if (Instance == this) Instance = null; }

        private void Update()
        {
            var state = SimulationHost.Instance?.Observable;
            string room = state == null ? "" : RoomFor(string.IsNullOrEmpty(state.TraineeLocation) ? state.Location : state.TraineeLocation);
            if (room != Room)
            {
                Room = room;
                if (room.Length == 0) Bed.Stop();
                else { Bed.clip = ToneBank.Room(room); Bed.Play(); }
            }
            float target = AlarmAudioView.HighSounding ? DuckedVolume : BedVolume;
            Bed.volume = Mathf.MoveTowards(Bed.volume, target, Time.unscaledDeltaTime * .5f);

            bool ringing = state?.Phone?.State == "RINGING";
            if (ringing && !Ring.isPlaying) { Ring.clip = ToneBank.Phone(); Ring.Play(); }
            else if (!ringing && Ring.isPlaying) Ring.Stop();
        }
    }
}
