using UnityEngine;

namespace ProcSense.VR.Views
{
    /// <summary>
    /// How a character turns to look at someone (P3-ANI-03): the angle to the target is limited to 60 degrees of yaw and 30 of pitch, the eyes take the first part of it (up to 25 and 15 degrees) and the head only the rest,
    /// so a small glance moves only the eyes and a big turn brings the head round. Pure maths, no scene needed.
    /// </summary>
    public static class LookAtLimits
    {
        public const float MaxYaw = 60f, MaxPitch = 30f, EyeYaw = 25f, EyePitch = 15f;

        public readonly struct Split
        {
            public readonly float EyeYaw, EyePitch, HeadYaw, HeadPitch;
            public Split(float eyeYaw, float eyePitch, float headYaw, float headPitch) { EyeYaw = eyeYaw; EyePitch = eyePitch; HeadYaw = headYaw; HeadPitch = headPitch; }
        }

        /// <summary>Limits the wanted yaw and pitch (degrees, either sign) and shares them between the eyes and the head.</summary>
        public static Split Share(float yaw, float pitch)
        {
            yaw = Mathf.Clamp(yaw, -MaxYaw, MaxYaw); pitch = Mathf.Clamp(pitch, -MaxPitch, MaxPitch);
            float eyeYaw = Mathf.Clamp(yaw, -EyeYaw, EyeYaw), eyePitch = Mathf.Clamp(pitch, -EyePitch, EyePitch);
            return new Split(eyeYaw, eyePitch, yaw - eyeYaw, pitch - eyePitch);
        }

        /// <summary>The yaw (about <paramref name="up"/>) and pitch (about <paramref name="right"/>) in degrees from <paramref name="forward"/> to the direction <paramref name="toTarget"/>.</summary>
        public static Vector2 Angles(Vector3 forward, Vector3 up, Vector3 right, Vector3 toTarget)
        {
            if (toTarget.sqrMagnitude < 1e-8f) return Vector2.zero;
            float yaw = Vector3.SignedAngle(forward, Vector3.ProjectOnPlane(toTarget, up), up);
            float pitch = Vector3.SignedAngle(forward, Vector3.ProjectOnPlane(toTarget, right), right);
            return new Vector2(yaw, pitch);
        }
    }
}
