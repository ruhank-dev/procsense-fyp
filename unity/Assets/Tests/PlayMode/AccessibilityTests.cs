using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using ProcSense.Core;
using ProcSense.VR.Bridge;
using ProcSense.VR.UI.Accessibility;
using ProcSense.VR.Views;
using UnityEngine;

namespace ProcSense.Tests.PlayMode
{
    /// <summary>Readability and comfort settings, text size in the headset and colour contrast (p0-vr-interaction-ui-s10, docs/plans/p0/p0-vr-interaction-ui/ui-guidelines.md).</summary>
    public sealed class AccessibilityTests
    {
        private readonly List<GameObject> _made = new List<GameObject>();
        private SimulationHost _host;

        private T Make<T>(string name) where T : Component
        {
            var go = new GameObject(name);
            _made.Add(go);
            return go.AddComponent<T>();
        }

        [SetUp]
        public void Setup()
        {
            AccessibilitySettings.Reset();
            if (SimulationHost.Instance != null) Object.DestroyImmediate(SimulationHost.Instance.gameObject);   // a host an earlier test left running would be the one the views follow
            _host = Make<SimulationHost>("a11y host");
            _host.AutoTick = false; _host.SaveLogOnDestroy = false;
            _host.StartSession("VAR-S1-A", 42, SessionMode.Practice);
        }

        [TearDown]
        public void Cleanup()
        {
            foreach (var go in _made) Object.DestroyImmediate(go);
            _made.Clear();
            AccessibilitySettings.Reset();
        }

        [Test]
        public void A_panel_size_change_rescales_every_panel_at_once_and_is_kept()
        {
            var tablet = Make<TabletView>("a11y tablet");
            tablet.Build();
            var canvas = tablet.Canvas.GetComponent<AccessibleCanvas>();
            Assert.AreEqual(.0006f, tablet.Canvas.transform.localScale.x, 1e-7);
            AccessibilitySettings.NextUiScale();
            Assert.AreEqual(.00075f, tablet.Canvas.transform.localScale.x, 1e-7);
            Assert.AreEqual(1, PlayerPrefs.GetInt(AccessibilitySettings.UiScaleKey), "kept on this headset");
            Assert.AreEqual(1.25f, AccessibilitySettings.UiScale);
            AccessibilitySettings.NextUiScale(); AccessibilitySettings.NextUiScale();
            Assert.AreEqual(.0006f, canvas.transform.localScale.x, 1e-7, "100, 125, 150 and back");
        }

        [Test]
        public void Subtitles_grow_with_the_subtitle_size_and_other_panels_do_not()
        {
            var subs = Make<ProcSense.VR.Audio.SubtitleView>("a11y subtitles");
            var monitor = Make<MonitorView>("a11y monitor");
            float sub = subs.Canvas.transform.localScale.x, mon = monitor.GetComponentInChildren<Canvas>().transform.localScale.x;
            AccessibilitySettings.NextSubtitleScale();
            Assert.AreEqual(sub * 1.3f, subs.Canvas.transform.localScale.x, 1e-7);
            Assert.AreEqual(mon, monitor.GetComponentInChildren<Canvas>().transform.localScale.x, 1e-7);
        }

        [Test]
        public void The_settings_tab_changes_and_shows_each_choice()
        {
            var tablet = Make<TabletView>("a11y tablet");
            tablet.Build();
            tablet.ShowTab(TabletView.SettingsTabId);
            var tab = tablet.SettingsTab;
            Assert.IsTrue(tab.gameObject.activeInHierarchy);
            tab.HandButton.onClick.Invoke();
            Assert.IsTrue(AccessibilitySettings.LeftHanded);
            StringAssert.Contains("Right stick walks", tab.SummaryText);
            tab.SubtitleButton.onClick.Invoke();
            StringAssert.Contains("Subtitles 130 %", tab.SummaryText);
            tab.ResetButton.onClick.Invoke();
            Assert.IsFalse(AccessibilitySettings.LeftHanded);
            StringAssert.Contains("Panel size 100 %", tab.SummaryText);
        }

        [Test]
        public void Every_text_colour_on_its_panel_meets_WCAG_AA()
        {
            var pairs = new (string name, Color text, Color back)[]
            {
                ("text on panel", UiKit.TextColor, UiKit.PanelColor), ("dim text on panel", UiKit.DimTextColor, UiKit.PanelColor), ("warning on panel", UiKit.WarnColor, UiKit.PanelColor),
                ("button label", UiKit.TextColor, UiKit.ButtonColor),   // a disabled button is exempt (WCAG 1.4.3 inactive components)
                ("monitor normal", new Color(.45f, .85f, .55f), UiKit.PanelColor), ("monitor watch", new Color(1f, .86f, .35f), UiKit.PanelColor),
                ("monitor worry", new Color(1f, .62f, .25f), UiKit.PanelColor), ("monitor emergency", new Color(1f, .36f, .36f), UiKit.PanelColor),
                ("pleth on black", new Color(.45f, .8f, 1f), Color.black),
            };
            var failing = pairs.Where(p => Readability.Contrast(p.text, p.back) < Readability.MinContrast).Select(p => p.name + " " + Readability.Contrast(p.text, p.back).ToString("0.0")).ToList();
            Assert.IsEmpty(failing, string.Join("; ", failing));
        }

        [Test]
        public void The_text_on_the_trainee_panels_is_at_least_24_headset_pixels_where_it_is_read()
        {
            var tablet = Make<TabletView>("a11y tablet");
            tablet.Build();
            Make<MonitorView>("a11y monitor");
            Make<ProcSense.VR.Audio.SubtitleView>("a11y subtitles");
            Make<ProcSense.VR.UI.Results.PhoneView>("a11y phone");
            Make<ProcSense.VR.UI.Results.ResultsView>("a11y results");
            Make<ProcSense.VR.Records.Emr.EmrShell>("a11y emr");
            Make<ProcSense.VR.UI.Checklist.ChecklistBoard>("a11y who board");
            Make<ProcSense.VR.UI.Checklist.WardChecklistPanel>("a11y ward checklist");
            Make<ProcSense.VR.UI.Debrief.DebriefView>("a11y debrief");
            Make<EventCardsView>("a11y cards").Build();
            var small = Object.FindObjectsByType<AccessibleCanvas>(FindObjectsInactive.Include, FindObjectsSortMode.None).SelectMany(Readability.TooSmall).ToList();
            Assert.IsEmpty(small, string.Join("\n", small));
        }
    }
}
