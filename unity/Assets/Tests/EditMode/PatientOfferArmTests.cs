using System.Linq;
using NUnit.Framework;
using ProcSense.VR.Views;
using UnityEditor;
using UnityEngine;

namespace ProcSense.Tests.EditMode
{
    /// <summary>The patient can hold the right arm out for the cuff and bring it back to rest.</summary>
    public class PatientOfferArmTests
    {
        [Test]
        public void The_arm_goes_out_to_the_side_and_comes_back()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Characters/RB_Medical_Male_04.prefab"); Assert.IsNotNull(prefab);
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            try
            {
                var motion = go.AddComponent<PatientMotion>(); motion.autoMotion = false; motion.Prepare();
                var hand = go.GetComponentsInChildren<Transform>(true).First(t => t.name == "Bip01 R Hand");
                motion.Step(0f, .05f); var rest = hand.position;
                motion.OfferArm(true); float t = 0f; for (int i = 0; i < 40; i++) { t += .05f; motion.Step(t, .05f); }
                Assert.AreEqual(1f, motion.Offer, 1e-4f);
                Assert.Greater(Vector3.Distance(hand.position, rest), .12f, "the hand moved out to the side");
                motion.OfferArm(false); for (int i = 0; i < 40; i++) { t += .05f; motion.Step(t, .05f); }
                Assert.AreEqual(0f, motion.Offer, 1e-4f);
                Assert.Less(Vector3.Distance(hand.position, rest), .01f, "the arm is back at rest");
            }
            finally { Object.DestroyImmediate(go); }
        }
    }
}
