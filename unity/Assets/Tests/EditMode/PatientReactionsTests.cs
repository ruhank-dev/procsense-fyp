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
        public void A_sentence_addressed_to_the_patient_makes_them_look_at_the_player_and_one_for_the_nurse_does_not()
        {
            var r = patient.AddComponent<PatientReactions>(); r.player = player.transform; r.Bind(); var motion = patient.GetComponent<PatientMotion>();
            ProcSense.VR.Interaction.AddresseeTarget.Choose("Nurse, please check the drip");
            Assert.AreEqual(CharacterState.Idle, r.State.State, "a sentence for the nurse is not for the patient");
            ProcSense.VR.Interaction.AddresseeTarget.Choose("Patient, how is the pain today");
            Assert.AreEqual(CharacterState.Addressed, r.State.State); Assert.AreSame(player.transform, motion.lookAt);
        }

        [Test]
        public void An_accepted_action_gets_a_nod_and_a_refused_one_a_head_shake()
        {
            var r = patient.AddComponent<PatientReactions>(); r.Bind(); var motion = patient.GetComponent<PatientMotion>();
            r.Reply(true); Assert.AreEqual(HeadGestureKind.Nod, motion.GestureKind);
            r.State.Step(CharacterEventState.GestureSeconds + .1f);
            r.Reply(false); Assert.AreEqual(HeadGestureKind.Shake, motion.GestureKind);
        }

        private static ProcSense.Core.Actions.ActionOutcome Outcome(params string[] effects) { var o = new ProcSense.Core.Actions.ActionOutcome(); o.Effects.AddRange(effects); return o; }

        [Test]
        public void A_finding_that_hurts_makes_the_patient_flinch_and_the_session_mood_comes_back()
        {
            var r = patient.AddComponent<PatientReactions>(); r.Bind(); var motion = patient.GetComponent<PatientMotion>();
            motion.SetMood("CONFUSED"); motion.Step(10f, .05f);
            r.OnApplied(new ProcSense.Core.Actions.SimAction("examine"), Outcome("finding:abdomen", "reaction:winces and tenses"));
            Assert.IsTrue(motion.Flinching, "the engine's reaction effect starts the flinch");
            motion.Step(10.05f, .05f); Assert.IsTrue(motion.Flinching);
            motion.Step(13f, .05f); Assert.IsFalse(motion.Flinching, "the flinch ends");
        }

        [Test]
        public void An_ordinary_finding_shows_no_flinch_so_it_gives_nothing_away()
        {
            var r = patient.AddComponent<PatientReactions>(); r.Bind(); var motion = patient.GetComponent<PatientMotion>();
            r.OnApplied(new ProcSense.Core.Actions.SimAction("examine"), Outcome("finding:abdomen"));
            Assert.IsFalse(motion.Flinching);
        }

        [Test]
        public void An_accepted_permission_gets_a_nod_and_a_refused_one_does_not()
        {
            var r = patient.AddComponent<PatientReactions>(); r.Bind(); var motion = patient.GetComponent<PatientMotion>();
            var refused = Outcome(); refused.Reject("NOT_NOW");
            r.OnApplied(new ProcSense.Core.Actions.SimAction("ask_exam_permission"), refused); Assert.AreEqual(HeadGestureKind.None, motion.GestureKind);
            r.OnApplied(new ProcSense.Core.Actions.SimAction("ask_exam_permission"), Outcome("permission_asked")); Assert.AreEqual(HeadGestureKind.Nod, motion.GestureKind);
        }
    }
}
