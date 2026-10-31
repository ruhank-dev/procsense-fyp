using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using ProcSense.Core;
using ProcSense.Core.Actions;
using ProcSense.Core.Workflow;
using ProcSense.VR.Boot;
using ProcSense.VR.Bridge;
using ProcSense.VR.Views;
using ProcSense.VR.XR;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation;

namespace ProcSense.Tests.PlayMode
{
    public class DiagnosticDoorViewTests
    {
        private readonly List<Object> _made = new List<Object>();
        private SimulationHost _host;
        private static SimAction Action(string verb, params (string key, string value)[] p) => new SimAction(verb, "patient", Channel.Vr, p.ToDictionary(k => k.key, k => k.value));

        [SetUp]
        public void SetUp()
        {
            if (SimulationHost.Instance != null)
            {
                SimulationHost.Instance.SaveLogOnDestroy = false;
                Object.DestroyImmediate(SimulationHost.Instance.gameObject);
            }
            var go = new GameObject("door test host"); _made.Add(go);
            _host = go.AddComponent<SimulationHost>(); _host.AutoTick = false; _host.SaveLogOnDestroy = false;
            _host.StartSession("VAR-S1-A", 42, SessionMode.Practice);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var item in _made.AsEnumerable().Reverse()) if (item != null) Object.DestroyImmediate(item);
            _made.Clear();
        }

        private DiagnosticDoorView View(string destination = "ct", bool laboratory = false)
        {
            var go = new GameObject("test diagnostic door"); _made.Add(go);
            var view = go.AddComponent<DiagnosticDoorView>();
            var copy = new TextAsset(File.ReadAllText(Path.Combine(Application.dataPath, "Data", "diagnostic-door-presentation.json"))); _made.Add(copy);
            view.Presentation = copy; view.Laboratory = laboratory; view.DestinationIds = new[] { destination };
            view.Build(); view.RenderNow(); return view;
        }

        private void Identity(Session reference = null)
        {
            foreach (var verb in new[] { "ask_identity", "read_wristband", "open_notes", "confirm_identity" })
            {
                var action = Action(verb); Assert.IsTrue(_host.Apply(action).Accepted); reference?.Apply(action);
            }
        }

        private void Until(string state, Session reference = null)
        {
            for (int i = 0; i < 12000 && _host.Observable.Transport?.State != state; i++) { _host.Tick(.5); reference?.Tick(.5); }
            Assert.AreEqual(state, _host.Observable.Transport?.State);
        }

        [Test]
        public void Approach_notice_is_front_only_once_per_visit_with_cooldown_and_changes_no_core_state()
        {
            var view = View(); string log = _host.Session.Log.Hash(), state = _host.Observable.Hash();
            Assert.IsFalse(view.ObserveApproach(Vector3.forward, 0));
            Assert.IsFalse(view.ObserveApproach(Vector3.back * 3, 0));
            Assert.IsTrue(view.ObserveApproach(Vector3.back, 0)); Assert.IsTrue(view.NoticeVisible); Assert.AreEqual(1, view.NoticeCount);
            view.ObserveApproach(Vector3.back, 6); Assert.IsFalse(view.NoticeVisible); Assert.AreEqual(1, view.NoticeCount);
            view.ObserveApproach(Vector3.back * 3, 6.1f); view.ObserveApproach(Vector3.back, 6.2f); Assert.AreEqual(2, view.NoticeCount);
            view.ObserveApproach(Vector3.back * 3, 6.3f); view.ObserveApproach(Vector3.back, 6.4f); Assert.AreEqual(2, view.NoticeCount);
            view.ObserveApproach(Vector3.back, 12); Assert.AreEqual(2, view.NoticeCount);
            view.ObserveApproach(Vector3.back * 3, 12.1f); view.ObserveApproach(Vector3.back, 12.2f); Assert.AreEqual(3, view.NoticeCount);
            Assert.IsFalse(view.ObserveApproach(new Vector3(float.NaN, 0, 0), 13));
            Assert.AreEqual(log, _host.Session.Log.Hash()); Assert.AreEqual(state, _host.Observable.Hash());
        }

        [TestCase("walk")]
        [TestCase("wheelchair")]
        [TestCase("trolley")]
        public void Journey_modes_and_all_stages_come_from_core_with_identical_replay_hashes(string mode)
        {
            var view = View();
            var content = _host.Content;
            var reference = new Session(content.Cases["VAR-S1-A"], 42, SessionMode.Practice, Subsystems.Default(content));
            Identity(reference);
            var request = Action("request_transport", ("to", "ct"), ("mode", mode), ("escort", "porter,nurse"), ("monitoring", "portable"), ("oxygen", "false"));
            Assert.IsTrue(_host.Apply(request).Accepted); Assert.IsTrue(reference.Apply(request).Accepted);
            foreach (string stage in new[] { "REQUESTED", "PREPARING", "IN_TRANSIT", "ARRIVED", "DONE", "RETURNING", "BACK" })
            {
                Until(stage, reference); view.RenderNow();
                StringAssert.Contains(stage, view.StatusText); StringAssert.Contains(mode, view.StatusText);
                if (!string.IsNullOrEmpty(_host.Observable.Transport.Arrives)) StringAssert.Contains(_host.Observable.Transport.Arrives, view.StatusText);
                else StringAssert.DoesNotContain("Expected arrival:", view.StatusText, "No timing label when Core has not published an arrival");
                Assert.AreEqual(reference.Log.Hash(), _host.Session.Log.Hash());
                Assert.AreEqual(reference.Observable.Hash(), _host.Observable.Hash());
            }
        }

