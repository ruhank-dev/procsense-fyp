using NUnit.Framework;
using ProcSense.VR.Interaction;
using ProcSense.VR.Views;
using TMPro;
using UnityEngine;

namespace ProcSense.Tests.EditMode
{
    public class BedAndSharpsTests
    {
        [Test]
        public void The_bed_aims_flat_head_up_or_fully_sitting()
        {
            Assert.AreEqual(0f, PatientMotion.SitGoal(0f, false));
            Assert.AreEqual(PatientMotion.HeadUpShare, PatientMotion.SitGoal(0f, true));
            Assert.AreEqual(1f, PatientMotion.SitGoal(5f, true), "an examination sit-up wins over head-up");
        }

        [Test]
        public void Without_a_patient_the_bed_control_says_so()
        {
            var go = new GameObject("bed", typeof(UnityEngine.XR.Interaction.Toolkit.Interactables.XRSimpleInteractable));
            var text = new GameObject("t").AddComponent<TextMeshPro>();
            try
            {
                var c = go.AddComponent<BedControl>(); c.Configure(text);
                Assert.AreEqual(BedControl.Flat, text.text);
                c.Press(null);
                StringAssert.Contains("No patient", text.text);
            }
            finally { Object.DestroyImmediate(text.gameObject); Object.DestroyImmediate(go); }
        }

        [Test]
        public void The_sharps_bin_takes_a_used_sharp_and_nothing_else()
        {
            var bin = new GameObject("bin").AddComponent<SharpsBin>();
            var sharp = new GameObject("needle").AddComponent<UsedSharp>();
            try
            {
                Assert.IsFalse(bin.Dispose(null));
                Assert.IsTrue(bin.Dispose(sharp));
                Assert.IsFalse(sharp.gameObject.activeSelf);
                Assert.AreEqual(1, bin.Count);
                Assert.IsFalse(bin.Dispose(sharp), "already in the bin");
                Assert.AreEqual(1, bin.Count);
            }
            finally { Object.DestroyImmediate(bin.gameObject); Object.DestroyImmediate(sharp.gameObject); }
        }
    }
}
