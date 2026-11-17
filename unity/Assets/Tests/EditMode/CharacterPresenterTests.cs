using NUnit.Framework;
using ProcSense.VR.Views;
using UnityEngine;

namespace ProcSense.Tests.EditMode
{
    /// <summary>A staff character turns the head toward the player when addressed, only for their own name, and never past the look-at limits.</summary>
    public class CharacterPresenterTests
    {
        private GameObject body, head, player;

        [SetUp]
        public void Setup()
        {
            body = new GameObject("nurse"); head = new GameObject("head"); head.transform.SetParent(body.transform, false); head.transform.localPosition = Vector3.up * 1.6f;
            player = new GameObject("player"); player.transform.position = new Vector3(1f, 1.6f, 1f);       // 45 degrees to the right of the nurse's forward (+z)
        }

        [TearDown] public void Teardown() { Object.DestroyImmediate(body); Object.DestroyImmediate(player); }

        [Test]
        public void Head_turn_points_at_the_target_within_the_limits()
        {
            var t = CharacterPresenter.HeadTurn(body.transform, head.transform.position, player.transform.position);
            Assert.AreEqual(45f, t.x, .5f); Assert.AreEqual(0f, t.y, .5f);
            player.transform.position = new Vector3(-5f, 1.6f, -1f);                                      // behind and to the left: clamped
            var far = CharacterPresenter.HeadTurn(body.transform, head.transform.position, player.transform.position);
            Assert.LessOrEqual(Mathf.Abs(far.x), LookAtLimits.MaxYaw + .01f);
        }

        [Test]
        public void A_sentence_for_the_nurse_addresses_the_nurse_and_not_the_porter()
        {
            var nurse = body.AddComponent<CharacterPresenter>(); nurse.Setup("nurse", head.transform);
            var other = new GameObject("porter").AddComponent<CharacterPresenter>(); other.Setup("porter", null);
            try
            {
                ProcSense.VR.Interaction.AddresseeTarget.Choose("Nurse, please check the drip");
                Assert.AreEqual(CharacterState.Addressed, nurse.State.State); Assert.AreEqual(CharacterState.Idle, other.State.State);
            }
            finally { Object.DestroyImmediate(other.gameObject); }
        }

        [Test]
        public void A_Rocketbox_staff_member_gets_a_mouth_that_follows_their_own_speech_only()
        {
            var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Characters/RB_Medical_Female_01.prefab"); Assert.IsNotNull(prefab);
            var model = (GameObject)UnityEditor.PrefabUtility.InstantiatePrefab(prefab, body.transform);
            var nurse = body.AddComponent<CharacterPresenter>(); nurse.Setup("nurse", null);
            var mouth = body.GetComponent<PatientSpeechFace>();
            Assert.IsNotNull(mouth); Assert.Greater(mouth.ShapesFound, 0, "jaw and viseme shapes found on the Rocketbox face");
            Assert.IsFalse(mouth.Gate(), "silent until their own line plays");
            nurse.State.StartSpeaking(); Assert.IsTrue(mouth.Gate());
        }

        [Test]
        public void Busy_staff_do_not_turn_to_the_player()
        {
            var nurse = body.AddComponent<CharacterPresenter>(); nurse.Setup("nurse", head.transform);
            nurse.SetBusy(true); nurse.Address();
            Assert.AreEqual(CharacterState.Busy, nurse.State.State);
        }
    }
}
