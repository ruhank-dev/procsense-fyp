using UnityEngine;

namespace ProcSense.VR.Views
{
    /// <summary>
    /// P3-ANI-04: the event states of a person, layered over whatever the Animator plays: addressed and listening turn the head toward the trainee, acknowledge nods, refuse shakes the head,
    /// speaking adds a small talking bob, busy looks down at the work. Procedural rotations of the head bone in LateUpdate (after the Animator), so no new clip or controller is needed and the
    /// body keeps its idle. Driven by the contextual panel (opened on the person: addressed; an action for them accepted or refused), push-to-talk (listening) and voice playback (speaking).
    /// Presentation only: it reads nothing clinical and sends nothing.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CharacterReactions : MonoBehaviour
    {
        public enum State { None, Addressed, Speaking, Listening, Acknowledge, Refuse, Busy }

        public const float NodSeconds = .8f, ShakeSeconds = .9f, LookWeight = .6f;

        public State Current { get; private set; }
        public float Remaining { get; private set; }
        public Transform Head { get; private set; }
        /// <summary>The trainee's eyes (the main camera unless set).</summary>
        public Transform Target;
        /// <summary>The head's offset from its animated pose this frame (degrees: pitch, yaw), for tests.</summary>
        public Vector2 Offset { get; private set; }

        private float _elapsed;
        private Quaternion? _applied;
        private Animator _animator; private bool _animatorLooked;
        private bool Animated { get { if (!_animatorLooked) { _animator = GetComponentInChildren<Animator>(); _animatorLooked = true; } return _animator != null && _animator.isActiveAndEnabled && _animator.runtimeAnimatorController != null; } }

        /// <summary>The reactions of a person (added on first use), or null for something without a head.</summary>
        public static CharacterReactions For(Component person)
        {
            if (person == null) return null;
            var r = person.GetComponent<CharacterReactions>();
            if (r != null) return r;
            var head = FindHead(person.transform);
            if (head == null) return null;
            r = person.gameObject.AddComponent<CharacterReactions>();
            r.Head = head;
            return r;
        }

        private static Transform FindHead(Transform root)
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                if (t.name == "head" || t.name == "Bip01 Head" || t.name == "Head" || t.name.EndsWith(" head")) return t;
            return null;
        }

        /// <summary>Starts a state for some seconds (one-shot states use their own length; the continuous ones last until replaced or for the time given).</summary>
        public void Trigger(State state, float seconds = 2f)
        {
            Current = state;
            _elapsed = 0f;
            Remaining = state == State.Acknowledge ? NodSeconds : state == State.Refuse ? ShakeSeconds : seconds;
        }

        /// <summary>The offset (pitch, yaw in degrees) of a state at a time into it; pure, for tests and LateUpdate.</summary>
        public static Vector2 OffsetOf(State state, float t, float length)
        {
            float fade = Mathf.Clamp01(Mathf.Min(t / .15f, (length - t) / .2f));
            switch (state)
            {
                case State.Acknowledge: return new Vector2(Mathf.Sin(t / NodSeconds * Mathf.PI * 4f) * 9f * fade, 0f);          // two nods
                case State.Refuse: return new Vector2(0f, Mathf.Sin(t / ShakeSeconds * Mathf.PI * 6f) * 12f * fade);             // three shakes
                case State.Speaking: return new Vector2(Mathf.Sin(t * 9f) * 1.5f * fade, Mathf.Sin(t * 2.3f) * 2f * fade);
                case State.Busy: return new Vector2(14f * fade, 0f);                                                           // looking down at the work
                default: return Vector2.zero;
            }
        }

        public void Step(float dt)
        {
            // with no Animator resetting the pose each frame, last frame's offset is taken off first so it never accumulates
            if (_applied.HasValue && Head != null && !Animated) Head.localRotation = _applied.Value;
            _applied = null;
            if (Current == State.None || Head == null) { Offset = Vector2.zero; return; }
            _elapsed += dt;
            Remaining -= dt;
            if (Remaining <= 0f) { Current = State.None; Offset = Vector2.zero; return; }
            float length = _elapsed + Remaining;
            Offset = OffsetOf(Current, _elapsed, length);
            // every rotation is about world axes taken from the body (up, and the body's right), so it works whatever axes the rig's head bone has
            var target = Target != null ? Target : (Camera.main != null ? Camera.main.transform : null);
            float yaw = Offset.y;
            if ((Current == State.Addressed || Current == State.Listening) && target != null)
            {
                var to = Vector3.ProjectOnPlane(target.position - Head.position, Vector3.up);
                float fade = Mathf.Clamp01(Mathf.Min(_elapsed / .25f, Remaining / .3f));
                if (to.sqrMagnitude > 1e-4f) yaw += Mathf.Clamp(Vector3.SignedAngle(Vector3.ProjectOnPlane(transform.forward, Vector3.up), to, Vector3.up), -50f, 50f) * LookWeight * fade;
                Offset = new Vector2(Offset.x, yaw);
            }
            _applied = Head.localRotation;
            Head.rotation = Quaternion.AngleAxis(yaw, Vector3.up) * Quaternion.AngleAxis(Offset.x, transform.right) * Head.rotation;
        }

        private void LateUpdate() => Step(Time.deltaTime);
    }
}
