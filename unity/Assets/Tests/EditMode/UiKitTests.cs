using System.Linq;
using NUnit.Framework;
using ProcSense.VR.UI.Accessibility;
using ProcSense.VR.UI.Kit;
using ProcSense.VR.UI.Theme;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ProcSense.Tests.EditMode
{
    /// <summary>
    /// P3-UIX-04: the kit sample builds every component; every target is at least the theme's minimum size; every label is at least 24 headset pixels at the sample's distance;
    /// text on the solid fills keeps 4.5:1; toggle, segmented control, ring and dialog behave; and every shape uses one of the three shared glass materials (batching).
    /// </summary>
    public class UiKitTests
    {
        private GameObject _root;
        private Canvas _canvas;

        [SetUp] public void SetUp() { UiMotion.Clear(); _root = new GameObject("kit test"); _canvas = KitSample.Build(_root.transform); Canvas.ForceUpdateCanvases(); }
        [TearDown] public void TearDown() { Object.DestroyImmediate(_root); UiMotion.Clear(); }

        [Test]
        public void The_sample_shows_every_component()
        {
            var c = _canvas.transform;
            Assert.GreaterOrEqual(c.GetComponentsInChildren<Button>(true).Length, 6);
            Assert.AreEqual(2, c.GetComponentsInChildren<KitToggle>(true).Length);
            Assert.AreEqual(1, c.GetComponentsInChildren<Slider>(true).Length);
            Assert.AreEqual(1, c.GetComponentsInChildren<KitSegmented>(true).Length);
            Assert.AreEqual(2, c.GetComponentsInChildren<ProgressRing>(true).Length, "the score ring and the open row's ring");
            Assert.AreEqual(1, c.GetComponentsInChildren<KitToast>(true).Length);
            Assert.AreEqual(1, c.GetComponentsInChildren<KitDialog>(true).Length);
            foreach (var name in new[] { "Header", "Tooltip", "Badge PASS", "Card Hint", "Row Confirm the patient's identity" })
                Assert.IsTrue(c.GetComponentsInChildren<Transform>(true).Any(t => t.name == name), name);
        }

        [Test]
        public void Every_target_is_at_least_the_minimum_size()
        {
            float min = UiTheme.Current.MinTarget;
            foreach (var s in _canvas.GetComponentsInChildren<Selectable>(true))
            {
                var hit = s is Slider slider ? slider.handleRect : (RectTransform)s.transform;
                Assert.GreaterOrEqual(hit.rect.height, min - .01f, s.name + " height");
                Assert.GreaterOrEqual(hit.rect.width, min - .01f, s.name + " width");
            }
        }

        [Test]
        public void Every_label_is_readable_at_the_sample_distance()
        {
            foreach (var label in _canvas.GetComponentsInChildren<TMP_Text>(true))
                Assert.GreaterOrEqual(Readability.HeadsetPixels(label, KitSample.Distance), Readability.MinHeadsetPixels - .01f, label.name + ": " + label.text);
        }

        [Test]
        public void Text_on_every_solid_fill_keeps_its_contrast()
        {
            var t = UiTheme.Current;
            foreach (var fill in new[] { t.Action, t.Danger, t.Done, t.Hint })
                Assert.GreaterOrEqual(Readability.Contrast(t.TextOnAccent, fill), Readability.MinContrast, "label on " + fill);
        }

        [Test]
        public void Every_shape_uses_a_shared_glass_material()
        {
            var shared = new[] { GlassPanel.Dark, GlassPanel.Light, GlassPanel.Solid };
            Assert.IsTrue(shared.All(m => m != null));
            foreach (var p in _canvas.GetComponentsInChildren<GlassPanel>(true))
                Assert.Contains(p.material, shared, p.name);
        }

        [Test]
        public void Toggle_segmented_and_dialog_behave()
        {
            var toggle = _canvas.GetComponentsInChildren<KitToggle>(true).First(k => !k.IsOn);
            toggle.IsOn = true;
            UiMotion.Tick(1f);
            Assert.AreEqual(48f, toggle.transform.Find("Track/Knob").GetComponent<RectTransform>().anchoredPosition.x, .01f);
            Assert.AreEqual(UiTheme.Current.Action, toggle.transform.Find("Track").GetComponent<GlassPanel>().color);

            var seg = _canvas.GetComponentInChildren<KitSegmented>(true);
            int heard = -1;
            seg.Changed += i => heard = i;
            seg.Option(1).onClick.Invoke();
            Assert.AreEqual(1, seg.Selected);
            Assert.AreEqual(1, heard);
            Assert.AreEqual(UiTheme.Current.TextOnAccent, seg.Option(1).GetComponentInChildren<TMP_Text>().color);

            var dialog = _canvas.GetComponentInChildren<KitDialog>(true);
            dialog.Cancel.onClick.Invoke();
            UiMotion.Tick(1f);
            Assert.IsFalse(dialog.IsOpen);
            dialog.Open();
            UiMotion.Tick(1f);
            Assert.IsTrue(dialog.IsOpen);
        }

        [Test]
        public void A_ring_draws_its_track_and_its_arc()
        {
            var ring = _canvas.GetComponentsInChildren<ProgressRing>(true).First(r => r.Value > 0);
            using (var vh = new VertexHelper())
            {
                typeof(ProgressRing).GetMethod("OnPopulateMesh", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance, null, new[] { typeof(VertexHelper) }, null).Invoke(ring, new object[] { vh });
                int track = (ProgressRing.Segments + 1) * 2, arc = (Mathf.CeilToInt(ProgressRing.Segments * ring.Value) + 1) * 2;
                Assert.AreEqual(track + arc, vh.currentVertCount);
            }
            ring.Value = 2f;
            Assert.AreEqual(1f, ring.Value, "clamped");
        }
    }
}
