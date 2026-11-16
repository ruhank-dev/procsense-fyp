using NUnit.Framework;
using ProcSense.VR.Views;
using UnityEngine;

namespace ProcSense.Tests.EditMode
{
    /// <summary>P3-ANI-04: acknowledge nods, refuse shakes, addressed turns the head to the trainee, each ends by itself, and with no Animator the head returns exactly to its pose.</summary>
    public class CharacterReactionsTests
    {
        private GameObject _person, _eyes;
        private Transform _head;

        [SetUp]
        public void SetUp()
        {
            _person = new GameObject("nurse");
            _head = new GameObject("head").transform;
            _head.SetParent(_person.transform, false);
            _head.localPosition = new Vector3(0, 1.6f, 0);
            _eyes = new GameObject("eyes");
            _eyes.transform.position = new Vector3(2, 1.6f, 0);   // the trainee stands to the nurse's right
        }

        [TearDown] public void TearDown() { Object.DestroyImmediate(_person); Object.DestroyImmediate(_eyes); }

        [Test]
        public void A_nod_a_shake_and_a_turn_each_end_and_leave_the_pose_as_it_was()
        {
            var r = CharacterReactions.For(_person.transform);
            Assert.IsNotNull(r);
            Assert.AreSame(_head, r.Head);
            r.Target = _eyes.transform;
            var rest = _head.localRotation;

            r.Trigger(CharacterReactions.State.Acknowledge);
            float maxPitch = 0, maxYaw = 0;
            for (float t = 0; t < CharacterReactions.NodSeconds + .2f; t += .02f) { r.Step(.02f); maxPitch = Mathf.Max(maxPitch, Mathf.Abs(r.Offset.x)); maxYaw = Mathf.Max(maxYaw, Mathf.Abs(r.Offset.y)); }
            Assert.Greater(maxPitch, 5f, "a nod moves the head up and down");
            Assert.Less(maxYaw, .01f);
            Assert.AreEqual(CharacterReactions.State.None, r.Current, "and ends by itself");
            r.Step(.02f);
            Assert.Less(Quaternion.Angle(rest, _head.localRotation), .01f, "no drift without an Animator");

            r.Trigger(CharacterReactions.State.Refuse);
            maxYaw = 0;
            for (float t = 0; t < CharacterReactions.ShakeSeconds / 2; t += .02f) { r.Step(.02f); maxYaw = Mathf.Max(maxYaw, Mathf.Abs(r.Offset.y)); }
            Assert.Greater(maxYaw, 6f, "a refusal shakes the head");

            r.Trigger(CharacterReactions.State.Addressed, 2f);
            for (int i = 0; i < 30; i++) r.Step(.02f);
            Assert.Greater(r.Offset.y, 15f, "turns toward the trainee on the right");
            for (int i = 0; i < 120; i++) r.Step(.02f);
            Assert.AreEqual(CharacterReactions.State.None, r.Current);
            Assert.Less(Quaternion.Angle(rest, _head.localRotation), .01f);
        }

        [Test]
        public void Something_without_a_head_has_no_reactions()
        {
            var monitor = new GameObject("monitor");
            try { Assert.IsNull(CharacterReactions.For(monitor.transform)); }
            finally { Object.DestroyImmediate(monitor); }
        }
    }
}
