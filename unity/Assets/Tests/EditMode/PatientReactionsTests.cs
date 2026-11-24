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

        [Test]
        public void The_arm_is_held_out_while_the_cuff_is_attached_and_comes_back_when_it_is_removed()
        {
            var r = patient.AddComponent<PatientReactions>(); r.Bind(); var motion = patient.GetComponent<PatientMotion>();
            var p = new System.Collections.Generic.Dictionary<string, string> { ["sensor"] = "nibp_cuff" };
            r.OnApplied(new ProcSense.Core.Actions.SimAction("attach_sensor", "patient", ProcSense.Core.Actions.Channel.Vr, p), Outcome());
            motion.Step(1f, 1f); Assert.AreEqual(1f, motion.Offer, 1e-3f);
            r.OnApplied(new ProcSense.Core.Actions.SimAction("detach_sensor", "patient", ProcSense.Core.Actions.Channel.Vr, p), Outcome());
            motion.Step(2f, 1f); Assert.AreEqual(0f, motion.Offer, 1e-3f);
        }

        [Test]
        public void Signing_consent_brings_the_hand_up_for_a_few_seconds_and_then_back()
        {
            var r = patient.AddComponent<PatientReactions>(); r.Bind(); var motion = patient.GetComponent<PatientMotion>();
            r.OnApplied(new ProcSense.Core.Actions.SimAction("sign_consent"), Outcome());
            motion.Step(1f, 1f); Assert.AreEqual(1f, motion.Signing, 1e-3f);
            motion.Step(6f, 1f); motion.Step(7f, 1f); Assert.AreEqual(0f, motion.Signing, 1e-3f);
        }

        [Test]
        public void Examining_the_chest_sits_the_patient_up_and_the_abdomen_does_not()
        {
            var r = patient.AddComponent<PatientReactions>(); r.Bind(); var motion = patient.GetComponent<PatientMotion>();
            var abdomen = new System.Collections.Generic.Dictionary<string, string> { ["region"] = "abdomen" };
            r.OnApplied(new ProcSense.Core.Actions.SimAction("examine", "patient", ProcSense.Core.Actions.Channel.Vr, abdomen), Outcome());
            motion.Step(1f, 2f); Assert.AreEqual(0f, motion.Sitting, 1e-3f);
            var chest = new System.Collections.Generic.Dictionary<string, string> { ["region"] = "chest" };
            r.OnApplied(new ProcSense.Core.Actions.SimAction("examine", "patient", ProcSense.Core.Actions.Channel.Vr, chest), Outcome());
            motion.Step(2f, 2f); Assert.AreEqual(1f, motion.Sitting, 1e-3f);
            motion.Step(20f, 20f); motion.Step(25f, 5f); Assert.AreEqual(0f, motion.Sitting, 1e-3f, "lies back afterwards");
        }
    }
}
