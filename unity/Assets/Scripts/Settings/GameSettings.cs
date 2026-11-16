using System;
using UnityEngine;

namespace ProcSense.VR.Settings
{
    /// <summary>Where the settings are kept: PlayerPrefs on the headset, memory in tests.</summary>
    public interface ISettingsStore
    {
        string GetString(string key);
        bool HasKey(string key);
        int GetInt(string key, int fallback);
        void SetString(string key, string value);
        void DeleteKey(string key);
        void Save();
    }

    public sealed class PlayerPrefsStore : ISettingsStore
    {
        public string GetString(string key) => PlayerPrefs.GetString(key, "");
        public bool HasKey(string key) => PlayerPrefs.HasKey(key);
        public int GetInt(string key, int fallback) => PlayerPrefs.GetInt(key, fallback);
        public void SetString(string key, string value) => PlayerPrefs.SetString(key, value);
        public void DeleteKey(string key) => PlayerPrefs.DeleteKey(key);
        public void Save() => PlayerPrefs.Save();
    }

    public enum InteractionModeSetting { Menu, Voice }
    public enum TurnStyle { Snap, Smooth }
    public enum MoveStyle { Teleport, Continuous }

    /// <summary>
    /// Every per-device choice in one versioned record (P3-SET-01), one field group per Settings tab (p3-13 section 1): Interaction, Audio, Subtitles,
    /// Comfort, Display. The Session tab holds actions only, nothing to keep. Nothing here is part of the simulation or the score; the session header
    /// records the interaction and comfort modes for analysis only.
    /// </summary>
    [Serializable]
    public sealed class SettingsData
    {
        public const int CurrentVersion = 1;
        public static readonly float[] PanelScales = { 1f, 1.25f, 1.5f };
        public static readonly float[] SubtitleScales = { 1f, 1.3f, 1.6f };

        public int version = CurrentVersion;

        // Interaction
        public InteractionModeSetting interactionMode = InteractionModeSetting.Menu;
        public bool voiceFeedback = true;

        // Audio, 0 to 1
        public float masterVolume = 1f, voiceVolume = 1f, musicVolume = .6f, effectsVolume = 1f, ambienceVolume = .7f, uiVolume = .8f;

        // Subtitles
        public bool subtitles = true;
        public int subtitleSize;          // index into SubtitleScales
        public bool subtitleBackground = true;

        // Comfort
        public TurnStyle turnStyle = TurnStyle.Snap;
        public float snapAngle = 45f;
        public float turnSpeed = 90f;
        public bool comfortVignette = true;
        public MoveStyle moveStyle = MoveStyle.Teleport;

        // Display
        public int panelSize;             // index into PanelScales
        public bool highContrast;
        public bool reduceMotion;
        public bool leftHanded;

        public SettingsData Copy() => (SettingsData)MemberwiseClone();

        /// <summary>Puts every value back inside its allowed range (a hand-edited or older record can hold anything).</summary>
        public void Clamp()
        {
            float C(float v) => float.IsNaN(v) ? 1f : Mathf.Clamp01(v);
            masterVolume = C(masterVolume); voiceVolume = C(voiceVolume); musicVolume = C(musicVolume);
            effectsVolume = C(effectsVolume); ambienceVolume = C(ambienceVolume); uiVolume = C(uiVolume);
            subtitleSize = Mathf.Clamp(subtitleSize, 0, SubtitleScales.Length - 1);
            panelSize = Mathf.Clamp(panelSize, 0, PanelScales.Length - 1);
            snapAngle = snapAngle == 30f || snapAngle == 45f || snapAngle == 60f || snapAngle == 90f ? snapAngle : 45f;
            turnSpeed = Mathf.Clamp(float.IsNaN(turnSpeed) ? 90f : turnSpeed, 30f, 180f);
            if (!Enum.IsDefined(typeof(InteractionModeSetting), interactionMode)) interactionMode = InteractionModeSetting.Menu;
            if (!Enum.IsDefined(typeof(TurnStyle), turnStyle)) turnStyle = TurnStyle.Snap;
            if (!Enum.IsDefined(typeof(MoveStyle), moveStyle)) moveStyle = MoveStyle.Teleport;
        }
    }

