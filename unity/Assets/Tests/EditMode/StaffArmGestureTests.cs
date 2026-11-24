using NUnit.Framework;
using ProcSense.VR.Views;

namespace ProcSense.Tests.EditMode
{
    public class StaffArmGestureTests
    {
        [Test]
        public void The_arm_eases_in_holds_and_eases_out()
        {
            Assert.AreEqual(0f, StaffArmGesture.Weight(0f, 3f), 1e-4f);
            Assert.AreEqual(1f, StaffArmGesture.Weight(1.5f, 3f), 1e-4f);
            Assert.AreEqual(0f, StaffArmGesture.Weight(3f, 3f), 1e-4f);
            Assert.Greater(StaffArmGesture.Weight(.3f, 3f), 0f);
            Assert.Less(StaffArmGesture.Weight(.3f, 3f), 1f);
        }

        [Test]
        public void A_gesture_without_a_skeleton_does_nothing_and_does_not_throw()
        {
            var go = new UnityEngine.GameObject("nobody");
            try { var g = go.AddComponent<StaffArmGesture>(); g.HandOver(); Assert.AreEqual(StaffArmGesture.Kind.HandOver, g.Current); }
            finally { UnityEngine.Object.DestroyImmediate(go); }
        }

        [Test]
        public void Only_the_accepted_action_it_is_set_for_starts_a_gesture()
        {
            var go = new UnityEngine.GameObject("writer");
            try
            {
                var g = go.AddComponent<StaffArmGesture>(); g.writeOn = "escalate";
                var accepted = new ProcSense.Core.Actions.ActionOutcome(); var refused = new ProcSense.Core.Actions.ActionOutcome(); refused.Reject("NO");
                g.OnApplied(new ProcSense.Core.Actions.SimAction("examine"), accepted); Assert.IsFalse(g.Active);
                g.OnApplied(new ProcSense.Core.Actions.SimAction("escalate"), refused); Assert.IsFalse(g.Active);
                g.OnApplied(new ProcSense.Core.Actions.SimAction("escalate"), accepted); Assert.AreEqual(StaffArmGesture.Kind.Write, g.Current);
            }
            finally { UnityEngine.Object.DestroyImmediate(go); }
        }
    }
}