        [Test]
        public void Cancel_restart_return_and_a_large_time_jump_never_invent_a_visual_completion()
        {
            Identity(); var view = View();
            Assert.IsTrue(_host.Apply(Action("request_transport", ("to", "ct"), ("mode", "trolley"))).Accepted);
            Assert.IsTrue(_host.Apply(Action("cancel_transport")).Accepted); view.RenderNow(); StringAssert.Contains("CANCELLED", view.StatusText);
            Assert.IsTrue(_host.Apply(Action("request_transport", ("to", "ct"), ("mode", "wheelchair"))).Accepted);
            Until("IN_TRANSIT"); Assert.IsTrue(_host.Apply(Action("cancel_transport")).Accepted); view.RenderNow(); StringAssert.Contains("RETURNING", view.StatusText);
            _host.Tick(7200); view.RenderNow(); StringAssert.Contains("BACK", view.StatusText);
            Assert.AreEqual("patient_bay", _host.Observable.Location);
        }

        [Test]
        public void Wrong_destination_and_session_replacement_do_not_leak_an_old_journey()
        {
            Identity(); var view = View();
            Assert.IsFalse(_host.Apply(Action("request_transport", ("to", "not_a_location"), ("mode", "walk"))).Accepted);
            view.RenderNow(); StringAssert.Contains("No transport", view.StatusText);
            Assert.IsTrue(_host.Apply(Action("request_transport", ("to", "ct"), ("mode", "walk"))).Accepted);
            view.RenderNow(); StringAssert.Contains("REQUESTED", view.StatusText); view.ObserveApproach(Vector3.back, 1);
            _host.StartSession("VAR-S1-A", 43, SessionMode.Practice); view.RenderNow();
            StringAssert.Contains("No transport", view.StatusText); Assert.AreEqual(0, view.NoticeCount);
            Assert.IsFalse(_host.Apply(Action("request_transport", ("to", "ct"), ("mode", "walk"))).Accepted, "New identity gate still applies");
            view.RenderNow(); StringAssert.DoesNotContain("REQUESTED", view.StatusText);
        }

