using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using ProcSense.VR.Bridge;
using ProcSense.VR.Views;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

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
            var monitor = Object.FindObjectsByType<EquipmentSurfaceView>(FindObjectsSortMode.None).Single(v => v.Kind == EquipmentSurfaceView.Surface.Monitor);
            monitor.RenderNow();
            Assert.That(monitor.DisplayText, Does.Contain("SpO2   --"));
            Assert.IsNotNull(Object.FindFirstObjectByType<IdentityCardView>());
            Assert.IsNotNull(Object.FindFirstObjectByType<NotesPanelView>(FindObjectsInactive.Include));
            Assert.AreEqual(1, Camera.allCamerasCount, "exactly one camera (the XR rig's) must draw, or head movement shows nothing");
        }

        [UnityTest]
        public IEnumerator Folder_requires_held_open_preserves_privacy_and_syncs_fallback_reset()
        {
            yield return LoadBoot();
            var host = SimulationHost.Instance;
            host.AutoTick = false;
            var folder = Object.FindFirstObjectByType<ProcSense.VR.ActionEmitters.NotesFolderInteraction>();
            Assert.IsNotNull(folder);
            Assert.IsFalse(folder.IsOpen);
            Assert.IsNull(host.Observable.Notes);
            Assert.IsFalse(folder.ToggleHeld(), "Unheld activation cannot open notes");
            int before = host.Session.Log.Count;
            var grab = folder.GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>();
            var handObject = new GameObject("Folder test hand");
            var hand = handObject.AddComponent<UnityEngine.XR.Interaction.Toolkit.Interactors.XRDirectInteractor>();
            hand.interactionManager = grab.interactionManager;
            try
            {
                grab.interactionManager.SelectEnter((IXRSelectInteractor)hand, (IXRSelectInteractable)grab);
                Assert.IsNull(host.Observable.Notes, "Picking up alone is not opening");
                grab.activated.Invoke(new UnityEngine.XR.Interaction.Toolkit.ActivateEventArgs { interactorObject = hand, interactableObject = grab });
                Assert.IsTrue(folder.IsOpen);
                Assert.AreEqual(before + 1, host.Session.Log.Count);
                var view = folder.GetComponentInChildren<NotesPanelView>();
                view.RenderNow();
                Assert.That(view.BodyText, Does.Contain(host.Observable.Notes.FullName));
                Assert.IsTrue(folder.ToggleHeld());
                Assert.IsFalse(folder.IsOpen);
                Assert.IsTrue(folder.ToggleHeld());
                Assert.AreEqual(before + 1, host.Session.Log.Count, "Reopening known pages creates no extra read action");
                grab.interactionManager.SelectExit((IXRSelectInteractor)hand, (IXRSelectInteractable)grab);
                host.StartSession("VAR-S1-A", 42, ProcSense.Core.SessionMode.Practice);
                folder.SyncNow();
                Assert.IsFalse(folder.IsOpen);
                Assert.IsNull(host.Observable.Notes);
                Object.FindFirstObjectByType<TabletView>().Press("open_notes");
                folder.SyncNow();
                Assert.IsTrue(folder.IsOpen, "Tablet fallback opens the same physical folder");
                Assert.AreEqual(1, host.Session.Log.Count);
            }
            finally { Object.DestroyImmediate(handObject); }
        }

        [UnityTest]
        public IEnumerator One_tablet_remove_stays_detached_across_physics_frames()
        {
            yield return LoadBoot();
            var tablet = Object.FindFirstObjectByType<TabletView>();
            var host = SimulationHost.Instance;
            host.AutoTick = false;
            foreach (string sensor in new[] { "spo2", "nibp_cuff" })
            {
                var binding = Object.FindObjectsByType<ProcSense.VR.ActionEmitters.SensorAttachment>(FindObjectsSortMode.None)
                    .Single(s => s.BuildAction().Params["sensor"] == sensor);
                for (int repeat = 0; repeat < 2; repeat++)
                {
                    tablet.Press("attach_sensor", "sensor", sensor);
                    for (int i = 0; i < 5; i++) yield return new WaitForFixedUpdate();
                    Assert.IsTrue(binding.Attached);
                    int before = host.Session.Log.Count;
                    tablet.Press("detach_sensor", "sensor", sensor);
                    for (int i = 0; i < 15; i++) yield return new WaitForFixedUpdate();
                    Assert.IsFalse(binding.Attached, sensor + " snapped back after one remove");
                    Assert.AreEqual(before + 1, host.Session.Log.Count, "No automatic reattach action");
                    Assert.IsFalse(binding.GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>().isSelected);
                }
                // Returning the actual prop to the socket must still work without a tablet action.
                var grab = binding.GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>();
                var target = binding.Destination.attachTransform;
                binding.transform.position += target.position - grab.attachTransform.position;
                var pose = new Pose(binding.transform.position, binding.transform.rotation);
                grab.SetTargetPose(pose);
                var body = binding.GetComponent<Rigidbody>();
                body.position = pose.position;
                body.rotation = pose.rotation;
                Physics.SyncTransforms();
                int actions = host.Session.Log.Count;
                for (int i = 0; i < 15; i++) yield return new WaitForFixedUpdate();
                Assert.IsTrue(binding.Attached, sensor + " must reattach when returned physically");
                Assert.AreEqual(actions + 1, host.Session.Log.Count);
            }
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
