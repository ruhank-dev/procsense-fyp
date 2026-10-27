using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using ProcSense.Core;
using ProcSense.Core.Actions;
using ProcSense.VR.Bridge;
using ProcSense.VR.Fallback;
using ProcSense.VR.Scenes;
using UnityEngine;

namespace ProcSense.Tests.PlayMode
{
    /// <summary>
    /// The SceneDirector (p0-vr-platform-s06, T-B4): the patient goes bay, corridor, CT door, hidden inside, and back, each move on Core's state; the cosmetic travel never gets ahead of Core; the trainee goes too
    /// only when they accompany.
    /// </summary>
    public class SceneDirectorTests
    {
        private readonly List<GameObject> _made = new List<GameObject>();
        private SimulationHost _host;

        private GameObject Made(GameObject go) { _made.Add(go); return go; }

        private ActionOutcome Do(string verb, params (string k, string v)[] p) => _host.Apply(new SimAction(verb, "patient", Channel.Vr, p.ToDictionary(x => x.k, x => x.v)));

        private SceneDirector Director(out Transform rig)
        {
            TabletFallbackRegistry.Clear();
            if (SimulationHost.Instance != null) Object.DestroyImmediate(SimulationHost.Instance.gameObject);
            _host = Made(new GameObject("host")).AddComponent<SimulationHost>();
            Assert.AreSame(_host, SimulationHost.Instance);
            _host.AutoTick = false;
            _host.SaveLogOnDestroy = false;
            _host.StartSession("VAR-S1-A", 42, SessionMode.Practice);
            ModeGate.SessionProvider = () => _host.Session;
            foreach (var v in new[] { "introduce_self", "ask_identity", "read_wristband", "open_notes", "confirm_identity" }) Assert.IsTrue(Do(v).Accepted, v);

            var root = Made(new GameObject("director"));
            void Anchor(string id, float x) { var a = new GameObject(id).transform; a.SetParent(root.transform, false); a.localPosition = new Vector3(x, 0, 0); }
            Anchor("patient_bay", 0f); Anchor("corridor", 5f); Anchor("door:ct", 12f); Anchor("inside:ct", 15f);
            var director = root.AddComponent<SceneDirector>();
            director.FindAnchors();
            director.Patient = Made(GameObject.CreatePrimitive(PrimitiveType.Capsule)).transform;
            director.Porter = Made(GameObject.CreatePrimitive(PrimitiveType.Cube)).transform;
            rig = Made(new GameObject("rig")).transform;
            director.TraineeRig = rig;
            director.Sync();
            return director;
        }

        private void TickUntil(SceneDirector d, string state)
        {
            for (int i = 0; i < 400 && (_host.Observable.Transport?.State ?? "") != state; i++) _host.Tick(15);
            Assert.AreEqual(state, _host.Observable.Transport?.State);
            d.Sync();
        }

        [TearDown]
        public void TearDown()
        {
            ModeGate.SessionProvider = () => SimulationHost.Instance != null ? SimulationHost.Instance.Session : null;
            foreach (var go in _made) if (go != null) Object.DestroyImmediate(go);
            _made.Clear();
        }

        [Test]
        public void The_patient_goes_to_the_CT_door_hides_inside_and_comes_back_on_Core_events()
        {
            var d = Director(out var rig);
            Assert.AreEqual(0f, d.Patient.position.x, 1e-4f, "in the bay");
            var rigStart = rig.position;
            var asked = Do("request_transport", ("to", "ct"), ("mode", "trolley"), ("monitoring", "none"), ("accompany", "false"), ("oxygen", "false"));
            Assert.IsTrue(asked.Accepted, asked.RejectReason);
            d.Sync();
            Assert.AreEqual("patient_bay", d.From);

            TickUntil(d, "PREPARING");
            Assert.IsTrue(d.Porter.gameObject.activeSelf, "the porter has come");
            Assert.AreEqual("Prop trolley", d.Prop.name);

            TickUntil(d, "IN_TRANSIT");
            Assert.IsTrue(d.Current.Moving);
            d.Travel(3f);
            Assert.Greater(d.Patient.position.x, 0f, "on the way");
            d.Travel(600f);
            Assert.AreEqual(12f, d.Patient.position.x, 1e-3f, "at the CT door, and no further while Core says IN_TRANSIT");
            Assert.AreEqual("IN_TRANSIT", _host.Observable.Transport.State);

            TickUntil(d, "ARRIVED");
            Assert.AreEqual(15f, d.Patient.position.x, 1e-3f, "inside");
            Assert.IsFalse(d.Patient.GetComponent<Renderer>().enabled, "hidden behind the door");
            Assert.AreEqual(rigStart, rig.position, "the trainee did not go");

            TickUntil(d, "RETURNING");
            Assert.IsTrue(d.Patient.GetComponent<Renderer>().enabled);
            Assert.IsTrue(d.Current.Moving);

            TickUntil(d, "BACK");
            Assert.AreEqual(0f, d.Patient.position.x, 1e-3f, "back in the bay");
            Assert.IsNull(d.Prop, "the trolley has gone");
            Assert.IsFalse(d.Porter.gameObject.activeSelf);
        }

        [Test]
        public void A_trainee_who_accompanies_goes_with_the_patient()
        {
            var d = Director(out var rig);
            Assert.IsTrue(Do("request_transport", ("to", "ct"), ("mode", "wheelchair"), ("monitoring", "none"), ("accompany", "true"), ("oxygen", "false")).Accepted);
            d.Sync();
            TickUntil(d, "IN_TRANSIT");
            Assert.IsTrue(d.Current.TraineeFollows);
            d.Travel(600f);
            Assert.AreEqual(12f, rig.position.x, 1.0f, "beside the patient at the door");
            TickUntil(d, "ARRIVED");
            Assert.AreEqual(12f, rig.position.x, 1.0f, "outside the hidden room, by its door");
        }
    }
}
