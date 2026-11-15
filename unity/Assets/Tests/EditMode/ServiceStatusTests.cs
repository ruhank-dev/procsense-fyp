using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using ProcSense.VR.Auth;
using ProcSense.VR.Settings;
using ProcSense.VR.UI.Kit;
using ProcSense.VR.Voice;
using UnityEngine;

namespace ProcSense.Tests.EditMode
{
    /// <summary>
    /// P3-API-03, P3-NET-08, P3-UIX-11, P3-VOX-10: the connection model from /health and /ai/health (connected, cloud fallback, version mismatch, reconnecting, not found), the back-off that
    /// notices a stopped laptop within 5 s and a restarted one within 10 s, the poll over a fake transport, the chip's colour and words, and the voice banner with its "Use menus" button.
    /// </summary>
    public class ServiceStatusTests
    {
        private static string Health(int api = 1) => "{\"status\":\"ok\",\"api_version\":" + api + ",\"engine_versions\":[\"" + ProcSense.Core.CoreInfo.Version + "\"]}";
        private const string AiUp = "{\"llm\":{\"up\":true,\"configured\":true}}", AiDown = "{\"llm\":{\"up\":false,\"configured\":true}}";

        private sealed class FakeHttp : IHttp
        {
            public bool Up = true;
            public string Ai = AiUp;
            public readonly List<string> Urls = new List<string>();
            public IEnumerator Send(string method, string url, string body, IDictionary<string, string> headers, int timeout, Action<HttpResult> done)
            {
                Urls.Add(url);
                if (!Up) done(new HttpResult(0, "", true));
                else done(new HttpResult(200, url.EndsWith("/ai/health") ? Ai : Health()));
                yield break;
            }
        }

        private static void Run(IEnumerator e) { var s = new Stack<IEnumerator>(); s.Push(e); while (s.Count > 0) { var t = s.Peek(); if (!t.MoveNext()) { s.Pop(); continue; } if (t.Current is IEnumerator inner) s.Push(inner); } }

        [Test]
        public void The_model_names_each_state()
        {
            var m = new ServiceStatusModel();
            Assert.AreEqual(ConnectionLevel.Degraded, m.Level, "unknown until the first answer");
            m.Report(Health(), AiUp, true, 0);
            Assert.AreEqual((ConnectionLevel.Connected, "Laptop connected · Cloud AI ready"), (m.Level, m.Text));
            Assert.IsTrue(m.VoiceAvailable);
            m.Report(Health(), AiDown, true, 3);
            Assert.AreEqual((ConnectionLevel.Degraded, "Cloud AI unavailable · local fallback"), (m.Level, m.Text));
            Assert.IsTrue(m.VoiceAvailable, "local fallback still serves voice");
            m.Report(Health(api: 9), AiUp, true, 6);
            Assert.AreEqual(ConnectionLevel.Degraded, m.Level);
            Assert.IsFalse(m.VoiceAvailable, "another API version: voice off, menus on");
            m.Report(null, null, true, 9);
            Assert.AreEqual((ConnectionLevel.Degraded, "Reconnecting…"), (m.Level, m.Text));
            m.Report(null, null, true, 10);
            Assert.AreEqual(ConnectionLevel.Offline, m.Level);
            StringAssert.StartsWith("Laptop not found", m.Text);
            m.Report(Health(), AiUp, true, 12);
            Assert.AreEqual(ConnectionLevel.Connected, m.Level, "back as soon as it answers");
        }

        [Test]
        public void A_stopped_laptop_shows_within_five_seconds_and_a_restarted_one_within_ten()
        {
            Assert.LessOrEqual(ServiceStatusModel.HealthyInterval + ServiceStatusModel.TimeoutSeconds, 5f, "stop: one interval and one timeout");
            float worst = 0;
            foreach (var b in ServiceStatusModel.Backoff) worst = Mathf.Max(worst, b);
            Assert.LessOrEqual(worst + ServiceStatusModel.TimeoutSeconds, 10f, "restart: the longest back-off and one timeout");
            var m = new ServiceStatusModel();
            m.Report(null, null, false, 0); Assert.AreEqual(1f, m.NextCheckAt);
            m.Report(null, null, false, 1); Assert.AreEqual(3f, m.NextCheckAt);
            m.Report(null, null, false, 3); Assert.AreEqual(7f, m.NextCheckAt);
            m.Report(null, null, false, 7); Assert.AreEqual(11f, m.NextCheckAt, "capped");
        }

        [Test]
        public void A_poll_asks_health_then_ai_health_and_only_health_when_the_laptop_is_away()
        {
            var go = new GameObject("status");
            try
            {
                var s = go.AddComponent<ServiceStatus>();
                var http = new FakeHttp();
                s.Http = () => http;
                Run(s.Poll("http://192.168.1.20:8000/"));
                CollectionAssert.AreEqual(new[] { "http://192.168.1.20:8000/health", "http://192.168.1.20:8000/ai/health" }, http.Urls);
                Assert.AreEqual(ConnectionLevel.Connected, s.Model.Level);
                http.Up = false; http.Urls.Clear();
                Run(s.Poll("http://192.168.1.20:8000"));
                Assert.AreEqual(1, http.Urls.Count);
                Assert.IsFalse(s.Model.Reachable);
            }
            finally { UnityEngine.Object.DestroyImmediate(go); }
        }

        [Test]
        public void The_chip_shows_the_colour_and_words_of_the_state()
        {
            var go = new GameObject("chip");
            try
            {
                var m = new ServiceStatusModel();
                var chip = go.AddComponent<ConnectionChip>();
                chip.Model = m;
                chip.Build();
                m.Report(Health(), AiUp, true, 0); chip.Draw();
                Assert.AreEqual(ConnectionChip.ColourOf(ConnectionLevel.Connected), chip.Dot.color);
                Assert.AreEqual("Laptop connected · Cloud AI ready", chip.Label.text);
                m.Report(null, null, true, 3); m.Report(null, null, true, 4); chip.Draw();
                Assert.AreEqual(ConnectionChip.ColourOf(ConnectionLevel.Offline), chip.Dot.color);
                Assert.AreNotEqual(ConnectionChip.ColourOf(ConnectionLevel.Connected), ConnectionChip.ColourOf(ConnectionLevel.Degraded));
            }
            finally { UnityEngine.Object.DestroyImmediate(go); }
        }

        [Test]
        public void In_voice_mode_a_lost_laptop_shows_the_banner_and_one_press_gives_the_menus()
        {
            var go = new GameObject("banner");
            var mode = InteractionMode.Current;
            try
            {
                var m = new ServiceStatusModel();
                var banner = go.AddComponent<VoiceBanner>();
                banner.Model = m;
                banner.Head = go.transform;
                InteractionMode.Set(InteractionModeSetting.Voice);
                m.Report(Health(), AiUp, true, 0); banner.Step(.1f);
                Assert.IsFalse(banner.Showing, "voice available: no banner");
                m.Report(null, null, true, 3); banner.Step(.1f);
                Assert.IsTrue(banner.Showing, "the laptop stopped: banner");
                banner.MenusButton.onClick.Invoke();
                Assert.AreEqual(InteractionModeSetting.Menu, InteractionMode.Current);
                banner.Step(.1f);
                Assert.IsFalse(banner.Showing, "menu mode needs no laptop");
            }
            finally { InteractionMode.Set(mode); UnityEngine.Object.DestroyImmediate(go); }
        }
    }
}
