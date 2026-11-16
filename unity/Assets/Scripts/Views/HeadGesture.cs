using UnityEngine;

namespace ProcSense.VR.Views
{
    public enum HeadGestureKind { None, Nod, Shake }

    /// <summary>
    /// Head gestures as an angle over time, for a nod (acknowledge, consent) and a head shake (refuse). Added on top of the head's pose by whatever holds the head bones; zero before the start and after <see cref="Seconds"/>.
    /// Nod is the chin dipping twice (negative nod, the same sign the mood poses use for chin down); shake is the head swinging both ways about the neck.
    /// </summary>
    public static class HeadGesture
    {
        public const float Seconds = .8f, NodDegrees = 14f, ShakeDegrees = 22f;

        /// <summary>Degrees of nod (about the lateral axis) and yaw (about the neck) at <paramref name="t"/> seconds into the gesture.</summary>
        public static Vector2 At(HeadGestureKind kind, float t)
        {
            if (kind == HeadGestureKind.None || t < 0f || t > Seconds) return Vector2.zero;
            float u = t / Seconds, fade = Mathf.Sin(Mathf.PI * u);                         // eases in and out so it never snaps
            switch (kind)
            {
                case HeadGestureKind.Nod: { float dip = Mathf.Sin(2f * Mathf.PI * u); return new Vector2(-NodDegrees * dip * dip * fade, 0f); }
                case HeadGestureKind.Shake: return new Vector2(0f, ShakeDegrees * Mathf.Sin(4f * Mathf.PI * u) * fade);
                default: return Vector2.zero;
            }
        }
    }
}
