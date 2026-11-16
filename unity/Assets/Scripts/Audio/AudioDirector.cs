using System.Collections.Generic;
using ProcSense.VR.Bridge;
using ProcSense.VR.Flow;
using ProcSense.VR.Settings;
using UnityEngine;
using UnityEngine.Audio;

namespace ProcSense.VR.Audio
{
    /// <summary>The mixer groups of Assets/Audio/ProcSense.mixer (P3-SND-01). Each has an exposed volume named "&lt;Group&gt;Volume".</summary>
    public enum MixGroup { Master, Voice, Music, Effects, Ambience, UI }

    /// <summary>What the mix follows: the menus (no session), a session running, a session paused, the results. Worked out from MenuFlow's phase and SimulationHost.Paused.</summary>
    public enum MixState { Menu, Play, Paused, Results }

    /// <summary>
    /// The one place that sets the mix (P3-SND-01). Each group's level is the player's volume setting (P3-SET-03 sliders write GameSettings) plus the
    /// offset of the current flow state (music only on the menu and the results, the room quieter while paused, the results screen calm) plus ducking
    /// of the room and music while someone speaks, so a voice is never masked. It also moves to the snapshot named after the state (Menu, Play, Paused,
    /// Results) so effects can be added to snapshots later. The levels are worked out in <see cref="Level"/> without any audio, so they are testable;
    /// in Unity an exposed group volume set from code no longer follows the snapshots, which is why the director owns it.
    /// </summary>
    public sealed class AudioDirector : MonoBehaviour
    {
        public const string MixerResource = "ProcSenseMixer";
        public const float Silent = -80f, DuckDb = -10f, DuckSeconds = .25f, ListeningDb = -8f;

        public static AudioDirector Instance { get; private set; }

        public AudioMixer Mixer;
        /// <summary>True while a voice line plays (VoicePlayer and the team voices tell the director).</summary>
        public bool Speaking { get; private set; }
        /// <summary>True while the trainee's microphone is open (push-to-talk): the room ducks so the recogniser hears the trainee (P3-SND-10).</summary>
        public bool Listening { get; private set; }
        public string Snapshot { get; private set; } = "";

        private float _duck;   // 0 to 1, eased
        private int _speakers;

        /// <summary>The level in decibels of one group, for a volume setting (0 to 1), a mix state and a duck amount (0 to 1).</summary>
        public static float Level(MixGroup group, float setting, MixState state, float duck, bool listening = false)
        {
            float db = ToDb(setting) + Offset(group, state);
            if (group == MixGroup.Ambience || group == MixGroup.Music) db += DuckDb * Mathf.Clamp01(duck);
            if (listening && group == MixGroup.Ambience) db += ListeningDb;   // the plan's Listening snapshot: ambience 8 dB down while the microphone is open
            return Mathf.Max(Silent, db);
        }

        /// <summary>The state's part of the mix: music only where there is no session running, a quieter room while paused, a calm results screen.</summary>
        public static float Offset(MixGroup group, MixState state)
        {
            switch (group)
            {
                case MixGroup.Music: return state == MixState.Menu || state == MixState.Results ? 0f : Silent;
                case MixGroup.Ambience: return state == MixState.Paused ? -12f : state == MixState.Results ? -18f : state == MixState.Menu ? Silent : 0f;
                case MixGroup.Voice: return state == MixState.Paused ? Silent : 0f;     // nobody talks over the pause menu
                case MixGroup.Effects: return state == MixState.Paused ? -12f : 0f;
                default: return 0f;
            }
        }

        /// <summary>The mix state of a menu phase (null: no menu flow, the fixed start-up case of the Editor) and whether the session is paused.</summary>
        public static MixState StateFor(MenuPhase? phase, bool paused)
        {
            if (phase == null) return paused ? MixState.Paused : MixState.Play;
            switch (phase.Value)
            {
                case MenuPhase.Briefing: case MenuPhase.Playing: return paused ? MixState.Paused : MixState.Play;
                case MenuPhase.Results: return MixState.Results;
                default: return MixState.Menu;   // Login, Consent, MainMenu, Surgery
            }
        }

        public static string SnapshotFor(MixState state) => state.ToString();

        public static float ToDb(float linear) => linear <= .0001f ? Silent : Mathf.Max(Silent, 20f * Mathf.Log10(linear));

        public static float SettingFor(MixGroup g, SettingsData s) => g switch
        {
            MixGroup.Master => s.masterVolume, MixGroup.Voice => s.voiceVolume, MixGroup.Music => s.musicVolume,
            MixGroup.Effects => s.effectsVolume, MixGroup.Ambience => s.ambienceVolume, _ => s.uiVolume,
        };

        /// <summary>Sends a source through one group of the mixer, so the player's volume for that group and the mix state apply to it. Without the director or the mixer the source plays unmixed.</summary>
        public static void Route(AudioSource source, MixGroup group)
        {
            var mixer = Instance != null ? Instance.Mixer : null;
            if (source == null || mixer == null) return;
            foreach (var g in mixer.FindMatchingGroups(group.ToString()))
                if (g.name == group.ToString()) { source.outputAudioMixerGroup = g; return; }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Make()
        {
            if (Instance != null) return;
            var mixer = Resources.Load<AudioMixer>(MixerResource);
            if (mixer == null) return;   // a build without the mixer plays unmixed, as before
            var go = new GameObject("Audio director");
            DontDestroyOnLoad(go);
            go.AddComponent<AudioDirector>().Mixer = mixer;
        }

        private void Awake() { if (Instance != null && Instance != this) { Destroy(this); return; } Instance = this; }
        private void OnDestroy() { if (Instance == this) Instance = null; }

        /// <summary>The microphone opens or closes (PushToTalkView tells the director).</summary>
        public void Listen(bool on) => Listening = on;

        /// <summary>A voice line starts or stops (counted, so two speakers overlapping keep the duck).</summary>
        public void Speak(bool on)
        {
            _speakers = Mathf.Max(0, _speakers + (on ? 1 : -1));
            Speaking = _speakers > 0;
        }

        private void Update()
        {
            _duck = Mathf.MoveTowards(_duck, Speaking ? 1f : 0f, Time.unscaledDeltaTime / DuckSeconds);
            var host = SimulationHost.Instance;
            bool paused = host != null && (host.Paused || (host.Session != null && host.Session.Paused));   // the pause menu pauses the engine (P3-MNU-08); a Core fault pauses the host
            Apply(StateFor(MenuFlow.Current != null ? MenuFlow.Current.Phase : (MenuPhase?)null, paused));
        }

        /// <summary>Sets every group for the state and moves to its snapshot (only when it changes).</summary>
        public void Apply(MixState state)
        {
            if (Mixer == null) return;
            var s = GameSettings.Current;
            foreach (MixGroup g in System.Enum.GetValues(typeof(MixGroup)))
                Mixer.SetFloat(g + "Volume", Level(g, SettingFor(g, s), state, _duck, Listening));
            var snap = SnapshotFor(state);
            if (snap == Snapshot) return;
            var target = Mixer.FindSnapshot(snap);
            if (target != null) target.TransitionTo(.5f);
            Snapshot = snap;
        }
    }
}
