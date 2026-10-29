using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using ProcSense.Core;
using ProcSense.Core.Actions;
using ProcSense.Core.Observation;
using ProcSense.VR.Audio;
using ProcSense.VR.Bridge;
using ProcSense.VR.Views;
using UnityEngine;
using UnityEngine.TestTools;

namespace ProcSense.Tests.PlayMode
{
    /// <summary>The monitor's pleth, cuff cycle, probe advice and alarms, and the alarm and room sounds (p0-vr-interaction-ui-s04, p0-voice-audio-s05).</summary>
    public sealed class MonitorViewTests
    {
        private SimulationHost _host;
        private MonitorView _monitor;
        private readonly List<(SimAction action, ActionOutcome outcome)> _applied = new List<(SimAction, ActionOutcome)>();

        private ActionOutcome Do(string verb, params (string key, string value)[] args) =>
            _host.Apply(new SimAction(verb, "patient", Channel.Vr, args.ToDictionary(p => p.key, p => p.value)));

        [SetUp]
        public void Setup()
        {
            if (SimulationHost.Instance != null) Object.DestroyImmediate(SimulationHost.Instance.gameObject);   // a host an earlier test left running would be the one the views follow
            _host = new GameObject("monitor host").AddComponent<SimulationHost>();
            _host.AutoTick = false; _host.SaveLogOnDestroy = false;
            _host.StartSession("VAR-S1-A", 42, SessionMode.Practice);
            _host.Applied += (a, o) => _applied.Add((a, o));
            _monitor = new GameObject("monitor").AddComponent<MonitorView>();
        }

        [TearDown]
        public void Cleanup()
        {
            Object.DestroyImmediate(_monitor.gameObject);
            Object.DestroyImmediate(_host.gameObject);
            _applied.Clear();
        }

        [Test]
        public void The_pleth_runs_only_with_the_probe_on_at_the_pulse_rate()
        {
            _monitor.RenderNow();
            Assert.IsFalse(_monitor.Pleth.On);
            Assert.AreEqual("No probe: no pleth", _monitor.ProbeText);
            Assert.IsTrue(Do("attach_sensor", ("sensor", "spo2")).Accepted);
            _monitor.RenderNow();
            Assert.IsTrue(_monitor.Pleth.On);
            Assert.AreEqual((float)_host.Session.Case.BaselineVitals.HeartRate, _monitor.Pleth.Rate, .5f);
            Assert.AreEqual("", _monitor.ProbeText);
            Assert.AreEqual(100, _monitor.Pleth.Advance(1f), "a fixed 100 samples a second, whatever the frame rate");
        }

        [Test]
        public void A_poor_probe_signal_gives_a_small_noisy_trace_and_says_what_to_check()
        {
            Assert.IsTrue(Do("attach_sensor", ("sensor", "spo2")).Accepted);
            _host.Session.Patient.Physiological.ProbeProblem = "cold_fingers";
            _host.Tick(1);
            _monitor.RenderNow();
            StringAssert.StartsWith("Poor signal", _monitor.ProbeText);
            Assert.Less(_monitor.Pleth.Amplitude, 1f);
            Assert.IsTrue(Do("resite_probe").Accepted);
            _monitor.RenderNow();
            Assert.AreEqual("", _monitor.ProbeText);
        }

        [Test]
        public void The_cuff_shows_it_is_cycling_until_its_reading_arrives()
        {
            Assert.IsTrue(Do("attach_sensor", ("sensor", "nibp_cuff")).Accepted);
            Assert.IsTrue(Do("measure_nibp").Accepted);
            _monitor.RenderNow();
            StringAssert.StartsWith("Cuff cycling", _monitor.CuffText);
            Assert.AreEqual("--", _monitor.ValueText("BP"));
            for (int i = 0; i < 120 && _host.Observable.Vitals.All(v => v.Name != "systolic_bp"); i++) _host.Tick(1);
            _monitor.RenderNow();
            Assert.AreEqual("", _monitor.CuffText);
            Assert.AreNotEqual("--", _monitor.ValueText("BP"));
        }

        [UnityTest]
        public IEnumerator An_alarm_sounds_and_silencing_it_is_a_logged_Core_action()
        {
            _monitor.RenderNow();
            // a low saturation (Core's zones say emergency below its limit) as the monitor would receive it
            _monitor.Alarms.Update(new[] { new ObservedVital("spo2", "85", "08:00:00", "emergency") }, Time.unscaledTime);
            yield return null;
            var audio = _monitor.GetComponent<AlarmAudioView>();
            Assert.AreEqual("high", audio.Playing);
            Assert.GreaterOrEqual(audio.Bursts, 1);
            Assert.IsTrue(AlarmAudioView.HighSounding);

            _monitor.Alarms.Update(new[] { new ObservedVital("spo2", "85", "08:00:00", "emergency") }, Time.unscaledTime);
            var ack = _monitor.Alarms.Acknowledge(Time.unscaledTime);
            Assert.AreEqual(new[] { "spo2" }, ack.ToArray());
            var o = _host.Apply(new SimAction("acknowledge_alarm", "monitor", Channel.Vr, new Dictionary<string, string> { ["vital"] = "spo2", ["priority"] = "high" }));
            Assert.IsTrue(o.Accepted);
            Assert.AreEqual("alarm_acknowledged:spo2", o.Effects.Single());
            yield return null;
            Assert.AreEqual("", audio.Playing, "silenced");
            Assert.IsFalse(AlarmAudioView.HighSounding);
        }

        [Test]
        public void The_silence_button_applies_one_acknowledgement_per_sounding_alarm()
        {
            _monitor.RenderNow();
            _monitor.Alarms.Update(new[] { new ObservedVital("spo2", "85", "08:00:00", "emergency"), new ObservedVital("heart_rate", "112", "08:00:00", "worry") }, Time.unscaledTime);
            _applied.Clear();
            _monitor.Acknowledge();
            CollectionAssert.AreEquivalent(new[] { "spo2", "heart_rate" }, _applied.Where(x => x.action.Verb == "acknowledge_alarm").Select(x => x.action.Params["vital"]).ToArray());
            Assert.IsTrue(_applied.All(x => x.outcome.Accepted));
        }

        [Test]
        public void The_room_sound_follows_where_the_trainee_is()
        {
            Assert.AreEqual("ward", RoomAudio.RoomFor("patient_bay"));
            Assert.AreEqual("theatre", RoomAudio.RoomFor("theatre_reception"));
            Assert.AreEqual("imaging", RoomAudio.RoomFor("ct"));
            Assert.AreEqual("", RoomAudio.RoomFor(""));
            var clip = ToneBank.Room("ward");
            Assert.AreSame(clip, ToneBank.Room("ward"), "made once");
            Assert.Greater(clip.length, 5f);
            Assert.Greater(ToneBank.Alarm("high", 10).length, ToneBank.Alarm("low", 2).length);
        }
    }
}
