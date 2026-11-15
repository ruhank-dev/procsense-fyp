using System.Linq;
using NUnit.Framework;
using ProcSense.VR.Settings;
using ProcSense.VR.UI.Accessibility;
using ProcSense.VR.UI.Kit;
using ProcSense.VR.UI.SettingsUi;
using ProcSense.VR.UI.Theme;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ProcSense.Tests.EditMode
{
    /// <summary>
    /// P3-SET-02: the settings panel has the six tabs; every control of every tab changes its GameSettings value at once (and so is saved and applied); the Session tab's actions
    /// wait for their confirmation; every control is a full-size target and every label readable at the panel's distance.
    /// </summary>
    public class SettingsPanelTests
    {
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

        private ISettingsStore _store;
        private SettingsPanel _panel;

        [SetUp]
        public void SetUp()
        {
            _store = GameSettings.Store;
            GameSettings.Store = new MemoryStore();
            GameSettings.Reset();
            UiMotion.Clear();
            _panel = SettingsPanel.Toggle(null);
        }

        [TearDown]
        public void TearDown()
        {
            if (_panel != null) Object.DestroyImmediate(_panel.gameObject);
            UiMotion.Clear();
            GameSettings.Store = _store;
        }

        private static SettingsData S => GameSettings.Current;

        private void Pick(string choice, int option) => _panel.Find<KitSegmented>("Choice " + choice).Option(option).onClick.Invoke();
        private void Flip(string toggle) { var t = _panel.Find<KitToggle>("Toggle " + toggle); t.IsOn = !t.IsOn; }
        private void Slide(string slider, float v) => _panel.Find<Transform>("Slider " + slider).GetComponentInChildren<Slider>().value = v;

        [Test]
        public void It_has_the_six_tabs_and_one_panel_at_a_time()
        {
            CollectionAssert.AreEqual(new[] { "Input", "Audio", "Subtitles", "Comfort", "Display", "Session" }, SettingsPanel.TabNames);
            Assert.AreSame(_panel, SettingsPanel.Open);
            Assert.IsNull(SettingsPanel.Toggle(null), "a second press closes it");
            Assert.IsNull(SettingsPanel.Open);
            _panel = SettingsPanel.Toggle(null);
        }

        [Test]
        public void Every_control_changes_its_setting_at_once()
        {
            _panel.Show(SettingsPanel.Tab.Interaction);
            Pick("Interaction", 1);
            Assert.AreEqual(InteractionModeSetting.Voice, S.interactionMode);
            StringAssert.StartsWith("Voice:", _panel.Find<TMP_Text>("Explanation").text, "the explanation follows the mode");
            Flip("Spoken feedback when voice fails");
            Assert.IsFalse(S.voiceFeedback);

            _panel.Show(SettingsPanel.Tab.Audio);
            Slide("Master", .3f); Slide("Voices", .4f); Slide("Music", .1f); Slide("Sound effects", .5f); Slide("Ambience", .2f); Slide("Interface", .6f);
            Assert.AreEqual((.3f, .4f, .1f, .5f, .2f, .6f), (S.masterVolume, S.voiceVolume, S.musicVolume, S.effectsVolume, S.ambienceVolume, S.uiVolume));

            _panel.Show(SettingsPanel.Tab.Subtitles);
            Flip("Subtitles"); Pick("Size", 2); Flip("Dark plate behind subtitles");
            Assert.AreEqual((false, 2, false), (S.subtitles, S.subtitleSize, S.subtitleBackground));

            _panel.Show(SettingsPanel.Tab.Comfort);
            Pick("Turning", 1); Pick("Snap angle", 3); Slide("Turn speed", 1f); Pick("Moving", 1); Flip("Comfort vignette while moving");
            Assert.AreEqual((TurnStyle.Smooth, 90f, 180f, MoveStyle.Continuous, false), (S.turnStyle, S.snapAngle, S.turnSpeed, S.moveStyle, S.comfortVignette));

            _panel.Show(SettingsPanel.Tab.Display);
            Pick("Panel size", 1); Flip("High contrast"); Flip("Reduce motion"); Pick("Dominant hand", 1);
            Assert.AreEqual((1, true, true, true), (S.panelSize, S.highContrast, S.reduceMotion, S.leftHanded));
        }

        [Test]
        public void A_tab_shows_the_saved_values()
        {
            GameSettings.Update(d => { d.subtitleSize = 1; d.subtitles = false; });
            _panel.Show(SettingsPanel.Tab.Subtitles);
            Assert.AreEqual(1, _panel.Find<KitSegmented>("Choice Size").Selected);
            Assert.IsFalse(_panel.Find<KitToggle>("Toggle Subtitles").IsOn);
        }

        [Test]
        public void Session_actions_and_reset_wait_for_their_confirmation()
        {
            int returned = 0, quit = 0;
            _panel.ReturnToMenu = () => returned++;
            _panel.Quit = () => quit++;
            _panel.Show(SettingsPanel.Tab.Session);
            _panel.Find<Button>("Button Quit ProcSense").onClick.Invoke();
            Assert.AreEqual(0, quit, "nothing until confirmed");
            _panel.Dialog.Cancel.onClick.Invoke();
            Assert.AreEqual(0, quit);
            _panel.Find<Button>("Button Quit ProcSense").onClick.Invoke();
            _panel.Dialog.Confirm.onClick.Invoke();
            Assert.AreEqual(1, quit);

            _panel.Find<Button>("Button Return to main menu").onClick.Invoke();
            _panel.Dialog.Confirm.onClick.Invoke();
            Assert.AreEqual(1, returned);
            Assert.IsNull(SettingsPanel.Open, "returning closes the panel");
            _panel = null;

            _panel = SettingsPanel.Toggle(null);
            GameSettings.Update(d => d.panelSize = 2);
            _panel.Show(SettingsPanel.Tab.Display);
            _panel.Find<Button>("Button Reset all settings").onClick.Invoke();
            Assert.AreEqual(2, S.panelSize);
            _panel.Dialog.Confirm.onClick.Invoke();
            Assert.AreEqual(0, S.panelSize);
        }

        [Test]
        public void A_volume_slider_moves_its_mixer_group()   // P3-SET-03: slider -> GameSettings -> the level AudioDirector sets on the group (the mixer itself only runs in Play Mode)
        {
            _panel.Show(SettingsPanel.Tab.Audio);
            foreach (var (slider, group) in new[] { ("Master", ProcSense.VR.Audio.MixGroup.Master), ("Voices", ProcSense.VR.Audio.MixGroup.Voice), ("Music", ProcSense.VR.Audio.MixGroup.Music),
                                                    ("Sound effects", ProcSense.VR.Audio.MixGroup.Effects), ("Ambience", ProcSense.VR.Audio.MixGroup.Ambience), ("Interface", ProcSense.VR.Audio.MixGroup.UI) })
            {
                Slide(slider, .5f);
                Assert.AreEqual(.5f, ProcSense.VR.Audio.AudioDirector.SettingFor(group, GameSettings.Current), 1e-4, slider + " reaches its group");
                float half = ProcSense.VR.Audio.AudioDirector.Level(group, ProcSense.VR.Audio.AudioDirector.SettingFor(group, GameSettings.Current), ProcSense.VR.Audio.MixState.Results, 0f);
                Slide(slider, 1f);
                float full = ProcSense.VR.Audio.AudioDirector.Level(group, ProcSense.VR.Audio.AudioDirector.SettingFor(group, GameSettings.Current), ProcSense.VR.Audio.MixState.Results, 0f);
                Assert.AreEqual(6.02f, full - half, .01f, slider + ": half volume is 6 dB down");
            }
        }

        [Test]
        public void Switching_the_interaction_mode_mid_session_changes_nothing_in_the_session()   // P3-SET-04
        {
            string repo = System.IO.Path.GetFullPath(System.IO.Path.Combine(Application.dataPath, "..", ".."));
            var reader = new ProcSense.Core.Content.CompositeContentReader(new ProcSense.Core.Content.FileContentReader(System.IO.Path.Combine(repo, "content")),
                                                                         new ProcSense.Core.Content.FileContentReader(System.IO.Path.Combine(repo, "tests", "fixtures")));
            var host = new GameObject("mode host").AddComponent<ProcSense.VR.Bridge.SimulationHost>();
            try
            {
                host.AutoTick = false;
                host.SaveLogOnDestroy = false;
                host.StartSession("VAR-S1-A", 42, ProcSense.Core.SessionMode.Practice, reader);
                host.Apply(new ProcSense.Core.Actions.SimAction("ask_identity", "patient"));
                string hash = host.Session.Log.Hash();
                int count = host.Session.Log.Count;
                _panel.Show(SettingsPanel.Tab.Interaction);
                Pick("Interaction", 1);
                Pick("Interaction", 0);
                Pick("Interaction", 1);
                Assert.AreEqual(InteractionModeSetting.Voice, S.interactionMode);
                Assert.AreEqual((hash, count), (host.Session.Log.Hash(), host.Session.Log.Count));
            }
            finally { Object.DestroyImmediate(host.gameObject); }
        }

        [Test]
        public void Subtitles_and_their_plate_follow_the_settings()   // P3-SET-06
        {
            var go = new GameObject("speaker");
            try
            {
                var subtitles = ProcSense.VR.Views.UiKit.WorldCanvas("Subtitles", go.transform, new Vector2(700, 150), .0009f);
                var plate = subtitles.transform.Find("Background").GetComponent<Image>();
                float alpha = plate.color.a;
                var other = ProcSense.VR.Views.UiKit.WorldCanvas("EMR", go.transform, new Vector2(700, 150), .0009f);
                // in Play Mode each canvas follows the settings' Changed event; Edit Mode runs no Awake, so the test applies the canvases itself after each change
                void Apply() { subtitles.GetComponent<AccessibleCanvas>().Apply(); other.GetComponent<AccessibleCanvas>().Apply(); }
                _panel.Show(SettingsPanel.Tab.Subtitles);
                Flip("Dark plate behind subtitles"); Apply();
                Assert.AreEqual(0f, plate.color.a, "no plate");
                Flip("Subtitles"); Apply();
                Assert.IsFalse(subtitles.enabled, "subtitles off");
                Assert.IsTrue(other.enabled, "other panels are not subtitles");
                Flip("Subtitles"); Flip("Dark plate behind subtitles"); Apply();
                Assert.IsTrue(subtitles.enabled);
                Assert.AreEqual(alpha, plate.color.a, 1e-4, "the plate comes back as it was");
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void High_contrast_darkens_the_glass_and_lifts_the_text_live()   // P3-SET-06
        {
            var normalTint = GlassPanel.Dark.GetColor("_Color");
            float normalSecondary = UiTheme.Current.TextSecondary.a;
            _panel.Show(SettingsPanel.Tab.Display);
            Flip("High contrast");
            Assert.IsTrue(S.highContrast);
            Assert.Greater(UiTheme.Current.GlassTint.a, .95f);
            Assert.Greater(UiTheme.Current.TextSecondary.a, normalSecondary);
            Assert.Less(Vector4.Distance(UiTheme.Current.GlassTint, GlassPanel.Dark.GetColor("_Color")), 1e-3f, "open panels change at once");
            Assert.IsTrue(_panel.Find<KitToggle>("Toggle High contrast").IsOn, "the panel was redrawn with the new value");
            foreach (var fill in new[] { Color.white, Color.black })
                Assert.GreaterOrEqual(Readability.Contrast(UiTheme.Current.TextSecondary, UiTheme.Over(UiTheme.Current.GlassTint, fill)), 7f, "AAA-level contrast for secondary text");
            Flip("High contrast");
            Assert.Less(Vector4.Distance(normalTint, GlassPanel.Dark.GetColor("_Color")), 1e-3f, "and back");
        }

        [Test]
        public void Every_tab_is_readable_and_its_targets_full_size()
        {
            float min = UiTheme.Current.MinTarget;
            foreach (SettingsPanel.Tab tab in System.Enum.GetValues(typeof(SettingsPanel.Tab)))
            {
                _panel.Show(tab);
                foreach (var s in _panel.Canvas.GetComponentsInChildren<Selectable>(true))
                {
                    var hit = s is Slider slider ? slider.handleRect : (RectTransform)s.transform;
                    Assert.GreaterOrEqual(hit.rect.height, min - .01f, tab + ": " + s.name);
                }
                foreach (var label in _panel.Canvas.GetComponentsInChildren<TMP_Text>(true).Where(l => l.text.Length > 0))
                    Assert.GreaterOrEqual(Readability.HeadsetPixels(label, SettingsPanel.Distance), Readability.MinHeadsetPixels - .01f, tab + ": " + label.name);
            }
        }
    }
}
