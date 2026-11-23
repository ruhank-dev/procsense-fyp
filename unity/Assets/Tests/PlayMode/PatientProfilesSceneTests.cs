using System.Collections;
using System.Linq;
using NUnit.Framework;
using ProcSense.VR.Boot;
using ProcSense.VR.Bridge;
using ProcSense.VR.Views;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace ProcSense.Tests.PlayMode
{
    /// <summary>P3-AST-03: the patient in the bay fits the case, and for every profile the sensors, the wristband, the ear contact and the nurse's wrist target are on the body that is shown.</summary>
    public class PatientProfilesSceneTests
    {
        [SetUp] public void BypassLogin() => ProcSense.VR.Login.DemoLoginSettings.LoginRequiredOverride = false;
        [TearDown] public void RestoreLogin() => ProcSense.VR.Login.DemoLoginSettings.LoginRequiredOverride = null;

        private IEnumerator LoadBoot()
        {
            yield return SceneManager.LoadSceneAsync("Boot");
            yield return TestHostHygiene.WaitForHost(30);
            var rooms = Object.FindAnyObjectByType<RoomLoader>();
            float end = Time.realtimeSinceStartup + 20; while (rooms != null && rooms.Busy && Time.realtimeSinceStartup < end) yield return null;
            yield return null; yield return null;
        }

        private static void AssertOnShownBody(PatientProfiles profiles, string key)
        {
            var bodies = Object.FindObjectsByType<PatientMotion>(FindObjectsInactive.Exclude);
            Assert.AreEqual(1, bodies.Length, key + ": exactly one patient body is active");
            var body = bodies[0].transform;
            Assert.AreEqual(key, profiles.Active);
            foreach (var name in new[] { "Patient wristband", "Thermometer ear contact", "spo2 patient socket", "nibp_cuff patient socket" })
                Assert.IsTrue(GameObject.Find(name).transform.IsChildOf(body), key + ": '" + name + "' is on the shown body");
            var nurse = Object.FindAnyObjectByType<NurseWristbandController>(FindObjectsInactive.Include);
            if (nurse != null && nurse.Wrist != null) Assert.IsTrue(nurse.Wrist.IsChildOf(body), key + ": the nurse's wrist target is on the shown body");
            var transport = Object.FindAnyObjectByType<PatientTransportView>(FindObjectsInactive.Include);
            Assert.IsTrue(transport.AdditionalBedsideVisuals.Contains(body), key + ": the transport view hides and shows the body that is on the bed");
        }

        [UnityTest]
        public IEnumerator The_default_case_shows_the_body_that_fits_its_patient()
        {
            yield return LoadBoot();
            var host = SimulationHost.Instance; var profiles = Object.FindAnyObjectByType<PatientProfiles>();
            Assert.IsNotNull(profiles, "the bay has its patient profiles");
            host.Content.CaseFiles.TryGetValue(host.Session.Case.Id, out var file);
            Assert.AreEqual(PatientProfiles.KeyFor(file.Identity.Sex, file.Identity.Age), profiles.Active, "the body follows the case's sex and age");
            AssertOnShownBody(profiles, profiles.Active);
        }

        [UnityTest]
        public IEnumerator The_bay_dressing_follows_the_settings_of_the_case()
        {
            yield return LoadBoot();
            var host = SimulationHost.Instance; var variant = Object.FindAnyObjectByType<BayVariant>(FindObjectsInactive.Include);
            Assert.IsNotNull(variant, "the bay has its variant switch");
            host.Content.CaseFiles.TryGetValue(host.Session.Case.Id, out var file);
            Assert.AreEqual(BayVariant.IsEmergency(file.Presentation.Setting), variant.Emergency);
            foreach (var g in variant.emergency) Assert.AreEqual(variant.Emergency, g.activeSelf, "the ED dressing is shown only for an emergency case");
        }

        [UnityTest]
        public IEnumerator Every_profile_keeps_every_binding()
        {
            yield return LoadBoot();
            var profiles = Object.FindAnyObjectByType<PatientProfiles>();
            Assert.GreaterOrEqual(profiles.profiles.Count, 5, "five profiles are in the bay");
            foreach (var p in profiles.profiles.ToList())
            {
                profiles.Select(p.key); yield return null;
                AssertOnShownBody(profiles, p.key);
                Assert.IsNotNull(p.root.GetComponent<PatientSpeechFace>(), p.key + ": the patient's mouth follows the patient's own lines");
            }
        }
    }
}