    /// <summary>
    /// The settings service (P3-SET-01): loads the record once, migrates the older single keys (the p0-vr-interaction-ui-s10 accessibility choices),
    /// saves every change at once and raises <see cref="Changed"/> so the appliers (audio, comfort, accessibility, interaction mode) follow. A record
    /// that cannot be read is replaced by the defaults and <see cref="LoadProblem"/> says so (the Settings panel shows it).
    /// </summary>
    public static class GameSettings
    {
        public const string Key = "procsense.settings";
        // the p0 keys this record replaces (AccessibilitySettings until 11 Nov 2026)
        public const string OldPanelKey = "procsense.ui_scale", OldSubtitleKey = "procsense.subtitle_scale", OldHandKey = "procsense.left_handed";

        private static ISettingsStore _store;
        private static SettingsData _data;

        /// <summary>Raised after any change, with the new values.</summary>
        public static event Action<SettingsData> Changed;

        /// <summary>Why the saved record was not used ("" when it was, or there was none).</summary>
        public static string LoadProblem { get; private set; } = "";

        /// <summary>The store in use; tests swap in a memory store (this also forgets the loaded record).</summary>
        public static ISettingsStore Store
        {
            get => _store ??= new PlayerPrefsStore();
            set { _store = value; _data = null; LoadProblem = ""; }
        }

        /// <summary>A copy of the current values: change them through <see cref="Update"/>.</summary>
        public static SettingsData Current => Data.Copy();

        private static SettingsData Data => _data ??= Load();

        /// <summary>Changes values, keeps them inside their ranges, saves and tells the appliers. Nothing happens when nothing changed.</summary>
        public static void Update(Action<SettingsData> change)
        {
            var next = Data.Copy();
            change(next);
            next.Clamp();
            if (JsonUtility.ToJson(next) == JsonUtility.ToJson(Data)) return;
            _data = next;
            Save();
            Changed?.Invoke(_data.Copy());
        }

        /// <summary>Back to the defaults (a shared headset handed to the next person).</summary>
        public static void Reset()
        {
            _data = new SettingsData();
            Save();
            Changed?.Invoke(_data.Copy());
        }

        public static float PanelScale => SettingsData.PanelScales[Data.panelSize];
        public static float SubtitleScale => SettingsData.SubtitleScales[Data.subtitleSize];

        private static void Save()
        {
            Store.SetString(Key, JsonUtility.ToJson(_data));
            Store.Save();
        }

        private static SettingsData Load()
        {
            LoadProblem = "";
            if (Store.HasKey(Key))
            {
                SettingsData read = null;
                try { read = JsonUtility.FromJson<SettingsData>(Store.GetString(Key)); }
                catch (ArgumentException) { }
                if (read == null) { LoadProblem = "Your saved settings could not be read, so the defaults are back."; return Fresh(); }
                if (read.version > SettingsData.CurrentVersion) { LoadProblem = "Your saved settings are from a newer version, so the defaults are used."; return Fresh(); }
                read.version = SettingsData.CurrentVersion;   // version 1 is the first; a later version migrates field by field here
                read.Clamp();
                return read;
            }
            // first run of this version: bring over the older single keys, then remove them
            var data = new SettingsData
            {
                panelSize = Store.GetInt(OldPanelKey, 0),
                subtitleSize = Store.GetInt(OldSubtitleKey, 0),
                leftHanded = Store.GetInt(OldHandKey, 0) == 1,
            };
            data.Clamp();
            _data = data;
            Save();
            foreach (var k in new[] { OldPanelKey, OldSubtitleKey, OldHandKey }) Store.DeleteKey(k);
            Store.Save();
            return data;
        }

        private static SettingsData Fresh()
        {
            _data = new SettingsData();
            Save();
            return _data;
        }
    }
}
