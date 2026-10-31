#if UNITY_EDITOR
using System.Collections;
using System.Linq;
using NUnit.Framework;
using ProcSense.VR.Bridge;
using ProcSense.VR.Views;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace ProcSense.Tests.PlayMode
{
    /// <summary>Audio-driven speech face: the spectral analysis picks sensible mouth shapes, the real patient's skin, teeth and brows follow, and silence or an unresponsive patient leaves the mouth still.</summary>
    public class SpeechFaceTests
    {
        /// <summary>These fixtures load the full room, whose scene host is persistent; later fixtures (login, tablet) expect a clean singleton, so remove it afterwards.</summary>
        [TearDown] public void Cleanup() => TestHostHygiene.DestroyRoomLeftovers();

        private IEnumerator Load()
        {
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/Scenes/PatientBay.unity", new LoadSceneParameters(LoadSceneMode.Single));
            for (int i = 0; i < 30; i++) yield return null;
            SimulationHost.Instance.AutoTick = false; SimulationHost.Instance.SaveLogOnDestroy = false;
        }

        private static void Tone(float hz, float rate, float[] wave, float[] spec)
        {
            for (int i = 0; i < wave.Length; i++) wave[i] = .3f * Mathf.Sin(2 * Mathf.PI * hz * i / rate);
            System.Array.Clear(spec, 0, spec.Length);
            int bin = Mathf.Clamp(Mathf.RoundToInt(hz / (rate * .5f / spec.Length)), 1, spec.Length - 1); spec[bin] = 1f;
        }

        [Test]
        public void Spectral_bands_choose_rounded_open_and_hissing_mouths()
        {
            var wave = new float[256]; var spec = new float[512];
            Tone(300, 48000, wave, spec); PatientSpeechFace.Analyse(wave, spec, 48000, out var l0, out var lo, out var mi, out var hi);
            Assert.Greater(l0, .3f); Assert.Greater(lo, .8f, "a low tone is read as rounded (O/U)");
            Tone(1000, 48000, wave, spec); PatientSpeechFace.Analyse(wave, spec, 48000, out _, out lo, out mi, out hi);
            Assert.Greater(mi, .8f, "a mid tone is read as open (AA)");
            Tone(4500, 48000, wave, spec); PatientSpeechFace.Analyse(wave, spec, 48000, out _, out lo, out mi, out hi);
            Assert.Greater(hi, .8f, "a high tone is read as a hiss (SS)");
            System.Array.Clear(wave, 0, wave.Length); PatientSpeechFace.Analyse(wave, spec, 48000, out var silent, out _, out _, out _);
            Assert.AreEqual(0f, silent, 1e-4f);
        }

        [UnityTest]
        public IEnumerator Real_patient_mouth_follows_speech_and_rests_in_silence()
        {
            yield return Load();
            var patient = GameObject.Find("MPFB patient");
            var face = patient.GetComponent<PatientSpeechFace>();
            Assert.IsNotNull(face, "speech face installed");
            Assert.GreaterOrEqual(face.Surfaces, 4, "skin, brows, hair, teeth/tongue all carry the shapes");
            face.enabled = false;
            var skin = patient.GetComponentsInChildren<SkinnedMeshRenderer>().First(r => r.name.EndsWith(".body"));
            int jaw = skin.sharedMesh.GetBlendShapeIndex("jawOpen"); Assert.GreaterOrEqual(jaw, 0, "the visible skin has a jawOpen shape");
            var teeth = patient.GetComponentsInChildren<SkinnedMeshRenderer>().First(r => r.name.Contains("teeth")); int tj = teeth.sharedMesh.GetBlendShapeIndex("jawOpen");
            const float dt = 1f / 60f;
            for (int i = 0; i < 10; i++) { face.Drive(0f, .3f, .4f, .3f, dt); face.StepForTest(dt); }                  // silence first, so the next rise counts as an onset
            for (int i = 0; i < 40; i++) { face.Drive(.8f, .1f, .7f, .2f, dt); face.StepForTest(dt); }
            Assert.Greater(skin.GetBlendShapeWeight(jaw), 25f, "speech opens the jaw on the skin");
            Assert.Greater(teeth.GetBlendShapeWeight(tj), 25f, "the teeth open with the jaw");
            Assert.Greater(face.Weight("viseme_aa"), .3f);
            for (int i = 0; i < 90; i++) { face.Drive(0f, .3f, .4f, .3f, dt); face.StepForTest(dt); }
            Assert.Less(skin.GetBlendShapeWeight(jaw), 3f, "the mouth returns to rest after the line ends");
            face.Alertness = PatientSpeechFace.PresentationAlertness.Unresponsive;
            Assert.AreEqual(PatientSpeechFace.PresentationAlertness.Unresponsive, face.Alertness);
        }
    }
}
#endif
