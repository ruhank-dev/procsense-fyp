using NUnit.Framework;
using ProcSense.VR.Views;
using UnityEngine;

namespace ProcSense.Tests.PlayMode
{
    /// <summary>The Quest profile (14 Nov 2026, sharpness revision 15 Nov 2026): the dynamic-resolution rule has hysteresis and stays inside 0.85 to 1.0, the profile is inert off Android, and the effective eye
    /// pixels are the allocated eye texture times the viewport scale.</summary>
    public class QuestPerformanceProfileTests
    {
        [TearDown]
        public void RestoreRange() { QuestPerformanceProfile.RangeMin = QuestPerformanceProfile.MinScale; QuestPerformanceProfile.RangeMax = QuestPerformanceProfile.MaxScale; }

        [Test]
        public void The_sharpness_profile_values_and_the_effective_eye_pixels()
        {
            Assert.AreEqual((1f, 0.9f, 0.85f, 1f, 0.5f), (QuestPerformanceProfile.EyeBufferScale, QuestPerformanceProfile.StartScale, QuestPerformanceProfile.MinScale, QuestPerformanceProfile.MaxScale, QuestPerformanceProfile.Foveation));
            Assert.AreEqual((QuestPerformanceProfile.MinScale, QuestPerformanceProfile.MaxScale), (QuestPerformanceProfile.RangeMin, QuestPerformanceProfile.RangeMax), "production range = the constants");
            // Quest 2 runtime default 1440 x 1584 per eye: the start scale draws 1296 x 1426; the first overhaul profile drew 0.9 x 0.889 of it = 1152 x 1267 or 1268 depending on rounding.
            Assert.AreEqual(new Vector2Int(1296, 1426), QuestPerformanceProfile.EffectiveEyePixels(1440, 1584, QuestPerformanceProfile.ViewportFor(QuestPerformanceProfile.StartScale)));
            Assert.AreEqual(new Vector2Int(1152, 1268), QuestPerformanceProfile.EffectiveEyePixels(1296, 1426, 0.8f / 0.9f));
            StringAssert.Contains("per eye", QuestPerformanceProfile.Describe(), "the device log line reads the values back from XRSettings");
        }

        [Test]
        public void A_diagnostic_range_overrides_the_controller_and_is_restored()
        {
            QuestPerformanceProfile.RangeMin = 0.7f; QuestPerformanceProfile.RangeMax = 0.9f;
            var c = new QuestPerformanceProfile.Controller { Cooldown = 0, OverSamples = 1, UnderSamples = 1 }; float s = 0.8f;
            for (int i = 0; i < 200; i++) s = c.Step(30f, s, 0.7f);
            Assert.AreEqual(0.7f, s, 1e-4f);
            for (int i = 0; i < 200; i++) s = c.Step(5f, s, 0.7f);
            Assert.AreEqual(0.9f, s, 1e-4f);
        }

        [Test]
        public void Dynamic_resolution_steps_down_only_after_sustained_over_budget_and_up_only_after_a_long_quiet_spell()
        {
            var c = new QuestPerformanceProfile.Controller(); float s = QuestPerformanceProfile.StartScale;
            for (int i = 0; i < c.OverSamples - 1; i++) s = c.Step(20f, s);
            Assert.AreEqual(QuestPerformanceProfile.StartScale, s, 1e-4f, "a short spike does not change the scale");
            s = c.Step(20f, s);
            Assert.Less(s, QuestPerformanceProfile.StartScale, "a sustained overrun lowers it");
            float lowered = s;
            for (int i = 0; i < c.Cooldown + 5; i++) s = c.Step(11f, s);
            Assert.AreEqual(lowered, s, 1e-4f, "inside the comfortable band nothing moves (hysteresis)");
            for (int i = 0; i < c.UnderSamples + c.Cooldown + 5; i++) s = c.Step(8f, s);
            Assert.Greater(s, lowered, "a long quiet spell raises it again");
        }

        [Test]
        public void Dynamic_resolution_never_leaves_the_profile_range()
        {
            var c = new QuestPerformanceProfile.Controller { Cooldown = 0, OverSamples = 1, UnderSamples = 1 }; float s = QuestPerformanceProfile.StartScale;
            for (int i = 0; i < 200; i++) s = c.Step(30f, s);
            Assert.AreEqual(QuestPerformanceProfile.MinScale, s, 1e-4f);
            for (int i = 0; i < 200; i++) s = c.Step(5f, s);
            Assert.AreEqual(QuestPerformanceProfile.MaxScale, s, 1e-4f);
        }

        [Test]
        public void The_profile_is_neutral_off_the_android_player_and_the_start_scale_fits_the_allocated_buffer()
        {
            Assert.IsFalse(QuestPerformanceProfile.Active, "the Editor and desktop keep their own settings");
            Assert.AreEqual((0f, 1f), (QuestPerformanceProfile.ProfileFoveation, QuestPerformanceProfile.ProfileEyeScale));
            Assert.LessOrEqual(QuestPerformanceProfile.ViewportFor(QuestPerformanceProfile.StartScale), 1f);
            Assert.AreEqual(QuestPerformanceProfile.StartScale / QuestPerformanceProfile.EyeBufferScale, QuestPerformanceProfile.ViewportFor(QuestPerformanceProfile.StartScale), 1e-4f);
        }
    }
}
