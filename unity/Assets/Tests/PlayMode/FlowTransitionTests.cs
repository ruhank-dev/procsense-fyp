using System.IO;
using NUnit.Framework;
using ProcSense.Core;
using ProcSense.VR.Bridge;
using ProcSense.VR.Content;
using ProcSense.VR.Flow;
using UnityEngine;

namespace ProcSense.Tests.PlayMode
{
    /// <summary>P3-ANI-10: a new case dips the view to dark and back, the results dim the room to half with the panel sorted above, leaving the results clears it, and the camera never moves.</summary>
    public class FlowTransitionTests
    {
        private SimulationHost _host;
        private MenuFlow _flow;
        private FlowPanel _panel;
        private FlowTransitions _fx;
        private Transform _head;

        [SetUp]
        public void SetUp()
        {
            ModeChoice.Reset();
            SimulationHost.SessionsDirectoryOverride = Path.Combine(Application.temporaryCachePath, "fx_tests");
            _host = new GameObject("fx host").AddComponent<SimulationHost>();
            _host.AutoTick = false;
            _host.SaveLogOnDestroy = false;
            _flow = new MenuFlow(_host, UnityContentReader.ForThisPlatform(), () => 11UL) { EndSessionOnSubmit = false };
            var go = new GameObject("panel");
            _panel = go.AddComponent<FlowPanel>();
            _panel.Build(_flow, go.transform);
            _head = new GameObject("head", typeof(Camera)).transform;
            _head.SetPositionAndRotation(new Vector3(.3f, 1.6f, -.2f), Quaternion.Euler(5, 20, 0));
            _fx = go.AddComponent<FlowTransitions>();
            _fx.Head = _head;
            _fx.Attach(_flow, _panel);
            _flow.SignedIn();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_panel.gameObject);
            Object.DestroyImmediate(_head.gameObject);
            Object.DestroyImmediate(_host.gameObject);
            ModeChoice.Reset();
            SimulationHost.SessionsDirectoryOverride = null;
        }

        private float _peak;
        private void Run(float seconds) { _peak = 0; for (float t = 0; t < seconds; t += .02f) { _fx.Step(.02f); _peak = Mathf.Max(_peak, _fx.Alpha); } }

        [Test]
        public void A_new_case_dips_to_dark_and_back_and_the_results_dim_without_moving_the_camera()
        {
            var pose = (_head.position, _head.rotation);
            Assert.IsTrue(_flow.ChooseMode(SessionMode.Practice));
            Assert.IsTrue(_flow.ChooseSurgery("cholecystectomy", TrainingLevel.Beginner), _flow.Message);
            Assert.IsTrue(_fx.Dipping);
            Run(FlowTransitions.DipOut + .02f);
            Assert.AreEqual(1f, _peak, .01f, "dark while the case is set up");
            Run(FlowTransitions.DipIn + .1f);
            Assert.AreEqual(0f, _fx.Alpha, .01f, "and back");
            Assert.IsFalse(_fx.Dipping);

            Assert.IsTrue(_flow.Begin());
            Assert.IsTrue(_flow.Submit(abandoned: true).Accepted);
            Run(FlowTransitions.DimSeconds + .1f);
            Assert.AreEqual(FlowTransitions.ResultsDim, _fx.Alpha, .01f, "the room dims on the results");
            Assert.Greater(_panel.Canvas.sortingOrder, FlowTransitions.VeilOrder, "the results panel stays above the dim");

            _flow.BackToMenu();
            Run(FlowTransitions.DimSeconds + .1f);
            Assert.AreEqual(0f, _fx.Alpha, .01f);
            Assert.AreEqual(pose, (_head.position, _head.rotation), "no camera motion in any transition");
        }

        [Test]
        public void The_results_freeze_the_hands_raise_the_panel_and_add_no_camera_or_effect()
        {
            var hand = new GameObject("hand", typeof(UnityEngine.XR.Interaction.Toolkit.Interactors.XRRayInteractor)).GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactors.XRRayInteractor>();
            var socket = new GameObject("socket", typeof(SphereCollider), typeof(UnityEngine.XR.Interaction.Toolkit.Interactors.XRSocketInteractor)).GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactors.XRSocketInteractor>();
            socket.GetComponent<SphereCollider>().isTrigger = true;
            try
            {
                var layers = hand.interactionLayers;
                var sockets = socket.interactionLayers;
                int cameras = Object.FindObjectsByType<Camera>(FindObjectsSortMode.None).Length;
                int volumes = Object.FindObjectsByType<UnityEngine.Rendering.Volume>(FindObjectsSortMode.None).Length;
                var home = _panel.Canvas.transform.localPosition;
                Assert.IsTrue(_flow.ChooseMode(SessionMode.Practice));
                Assert.IsTrue(_flow.ChooseSurgery("cholecystectomy", TrainingLevel.Beginner), _flow.Message);
                Run(1f);
                Assert.IsTrue(_flow.Begin());
                Assert.IsFalse(_fx.InputFrozen, "the hands work during the case");

                Assert.IsTrue(_flow.Submit(abandoned: true).Accepted);
                Assert.IsTrue(_fx.InputFrozen);
                Assert.AreEqual(0, hand.interactionLayers.value, "no grabbing or teleporting on the results");
                Assert.AreEqual(sockets.value, socket.interactionLayers.value, "equipment stays in its sockets");
                Assert.Greater(_fx.RiseLeft, .1f, "the panel starts below its place");
                Run(FlowTransitions.DimSeconds + .05f);
                Assert.AreEqual(0f, _fx.RiseLeft, 1e-4f);
                Assert.AreEqual(home, _panel.Canvas.transform.localPosition, "and rises into it");
                Assert.AreEqual(cameras, Object.FindObjectsByType<Camera>(FindObjectsSortMode.None).Length, "no extra camera, so no extra render pass");
                Assert.AreEqual(volumes, Object.FindObjectsByType<UnityEngine.Rendering.Volume>(FindObjectsSortMode.None).Length, "no post-processing volume");

                _flow.BackToMenu();
                Assert.IsFalse(_fx.InputFrozen);
                Assert.AreEqual(layers.value, hand.interactionLayers.value, "the hands come back");
            }
            finally { Object.DestroyImmediate(hand.gameObject); Object.DestroyImmediate(socket.gameObject); }
        }
    }
}
