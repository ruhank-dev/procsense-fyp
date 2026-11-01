#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using ProcSense.Core;
using ProcSense.Core.Actions;
using ProcSense.Core.Content;
using ProcSense.Core.Workflow;
using ProcSense.VR.Audio;
using ProcSense.VR.Boot;
using ProcSense.VR.Bridge;
using ProcSense.VR.Views;
using ProcSense.VR.Sync;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace ProcSense.Tests.PlayMode
{
    public sealed class NurseVisitSceneTests
    {
        private SimulationHost _host;
        private NurseWristbandController _nurse;
        private Session _reference;
        private float _timeScale;
        private bool? _login;
        private AudioClip _clip;
        private string _oldDirectory;
        private SessionSync _oldSync;
        private readonly List<GameObject> _objects = new List<GameObject>();
        private static string Repo => Path.GetFullPath(Path.Combine(Application.dataPath, "../.."));
        private sealed class Clips : IVoiceClips { public AudioClip Clip; public AudioClip Find(string id) => Clip; }
        [SetUp] public void Setup()
        {
            _oldDirectory = SimulationHost.SessionsDirectoryOverride; _oldSync = null;
            _timeScale = Time.timeScale; Time.timeScale = 3;
            _login = ProcSense.VR.Login.DemoLoginSettings.LoginRequiredOverride;
            ProcSense.VR.Login.DemoLoginSettings.LoginRequiredOverride = false;
        }
        [UnityTearDown] public IEnumerator Cleanup()
        {
            if (_nurse != null) _nurse.enabled = false;
            foreach (var go in _objects) if (go != null) Object.DestroyImmediate(go);
            _objects.Clear();
            if (SessionPresentation.Instance != null) SessionPresentation.Instance.enabled = false;
            if (_clip != null) Object.DestroyImmediate(_clip);
            SimulationHost.SessionsDirectoryOverride = _oldDirectory;
            if (_oldSync != null && SessionUploader.Instance != null) SessionUploader.Instance.Sync = _oldSync;
            Time.timeScale = _timeScale; ProcSense.VR.Login.DemoLoginSettings.LoginRequiredOverride = _login;
            yield return EditorXRTestEnvironment.Cleanup();
        }
        private IEnumerator Boot(string id)
        {
            yield return SceneManager.LoadSceneAsync("Boot"); yield return EditorXRTestEnvironment.EnsureSimulator();
            _host = SimulationHost.Instance; Assert.IsNotNull(_host);
            _host.AutoTick = false; _host.SaveLogOnDestroy = false;
            var reader = new CompositeContentReader(new FileContentReader(Path.Combine(Repo, "content")), new FileContentReader(Path.Combine(Repo, "tests/fixtures")));
            _host.StartSession(id, 42, SessionMode.Practice, reader);
            _reference = new Session(_host.Content.Cases[id], 42, SessionMode.Practice, Subsystems.Default(_host.Content));
            var rig = Object.FindAnyObjectByType<XROrigin>(); Assert.IsNotNull(rig);
            rig.transform.position = new Vector3(-1.7f, 0, -1.0f);
            for (int i = 0; i < 5; i++) yield return null;
            _nurse = Object.FindAnyObjectByType<NurseWristbandController>(); Assert.IsNotNull(_nurse);
            _nurse.Sync(); SessionPresentation.Instance.Sync();
            _clip = AudioClip.Create("Silent nurse verification clip", 6615, 1, 44100, false);
            SessionPresentation.Instance.Voice.Clips = new Clips { Clip = _clip };
            Assert.AreEqual(1, Object.FindObjectsByType<NurseWristbandController>().Length);
        }
        private ActionOutcome Act(string verb, params (string key, string value)[] args)
        {
            var action = new SimAction(verb, "patient", Channel.Vr, args.ToDictionary(p => p.key, p => p.value));
            var expected = _reference.Apply(action); var actual = _host.Apply(action);
            Assert.AreEqual(expected.Accepted, actual.Accepted); Parity(); return actual;
        }
        private void Parity()
        {
            Assert.AreEqual(_reference.Log.Hash(), _host.Session.Log.Hash());
            Assert.AreEqual(_reference.Observable.Hash(), _host.Observable.Hash());
        }
        private void PrepareMismatch()
        {
            foreach (var verb in new[] { "ask_identity", "open_notes", "read_wristband" }) Assert.IsTrue(Act(verb).Accepted);
            Assert.IsFalse(Act("confirm_identity").Accepted); Assert.IsTrue(Act("report_mismatch").Accepted);
        }
        private IEnumerator Until(Func<bool> ready, float seconds = 60)
        {
            float end = Time.realtimeSinceStartup + seconds;
            while (!ready() && Time.realtimeSinceStartup < end) { yield return null; Parity(); }
            Assert.IsTrue(ready(), "Nurse did not reach expected state: " + _nurse.Phase + "; " + _nurse.Status +
                "; position=" + (_nurse.Actor != null ? _nurse.Actor.transform.position.ToString() : "none"));
        }

        [UnityTest] public IEnumerator Mismatch_walk_check_reach_and_exit_follow_the_real_wrist_without_clinical_writes()
        {
            yield return Boot("TEST-ID-MISMATCH"); PrepareMismatch();
            Assert.AreEqual("DONE", _host.Observable.Nurse, "Core finishes in the request, before the cosmetic visit");
            Assert.AreEqual(NurseVisitPhase.Entering, _nurse.Phase);
            Assert.Greater(_nurse.Actor.transform.position.x, 3.8f, "Starts outside the bay");
            Assert.IsNull(_host.Observable.Wristband, "No identifiers disclosed by the animation");
            var voice = SessionPresentation.Instance.Voice;
            var seen = new HashSet<NurseVisitPhase>(); bool reached = false; bool checkedRecords = false;
            float deadline = Time.realtimeSinceStartup + 60;
            while ((_nurse.Phase != NurseVisitPhase.Idle || !_nurse.BandPlaced) && Time.realtimeSinceStartup < deadline)
            {
                seen.Add(_nurse.Phase); Parity();
                Assert.IsTrue(Object.FindObjectsByType<NurseSubtitleView>().All(v => v.Canvas == null || !v.Canvas.gameObject.activeSelf), "Shared presenter must not duplicate legacy nurse captions");
                var chip = Object.FindAnyObjectByType<IdentityStatusView>(); Assert.AreEqual(_nurse.CoreStep, chip.NurseStep);
                if (_nurse.Actor != null && _nurse.BandPlaced && !reached)
                {
                    Assert.Less(Vector3.Distance(_nurse.Actor.RightHand.position, _nurse.Wrist.position), .06f, "Reach must meet the physical wrist");
                    Photograph("nurse-replacing.png"); reached = true;
                }
                if (_nurse.Phase == NurseVisitPhase.CheckingRecords && !checkedRecords) { Photograph("nurse-checking.png"); checkedRecords = true; }
                yield return null;
            }
            Assert.IsTrue(reached); Assert.AreEqual(NurseVisitPhase.Idle, _nurse.Phase);
            CollectionAssert.IsSubsetOf(new[] { NurseVisitPhase.Entering, NurseVisitPhase.CheckingRecords, NurseVisitPhase.ReplacingBand, NurseVisitPhase.Resolved, NurseVisitPhase.Leaving }, seen);
            Assert.IsNull(_nurse.Actor); Assert.AreEqual(1, _nurse.VisitsStarted);
            var stateLines = voice.Catalog.ForCase("TEST-ID-MISMATCH").Where(l => l.Event.TryGetValue("nurse_state", out var s) &&
                (s as string != "REPLACING" || !l.Event.TryGetValue("effect", out var e) || (string)e == "wristband_replaced")).Select(l => l.Id).ToList();
            CollectionAssert.AreEquivalent(stateLines, voice.Played.Where(id => stateLines.Contains(id)).ToList());
            Assert.IsTrue(stateLines.All(id => voice.Played.Count(p => p == id) == 1), "No duplicated state narration");
            Assert.IsNull(_host.Observable.Wristband); Assert.IsFalse(Act("confirm_identity").Accepted);
            Assert.IsTrue(Act("read_wristband").Accepted); Assert.IsTrue(Act("confirm_identity").Accepted); Parity();
        }

        [UnityTest] public IEnumerator Missing_band_rejection_and_allergy_application_wait_for_the_accepted_visual_swap()
        {
            yield return Boot("TEST-ID-MISSING-BAND-ALLERGY");
            Assert.IsTrue(Act("ask_identity").Accepted); Assert.IsFalse(Act("read_wristband").Accepted);
            Assert.IsFalse(Act("request_wristband").Accepted);
            Assert.IsNull(_nurse.Actor); Assert.AreEqual(0, _nurse.VisitsStarted);
            yield return null;
            Assert.IsTrue(_nurse.Wrist.GetComponentsInChildren<Renderer>(true).All(r => !r.enabled));
            Assert.IsTrue(Act("open_notes").Accepted); Assert.IsTrue(Act("request_wristband").Accepted);
            Assert.IsTrue(_host.Observable.AllergyBand); Assert.IsFalse(_nurse.BandPlaced);
            yield return Until(() => _nurse.BandPlaced);
            Assert.IsTrue(_nurse.Wrist.GetComponentsInChildren<Renderer>(true).Any(r => r.enabled));
            Assert.IsNotNull(GameObject.Find("Prototype applied allergy band"));
            yield return Until(() => _nurse.Phase == NurseVisitPhase.Idle && _nurse.Actor == null);
            Photograph("nurse-allergy.png", true);
            Assert.IsNull(_host.Observable.Wristband);
            Assert.IsTrue(Act("read_wristband").Accepted); Assert.IsTrue(Act("confirm_identity").Accepted); Parity();
        }

        [UnityTest] public IEnumerator Blocked_doorway_waits_and_transport_cannot_release_the_nurses_request()
        {
            yield return Boot("TEST-ID-MISMATCH"); PrepareMismatch();
            var door = Object.FindObjectsByType<SwingDoor>().Single(d => d.Connection == "BayCorridor");
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube); _objects.Add(cube);
            cube.name = "Nurse obstruction fixture"; cube.transform.position = new Vector3(3.2f, .9f, -.8f); cube.transform.localScale = new Vector3(.25f, 1.8f, 1.5f);
            Physics.SyncTransforms();
            yield return Until(() => _nurse.Blocked, 15);
            Assert.IsTrue(door.NpcPassageRequested);
            Object.FindAnyObjectByType<PatientTransportView>().RenderNow();
            Assert.IsTrue(door.NpcPassageRequested, "Transport cleanup must release only its own request");
            Assert.Greater(_nurse.Actor.transform.position.x, 3.25f);
            cube.SetActive(false); Physics.SyncTransforms();
            yield return Until(() => _nurse.Phase == NurseVisitPhase.CheckingRecords);
            Parity();
            var other = new GameObject("Other door owner"); _objects.Add(other); door.SetNpcPassage(other, true);
            _nurse.enabled = false; Assert.IsTrue(door.NpcPassageRequested);
            door.SetNpcPassage(other, false); Assert.IsFalse(door.NpcPassageRequested);
        }

        [UnityTest] public IEnumerator Disable_and_new_session_cancel_only_owned_speech_and_clear_the_status()
        {
            yield return Boot("TEST-ID-MISMATCH"); PrepareMismatch(); yield return null;
            var voice = SessionPresentation.Instance.Voice;
            var unrelated = new VoiceLine { Id = "NUR-WRISTBAND-CALLED", Speaker = "patient", Text = "Unrelated patient sentence." };
            voice.Say(unrelated);
            _nurse.enabled = false;
            Assert.IsNull(_nurse.Actor); Assert.IsTrue(ReferenceEquals(voice.Current, unrelated) || voice.Queue.Contains(unrelated), "Cancellation uses line ownership, not a shared catalogue id");
            _nurse.enabled = true; _nurse.Sync(); Assert.IsNull(_nurse.Actor, "Do not replay a completed Core request on enable");
            _host.StartSession("VAR-S1-A", 44, SessionMode.Practice);
            yield return null; _nurse.Sync();
            Assert.AreEqual(0, _nurse.VisitsStarted); Assert.IsNull(_nurse.Actor);
            Assert.IsEmpty(Object.FindAnyObjectByType<IdentityStatusView>().NurseStep);
            Assert.AreEqual(0, _host.Session.Log.Count);
        }

        [UnityTest] public IEnumerator Missing_route_recovers_without_another_request_or_clock_change()
        {
            yield return Boot("TEST-ID-MISMATCH");
            var entry = _nurse.Entry.position;
            _nurse.Entry.position = new Vector3(80, 0, 80); PrepareMismatch();
            for (int i = 0; i < 10; i++) { yield return null; Parity(); }
            Assert.IsNull(_nurse.Actor); Assert.IsTrue(_nurse.Blocked);
            _nurse.Entry.position = entry;
            yield return Until(() => _nurse.Actor != null, 10);
            Assert.AreEqual(1, _nurse.VisitsStarted); Parity();
        }

        [UnityTest] public IEnumerator A_departing_patient_pauses_the_cosmetic_visit_instead_of_treating_an_empty_bed()
        {
            yield return Boot("TEST-ID-MISMATCH"); PrepareMismatch();
            Assert.IsTrue(Act("read_wristband").Accepted); Assert.IsTrue(Act("confirm_identity").Accepted);
            Assert.IsTrue(Act("request_transport", ("to","ct"), ("mode","trolley"), ("escort","porter,nurse"), ("monitoring","none"), ("oxygen","false"), ("accompany","false")).Accepted);
            for (int i = 0; i < 120 && _host.Observable.Transport?.State != "IN_TRANSIT"; i++) { _host.Tick(15); _reference.Tick(15); }
            Assert.AreEqual("IN_TRANSIT", _host.Observable.Transport.State);
            yield return null;
            Assert.IsNull(_nurse.Actor); Assert.AreEqual(NurseVisitPhase.WaitingForPatient, _nurse.Phase);
            for (int i = 0; i < 10; i++) { yield return null; Parity(); }
            for (int i = 0; i < 2000 && _host.Observable.Transport.State != "BACK"; i++) { _host.Tick(15); _reference.Tick(15); }
            Assert.AreEqual("BACK", _host.Observable.Transport.State); Parity();
            yield return Until(() => _nurse.Actor != null, 10);
            Assert.AreEqual(1, _nurse.VisitsStarted, "Patient return resumes the same cosmetic request");
            yield return Until(() => _nurse.BandPlaced);
            Assert.Less(Vector3.Distance(_nurse.Actor.RightHand.position, _nurse.Wrist.position), .06f); Parity();
        }

        [UnityTest] public IEnumerator Finish_cancels_the_visit_and_speech_without_changing_the_clinical_result()
        {
            yield return Boot("TEST-ID-MISMATCH"); PrepareMismatch(); yield return null;
            var directory = Path.GetFullPath(Path.Combine(Application.dataPath, "../Logs/NurseVisit/Sessions/" + Guid.NewGuid().ToString("N")));
            SimulationHost.SessionsDirectoryOverride = directory; _oldSync = SessionUploader.Instance.Sync;
            SessionUploader.Instance.Sync = new SessionSync(new UploadQueue(Path.Combine(directory, "outbox"), SessionUploader.UnixNow), () => null, SessionUploader.UnixNow);
            string hash = _host.Session.Log.Hash(), state = _host.Observable.Hash();
            Assert.IsNotNull(_host.EndSession()); yield return null;
            Assert.IsNull(_nurse.Actor); Assert.IsEmpty(Object.FindObjectsByType<NurseVisitActor>());
            Assert.IsEmpty(Object.FindAnyObjectByType<IdentityStatusView>().NurseStep, "Finished sessions cannot resume an obsolete fallback visit");
            Assert.AreEqual(hash, _host.Session.Log.Hash()); Assert.AreEqual(state, _host.Observable.Hash());
            Assert.IsEmpty(Object.FindObjectsByType<VoicePlayer>());
        }
        [UnityTest] public IEnumerator An_unreachable_wrist_waits_then_resumes_without_claiming_a_swap()
        {
            yield return Boot("TEST-ID-MISMATCH"); PrepareMismatch();
            yield return Until(() => _nurse.Phase == NurseVisitPhase.CheckingRecords);
            var position = _nurse.Wrist.localPosition;
            _nurse.Wrist.localPosition += Vector3.up * 2;
            yield return Until(() => _nurse.Phase == NurseVisitPhase.ReplacingBand && _nurse.Blocked);
            Assert.IsFalse(_nurse.BandPlaced); Assert.AreEqual("Waiting for reachable wrist", _nurse.Status);
            for (int i = 0; i < 20; i++) { yield return null; Parity(); Assert.IsFalse(_nurse.BandPlaced); }
            _nurse.Wrist.localPosition = position;
            yield return Until(() => _nurse.BandPlaced);
            Assert.Less(Vector3.Distance(_nurse.Actor.RightHand.position, _nurse.Wrist.position), .06f);
            Parity();
        }
        [UnityTest] public IEnumerator Host_replacement_removes_the_old_actor_and_binds_only_the_new_session()
        {
            yield return Boot("TEST-ID-MISMATCH"); PrepareMismatch(); yield return null;
            Assert.IsNotNull(_nurse.Actor);
            Object.DestroyImmediate(_host.gameObject); _nurse.Sync();
            Assert.IsNull(_nurse.Actor); Assert.IsNull(_nurse.Head);
            var go = new GameObject("Replacement nurse host fixture"); _objects.Add(go);
            _host = go.AddComponent<SimulationHost>(); _host.AutoTick = false; _host.SaveLogOnDestroy = false;
            _nurse.Sync(); Assert.IsNull(_nurse.Actor);
            var reader = new CompositeContentReader(new FileContentReader(Path.Combine(Repo, "content")), new FileContentReader(Path.Combine(Repo, "tests/fixtures")));
            _host.StartSession("TEST-ID-MISMATCH",42,SessionMode.Practice,reader);
            _reference = new Session(_host.Content.Cases["TEST-ID-MISMATCH"],42,SessionMode.Practice,Subsystems.Default(_host.Content));
            yield return null; _nurse.Sync(); SessionPresentation.Instance.Sync();
            SessionPresentation.Instance.Voice.Clips = new Clips { Clip = _clip };
            Assert.AreEqual(0, _nurse.VisitsStarted); Assert.IsNull(_nurse.Actor);
            PrepareMismatch(); yield return null;
            Assert.AreEqual(1, _nurse.VisitsStarted); Assert.IsNotNull(_nurse.Actor);
            Assert.AreSame(_nurse.Head, SessionPresentation.Instance.Voice.Heads["nurse"]); Parity();
        }
        private void Photograph(string name, bool wristFocus = false)
        {
            var go = new GameObject("Nurse evidence camera"); var camera = go.AddComponent<Camera>();
            camera.transform.position = new Vector3(0, 1.65f, -.6f);
            camera.transform.LookAt(wristFocus ? _nurse.Wrist.position : _nurse.Actor.transform.position + Vector3.up * .85f); camera.fieldOfView = 65; camera.nearClipPlane = .05f;
            if (wristFocus) { camera.transform.position = _nurse.Wrist.position + new Vector3(.45f,.4f,-.65f); camera.transform.LookAt(_nurse.Wrist.position); camera.fieldOfView = 50; }
            var rt = new RenderTexture(1400, 1100, 24); var old = RenderTexture.active; var image = new Texture2D(1400, 1100, TextureFormat.RGB24, false);
            try { camera.targetTexture = rt; Canvas.ForceUpdateCanvases(); camera.Render(); RenderTexture.active = rt; image.ReadPixels(new Rect(0,0,1400,1100),0,0); image.Apply(); File.WriteAllBytes(Snapshots.Out(name), image.EncodeToPNG()); }
            finally { camera.targetTexture = null; RenderTexture.active = old; Object.DestroyImmediate(image); rt.Release(); Object.DestroyImmediate(rt); Object.DestroyImmediate(go); }
        }
    }
}
#endif
