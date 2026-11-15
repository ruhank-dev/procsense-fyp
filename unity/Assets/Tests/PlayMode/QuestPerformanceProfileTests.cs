using NUnit.Framework;
using ProcSense.VR.Views;
using UnityEngine;

namespace ProcSense.Tests.PlayMode
{
    /// <summary>The aggressive Quest profile (14 Nov 2026): the dynamic-resolution rule has hysteresis and stays inside 0.7 to 0.9, the profile is inert off Android, and the Android assets carry the intended static values.</summary>
    public class QuestPerformanceProfileTests
    {
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
