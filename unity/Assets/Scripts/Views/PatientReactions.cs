using UnityEngine;

namespace ProcSense.VR.Views
{
    /// <summary>
    /// Turns the patient's conversation state (<see cref="CharacterEventState"/>) into what the patient does: looks at the player while addressed, speaking or listening, nods when an action aimed at them is accepted, shakes the head when it is refused.
    /// Whatever hears the events (speech playback, SimFeedback once it exists) only calls <see cref="Address"/>, <see cref="StartSpeaking"/>, <see cref="EndSpeaking"/>, <see cref="Reply"/>; this decides nothing clinical.
    /// </summary>
    [DisallowMultipleComponent, RequireComponent(typeof(PatientMotion))]
    public sealed class PatientReactions : MonoBehaviour
    {
        [Tooltip("Who the patient looks at: the player's head. Empty: the main camera.")] public Transform player;
        public CharacterEventState State { get; } = new CharacterEventState();
        private PatientMotion motion;
        private bool bound;

        private ProcSense.VR.Audio.VoicePlayer voice;

        /// <summary>Follows this voice player: the patient is "speaking" while one of the patient's own lines plays, then listens. Safe to call every frame with the same player.</summary>
        public void Attach(ProcSense.VR.Audio.VoicePlayer player)
        {
            if (voice == player) return;
            if (voice != null) voice.Started -= OnLine;
            voice = player;
            if (voice != null) voice.Started += OnLine;
        }

        private void OnLine(ProcSense.VR.Audio.VoiceLine line) { if (line.Speaker == "patient") State.StartSpeaking(); }

        private void OnDestroy() { if (voice != null) voice.Started -= OnLine; }

        public void Address() => State.Address();
        public void StartSpeaking() => State.StartSpeaking();
        public void EndSpeaking() => State.EndSpeaking();
        public void Reply(bool accepted) => State.Reply(accepted);

        private void Awake() => Bind();

        /// <summary>Connects the state machine to the patient's motion (Awake does it; a test calls it directly).</summary>
        public void Bind()
        {
            if (bound) return;
            bound = true; motion = GetComponent<PatientMotion>(); State.Changed += OnChanged;
        }

        private Transform Target => player != null ? player : (Camera.main != null ? Camera.main.transform : null);

        private void OnChanged(CharacterState from, CharacterState to)
        {
            switch (to)
            {
                case CharacterState.Acknowledge: motion.Gesture(HeadGestureKind.Nod); break;
                case CharacterState.Refuse: motion.Gesture(HeadGestureKind.Shake); break;
                case CharacterState.Addressed: case CharacterState.Speaking: case CharacterState.Listening: motion.lookAt = Target; break;
                case CharacterState.Idle: case CharacterState.Busy: motion.lookAt = null; break;
            }
        }

        private void Update()
        {
            if (State.State == CharacterState.Speaking && (voice == null || voice.Current == null || voice.Current.Speaker != "patient")) State.EndSpeaking();     // the patient's line is over
            State.Step(Time.deltaTime);
        }
    }
}
