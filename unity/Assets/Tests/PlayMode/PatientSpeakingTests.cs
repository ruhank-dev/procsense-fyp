using System.Collections;
using NUnit.Framework;
using ProcSense.VR.Audio;
using ProcSense.VR.Views;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace ProcSense.Tests.PlayMode
{
    /// <summary>The patient is in the speaking state while one of their own voice lines plays, and listens afterwards; other speakers do not make the patient speak.</summary>
    public sealed class PatientSpeakingTests
    {
        [SetUp] public void Setup() => ProcSense.VR.Login.DemoLoginSettings.LoginRequiredOverride = false;
        [UnityTearDown] public IEnumerator Cleanup()
        {
            ProcSense.VR.Login.DemoLoginSettings.LoginRequiredOverride = null;
            yield return EditorXRTestEnvironment.Cleanup();
        }

        [UnityTest]
        public IEnumerator The_patient_speaks_during_their_own_line_and_then_listens()
        {
            yield return SceneManager.LoadSceneAsync("Boot"); yield return EditorXRTestEnvironment.EnsureSimulator();
            var motion = Object.FindAnyObjectByType<PatientMotion>(); Assert.IsNotNull(motion);
            foreach (var presentation in Object.FindObjectsByType<SessionPresentation>(FindObjectsSortMode.None)) presentation.enabled = false;     // it attaches the patient to the session's own voice player every frame; this test uses its own
            var reactions = motion.GetComponent<PatientReactions>(); if (reactions == null) reactions = motion.gameObject.AddComponent<PatientReactions>();
            var voice = new GameObject("test voice").AddComponent<VoicePlayer>();
            try
            {
                reactions.Attach(voice);
                voice.Say(new VoiceLine { Id = "test_nurse_line", Speaker = "nurse", Text = "Hello there" });
                Assert.AreNotEqual(CharacterState.Speaking, reactions.State.State, "a nurse line does not make the patient speak");
                voice.Tick(100f); yield return null;
                voice.Say(new VoiceLine { Id = "test_patient_line", Speaker = "patient", Text = "my tummy hurts" });
                Assert.AreEqual(CharacterState.Speaking, reactions.State.State);
                voice.Tick(100f); yield return null; yield return null;
                Assert.AreEqual(CharacterState.Listening, reactions.State.State, "the line is over: the patient listens");
            }
            finally { reactions.Attach(null); Object.Destroy(voice.gameObject); }
        }
    }
}
