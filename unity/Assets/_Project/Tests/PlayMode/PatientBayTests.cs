using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using ProcSense.VR.Bridge;
using ProcSense.VR.Views;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace ProcSense.Tests.PlayMode
{
    /// <summary>The generated greybox patient bay: it boots, holds the views and lets a trainee finish the identity check on the tablet.</summary>
    public class PatientBayTests
    {
        private IEnumerator LoadBoot()
        {
            yield return SceneManager.LoadSceneAsync("Boot");
            for (int i = 0; i < 30; i++) yield return null;   // BootLoader loads Sys_Core, the bay and (in the Editor) DevOnly
        }

        [UnityTest]
        public IEnumerator The_bay_boots_with_a_session_and_all_views()
        {
            yield return LoadBoot();

            Assert.IsNotNull(SimulationHost.Instance);
            Assert.IsNotNull(SimulationHost.Instance.Session);
            Assert.IsNotNull(Object.FindFirstObjectByType<TabletView>());
            Assert.IsNotNull(Object.FindFirstObjectByType<MonitorView>());
            Assert.IsNotNull(Object.FindFirstObjectByType<IdentityCardView>());
            Assert.IsNotNull(Object.FindFirstObjectByType<NotesPanelView>());
            Assert.AreEqual(1, Camera.allCamerasCount, "exactly one camera (the XR rig's) must draw, or head movement shows nothing");
        }

        [UnityTest]
        public IEnumerator The_identity_check_can_be_finished_from_the_tablet_alone()
        {
            yield return LoadBoot();
            var tablet = Object.FindFirstObjectByType<TabletView>();
            var host = SimulationHost.Instance;

            foreach (var verb in new[] { "introduce_self", "ask_identity", "read_wristband", "open_notes" }) Assert.IsTrue(tablet.Press(verb), verb);
            yield return null;
            tablet.RenderNow();
            Assert.IsTrue(tablet.Buttons.First(b => b.label == "Confirm identity").button.interactable);
            Assert.IsTrue(tablet.Press("confirm_identity"));

            Assert.IsTrue(host.Observable.IdentityConfirmed);
            Assert.IsTrue(host.Session.Log.Count >= 5);
        }

        [UnityTest]
        public IEnumerator A_screenshot_of_the_bay_is_saved_for_review()
        {
            yield return LoadBoot();
            var tablet = Object.FindFirstObjectByType<TabletView>();
            foreach (var verb in new[] { "introduce_self", "ask_identity", "read_wristband", "open_notes" }) tablet.Press(verb);
            tablet.Press("attach_sensor", "sensor", "spo2");
            foreach (var view in Object.FindObjectsByType<StateView>(FindObjectsSortMode.None)) view.RenderNow();
            yield return null;

            string dir = Path.Combine(Application.dataPath, "..", "..", "artifacts", "unity");
            Directory.CreateDirectory(dir);
            yield return Shoot(Path.Combine(dir, "bay_overview.png"), new Vector3(0.9f, 1.7f, -1.9f), new Vector3(-0.6f, 1.0f, 1.0f));
            yield return Shoot(Path.Combine(dir, "bay_tablet.png"), new Vector3(-0.6f, 1.25f, -1.3f), tablet.transform.position);
            Assert.IsTrue(File.Exists(Path.Combine(dir, "bay_overview.png")));
        }

        private static IEnumerator Shoot(string file, Vector3 from, Vector3 lookAt)
        {
            var go = new GameObject("shot camera", typeof(Camera));
            var cam = go.GetComponent<Camera>();
            cam.transform.position = from;
            cam.transform.LookAt(lookAt);
            cam.fieldOfView = 60;
            var rt = new RenderTexture(1280, 720, 24);
            cam.targetTexture = rt;
            yield return null;
            cam.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(1280, 720, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
            tex.Apply();
            File.WriteAllBytes(file, tex.EncodeToPNG());
            RenderTexture.active = null;
            Object.Destroy(go);
            Object.Destroy(rt);
        }
    }
}
