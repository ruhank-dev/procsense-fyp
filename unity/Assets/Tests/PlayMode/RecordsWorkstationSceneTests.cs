using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using ProcSense.Core;
using ProcSense.Core.Actions;
using ProcSense.VR.Bridge;
using ProcSense.VR.Records;
using ProcSense.VR.Records.Folder;
using ProcSense.VR.UI.Results;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.UI;

namespace ProcSense.Tests.PlayMode
{
    public sealed class RecordsWorkstationSceneTests
    {
        private SimulationHost _host;
        private RecordsWorkstation _station;
        private GameObject _testHand;
        [SetUp] public void BypassLogin() => ProcSense.VR.Login.DemoLoginSettings.LoginRequiredOverride = false;
        [TearDown] public void RestoreLogin() => ProcSense.VR.Login.DemoLoginSettings.LoginRequiredOverride = null;
        [UnityTearDown] public IEnumerator Cleanup()
        {
            if (_testHand != null) Object.DestroyImmediate(_testHand);
            yield return EditorXRTestEnvironment.Cleanup();
        }
        private IEnumerator Boot(string caseId = "VAR-S1-A")
        {
            yield return SceneManager.LoadSceneAsync("Boot");
            yield return EditorXRTestEnvironment.EnsureSimulator();
            _host = SimulationHost.Instance;
            _host.AutoTick = false; _host.SaveLogOnDestroy = false;
            _host.StartSession(caseId, 42, SessionMode.Practice);
            _station = Object.FindObjectsByType<RecordsWorkstation>(FindObjectsInactive.Exclude).Single();
            _station.Sync();
            yield return null;
        }
        private ActionOutcome Do(string verb, params (string key, string value)[] p) =>
            _host.Apply(new SimAction(verb, "patient", Channel.Vr, p.ToDictionary(x => x.key, x => x.value)));
        private void Identify()
        {
            foreach (var verb in new[] { "introduce_self", "ask_identity", "read_wristband", "open_notes", "confirm_identity" })
                Assert.IsTrue(Do(verb).Accepted, verb);
        }

        [UnityTest]
        public IEnumerator Saved_workstation_rebuilds_only_owned_views_without_clinical_actions()
        {
            yield return Boot();
            Assert.AreEqual("PatientBay", _station.gameObject.scene.name);
            var hash = _host.Session.Log.Hash(); var state = _host.Observable.Hash();
            var old = _station.Emr;
            _station.Sync(); _station.Sync();
            Assert.AreSame(old, _station.Emr, "ordinary snapshots retain typed text and selection");
            foreach (var canvas in _station.GetComponentsInChildren<Canvas>())
            {
                Assert.AreEqual(RenderMode.WorldSpace, canvas.renderMode);
                Assert.IsNotNull(canvas.GetComponent<TrackedDeviceGraphicRaycaster>(), canvas.name);
            }
            Assert.AreEqual(hash, _host.Session.Log.Hash()); Assert.AreEqual(state, _host.Observable.Hash());
            Assert.IsTrue(_station.Emr.Choose(_station.Emr.Shown.First(p => p.IsCasePatient).Id));
            _host.StartSession("STK-S1-C", 43, SessionMode.Practice);
            hash = _host.Session.Log.Hash(); state = _host.Observable.Hash();
            _station.Sync();
            Assert.IsFalse(old.gameObject.activeInHierarchy, "old buttons disabled before deferred destruction");
            Assert.AreNotSame(old, _station.Emr);
            Assert.IsEmpty(_station.Emr.SelectedPatient);
            Assert.IsTrue(_station.Emr.Shown.Any(p => p.IsCasePatient && p.Id == "STK-S1-C" && p.Name.Contains("Helena Novak")));
            Assert.IsEmpty(_station.Tray.Printouts);
            yield return null;
            Assert.AreEqual(1, Object.FindObjectsByType<ProcSense.VR.Records.Emr.EmrShell>(FindObjectsInactive.Exclude).Length);
            Assert.AreEqual(1, Object.FindObjectsByType<ResultsView>(FindObjectsInactive.Exclude).Length);
            Assert.AreEqual(1, Object.FindObjectsByType<PhoneView>(FindObjectsInactive.Exclude).Length);
            Assert.AreEqual(hash, _host.Session.Log.Hash()); Assert.AreEqual(state, _host.Observable.Hash());
            _station.enabled = false;
            Assert.IsNull(_station.Emr); Assert.IsNull(_station.Tray);
            yield return null;
            _station.enabled = true; _station.Sync();
            Assert.IsNotNull(_station.Emr);
            Assert.AreEqual(hash, _host.Session.Log.Hash());
            // No incomplete EMR is built while a replacement Core host has no case.
            Object.DestroyImmediate(_host.gameObject);
            _station.Sync(); Assert.IsNull(_station.Emr);
            _host = new GameObject("replacement host").AddComponent<SimulationHost>();
            _host.AutoTick = false; _host.SaveLogOnDestroy = false;
            _station.Sync(); Assert.IsNull(_station.Emr);
            _host.StartSession("VAR-S1-A", 44, SessionMode.Practice);
            hash = _host.Session.Log.Hash(); state = _host.Observable.Hash();
            _station.Sync(); Assert.IsNotNull(_station.Emr);
            Assert.IsTrue(_station.Emr.Choose(_station.Emr.Shown.First(p => p.IsCasePatient).Id));
            Assert.IsTrue(_station.Emr.OpenTab("labs"), "section controls exist after delayed Core startup");
            Assert.AreEqual(hash, _host.Session.Log.Hash()); Assert.AreEqual(state, _host.Observable.Hash());
        }