        [Test]
        public void Lab_shows_only_observed_order_status_keeps_patient_beside_bed_and_never_discloses_values()
        {
            Identity(); var view = View("", true);
            Assert.IsTrue(_host.Apply(Action("order_investigation", ("test", "lft"))).Accepted);
            view.RenderNow(); StringAssert.Contains("REQUESTED", view.StatusText);
            Assert.IsTrue(_host.Apply(Action("take_sample", ("test", "lft"), ("tube", "serum_gold"), ("label", "bedside"), ("send", "later"))).Accepted);
            view.RenderNow(); StringAssert.Contains("SAMPLE_TAKEN", view.StatusText);
            _host.Tick(1200); view.RenderNow(); StringAssert.Contains("SAMPLE_TAKEN", view.StatusText, "Waiting cannot dispatch a held sample");
            Assert.AreEqual("patient_bay", _host.Observable.Location); Assert.IsNull(_host.Observable.Transport);
            StringAssert.DoesNotContain("Bilirubin", view.StatusText);
            Assert.IsEmpty(_host.Observable.Investigations.Single().Results);
        }
    }

    public class DiagnosticDoorSceneTests
    {
        [SetUp] public void BypassLogin() => ProcSense.VR.Login.DemoLoginSettings.LoginRequiredOverride = false;
        [TearDown] public void RestoreLogin() => ProcSense.VR.Login.DemoLoginSettings.LoginRequiredOverride = null;
        [UnityTearDown] public IEnumerator Cleanup() => EditorXRTestEnvironment.Cleanup();

        [UnityTest]
        public IEnumerator Five_endpoints_block_capsule_and_teleport_and_show_information_without_emitting_actions()
        {
            yield return SceneManager.LoadSceneAsync("Boot");
            yield return EditorXRTestEnvironment.EnsureSimulator();
            var host = SimulationHost.Instance; host.AutoTick = false; host.SaveLogOnDestroy = false;
            var rooms = Object.FindAnyObjectByType<RoomLoader>();
            for (int i = 0; i < 300 && rooms.Busy; i++) yield return null;
            Assert.IsFalse(rooms.Busy);
            Assert.IsTrue(rooms.RequestRoom("Corridor"));
            for (int i = 0; i < 300 && rooms.Busy; i++) yield return null;
            Assert.IsFalse(rooms.Busy);
            var rig = Object.FindAnyObjectByType<XROrigin>();
            var controller = rig.GetComponent<CharacterController>();
            var teleport = rig.GetComponentInChildren<SafeTeleportProvider>();
            var views = Object.FindObjectsByType<DiagnosticDoorView>();
            Assert.AreEqual(5, views.Length);
            CollectionAssert.AreEquivalent(new[] { "ct", "ultrasound", "xray", "mri" }, views.SelectMany(v => v.DestinationIds));
            Assert.AreEqual(1, views.Count(v => v.Laboratory));
            string hash = host.Session.Log.Hash();
            foreach (var view in views)
            {
                Assert.IsNotNull(view.Presentation); Assert.IsNotNull(view.InsideAnchor); Assert.IsNotNull(view.OutsideAnchor);
                var door = view.GetComponent<RoomDoor>(); Assert.IsTrue(door.Restricted);
                Assert.IsEmpty(door.GetComponentsInChildren<UnityEngine.UI.Button>(true));
                controller.enabled = false; rig.transform.position = view.OutsideAnchor.position; controller.enabled = true;
                Physics.SyncTransforms();
                var outside = view.OutsideAnchor.position;
                Assert.IsEmpty(Physics.OverlapCapsule(outside + Vector3.up * .3f, outside + Vector3.up * 1.3f, .25f, ~0, QueryTriggerInteraction.Ignore)
                    .Where(c => !c.transform.IsChildOf(rig.transform)), door.name + " has an unobstructed approach, not furniture blocking it");
                Assert.IsTrue(Physics.Raycast(outside + Vector3.up * 1.2f, door.transform.forward, out var barrier, 2, ~0, QueryTriggerInteraction.Ignore));
                StringAssert.StartsWith("Wall", barrier.collider.name, "The room boundary is the barrier, not a furnishing");
                for (int i = 0; i < 20; i++) controller.Move(door.transform.forward * .1f);
                Assert.Less(Vector3.Dot(rig.transform.position - door.transform.position, door.transform.forward), 0, door.name + " blocks capsule walking");
                Assert.IsFalse(teleport.CanTeleport(view.InsideAnchor.position), door.name + " blocks inside teleport");
                Assert.IsFalse(teleport.QueueTeleportRequest(new TeleportRequest { destinationPosition = view.InsideAnchor.position }));
                door.Enter(); Assert.IsFalse(rooms.Busy); Assert.AreEqual("Corridor", rooms.CurrentRoom);
                view.RenderNow(); view.ObserveApproach(view.OutsideAnchor.position + Vector3.up * 1.6f, Time.unscaledTime);
                Assert.IsTrue(view.NoticeVisible); Assert.AreEqual(hash, host.Session.Log.Hash());
                Photograph(view);
            }
            var tablet = Object.FindAnyObjectByType<TabletView>();
            foreach (var verb in new[] { "ask_identity", "read_wristband", "open_notes", "confirm_identity" }) Assert.IsTrue(tablet.Press(verb));
            Assert.IsTrue(tablet.PressAction("request_transport", new Dictionary<string, string>
            {
                ["to"] = "ct", ["mode"] = "wheelchair", ["escort"] = "porter,nurse", ["monitoring"] = "portable", ["oxygen"] = "false", ["accompany"] = "false"
            }));
            Assert.AreEqual("REQUESTED", host.Observable.Transport.State);
            var ct = views.Single(v => v.DestinationIds.Contains("ct"));
            ct.RenderNow(); ct.ObserveApproach(ct.OutsideAnchor.position + Vector3.up * 1.6f, Time.unscaledTime);
            StringAssert.Contains("wheelchair", ct.StatusText); Photograph(ct, "-requested");
            host.Tick(7200); ct.RenderNow();
            Assert.AreEqual("BACK", host.Observable.Transport.State); StringAssert.Contains("BACK", ct.StatusText);
            Photograph(ct, "-returned");
        }

        private static void Photograph(DiagnosticDoorView view, string suffix = "")
        {
            var go = new GameObject("diagnostic door review camera"); var camera = go.AddComponent<Camera>();
            var rt = new RenderTexture(1400, 1000, 24); var texture = new Texture2D(1400, 1000, TextureFormat.RGB24, false); var previous = RenderTexture.active;
            try
            {
                camera.fieldOfView = 75; camera.nearClipPlane = .02f;
                camera.transform.position = view.transform.position - view.transform.forward * 2.1f + view.transform.right * .35f + Vector3.up * .25f;
                camera.transform.LookAt(view.transform.position + view.transform.right * .35f + Vector3.up * .15f);
                camera.targetTexture = rt; Canvas.ForceUpdateCanvases(); camera.Render(); RenderTexture.active = rt;
                texture.ReadPixels(new Rect(0, 0, 1400, 1000), 0, 0); texture.Apply();
                var dir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Logs", "DiagnosticDoorReview")); Directory.CreateDirectory(dir);
                File.WriteAllBytes(Path.Combine(dir, (view.Laboratory ? "laboratory" : view.DestinationIds.Single()) + suffix + ".png"), texture.EncodeToPNG());
            }
            finally { camera.targetTexture = null; RenderTexture.active = previous; Object.DestroyImmediate(rt); Object.DestroyImmediate(texture); Object.DestroyImmediate(go); }
        }
    }
}