using System.Collections.Generic;
using NUnit.Framework;
using ProcSense.VR.Settings;
using ProcSense.VR.UI.Accessibility;

namespace ProcSense.Tests.EditMode
{
    /// <summary>The one settings record (P3-SET-01): defaults, migration of the p0 accessibility keys, the round trip, ranges, a corrupt or newer record, change events.</summary>
    public class GameSettingsTests
    {
        private sealed class MemoryStore : ISettingsStore
        {
            public readonly Dictionary<string, string> Values = new Dictionary<string, string>();
            public int Saves;
            public string GetString(string key) => Values.TryGetValue(key, out var v) ? v : "";
            public bool HasKey(string key) => Values.ContainsKey(key);
            public int GetInt(string key, int fallback) => Values.TryGetValue(key, out var v) && int.TryParse(v, out var i) ? i : fallback;
            public void SetString(string key, string value) => Values[key] = value;
            public void DeleteKey(string key) => Values.Remove(key);
            public void Save() => Saves++;
        }

        private MemoryStore _store;
        private ISettingsStore _before;

        [SetUp] public void Swap() { _before = GameSettings.Store; _store = new MemoryStore(); GameSettings.Store = _store; }
        [TearDown] public void Restore() => GameSettings.Store = _before;

        [Test]
        public void A_new_headset_gets_the_defaults_and_saves_them()
        {
            var s = GameSettings.Current;
            Assert.AreEqual(InteractionModeSetting.Menu, s.interactionMode);
            Assert.IsTrue(s.subtitles && s.subtitleBackground && s.voiceFeedback && s.comfortVignette);
            Assert.AreEqual((TurnStyle.Snap, 45f, MoveStyle.Teleport), (s.turnStyle, s.snapAngle, s.moveStyle));
            Assert.AreEqual((0, 0, false, false, false), (s.panelSize, s.subtitleSize, s.highContrast, s.reduceMotion, s.leftHanded));
            Assert.AreEqual(SettingsData.CurrentVersion, s.version);
            Assert.IsTrue(_store.HasKey(GameSettings.Key));
            Assert.AreEqual("", GameSettings.LoadProblem);
        }

        [Test]
        public void The_p0_accessibility_keys_are_migrated_and_then_removed()
        {
            _store.Values[GameSettings.OldPanelKey] = "2";
            _store.Values[GameSettings.OldSubtitleKey] = "1";
            _store.Values[GameSettings.OldHandKey] = "1";
            GameSettings.Store = _store;   // forget the loaded record
            var s = GameSettings.Current;
            Assert.AreEqual((2, 1, true), (s.panelSize, s.subtitleSize, s.leftHanded));
            Assert.AreEqual(1.5f, AccessibilitySettings.UiScale);
            Assert.IsTrue(AccessibilitySettings.LeftHanded);
            foreach (var k in new[] { GameSettings.OldPanelKey, GameSettings.OldSubtitleKey, GameSettings.OldHandKey }) Assert.IsFalse(_store.HasKey(k), k);
        }

        [Test]
        public void A_change_is_saved_and_read_back_the_same()
        {
            GameSettings.Update(s => { s.interactionMode = InteractionModeSetting.Voice; s.musicVolume = .25f; s.turnStyle = TurnStyle.Smooth; s.highContrast = true; s.snapAngle = 30f; });
            string saved = _store.GetString(GameSettings.Key);
            GameSettings.Store = _store;   // a fresh start of the app
            var s = GameSettings.Current;
            Assert.AreEqual((InteractionModeSetting.Voice, .25f, TurnStyle.Smooth, true, 30f), (s.interactionMode, s.musicVolume, s.turnStyle, s.highContrast, s.snapAngle));
            Assert.AreEqual(saved, UnityEngine.JsonUtility.ToJson(s));
        }

        [Test]
        public void Values_outside_their_range_are_brought_back_in()
        {
            GameSettings.Update(s => { s.masterVolume = 3f; s.voiceVolume = -1f; s.panelSize = 9; s.subtitleSize = -2; s.snapAngle = 17f; s.turnSpeed = 1000f; });
            var s2 = GameSettings.Current;
            Assert.AreEqual((1f, 0f, 2, 0, 45f, 180f), (s2.masterVolume, s2.voiceVolume, s2.panelSize, s2.subtitleSize, s2.snapAngle, s2.turnSpeed));
        }

        [Test]
        public void A_corrupt_or_newer_record_gives_the_defaults_and_says_why()
        {
            _store.Values[GameSettings.Key] = "{ not json";
            GameSettings.Store = _store;
            Assert.AreEqual(0, GameSettings.Current.panelSize);
            StringAssert.Contains("could not be read", GameSettings.LoadProblem);

            _store.Values[GameSettings.Key] = "{\"version\":99,\"panelSize\":2}";
            GameSettings.Store = _store;
            Assert.AreEqual(0, GameSettings.Current.panelSize);
            StringAssert.Contains("newer version", GameSettings.LoadProblem);
        }

        [Test]
        public void Appliers_hear_a_change_once_and_nothing_when_nothing_changed()
        {
            int heard = 0, legacy = 0;
            void OnChange(SettingsData _) => heard++;
            void OnLegacy() => legacy++;
            GameSettings.Changed += OnChange;
            AccessibilitySettings.Changed += OnLegacy;
            try
            {
                GameSettings.Update(s => s.reduceMotion = true);
                GameSettings.Update(s => s.reduceMotion = true);
                AccessibilitySettings.NextUiScale();
                Assert.AreEqual(2, heard);
                Assert.AreEqual(2, legacy, "the p0 panels still hear every change");
            }
            finally { GameSettings.Changed -= OnChange; AccessibilitySettings.Changed -= OnLegacy; }
        }

        [Test]
        public void Changing_a_copy_does_not_change_the_settings()
        {
            var copy = GameSettings.Current;
            copy.panelSize = 2;
            Assert.AreEqual(0, GameSettings.Current.panelSize);
        }
    }
}
