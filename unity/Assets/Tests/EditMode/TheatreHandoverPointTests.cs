using NUnit.Framework;
using ProcSense.VR.Interaction;
using TMPro;
using UnityEngine;

namespace ProcSense.Tests.EditMode
{
    public class TheatreHandoverPointTests
    {
        [Test]
        public void With_no_session_it_says_there_is_nothing_to_hand_over()
        {
            var go = new GameObject("handover", typeof(UnityEngine.XR.Interaction.Toolkit.Interactables.XRSimpleInteractable));
            try
            {
                var text = new GameObject("t").AddComponent<TextMeshPro>();
                var p = go.AddComponent<TheatreHandoverPoint>(); p.Configure(text);
                Assert.AreEqual(TheatreHandoverPoint.Idle, text.text);
                p.Press(null);
                StringAssert.Contains("No patient", text.text); Assert.IsFalse(p.Armed);
                Object.DestroyImmediate(text.gameObject);
            }
            finally { Object.DestroyImmediate(go); }
        }
    }
}
