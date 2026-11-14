using System.Collections;
using NUnit.Framework;
using ProcSense.VR.Views;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace ProcSense.Tests.PlayMode
{
    /// <summary>Breathing moves the chest, not the head: the head must not rise and sink in the pillow with each breath.</summary>
    public sealed class PatientBreathHeadTests
    {
        [SetUp] public void Setup() => ProcSense.VR.Login.DemoLoginSettings.LoginRequiredOverride = false;
        [UnityTearDown] public IEnumerator Cleanup()
        {
            ProcSense.VR.Login.DemoLoginSettings.LoginRequiredOverride = null;
            yield return EditorXRTestEnvironment.Cleanup();
        }

        [UnityTest]
        public IEnumerator The_head_stays_put_through_a_breath()
        {
            yield return SceneManager.LoadSceneAsync("Boot"); yield return EditorXRTestEnvironment.EnsureSimulator();
            var patient = Object.FindAnyObjectByType<PatientMotion>(); Assert.IsNotNull(patient);
            patient.autoMotion = true; patient.breathsPerMinute = 12f;
            patient.Step(0f, .016f); var rest = patient.Head.position; float worst = 0f;
            for (float t = 0f; t < 10f; t += .1f)
            {
                patient.Step(t, .016f);
                worst = Mathf.Max(worst, Vector3.Distance(patient.Head.position, rest));
            }
            Assert.Less(worst, .008f, "the head moved " + worst + " m with the breath");
        }
    }
}
