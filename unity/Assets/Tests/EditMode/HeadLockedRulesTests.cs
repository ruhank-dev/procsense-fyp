using NUnit.Framework;
using ProcSense.VR.Audio;
using ProcSense.VR.UI.Accessibility;
using ProcSense.VR.Views;
using UnityEngine;

namespace ProcSense.Tests.EditMode
{
    /// <summary>The comfort rules for content that follows the head (P3-UIX-08), and the two head-locked views the project has keep them.</summary>
    public class HeadLockedRulesTests
    {
        [Test]
        public void Each_rule_catches_what_it_should()
        {
            Assert.IsEmpty(Readability.HeadLockedProblems(new Vector3(0, -.3f, 1f), .5f));
            StringAssert.Contains("nearer", Readability.HeadLockedProblems(new Vector3(0, 0, .5f), .2f)[0]);
            StringAssert.Contains("up or down", Readability.HeadLockedProblems(new Vector3(0, -.8f, 1f), .2f)[0]);
            StringAssert.Contains("up or down", Readability.HeadLockedProblems(new Vector3(0, .4f, 1f), .2f)[0]);
            StringAssert.Contains("side", Readability.HeadLockedProblems(new Vector3(.6f, 0, 1f), .2f)[0]);
            StringAssert.Contains("wide", Readability.HeadLockedProblems(new Vector3(0, 0, 1f), 1f)[0]);
            StringAssert.Contains("behind", Readability.HeadLockedProblems(new Vector3(0, 0, -1f), .2f)[0]);
        }

        [Test]
        public void The_subtitle_line_follows_the_head_within_the_rules()
        {
            var go = new GameObject("subtitle check");
            try
            {
                var view = go.AddComponent<SubtitleView>();
                view.Build();
                var rect = (RectTransform)view.Canvas.transform;
                float width = rect.sizeDelta.x * rect.localScale.x / ProcSense.VR.UI.Accessibility.AccessibilitySettings.SubtitleScale;   // at 100 %
                CollectionAssert.IsEmpty(Readability.HeadLockedProblems(view.Offset, width));
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void The_camera_hud_anchor_sits_within_the_rules()
        {
            var go = new GameObject("hud check");
            try { CollectionAssert.IsEmpty(Readability.HeadLockedProblems(go.AddComponent<CameraHudAnchor>().Offset, .5f)); }
            finally { Object.DestroyImmediate(go); }
        }
    }
}
