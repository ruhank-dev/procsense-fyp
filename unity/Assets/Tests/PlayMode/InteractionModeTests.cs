using NUnit.Framework;
using ProcSense.Core;
using ProcSense.Core.Actions;
using ProcSense.VR.Bridge;
using ProcSense.VR.Settings;
using UnityEngine;

namespace ProcSense.Tests.PlayMode
{
    /// <summary>Menu or voice (P3-VOX-02): kept in the settings, one event per real change, and switching mid-session never changes the engine's state.</summary>
    public sealed class InteractionModeTests
    {
        private SimulationHost _host;

        [SetUp]
        public void Make()
        {
            GameSettings.Reset();
            if (SimulationHost.Instance != null) Object.DestroyImmediate(SimulationHost.Instance.gameObject);
            _host = new GameObject("mode host").AddComponent<SimulationHost>();
            _host.AutoTick = false; _host.SaveLogOnDestroy = false;
            _host.StartSession("VAR-S1-A", 42, SessionMode.Practice);
        }

        [TearDown]
        public void Drop()
        {
            Object.DestroyImmediate(_host.gameObject);
            GameSettings.Reset();
        }

        [Test]
        public void Switching_mid_session_keeps_the_state_and_the_log_identical()
        {
            Assert.IsTrue(_host.Apply(new SimAction("introduce_self", "patient", Channel.Vr)).Accepted);
            string state = _host.Observable.Hash(), log = _host.Session.Log.Hash();
            InteractionMode.Set(InteractionModeSetting.Voice);
            InteractionMode.Toggle();
            InteractionMode.Set(InteractionModeSetting.Voice);
            Assert.AreEqual(state, _host.Observable.Hash());
            Assert.AreEqual(log, _host.Session.Log.Hash());
        }

        [Test]
        public void The_mode_is_kept_and_heard_once_per_real_change()
        {
            InteractionMode.BeginSession();
            int heard = 0;
            void OnChange(InteractionModeSetting m) => heard++;
            InteractionMode.Changed += OnChange;
            try
            {
                InteractionMode.Set(InteractionModeSetting.Voice);
                InteractionMode.Set(InteractionModeSetting.Voice);
                Assert.AreEqual(1, heard);
                Assert.IsTrue(InteractionMode.Voice);
                Assert.AreEqual(InteractionModeSetting.Voice, GameSettings.Current.interactionMode, "saved in the settings record");
                InteractionMode.Toggle();
                Assert.AreEqual(new[] { InteractionModeSetting.Menu, InteractionModeSetting.Voice, InteractionModeSetting.Menu }, InteractionMode.History);
            }
            finally { InteractionMode.Changed -= OnChange; }
        }
    }
}
