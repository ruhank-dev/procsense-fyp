using NUnit.Framework;
using ProcSense.VR.UI.Accessibility;
using ProcSense.VR.UI.Theme;
using UnityEngine;

namespace ProcSense.Tests.EditMode
{
    /// <summary>
    /// The design tokens (P3-UIX-01) keep the readability rules (p0-vr-interaction-ui-s10, P3-UIX-08): every text colour reaches WCAG AA on glass over
    /// whatever the room puts behind a panel, text on an accent fill reads, every type size is at least 24 headset pixels at every allowed distance,
    /// and buttons are big enough to hit.
    /// </summary>
    public class UiThemeTests
    {
        // what can be behind a panel: the window in sunlight, a dark doorway, the grey floor, the warm wall
        private static readonly Color[] Backgrounds = { Color.white, Color.black, new Color(.5f, .5f, .5f), new Color(.85f, .8f, .7f) };
        private UiTheme _t;

        [SetUp] public void Make() => _t = UiTheme.Default();
        [TearDown] public void Drop() => Object.DestroyImmediate(_t);

        [Test]
        public void Every_text_colour_reaches_AA_on_glass_over_any_background()
        {
            var texts = new (string name, Color c)[]
            {
                ("primary", _t.TextPrimary), ("secondary", _t.TextSecondary), ("action", _t.ActionText), ("hint", _t.HintText), ("done", _t.DoneText), ("danger", _t.DangerText),
            };
            foreach (var bg in Backgrounds)
            {
                var glass = UiTheme.Over(_t.GlassTint, bg);
                foreach (var (name, c) in texts)
                {
                    float ratio = Readability.Contrast(UiTheme.Over(c, glass), glass);
                    Assert.GreaterOrEqual(ratio, Readability.MinContrast, name + " over " + bg + ": " + ratio.ToString("0.00"));
                }
            }
        }

        [Test]
        public void The_plans_first_tint_would_fail_in_front_of_a_bright_window()
        {
            var first = UiTheme.Hex(0x0F1A24, .62f);
            var glass = UiTheme.Over(first, Color.white);
            Assert.Less(Readability.Contrast(_t.ActionText, glass), Readability.MinContrast, "why the tint is 85 % (ui-design-tokens.md)");
        }

        [Test]
        public void Text_on_every_accent_fill_reaches_AA()
        {
            foreach (var fill in new[] { _t.Action, _t.Hint, _t.Danger, _t.Done })
                Assert.GreaterOrEqual(Readability.Contrast(_t.TextOnAccent, fill), Readability.MinContrast - .01f, fill.ToString());
        }

        [Test]
        public void Every_type_size_is_at_least_24_headset_pixels_at_every_allowed_distance()
        {
            foreach (float d in new[] { _t.NearestDistance, _t.WristDistance, _t.ContextDistance.x, _t.ContextDistance.y, _t.MenuDistance.x, _t.MenuDistance.y })
                foreach (float size in new[] { _t.Caption, _t.Body, _t.Heading, _t.Title })
                {
                    float px = Readability.HeadsetPixelsForHeight(size * _t.MetresPerUnit(d), d);
                    Assert.GreaterOrEqual(px, Readability.MinHeadsetPixels, size + " units at " + d + " m: " + px.ToString("0.0") + " px");
                }
        }

        [Test]
        public void A_fixed_scale_would_make_body_text_too_small_on_a_menu()
        {
            float px = Readability.HeadsetPixelsForHeight(_t.Body * _t.ReferenceMetresPerUnit, _t.MenuDistance.x);
            Assert.Less(px, Readability.MinHeadsetPixels, "why the scale grows with distance");
        }

        [Test]
        public void Buttons_are_at_least_two_degrees_wide_and_the_rest_is_sane()
        {
            float degrees = Mathf.Atan2(_t.MinTarget * _t.MetresPerUnit(1f), 1f) * Mathf.Rad2Deg;
            Assert.GreaterOrEqual(degrees, 2f);
            Assert.Greater(_t.TargetGap, 0);
            Assert.That(_t.Title > _t.Heading && _t.Heading > _t.Body && _t.Body > _t.Caption);
            Assert.That(_t.Close < _t.Open && _t.Hover < _t.Close);
            Assert.That(_t.NearestDistance <= _t.WristDistance && _t.WristDistance < _t.ContextDistance.x && _t.ContextDistance.y <= _t.MenuDistance.y);
            Assert.AreEqual(new[] { 8f, 16f, 24f, 32f }, _t.Spacing);
        }

        [Test]
        public void Current_falls_back_to_the_shipped_values_without_an_asset()
        {
            Assert.IsNotNull(UiTheme.Current);
            Assert.AreEqual(_t.GlassTint, UiTheme.Current.GlassTint);
        }
    }
}