        [UnityTest]
        public IEnumerator Saved_EMR_results_and_real_folder_file_a_delivered_report()
        {
            yield return Boot(); Identify();
            var emr = _station.Emr;
            Assert.IsTrue(emr.Find("khan").Accepted);
            Assert.IsTrue(emr.Choose(emr.Shown.First(p => !p.IsCasePatient).Id));
            Assert.IsFalse(emr.OpenTab("labs"), "a near-miss cannot show the case record");
            Assert.IsTrue(emr.Choose(emr.Shown.First(p => p.IsCasePatient).Id));
            Assert.IsTrue(emr.OpenTab("imaging")); Assert.IsNotEmpty(emr.Documents);
            Assert.IsTrue(emr.OpenDocument(emr.Documents[0].Id).Accepted);
            Assert.IsTrue(Do("order_investigation", ("test", "fbc")).Accepted);
            Assert.IsTrue(Do("take_sample", ("test", "fbc"), ("tube", "edta_purple")).Accepted);
            for (int i = 0; i < 24 && _host.Session.Records.Desk.Count == 0; i++) _host.Tick(15 * 60);
            _station.Tray.Sync();
            var paper = _station.Tray.Printouts.Single();
            Assert.IsTrue(_station.Results.Select("fbc"));
            Assert.IsTrue(_station.Results.View().Accepted);
            Assert.IsTrue(_station.Results.Acknowledge().Accepted);
            Assert.Greater(paper.transform.position.y, .9975f);
            Assert.That(paper.transform.position.x, Is.InRange(1.28f, 1.385f));
            Assert.That(paper.transform.position.z, Is.InRange(-1.78f, -1.62f));
            var folder = Object.FindObjectsByType<FolderController>(FindObjectsInactive.Exclude).Single();
            folder.GetComponent<ProcSense.VR.ActionEmitters.NotesFolderInteraction>().SyncNow();
            folder.Refresh();
            Photograph("bay-records-workstation.png");
            string documentId = paper.DocumentId;
            folder.Pocket.interactionManager.SelectEnter((IXRSelectInteractor)folder.Pocket, (IXRSelectInteractable)paper.GetComponent<XRGrabInteractable>());
            Assert.IsEmpty(folder.LastRejection); Assert.IsEmpty(_host.Session.Records.Desk);
            Assert.IsTrue(folder.Pages.Any(p => p.DocumentId == documentId));
            _station.Tray.Sync(); yield return null;
            Assert.IsEmpty(_station.Tray.Printouts);
            Assert.AreEqual(1, _host.Session.Log.Entries.Count(e => e.Verb == "file_document"), "one socket filing action");
        }

