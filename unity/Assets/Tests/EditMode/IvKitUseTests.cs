using NUnit.Framework;
using ProcSense.VR.ActionEmitters;
using UnityEngine;

namespace ProcSense.Tests.EditMode
{
    public class IvKitUseTests
    {
        [Test]
        public void The_kit_counts_as_at_the_arm_only_within_reach()
        {
            var arm = new Vector3(0, 1, 0);
            Assert.IsTrue(IvKitUse.AtArm(arm + Vector3.right * .1f, arm));
            Assert.IsTrue(IvKitUse.AtArm(arm + Vector3.up * (IvKitUse.Reach - .01f), arm));
            Assert.IsFalse(IvKitUse.AtArm(arm + Vector3.right * (IvKitUse.Reach + .05f), arm));
        }

        [Test]
        public void Letting_go_with_no_session_sends_nothing_and_does_not_throw()
        {
            var go = new GameObject("kit", typeof(UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable));
            var arm = new GameObject("arm");
            try { var kit = go.AddComponent<IvKitUse>(); Assert.DoesNotThrow(() => kit.Use(arm.transform)); }
            finally { Object.DestroyImmediate(go); Object.DestroyImmediate(arm); }
        }
    }
}
