using System.Collections.Generic;
using NUnit.Framework;
using ProcSense.VR.Auth;

namespace ProcSense.Tests.EditMode
{
    public class HeadsetNoticeSessionTests
    {
        private sealed class Fake : INoticeTransport
        {
            public readonly Queue<string> Inbox = new Queue<string>();
            public bool Opened, Stopped; public bool Closed { get; set; }
            public void Open(string url, string token) { Opened = true; }
            public bool TryReceive(out string m) { if (Inbox.Count > 0) { m = Inbox.Dequeue(); return true; } m = null; return false; }
            public void Close() { Stopped = true; }
        }

        private const string Url = "http://192.168.1.20:8000";
        private DeviceTokenStore.IStorage _storage; private bool _privateBefore; private List<Fake> _made;
        [SetUp] public void SetUp() { _storage = DeviceTokenStore.Storage; DeviceTokenStore.Storage = new LanRun.MemoryStorage(); _privateBefore = ServiceSettings.AllowPrivateHttp; ServiceSettings.AllowPrivateHttp = true; _made = new List<Fake>(); TlsPin.ResetState(); }
        [TearDown] public void TearDown() { DeviceTokenStore.Storage = _storage; ServiceSettings.AllowPrivateHttp = _privateBefore; TlsPin.ResetState(); }
        private HeadsetNoticeSession Make() => new HeadsetNoticeSession(() => { var f = new Fake(); _made.Add(f); return f; });

        [Test]
        public void Unpaired_headset_never_connects_and_stays_offline()
        {
            var s = Make();
            Assert.IsFalse(s.Begin(Url, "s1", 0f));
            s.Tick(100f);
            Assert.AreEqual(0, _made.Count); Assert.AreEqual(NoticeLink.Offline, s.Link);
            StringAssert.Contains("offline", s.Line());
        }

        [Test]
        public void Hello_connects_and_end_stops_it()
        {
            DeviceTokenStore.Set("T");
            var s = Make();
            Assert.IsTrue(s.Begin(Url, "s1", 0f));
            Assert.AreEqual(NoticeLink.Reconnecting, s.Link);
            _made[0].Inbox.Enqueue("{\"kind\":\"hello\"}");
            s.Tick(0.1f);
            Assert.AreEqual(NoticeLink.Connected, s.Link); StringAssert.Contains("Connected", s.Line());
            s.End();
            Assert.IsTrue(_made[0].Stopped); Assert.AreEqual(NoticeLink.Offline, s.Link);
            Assert.DoesNotThrow(() => s.Tick(50f)); Assert.AreEqual(1, _made.Count);
        }

        [Test]
        public void A_dropped_socket_reconnects_with_doubling_backoff_capped_at_thirty_seconds()
        {
            DeviceTokenStore.Set("T");
            var s = Make(); s.Begin(Url, "s1", 0f);
            float t = 0f; float expected = HeadsetNoticeSession.FirstDelay;
            for (int i = 0; i < 8; i++)
            {
                _made[_made.Count - 1].Closed = true;
                s.Tick(t + 0.5f);                              // too early: no new attempt
                Assert.AreEqual(i + 1, _made.Count);
                t += expected; expected = System.Math.Min(expected * 2f, HeadsetNoticeSession.MaxDelay);
                s.Tick(t);
                Assert.AreEqual(i + 2, _made.Count, "attempt " + i);
            }
            Assert.AreEqual(HeadsetNoticeSession.MaxDelay, s.NextDelay, 0.001f);
        }

        [Test]
        public void Shutdown_notice_stops_reconnecting_and_shows_the_line()
        {
            DeviceTokenStore.Set("T");
            var s = Make(); s.Begin(Url, "s1", 0f);
            _made[0].Inbox.Enqueue("{\"kind\":\"shutdown\"}"); _made[0].Closed = true;
            s.Tick(1f); s.Tick(100f);
            Assert.AreEqual(1, _made.Count); StringAssert.Contains("shutting down", s.Line());
        }

        [Test]
        public void Progress_and_quota_lines_and_a_new_session_id_restarts()
        {
            DeviceTokenStore.Set("T");
            var s = Make(); s.Begin(Url, "s1", 0f);
            _made[0].Inbox.Enqueue("{\"kind\":\"hello\"}"); _made[0].Inbox.Enqueue("{\"kind\":\"generation_progress\",\"progress\":0.25}");
            s.Tick(0.1f); StringAssert.Contains("25%", s.Line());
            _made[0].Inbox.Enqueue("{\"kind\":\"quota\"}"); s.Tick(0.2f); StringAssert.Contains("quota", s.Line());
            s.Begin(Url, "s2", 1f);
            Assert.AreEqual(2, _made.Count); Assert.IsTrue(_made[0].Stopped);
        }

        [Test]
        public void A_throwing_factory_never_throws_and_a_pin_mismatch_banner_wins()
        {
            DeviceTokenStore.Set("T");
            var bad = new HeadsetNoticeSession(() => throw new System.InvalidOperationException("x"));
            Assert.DoesNotThrow(() => { bad.Begin(Url, "s1", 0f); bad.Tick(10f); bad.End(); });
            var s = Make(); s.Begin(Url, "s1", 0f);
            TlsPin.Set("aabb"); TlsPin.Decide("ccdd");
            Assert.IsTrue(TlsPin.Mismatch); StringAssert.Contains("certificate", s.Line());
        }
    }
}
