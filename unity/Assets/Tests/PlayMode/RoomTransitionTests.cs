using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using ProcSense.VR.Boot;
using ProcSense.VR.Bridge;
using ProcSense.VR.Views;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace ProcSense.Tests.PlayMode
{
    public class RoomTransitionTests
    {
        [UnityTest]
        public IEnumerator Direct_room_play_loads_one_camera_session_and_simulator_and_can_navigate()
        {
            foreach (string room in new[] { "PatientBay", "Corridor", "OT" })
            {
                yield return SceneManager.LoadSceneAsync(room, LoadSceneMode.Single);
                float end = Time.realtimeSinceStartup + 10;
                while ((!SceneManager.GetSceneByName("DevOnly").isLoaded || Camera.allCamerasCount == 0) && Time.realtimeSinceStartup < end) yield return null;
                yield return null;
                Assert.IsNotNull(SimulationHost.Instance);
                Assert.IsNotNull(SimulationHost.Instance.Session);
                Assert.AreEqual(1, Camera.allCamerasCount, room);
                Assert.AreEqual(1, Object.FindObjectsByType<XROrigin>().Length, room);
                Assert.AreEqual(1, Object.FindObjectsByType<EventSystem>().Length, room);
                var loader = Object.FindAnyObjectByType<RoomLoader>();
                Assert.AreEqual(room, loader.CurrentRoom);
                yield return Travel(loader, room == "Corridor" ? "PatientBay" : "Corridor");
            }
        }

        private static IEnumerator Boot()
        {
            yield return SceneManager.LoadSceneAsync("Boot");
            for (int i = 0; i < 30; i++) yield return null;
        }

        private static IEnumerator Travel(RoomLoader loader, string room)
        {
            Assert.IsTrue(loader.RequestRoom(room), loader.LastError);
            Assert.IsFalse(loader.RequestRoom(room), "reject duplicate requests during transition");
            float end = Time.realtimeSinceStartup + 15;
            while (loader.Busy && Time.realtimeSinceStartup < end) yield return null;
            Assert.IsFalse(loader.Busy, "transition timed out");
            Assert.AreEqual(room, loader.CurrentRoom, loader.LastError);
            Assert.IsEmpty(loader.LastError);
        }

        [UnityTest]
        public IEnumerator Repeated_round_trips_preserve_identity_sensor_session_and_service_ownership()
        {
            yield return Boot();
            var host = SimulationHost.Instance;
            host.AutoTick = false;
            var tablet = Object.FindAnyObjectByType<TabletView>();
            foreach (var verb in new[] { "introduce_self", "ask_identity", "read_wristband", "open_notes", "confirm_identity" }) Assert.IsTrue(tablet.Press(verb));
            tablet.Press("attach_sensor", "sensor", "spo2");
            var session = host.Session;
            var hash = host.Observable.Hash();
            var log = session.Log.Hash();
            var rig = Object.FindAnyObjectByType<XROrigin>();
            var loader = Object.FindAnyObjectByType<RoomLoader>();
            Assert.AreEqual("Sys_Core", rig.gameObject.scene.name);
            for (int cycle = 0; cycle < 2; cycle++)
            {
                foreach (string room in new[] { "Corridor", "OT", "Corridor", "PatientBay" })
                {
                    yield return Travel(loader, room);
                    Assert.AreSame(host, SimulationHost.Instance);
                    Assert.AreSame(session, host.Session);
                    Assert.AreSame(rig, Object.FindAnyObjectByType<XROrigin>());
                    Assert.AreEqual(hash, host.Observable.Hash());
                    Assert.AreEqual(log, session.Log.Hash(), "navigation cannot issue clinical actions");
                    Assert.AreEqual(1, Object.FindObjectsByType<SimulationHost>().Length);
                    Assert.AreEqual(1, Object.FindObjectsByType<XROrigin>().Length);
                    Assert.AreEqual(1, Object.FindObjectsByType<EventSystem>().Length);
                    Assert.AreEqual(1, Camera.allCamerasCount);
                    Assert.AreEqual(1, new[] { "PatientBay", "Corridor", "OT" }.Count(n => SceneManager.GetSceneByName(n).isLoaded));
                    Assert.AreEqual(0, rig.Camera.GetComponentInChildren<CanvasGroup>().alpha);
                }
                foreach (var view in Object.FindObjectsByType<StateView>()) view.RenderNow();
                Assert.IsTrue(host.Observable.IdentityConfirmed);
                Assert.AreNotEqual("--", Object.FindAnyObjectByType<MonitorView>().ValueText("SpO2"));
            }
        }

        [UnityTest]
        public IEnumerator Restricted_routes_are_rejected_and_diagnostic_doors_are_solid()
        {
            yield return Boot();
            var loader = Object.FindAnyObjectByType<RoomLoader>();
            Assert.IsFalse(loader.RequestRoom("OT"));
            Assert.IsFalse(loader.RequestRoom("Sys_Core"));
            Assert.IsFalse(loader.RequestRoom("CT"));
            Assert.IsFalse(loader.Busy);
            yield return Travel(loader, "Corridor");
            var doors = Object.FindObjectsByType<RoomDoor>().Where(d => d.Restricted).ToArray();
            Assert.AreEqual(2, doors.Length);
            foreach (var door in doors)
            {
                door.Enter();
                Assert.IsFalse(loader.Busy);
                Assert.IsTrue(Physics.Raycast(door.transform.position - door.transform.forward * 0.3f,
                    door.transform.forward, 1f, ~0, QueryTriggerInteraction.Ignore), "solid wall behind restricted sign");
            }
        }

        [UnityTest]
        public IEnumerator Blocked_arrival_rolls_back_and_restores_movement()
        {
            yield return Boot();
            var loader = Object.FindAnyObjectByType<RoomLoader>();
            var rig = Object.FindAnyObjectByType<XROrigin>();
            var start = rig.transform.position;
            void Block(Scene scene, LoadSceneMode mode)
            {
                if (scene.name != "Corridor") return;
                var obstacle = GameObject.CreatePrimitive(PrimitiveType.Cube);
                SceneManager.MoveGameObjectToScene(obstacle, scene);
                obstacle.transform.position = new Vector3(-0.8f, 1, -3);
            }
            SceneManager.sceneLoaded += Block;
            try
            {
                Assert.IsTrue(loader.RequestRoom("Corridor"));
                float end = Time.realtimeSinceStartup + 15;
                while (loader.Busy && Time.realtimeSinceStartup < end) yield return null;
                Assert.IsFalse(loader.Busy);
                Assert.AreEqual("PatientBay", loader.CurrentRoom);
                Assert.IsFalse(SceneManager.GetSceneByName("Corridor").isLoaded);
                Assert.AreEqual(start, rig.transform.position);
                StringAssert.Contains("blocked", loader.LastError);
                Assert.IsTrue(rig.GetComponent<CharacterController>().enabled);
                Assert.IsTrue(Object.FindAnyObjectByType<ProcSense.VR.XR.SimulatorLocomotion>().Suspended == false);
            }
            finally { SceneManager.sceneLoaded -= Block; }
            yield return Travel(loader, "Corridor");
        }

        [UnityTest]
        public IEnumerator Disabling_loader_during_load_cleans_up_destination()
        {
            yield return Boot();
            var loader = Object.FindAnyObjectByType<RoomLoader>();
            Assert.IsTrue(loader.RequestRoom("Corridor"));
            loader.enabled = false;
            float end = Time.realtimeSinceStartup + 15;
            while (loader.Busy && Time.realtimeSinceStartup < end) yield return null;
            Assert.IsFalse(loader.Busy);
            Assert.AreEqual("PatientBay", loader.CurrentRoom);
            Assert.IsFalse(SceneManager.GetSceneByName("Corridor").isLoaded);
            loader.enabled = true;
            yield return Travel(loader, "Corridor");
        }

        [UnityTest]
        public IEnumerator Paused_time_does_not_stall_transition_and_route_previews_are_saved()
        {
            yield return Boot();
            var loader = Object.FindAnyObjectByType<RoomLoader>();
            float previousScale = Time.timeScale;
            try
            {
                Time.timeScale = 0;
                yield return Travel(loader, "Corridor");
                Assert.AreEqual(0, Time.timeScale);
            }
            finally { Time.timeScale = previousScale; }
            yield return new WaitForSecondsRealtime(0.12f);
            Shoot("corridor", new Vector3(1.1f, 1.7f, -4), new Vector3(-0.6f, 1.4f, 0));
            var otDoor = Object.FindObjectsByType<RoomDoor>().First(d => d.Destination == "OT");
            var enter = otDoor.GetComponentInChildren<UnityEngine.UI.Button>();
            Assert.IsTrue(enter.interactable, "idle door control must be enabled");
            enter.onClick.Invoke();
            Assert.IsTrue(loader.Busy, "Enter must request the transition");
            float end = Time.realtimeSinceStartup + 15;
            while (loader.Busy && Time.realtimeSinceStartup < end) yield return null;
            Assert.AreEqual("OT", loader.CurrentRoom, loader.LastError);
            yield return new WaitForSecondsRealtime(0.12f);
            Shoot("ot", new Vector3(-2.4f, 1.7f, -2.4f), new Vector3(0.3f, 0.8f, 0.7f));
            Shoot("ot-door", new Vector3(0, 1.6f, -2.3f), new Vector3(0, 1.45f, -3.35f));
            yield return Travel(loader, "Corridor");
            yield return Travel(loader, "PatientBay");
            yield return new WaitForSecondsRealtime(0.12f);
            Shoot("bay-door", new Vector3(1.3f, 1.6f, -0.8f), new Vector3(2.35f, 1.45f, -0.8f));
        }

        private static void Shoot(string name, Vector3 from, Vector3 look)
        {
            string directory = Path.Combine(Application.dataPath, "..", "Logs", "navigation");
            Directory.CreateDirectory(directory);
            var go = new GameObject("PreviewCamera", typeof(Camera));
            var camera = go.GetComponent<Camera>();
            camera.enabled = false;
            camera.transform.position = from;
            camera.transform.LookAt(look);
            var target = new RenderTexture(1280, 720, 24);
            var texture = new Texture2D(1280, 720, TextureFormat.RGB24, false);
            var previous = RenderTexture.active;
            try
            {
                camera.targetTexture = target;
                camera.Render();
                RenderTexture.active = target;
                texture.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
                texture.Apply();
                File.WriteAllBytes(Path.Combine(directory, name + ".png"), texture.EncodeToPNG());
            }
            finally
            {
                RenderTexture.active = previous;
                camera.targetTexture = null;
                target.Release();
                Object.Destroy(target);
                Object.Destroy(texture);
                Object.Destroy(go);
            }
        }
    }
}
