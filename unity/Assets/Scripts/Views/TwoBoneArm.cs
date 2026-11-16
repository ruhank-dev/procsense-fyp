using UnityEngine;

namespace ProcSense.VR.Views
{
    /// <summary>Two-bone reach for a humanoid arm: moves the hand bone to a point by bending the elbow towards a pole direction (law of cosines), keeping the bone lengths.</summary>
    public static class TwoBoneArm
    {
        /// <summary>Reaches <paramref name="hand"/> to <paramref name="target"/>. <paramref name="poleHint"/> says which way the elbow bends (projected off the reach line). <paramref name="weight"/> 0..1 blends from the current pose.</summary>
        public static void Reach(Transform upper, Transform fore, Transform hand, Vector3 target, Vector3 poleHint, float weight)
        {
            if (weight <= 0f) return;
            var s = upper.position; var e0 = fore.position; var h0 = hand.position;
            float a = (e0 - s).magnitude, b = (h0 - e0).magnitude;
            var line = target - s; float d = Mathf.Clamp(line.magnitude, Mathf.Abs(a - b) + .01f, a + b - .01f); var u = line.normalized;
            var pole = Vector3.ProjectOnPlane(poleHint, u).normalized;
            float x = (a * a - b * b + d * d) / (2f * d), h = Mathf.Sqrt(Mathf.Max(0f, a * a - x * x));
            var elbow = s + u * x + pole * h;
            upper.rotation = Quaternion.Slerp(Quaternion.identity, Quaternion.FromToRotation(e0 - s, elbow - s), weight) * upper.rotation;
            var e1 = fore.position; var h1 = hand.position;
            fore.rotation = Quaternion.Slerp(Quaternion.identity, Quaternion.FromToRotation(h1 - e1, s + u * d - e1), weight) * fore.rotation;
        }
    }
}
