using NUnit.Framework;
using ProcSense.VR.Settings;
using ProcSense.VR.UI.Theme;
using UnityEngine;

namespace ProcSense.Tests.EditMode
{
    /// <summary>The UI motion library (P3-ANI-09): timings from the tokens, one tween per owner and channel, a replaced tween never leaves its panel shrunk, reduce motion.</summary>
    public class UiMotionTests
    {
        private ISettingsStore _store;
        private GameObject _go;

        private sealed class MemoryStore : ISettingsStore
        {
            private readonly System.Collections.Generic.Dictionary<string, string> _v = new System.Collections.Generic.Dictionary<string, string>();
            public string GetString(string key) => _v.TryGetValue(key, out var s) ? s : "";
            public bool HasKey(string key) => _v.ContainsKey(key);
            public int GetInt(string key, int fallback) => fallback;
            public void SetString(string key, string value) => _v[key] = value;
            public void DeleteKey(string key) => _v.Remove(key);
            public void Save() { }
        }

        [SetUp]
        public void Make()
        {
            _store = GameSettings.Store;
            GameSettings.Store = new MemoryStore();
            UiMotion.Clear();
            _go = new GameObject("panel", typeof(RectTransform), typeof(CanvasGroup));
            _go.transform.localScale = Vector3.one * 2f;
        }

        [TearDown]
        public void Drop()
        {
            UiMotion.Clear();
            Object.DestroyImmediate(_go);
            GameSettings.Store = _store;
        }

        [Test]
        public void A_panel_opens_in_the_token_time_and_ends_at_rest()
        {
            var group = _go.GetComponent<CanvasGroup>();
            UiMotion.Open(_go.transform, group);
            UiMotion.Tick(.09f);
            Assert.That(group.alpha, Is.InRange(.5f, .99f), "half-way and eased out");
            Assert.Less(_go.transform.localScale.x, 2f);
            UiMotion.Tick(.1f);
            Assert.AreEqual(1f, group.alpha);
            Assert.AreEqual(2f, _go.transform.localScale.x, 1e-5);
            Assert.AreEqual(0, UiMotion.Count);
        }

        [Test]
        public void Closing_half_way_through_opening_still_returns_to_the_resting_scale()
        {
            var group = _go.GetComponent<CanvasGroup>();
            UiMotion.Open(_go.transform, group);
            UiMotion.Tick(.05f);
            UiMotion.Close(_go.transform, group);
            Assert.AreEqual(1, UiMotion.Count, "one tween per owner and channel");
            UiMotion.Tick(.2f);
            Assert.IsFalse(_go.activeSelf);
            Assert.AreEqual(2f, _go.transform.localScale.x, 1e-5);
        }

        [Test]
        public void Reduce_motion_skips_decoration_but_keeps_the_meaningful_feedback()
        {
            GameSettings.Update(s => s.reduceMotion = true);
            var group = _go.GetComponent<CanvasGroup>();
            group.alpha = 0f;
            UiMotion.Open(_go.transform, group);
            Assert.AreEqual(1f, group.alpha, "the open finished at once");
            int shown = -1;
            UiMotion.CountUp(this, 80, v => shown = v);
            Assert.AreEqual(1, UiMotion.Count, "the count-up still plays");
            UiMotion.Tick(.2f);
            Assert.That(shown, Is.InRange(1, 79));
            UiMotion.Tick(1f);
            Assert.AreEqual(80, shown);
        }

        [Test]
        public void Timings_come_from_the_tokens_and_the_easings_meet_their_ends()
        {
            var t = UiTheme.Current;
            Assert.AreEqual(t.Open, UiMotion.Open(_go.transform, null).Duration);
            Assert.AreEqual(t.Close, UiMotion.Close(_go.transform, null).Duration);
            foreach (var ease in new System.Func<float, float>[] { UiMotion.EaseOut, UiMotion.EaseIn, UiMotion.Linear })
            { Assert.AreEqual(0f, ease(0f), 1e-6); Assert.AreEqual(1f, ease(1f), 1e-6); }
            Assert.AreEqual(1f, UiMotion.Dip(0f), 1e-6);
            Assert.AreEqual(1f, UiMotion.Dip(1f), 1e-5);
            Assert.Less(UiMotion.Dip(.5f), 1f);
        }

        [Test]
        public void A_tween_whose_object_is_gone_is_dropped()
        {
            var other = new GameObject("gone");
            UiMotion.Open(other.transform, null);
            Object.DestroyImmediate(other);
            UiMotion.Tick(.01f);
            Assert.AreEqual(0, UiMotion.Count);
        }
    }
}
