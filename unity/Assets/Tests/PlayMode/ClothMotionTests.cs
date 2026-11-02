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
    /// <summary>Demonstrates the cosmetic cloth: gown hem and sleeves react to real leg motion and settle; curtains swing, settle, and slide open/closed. Deterministic fixed-step.</summary>
    public class ClothMotionTests
    {
        private const float Dt = 1f / 60f;

        /// <summary>These fixtures load the full room, whose scene host is persistent; later fixtures (login, tablet) expect a clean singleton, so remove it afterwards.</summary>
        [TearDown] public void Cleanup() => TestHostHygiene.DestroyRoomLeftovers();

        private IEnumerator Load()
        {
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/Scenes/PatientBay.unity", new LoadSceneParameters(LoadSceneMode.Single));
            for (int i = 0; i < 30; i++) yield return null;
            SimulationHost.Instance.AutoTick = false; SimulationHost.Instance.SaveLogOnDestroy = false;
        }

        private static void Settle(System.Action<float> step, int frames = 240) { for (int i = 0; i < frames; i++) step(Dt); }

        [UnityTest]
        public IEnumerator Gown_hem_lags_a_rotating_leg_then_settles_without_clipping()
        {
            yield return Load();
            var patient = GameObject.Find("MPFB patient");
            var gown = patient.GetComponent<GownSecondaryMotion>();
            Assert.IsNotNull(gown, "gown secondary motion rig is installed");
            patient.GetComponent<PatientIdleView>().enabled = false; gown.enabled = false;
            patient.GetComponent<PatientIdleView>().RestorePose();
            Settle(gown.Step);
            Assert.Less(gown.MaxLag(), .012f, "at rest the cloth sits where the skeleton puts it");
            var thigh = patient.GetComponentsInChildren<Transform>().First(t => t.name == "thigh_r");
            var rest = thigh.rotation;
            thigh.rotation = Quaternion.AngleAxis(38f, Vector3.right) * rest;           // raise the knee: a sudden hip rotation
            gown.Step(Dt);
            float lag = 0; for (int i = 0; i < 20; i++) { gown.Step(Dt); lag = Mathf.Max(lag, gown.MaxLag()); }
            Assert.Greater(lag, .018f, "the hem lags behind the moving leg");
            Settle(gown.Step, 360);
            Assert.Less(gown.MaxSpeed(Dt), .01f, "the cloth has stopped moving on the raised leg");
            Assert.Less(gown.MaxLag(), .08f, "and sits near the skeleton, pushed out only where the raised leg meets it");
            thigh.rotation = rest;                                                        // lower the leg again
            float lag2 = 0; for (int i = 0; i < 20; i++) { gown.Step(Dt); lag2 = Mathf.Max(lag2, gown.MaxLag()); }
            Assert.Greater(lag2, .018f, "the hem drops and swings when the leg lowers");
            Settle(gown.Step, 360);
            Assert.Less(gown.MaxLag(), .012f, "returns to rest and stays stable after repeated movement");
            foreach (var n in gown.Nodes) Assert.GreaterOrEqual(n.Pos.y, .70f - n.FloorSlack - .001f, "cloth never sinks into the mattress beyond the accepted rest fit");
            var smr = GameObject.Find("Draped patient gown").GetComponent<SkinnedMeshRenderer>(); var mesh = new Mesh(); smr.BakeMesh(mesh, true);
            Assert.IsTrue(mesh.vertices.All(v => !float.IsNaN(v.x + v.y + v.z) && Mathf.Abs(v.x) < 5f), "skinned gown vertices stay finite");
            Object.DestroyImmediate(mesh);
        }

        [UnityTest]
        public IEnumerator Curtains_swing_when_nudged_slide_open_and_settle()
        {
            yield return Load();
            var curtains = Object.FindObjectsByType<CurtainCloth>(FindObjectsSortMode.None);
            Assert.GreaterOrEqual(curtains.Length, 3, "two window curtains and the privacy curtain are rigged");
            foreach (var c in curtains)
            {
                c.enabled = false; Settle(c.Step, 60);
                Assert.Less(c.MaxLag(), .004f, c.name + " hangs still at rest");
                var mid = c.Nodes[c.Nodes.Length / 2].Pos;
                c.Nudge(mid, .7f, c.transform.right + Vector3.back, .35f);                // a firm hand push
                float lag = 0; for (int i = 0; i < 15; i++) { c.Step(Dt); lag = Mathf.Max(lag, c.MaxLag()); }
                Assert.Greater(lag, .02f, c.name + " swings when pushed");
                Settle(c.Step, 420);
                Assert.Less(c.MaxLag(), .006f, c.name + " settles");
                c.Open = 0f; Settle(c.Step, 300);                                                                // the privacy curtain now rests gathered (open); draw it across first, then open it again
                var far = c.Nodes.OrderByDescending(n => n.Column).First(); var before = far.Bone.position;
                c.Open = 1f; Settle(c.Step, 300);
                Assert.Greater(Vector3.Distance(before, far.Bone.position), .25f * c.Nodes.Max(n => n.Column), c.name + " gathers along its track");
                c.Open = 0f; Settle(c.Step, 300);
                Assert.Less(Vector3.Distance(before, far.Bone.position), .03f, c.name + " closes back to where it started");
            }
        }
    }
}
#endif
