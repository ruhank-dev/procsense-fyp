using NUnit.Framework;
using ProcSense.VR.ActionEmitters;
using UnityEngine;

namespace ProcSense.Tests.PlayMode
{
    public class PlacementGuideTests
    {
        [Test]
        public void A_guide_without_a_socket_is_blue_while_approaching_green_when_ready_and_gone_when_not_held()
        {
            var target = new GameObject("ear").transform;
            var material = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            var guide = new SensorPlacementGuide(target, .05f, null, material, 0f);
            try
            {
                Assert.IsFalse(guide.Visible);
                guide.Refresh(true, false, false);
                Assert.IsTrue(guide.Visible); Assert.AreEqual(SensorPlacementGuide.TargetState.Approach, guide.PatientState);
                guide.Refresh(true, true, false);
                Assert.AreEqual(SensorPlacementGuide.TargetState.Ready, guide.PatientState);
                guide.Refresh(true, true, false, available: false);
                Assert.AreEqual(SensorPlacementGuide.TargetState.Unavailable, guide.PatientState);
                guide.Refresh(false, true, false);
                Assert.IsFalse(guide.Visible);
            }
            finally { guide.Dispose(); Object.DestroyImmediate(target.gameObject); Object.DestroyImmediate(material); }
        }
    }
}