        [UnityTest]
        public IEnumerator Saved_phone_preserves_Core_readback_and_acknowledgement_gates()
        {
            yield return Boot("STK-S1-C"); Identify();
            Assert.IsTrue(Do("order_investigation", ("test", "u_and_e")).Accepted);
            Assert.IsTrue(Do("take_sample", ("test", "u_and_e"), ("tube", "serum_gold")).Accepted);
            _host.Tick(3600);
            var phone = _station.Phone; var results = _station.Results;
            phone.RenderNow(); Assert.IsTrue(phone.Ringing);
            Assert.IsTrue(results.Select("u_and_e")); Assert.IsTrue(results.View().Accepted);
            Assert.AreEqual("CALL_NOT_ANSWERED", results.Acknowledge("treat_and_repeat", "phoned").RejectReason);
            Assert.IsTrue(phone.Answer().Accepted); Assert.IsTrue(phone.ConfirmIdentity().Accepted);
            var choices = phone.Choices.ToList();
            phone.ReadBack(choices.FindIndex(c => !c.Exact));
            Assert.AreEqual("IDENTITY_CONFIRMED", phone.State);
            results.RenderNow(); Assert.IsFalse(results.AcknowledgeEnabled);
            phone.ReadBack(phone.Choices.ToList().FindIndex(c => c.Exact));
            Assert.AreEqual("READ_BACK", phone.State);
            Assert.IsTrue(results.Acknowledge("treat_and_repeat", "Potassium 6.6 read back; treating and repeating").Accepted);
            phone.RenderNow(); Assert.IsFalse(phone.Ringing);
            Photograph("bay-critical-phone.png");
        }

        [UnityTest]
        public IEnumerator Held_printout_is_removed_when_the_session_changes()
        {
            yield return Boot(); Identify();
            Assert.IsTrue(Do("order_investigation", ("test", "fbc")).Accepted);
            Assert.IsTrue(Do("take_sample", ("test", "fbc"), ("tube", "edta_purple")).Accepted);
            for (int i = 0; i < 24 && _host.Session.Records.Desk.Count == 0; i++) _host.Tick(15 * 60);
            _station.Tray.Sync();
            var paper = _station.Tray.Printouts.Single();
            var grab = paper.GetComponent<XRGrabInteractable>();
            _testHand = new GameObject("Test hand for held-report reset");
            _testHand.transform.position = paper.transform.position;
            _testHand.AddComponent<SphereCollider>().isTrigger = true;
            _testHand.AddComponent<Rigidbody>().isKinematic = true;
            var ray = _testHand.AddComponent<XRDirectInteractor>();
            ray.interactionManager = grab.interactionManager;
            ray.interactionManager.SelectEnter((IXRSelectInteractor)ray, (IXRSelectInteractable)grab);
            Assert.IsTrue(grab.isSelected);
            Assert.IsNull(paper.transform.parent, "XRI reparents held paper out of its tray");
            _host.StartSession("STK-S1-C", 45, SessionMode.Practice);
            var log = _host.Session.Log.Hash(); var state = _host.Observable.Hash();
            _station.Sync();
            Assert.IsFalse(paper.gameObject.activeInHierarchy, "old held paper cannot be filed into the new case");
            yield return null;
            Assert.IsTrue(paper == null);
            Assert.IsEmpty(_station.Tray.Printouts);
            Assert.AreEqual(log, _host.Session.Log.Hash()); Assert.AreEqual(state, _host.Observable.Hash());
        }

        private void Photograph(string name)
        {
            Canvas.ForceUpdateCanvases();
            var go = new GameObject("Workstation evidence camera"); var camera = go.AddComponent<Camera>();
            go.transform.position = new Vector3(1.35f, 1.55f, -.35f);
            go.transform.rotation = Quaternion.LookRotation(new Vector3(1.40f, 1.43f, -1.8f) - go.transform.position);
            camera.fieldOfView = 68; camera.nearClipPlane = .05f;
            var target = new RenderTexture(1600, 1200, 24);
            var previous = RenderTexture.active;
            var picture = new Texture2D(1600, 1200, TextureFormat.RGB24, false);
            try
            {
                camera.targetTexture = target; camera.Render(); RenderTexture.active = target;
                picture.ReadPixels(new Rect(0, 0, 1600, 1200), 0, 0); picture.Apply();
                File.WriteAllBytes(Snapshots.Out(name), picture.EncodeToPNG());
            }
            finally
            {
                camera.targetTexture = null; RenderTexture.active = previous;
                Object.DestroyImmediate(picture); Object.DestroyImmediate(target); Object.DestroyImmediate(go);
            }
        }
    }
}
