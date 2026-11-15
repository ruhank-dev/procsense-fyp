using ProcSense.VR.Audio;
using UnityEngine;

namespace ProcSense.VR.Voice
{
    /// <summary>
    /// The listening cues of push-to-talk (P3-SND-08): two rising tones when the microphone opens, two falling when the recording is sent, so the trainee knows without
    /// reading the panel. Made by tools/voice/render_feedback.py (ffmpeg tones, committed under Assets/Audio/VoiceFeedback/Resources/VoiceCues) and played through the UI
    /// group of the mixer. One cue per change of the recording state; a missing clip plays nothing.
    /// </summary>
    public static class VoiceCues
    {
        public const string Folder = "VoiceCues/", ListenStart = "listen_start", ListenStop = "listen_stop";

        /// <summary>The cue for the recording starting (true) or stopping (false).</summary>
        public static string For(bool recording) => recording ? ListenStart : ListenStop;

        public static AudioClip Load(string name) => Resources.Load<AudioClip>(Folder + name);

        /// <summary>A flat (2D) source for the cues on <paramref name="owner"/>, routed to the UI group.</summary>
        public static AudioSource MakeSource(GameObject owner)
        {
            var source = owner.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            AudioDirector.Route(source, MixGroup.UI);
            return source;
        }

        /// <summary>Plays the cue for the new recording state. Returns the clip played, or null when there is none.</summary>
        public static AudioClip Play(AudioSource source, bool recording)
        {
            var clip = Load(For(recording));
            if (clip != null && source != null) source.PlayOneShot(clip);
            return clip;
        }
    }
}
