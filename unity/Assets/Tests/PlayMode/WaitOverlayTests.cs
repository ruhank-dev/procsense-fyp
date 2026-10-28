using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using NUnit.Framework;
using ProcSense.Core;
using ProcSense.Core.Actions;
using ProcSense.Core.Content;
using ProcSense.VR.Bridge;
using ProcSense.VR.Diagnostics;
using ProcSense.VR.Fallback;
using UnityEngine;

namespace ProcSense.Tests.PlayMode
{
    /// <summary>
    /// The waiting overlay (p0-diagnostic-transitions-s06): when the patient goes into the CT scanner it says where they are and when they will be back on the sim clock (the time Core then brings them back), shrinks
    /// to a corner note after the content's wait, shows a fast-forward note for a time skip and "Results are back" when a result comes in; changing the wait in content changes the overlay with no code change.
    /// </summary>
    public class WaitOverlayTests
    {
        private readonly List<GameObject> _made = new List<GameObject>();
        private SimulationHost _host;

        /// <summary>The real content with a different overlay wait, as a content author would set it.</summary>
        private sealed class Wait : IContentReader
        {
            private readonly IContentReader _inner = ProcSense.VR.Content.UnityContentReader.ForThisPlatform();
            private readonly int _seconds;
            public Wait(int seconds) { _seconds = seconds; }
            public string ReadText(string path)
            {
                var text = _inner.ReadText(path);
                return path == "diagnostics/diagnostics.json" ? text.Replace("\"seconds\": 10,", "\"seconds\": " + _seconds + ",") : text;
            }
        }

        private ActionOutcome Do(string verb, params (string k, string v)[] p) => _host.Apply(new SimAction(verb, "patient", Channel.Vr, p.ToDictionary(x => x.k, x => x.v)));

        private WaitOverlay Overlay(IContentReader reader = null)
        {
            TabletFallbackRegistry.Clear();
            if (SimulationHost.Instance != null) UnityEngine.Object.DestroyImmediate(SimulationHost.Instance.gameObject);
            var go = new GameObject("host");
            _made.Add(go);
            _host = go.AddComponent<SimulationHost>();
            _host.AutoTick = false;
            _host.SaveLogOnDestroy = false;
            _host.StartSession("VAR-S1-A", 42, SessionMode.Practice, reader);
            foreach (var v in new[] { "introduce_self", "ask_identity", "read_wristband", "open_notes", "confirm_identity" }) Assert.IsTrue(Do(v).Accepted, v);
            var o = new GameObject("overlay");
            _made.Add(o);
            return o.AddComponent<WaitOverlay>();
        }

        private void TickUntil(WaitOverlay w, string state)
        {
            for (int i = 0; i < 600 && (_host.Observable.Transport?.State ?? "") != state; i++) { _host.Tick(15); w.Sync(); }
            Assert.AreEqual(state, _host.Observable.Transport?.State);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var go in _made) if (go != null) UnityEngine.Object.DestroyImmediate(go);
            _made.Clear();
        }

        [Test]
        public void The_overlay_says_where_the_patient_is_and_when_Core_brings_them_back()
        {
            var w = Overlay();
            Assert.AreEqual(10f, w.WaitSeconds);
            Assert.IsTrue(new[] { "ultrasound", "xray", "ct", "mri" }.All(w.BehindDoor), "the rooms the patient stays in, from content");
            Assert.IsFalse(w.BehindDoor("theatre_reception") || w.BehindDoor("corridor") || w.BehindDoor("patient_bay"), "no waiting overlay for these");
            Assert.IsTrue(Do("order_investigation", ("test", "fbc")).Accepted);
            Assert.IsTrue(Do("take_sample", ("test", "fbc"), ("tube", "edta_purple")).Accepted);
            Assert.IsTrue(Do("request_transport", ("to", "ct"), ("mode", "trolley"), ("monitoring", "none"), ("accompany", "false"), ("oxygen", "false")).Accepted);
            w.Sync();
            TickUntil(w, "ARRIVED");
            Assert.IsTrue(w.FullShown);
            StringAssert.Contains("CT scanner", w.FullText);
            StringAssert.Contains("carry on with other tasks", w.FullText);
            Assert.IsNotEmpty(w.BackAt);

            w.Advance(9f);
            Assert.IsTrue(w.FullShown);
            w.Advance(1.5f);
            Assert.IsFalse(w.FullShown, "after the content's wait the panel gives way");
            Assert.IsTrue(w.CornerShown);
            StringAssert.Contains(w.BackAt, w.CornerText);

            string back = w.BackAt;
            TickUntil(w, "BACK");
            var core = DateTime.ParseExact(_host.Observable.ClockTime.Substring(0, 5), "HH:mm", CultureInfo.InvariantCulture);
            var said = DateTime.ParseExact(back, "HH:mm", CultureInfo.InvariantCulture);
            Assert.LessOrEqual(Math.Abs((core - said).TotalMinutes), 1.0, "Core brought the patient back when the overlay said");
            Assert.IsFalse(w.CornerShown, "gone once the patient is back");

            Assert.IsTrue(Do("time_skip", ("minutes", "30")).Accepted);
            StringAssert.Contains("+30 min", w.NoteText, "a time skip shows the fast-forward note");
        }

        [Test]
        public void A_result_coming_back_is_announced_and_the_wait_comes_from_content()
        {
            var w = Overlay(new Wait(3));
            Assert.AreEqual(3f, w.WaitSeconds, "the content's wait, no code change");
            Assert.IsTrue(Do("order_investigation", ("test", "fbc")).Accepted);
            Assert.IsTrue(Do("take_sample", ("test", "fbc"), ("tube", "edta_purple")).Accepted);
            for (int i = 0; i < 40 && !_host.Observable.Investigations.Any(x => x.State == "RESULT_AVAILABLE"); i++) { _host.Tick(5 * 60); w.Sync(); }
            StringAssert.Contains("Results are back: Full blood count", w.NoteText);
            w.Advance(WaitOverlay.NoteSeconds + 0.1f);
            Assert.AreEqual("", w.NoteText);
        }
    }
}
