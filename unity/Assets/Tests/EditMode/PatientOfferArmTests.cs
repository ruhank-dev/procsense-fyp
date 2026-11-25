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

        [Test]
        public void The_arm_nearest_the_cuff_target_is_the_one_offered()
        {
            Assert.IsTrue(PatientMotion.NearerLeft(new Vector3(-1, 0, 0), new Vector3(-.2f, 0, 0), new Vector3(.2f, 0, 0)));
            Assert.IsFalse(PatientMotion.NearerLeft(new Vector3(1, 0, 0), new Vector3(-.2f, 0, 0), new Vector3(.2f, 0, 0)));
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Characters/RB_Medical_Male_04.prefab"); Assert.IsNotNull(prefab);
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            var target = new GameObject("cuff target").transform;
            try
            {
                var motion = go.AddComponent<PatientMotion>(); motion.autoMotion = false; motion.Prepare();
                var bones = go.GetComponentsInChildren<Transform>(true);
                var left = bones.First(t => t.name == "Bip01 L Hand"); var right = bones.First(t => t.name == "Bip01 R Hand");
                motion.Step(0f, .05f); var leftRest = left.position; var rightRest = right.position;
                target.position = bones.First(t => t.name == "Bip01 L Forearm").position;       // the cuff goes on the left forearm
                motion.offerNear = target;
                motion.OfferArm(true); float t = 0f; for (int i = 0; i < 40; i++) { t += .05f; motion.Step(t, .05f); }
                Assert.IsTrue(motion.OfferingLeft);
                Assert.Greater(Vector3.Distance(left.position, leftRest), .12f, "the left hand moved out");
                Assert.Less(Vector3.Distance(right.position, rightRest), .01f, "the right arm stayed down");
                motion.OfferArm(false); for (int i = 0; i < 40; i++) { t += .05f; motion.Step(t, .05f); }
                Assert.Less(Vector3.Distance(left.position, leftRest), .01f, "the left arm is back at rest");
            }
            finally { Object.DestroyImmediate(go); Object.DestroyImmediate(target.gameObject); }
        }

        [Test]
        public void A_neck_pose_set_from_outside_is_kept_as_the_new_rest()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Characters/RB_Medical_Male_04.prefab"); Assert.IsNotNull(prefab);
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            try
            {
                var motion = go.AddComponent<PatientMotion>(); motion.autoMotion = false; motion.Prepare();
                var neck = go.GetComponentsInChildren<Transform>(true).First(t => t.name == "Bip01 Neck");
                motion.Step(0f, .05f);
                var moved = neck.localRotation * Quaternion.Euler(0f, 0f, 10f); neck.localRotation = moved;      // what the Animator does each frame
                motion.Step(.1f, .05f); motion.Step(.2f, .05f);
                Assert.Less(Quaternion.Angle(neck.localRotation, moved), .1f, "the neck keeps the Animator's pose instead of snapping back to the pose at start-up");
            }
            finally { Object.DestroyImmediate(go); }
        }
    }
}
