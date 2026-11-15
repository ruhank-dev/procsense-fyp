using NUnit.Framework;
using ProcSense.VR.UI.Accessibility;
using ProcSense.VR.Views;
using UnityEngine;

namespace ProcSense.Tests.EditMode
{
    /// <summary>Dynamic resolution never draws a clinical text panel below 0.8 while it is read, and the reading cone and eye-buffer estimate are plain geometry (14 Nov 2026 UTC).</summary>
    public class ClinicalTextFloorTests
    {
        [Test]
        public void Over_budget_frames_lower_the_scale_only_to_the_text_floor()
        {
            var ctl = new QuestPerformanceProfile.Controller { Cooldown = 0 };
            float scale = QuestPerformanceProfile.StartScale;
            for (int i = 0; i < 200; i++) scale = ctl.Step(20f, scale, ClinicalText.FloorScale);
            Assert.AreEqual(ClinicalText.FloorScale, scale, 1e-5f, "reading: held at the floor");
            for (int i = 0; i < 200; i++) scale = ctl.Step(20f, scale);
            Assert.AreEqual(QuestPerformanceProfile.MinScale, scale, 1e-5f, "not reading: may go to the profile minimum");
        }

        [Test]
        public void A_scale_below_the_floor_is_raised_at_once_when_reading_starts()
        {
            var ctl = new QuestPerformanceProfile.Controller();
            Assert.AreEqual(ClinicalText.FloorScale, ctl.Step(11f, QuestPerformanceProfile.MinScale, ClinicalText.FloorScale), 1e-5f);
            Assert.AreEqual(QuestPerformanceProfile.MaxScale, ctl.Step(11f, .5f, 5f), 1e-5f, "floor clamped to the profile range");
        }

        [Test]
        public void The_floor_is_the_profile_start_scale_and_inside_its_range()
        {
            Assert.AreEqual(QuestPerformanceProfile.StartScale, ClinicalText.FloorScale, 1e-5f);
            Assert.That(ClinicalText.FloorScale, Is.InRange(QuestPerformanceProfile.MinScale, QuestPerformanceProfile.MaxScale));
        }

        [Test]
        public void Reading_cone_is_distance_and_angle()
        {
            Assert.IsTrue(ClinicalText.InView(Vector3.zero, Vector3.forward, new Vector3(0, 0, 1.5f)));
            Assert.IsTrue(ClinicalText.InView(Vector3.zero, Vector3.forward, Quaternion.Euler(0, 25, 0) * Vector3.forward));
            Assert.IsFalse(ClinicalText.InView(Vector3.zero, Vector3.forward, Quaternion.Euler(0, 40, 0) * Vector3.forward));
            Assert.IsFalse(ClinicalText.InView(Vector3.zero, Vector3.forward, new Vector3(0, 0, 2.5f)));
        }

        [Test]
        public void Eye_buffer_pixels_per_degree_follow_the_render_scale()
        {
            // 1440 px over an assumed 90 degrees: 1440 / 2 / tan(45) * pi / 180 = 12.57 px per degree at 1.0
            Assert.AreEqual(12.566f, Readability.Quest2EyeBufferPixelsPerDegree(1f), 0.01f);
            Assert.AreEqual(12.566f * .8f, Readability.Quest2EyeBufferPixelsPerDegree(.8f), 0.01f);
        }

        [Test]
        public void Only_the_clinical_panels_are_marked()
        {
            foreach (var n in new[] { "Tablet", "EMR shell", "EMR", "Monitor", "Game flow", "Main menu" }) Assert.IsTrue(ClinicalText.IsClinical(n), n);
            foreach (var n in new[] { "Phone", "Subtitles", "WHO board", null }) Assert.IsFalse(ClinicalText.IsClinical(n), n ?? "null");
        }
    }
}
