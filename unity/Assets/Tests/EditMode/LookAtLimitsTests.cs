using NUnit.Framework;
using ProcSense.VR.Views;
using UnityEngine;

namespace ProcSense.Tests.EditMode
{
    /// <summary>Look-at limits: 60 degrees of yaw and 30 of pitch, eyes first and the head only for the rest (P3-ANI-03).</summary>
    public class LookAtLimitsTests
    {
        [Test]
        public void A_small_glance_moves_only_the_eyes()
        {
            var s = LookAtLimits.Share(10f, -8f);
            Assert.AreEqual(10f, s.EyeYaw, 1e-4f); Assert.AreEqual(-8f, s.EyePitch, 1e-4f);
            Assert.AreEqual(0f, s.HeadYaw, 1e-4f); Assert.AreEqual(0f, s.HeadPitch, 1e-4f);
        }

        [Test]
        public void A_big_turn_fills_the_eyes_first_then_the_head_takes_the_rest()
        {
            var s = LookAtLimits.Share(50f, 28f);
            Assert.AreEqual(LookAtLimits.EyeYaw, s.EyeYaw, 1e-4f); Assert.AreEqual(25f, s.HeadYaw, 1e-4f);
            Assert.AreEqual(LookAtLimits.EyePitch, s.EyePitch, 1e-4f); Assert.AreEqual(13f, s.HeadPitch, 1e-4f);
        }

        [Test]
        public void Nothing_goes_past_the_limits_in_either_direction()
        {
            foreach (var sign in new[] { 1f, -1f })
            {
                var s = LookAtLimits.Share(sign * 170f, sign * 90f);
                Assert.AreEqual(LookAtLimits.MaxYaw, Mathf.Abs(s.EyeYaw + s.HeadYaw), 1e-4f);
                Assert.AreEqual(LookAtLimits.MaxPitch, Mathf.Abs(s.EyePitch + s.HeadPitch), 1e-4f);
            }
        }

        [Test]
        public void Angles_measure_the_turn_to_a_target()
        {
            var a = LookAtLimits.Angles(Vector3.forward, Vector3.up, Vector3.right, Quaternion.Euler(0, 30, 0) * Vector3.forward);
            Assert.AreEqual(30f, a.x, 1e-3f); Assert.AreEqual(0f, a.y, 1e-3f);
            var b = LookAtLimits.Angles(Vector3.forward, Vector3.up, Vector3.right, Quaternion.Euler(-20, 0, 0) * Vector3.forward);
            Assert.AreEqual(0f, b.x, 1e-3f); Assert.AreEqual(-20f, b.y, 1e-3f);
            Assert.AreEqual(Vector2.zero, LookAtLimits.Angles(Vector3.forward, Vector3.up, Vector3.right, Vector3.zero));
        }
    }
}
