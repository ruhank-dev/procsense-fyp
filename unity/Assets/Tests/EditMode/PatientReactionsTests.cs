using NUnit.Framework;
using ProcSense.VR.Views;
using UnityEditor;
using UnityEngine;

namespace ProcSense.Tests.EditMode
{
    /// <summary>The patient looks at the player while in a conversation, nods for an accepted action, shakes the head for a refused one, and stops looking when it is over.</summary>
    public class PatientReactionsTests
    {
        private GameObject patient, player;

        [SetUp]
        public void Setup()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Characters/RB_Medical_Male_04.prefab"); Assert.IsNotNull(prefab);
            patient = (GameObject)PrefabUtility.InstantiatePrefab(prefab); patient.AddComponent<PatientMotion>().Prepare();
            player = new GameObject("player");
        }

        [TearDown] public void Teardown() { Object.DestroyImmediate(patient); Object.DestroyImmediate(player); }

        [Test]
        public void Looks_at_the_player_while_in_conversation_and_stops_when_it_ends()
        {
            var r = patient.AddComponent<PatientReactions>(); r.player = player.transform; r.Bind();
            var motion = patient.GetComponent<PatientMotion>(); Assert.IsNull(motion.lookAt);
            r.Address(); Assert.AreSame(player.transform, motion.lookAt);
            r.StartSpeaking(); Assert.AreSame(player.transform, motion.lookAt);
            r.EndSpeaking(); Assert.AreSame(player.transform, motion.lookAt, "still listening");
            r.State.Step(CharacterEventState.ListenSeconds + .1f); Assert.IsNull(motion.lookAt, "the conversation is over");
        }

        [Test]
        public void An_accepted_action_gets_a_nod_and_a_refused_one_a_head_shake()
        {
            var r = patient.AddComponent<PatientReactions>(); r.Bind(); var motion = patient.GetComponent<PatientMotion>();
            r.Reply(true); Assert.AreEqual(HeadGestureKind.Nod, motion.GestureKind);
            r.State.Step(CharacterEventState.GestureSeconds + .1f);
            r.Reply(false); Assert.AreEqual(HeadGestureKind.Shake, motion.GestureKind);
        }
    }
}
