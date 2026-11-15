using NUnit.Framework;
using ProcSense.VR.Settings;
using ProcSense.VR.UI.Kit;
using ProcSense.VR.UI.Theme;
using TMPro;
using UnityEngine;

namespace ProcSense.Tests.EditMode
{
    /// <summary>P3-UIX-07: the tick draws in fully (also with reduce motion), the error shake returns the control to its place and turns the message red, the listening ring shows, pulses and turns, and stays still with reduce motion.</summary>
    public class UiKitFeedbackTests
    {
        private GameObject _root;
        private Canvas _canvas;
        private ISettingsStore _store;

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

        private static void Reduce(bool on) => GameSettings.Update(s => s.reduceMotion = on);

        [SetUp]
        public void SetUp() { _store = GameSettings.Store; GameSettings.Store = new MemoryStore(); UiMotion.Clear(); _root = new GameObject("feedback"); _canvas = Kit.Canvas("Feedback test", _root.transform, new Vector2(600, 400), 1f); }

        [TearDown]
        public void TearDown() { UiMotion.Clear(); Object.DestroyImmediate(_root); GameSettings.Store = _store; }

        [Test]
        public void The_tick_draws_in_even_with_reduce_motion()
        {
            foreach (var reduce in new[] { false, true })
            {
                Reduce(reduce);
                var tick = KitFeedback.Tick(_canvas.transform, Vector2.zero, 64);
                Assert.AreEqual(0f, tick.fillAmount);
                Assert.IsNotNull(tick.sprite);
                bool done = false;
                KitFeedback.Draw(tick, () => done = true);
                UiMotion.Tick(1f);
                Assert.AreEqual(1f, tick.fillAmount, 1e-4, "reduce motion " + reduce);
                Assert.IsTrue(done);
            }
        }

        [Test]
        public void The_error_shake_returns_the_control_and_colours_the_message()
        {
            Reduce(false);
            var button = Kit.Button(_canvas.transform, "Send", new Vector2(10, 10), new Vector2(200, 72));
            var rt = (RectTransform)button.transform;
            var label = button.GetComponentInChildren<TMP_Text>();
            var start = rt.anchoredPosition;
            KitFeedback.Error(rt, label);
            UiMotion.Tick(.03f);
            Assert.AreNotEqual(start, rt.anchoredPosition, "it moves");
            UiMotion.Tick(1f);
            Assert.AreEqual(start, rt.anchoredPosition, "and comes back");
            Assert.AreEqual(UiTheme.Current.DangerText, label.color);
        }

        [Test]
        public void The_listening_ring_pulses_and_turns_and_is_still_with_reduce_motion()
        {
            Reduce(false);
            var l = KitFeedback.Listening(_canvas.transform, Vector2.zero, 96);
            Assert.IsFalse(l.Ring.enabled, "hidden when idle");
            l.Set(KitListening.Mode.Listening);
            Assert.IsTrue(l.Ring.enabled);
            Assert.AreEqual(1f, l.Ring.Value);
            l.Level = 1f;
            l.Step(.3f);
            Assert.Greater(l.Ring.rectTransform.localScale.x, 1f, "pulse");
            Assert.AreEqual(12f, l.Ring.Thickness, 1e-3, "loud: thick ring");
            l.Set(KitListening.Mode.Processing);
            Assert.AreEqual(.25f, l.Ring.Value);
            l.Step(.2f);
            Assert.AreNotEqual(0f, l.Ring.rectTransform.localEulerAngles.z, "turning");

            Reduce(true);
            l.Set(KitListening.Mode.Listening);
            l.Step(.3f);
            Assert.AreEqual(Vector3.one, l.Ring.rectTransform.localScale, "no pulse with reduce motion");
        }
    }
}
