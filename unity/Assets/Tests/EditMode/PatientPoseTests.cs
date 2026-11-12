using System.Linq;
using NUnit.Framework;
using ProcSense.VR.Views;

namespace ProcSense.Tests.EditMode
{
    /// <summary>Every mood has a pose beside its face, and the pose never moves the breathing rate (that follows the measured respiratory rate) (p0-characters-s03, s05).</summary>
    public class PatientPoseTests
    {
        [Test]
        public void Every_mode_with_a_face_has_a_pose_and_the_reverse()
        {
            CollectionAssert.AreEquivalent(PatientExpression.Modes.ToList(), PatientPose.Modes.ToList());
        }

        [Test]
        public void An_empty_or_unknown_mode_is_a_calm_pose()
        {
            foreach (var mode in new[] { null, "", "NOT_A_MODE" })
            {
                var p = PatientPose.For(mode);
                Assert.AreEqual(0f, p.Guard); Assert.AreEqual(0f, p.Yaw); Assert.AreEqual(1f, p.Depth); Assert.AreEqual(1f, p.Restless);
            }
        }

        [Test]
        public void Pose_values_stay_in_range()
        {
            foreach (var mode in PatientPose.Modes)
            {
                var p = PatientPose.For(mode);
                Assert.That(p.Guard, Is.InRange(0f, 1f), mode);
                Assert.That(p.Depth, Is.InRange(.3f, 1.5f), mode);
                Assert.That(p.Restless, Is.InRange(0f, 3f), mode);
                Assert.That(p.Dart, Is.InRange(0f, 1f), mode);
                Assert.That(System.Math.Abs(p.Yaw), Is.LessThanOrEqualTo(40f), mode);
                Assert.That(System.Math.Abs(p.Nod), Is.LessThanOrEqualTo(30f), mode);
                Assert.That(System.Math.Abs(p.Tilt), Is.LessThanOrEqualTo(30f), mode);
            }
        }

        [Test]
        public void Pain_guards_the_abdomen_and_a_blend_runs_between_two_poses()
        {
            Assert.AreEqual(1f, PatientPose.For("IN_PAIN").Guard);
            var half = PatientPose.Lerp(PatientPose.Calm, PatientPose.For("IN_PAIN"), .5f);
            Assert.AreEqual(.5f, half.Guard, 1e-5f);
            Assert.AreEqual(PatientPose.For("IN_PAIN").Yaw * .5f, half.Yaw, 1e-5f);
        }
    }
}
