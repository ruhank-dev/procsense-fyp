using UnityEngine;

namespace ProcSense.VR.Audio
{
    /// <summary>
    /// P3-SND-06/07: a clip that loops, near the thing that makes it, only while that thing moves (footsteps for a walking person, castors for a trolley). It reads the speed from the object's own movement,
    /// so it needs nothing from the animation or the agent. Silent when the clip is missing (batch tests have no audio device).
    /// </summary>
    public sealed class MovingSound : MonoBehaviour
    {
        public const float WalkSpeed = .15f;
        [SerializeField] private string clipId = ""; [SerializeField] private float volume = .5f;
        private AudioSource source; private Vector3 last; private bool started;

        /// <summary>Is something moving at this speed (metres per second) fast enough to be heard? Public for tests.</summary>
        public static bool Audible(float speed) => speed >= WalkSpeed;

        public static MovingSound Attach(GameObject on, string clipId, float volume = .5f)
        {
            if (on == null) return null;
            var m = on.GetComponent<MovingSound>() ?? on.AddComponent<MovingSound>();
            m.clipId = clipId; m.volume = volume; return m;
        }

        private void Start()
        {
            last = transform.position;
            var clip = SoundPlayer.Clip(clipId); if (clip == null) return;
            source = gameObject.AddComponent<AudioSource>();
            source.clip = clip; source.loop = true; source.playOnAwake = false; source.spatialBlend = 1f; source.minDistance = 1f; source.maxDistance = 12f; source.volume = volume;
            AudioDirector.Route(source, MixGroup.Effects);
            started = true;
        }

        private void Update()
        {
            if (!started) return;
            float dt = Time.deltaTime; if (dt <= 0f) return;
            float speed = (transform.position - last).magnitude / dt; last = transform.position;
            if (Audible(speed)) { if (!source.isPlaying) source.Play(); } else if (source.isPlaying) source.Pause();
        }
    }
}
