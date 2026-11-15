using System;
using System.Collections.Generic;
using ProcSense.VR.Flow;
using ProcSense.VR.Settings;
using ProcSense.VR.UI.Kit;
using ProcSense.VR.UI.Theme;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ProcSense.VR.UI.SettingsUi
{
    /// <summary>
    /// The Settings panel (P3-SET-02): six tabs, Interaction, Audio, Subtitles, Comfort, Display and Session, built with the component kit. Every control writes
    /// <see cref="GameSettings"/> at once (saved, and the appliers follow its Changed event), so a change applies live and is remembered. The Session tab holds
    /// actions only (return to the main menu, quit), each behind a confirmation. Opened beside the game-flow menu by its Settings button (<see cref="Toggle"/>);
    /// the pause and wrist menus (P3-MNU-08) open the same panel.
    /// </summary>
    public sealed class SettingsPanel : MonoBehaviour
    {
        public enum Tab { Interaction, Audio, Subtitles, Comfort, Display, Session }
        public static readonly string[] TabNames = { "Input", "Audio", "Subtitles", "Comfort", "Display", "Session" };

        public const float Distance = 1.4f;
        public static readonly Vector2 Size = new Vector2(1000, 860);
        private const float Pad = 48, ContentTop = 260, RowGap = 80;

        public static SettingsPanel Open { get; private set; }

        public Canvas Canvas { get; private set; }
        public Tab Current { get; private set; }
        /// <summary>Replaced by tests and by the pause menu: what "Return to main menu" and "Quit" do after their confirmation.</summary>
        /// <summary>P3-SET-07: the same semantics as the pause menu: during a case the attempt ends as abandoned (saved and uploaded; the examination shows its results); on the menu it simply goes back.</summary>
        public Action ReturnToMenu = () => { var pause = PauseMenu.Running(); if (pause != null) pause.ReturnToMenu(); else if (MenuFlow.Current != null) MenuFlow.Current.BackToMenu(); };
        public Action Quit = () => { var pause = PauseMenu.Running(); if (pause != null) pause.Quit(); else Application.Quit(); };
        public event Action Closed;

        private RectTransform _content;
        private KitSegmented _tabs;
        private TMP_Text _note;

        /// <summary>Opens the panel beside <paramref name="anchor"/> (turned a little towards the viewer), or closes it when it is open.</summary>
        public static SettingsPanel Toggle(Transform anchor, float sideways = 1.35f)
        {
            if (Open != null) { Open.Close(); return null; }
            var go = new GameObject("Settings panel");
            if (anchor != null)
            {
                go.transform.SetParent(anchor.parent, false);
                go.transform.SetPositionAndRotation(anchor.position + anchor.right * sideways, anchor.rotation * Quaternion.Euler(0, 20f, 0));
            }
            var panel = go.AddComponent<SettingsPanel>();
            panel.Build();
            return panel;
        }

        public void Build(Tab first = Tab.Interaction)
        {
            if (Canvas != null) return;
            Open = this;
            Canvas = Kit.Kit.Canvas("Settings", transform, Size, Distance);
            var root = Canvas.transform;
            Kit.Kit.Panel(root, "Panel", Vector2.zero, Size);
            Kit.Kit.Header(root, "Settings", "Changes apply at once and are remembered.", new Vector2(Pad, Pad), Size.x - 2 * Pad - 88, UiIcons.Settings);
            Kit.Kit.Button(root, null, new Vector2(Size.x - Pad - 72, Pad), new Vector2(72, 72), ButtonKind.Icon, UiIcons.Cancel, Close).name = "Close";
            _tabs = Kit.Kit.Segmented(root, TabNames, new Vector2(Pad, 176), new Vector2(Size.x - 2 * Pad, 72), (int)first, i => Show((Tab)i));
            _content = Kit.Kit.Place(new GameObject("Content", typeof(RectTransform)).GetComponent<RectTransform>(), Vector2.zero, Size);
            _content.SetParent(root, false);
            Kit.Kit.Place(_content, new Vector2(Pad, ContentTop), new Vector2(Size.x - 2 * Pad, Size.y - ContentTop - Pad));
            _note = Kit.Kit.Text(root, "Note", GameSettings.LoadProblem, new Vector2(Pad, Size.y - Pad - 32), new Vector2(Size.x - 2 * Pad, 40), TextStyle.Caption, UiTheme.Current.HintText);
            Show(first);
        }

        public void Close()
        {
            if (Open == this) Open = null;
            Closed?.Invoke();
            if (Application.isPlaying) Destroy(gameObject); else DestroyImmediate(gameObject);
        }

        private void OnDestroy() { if (Open == this) Open = null; }

        /// <summary>Shows a tab (the tab bar calls it; tests and other menus may too).</summary>
        public void Show(Tab tab)
        {
            Current = tab;
            if (_tabs.Selected != (int)tab) _tabs.Select((int)tab);
            for (int i = _content.childCount - 1; i >= 0; i--)
            {
                var child = _content.GetChild(i).gameObject;
                child.transform.SetParent(null);
                if (Application.isPlaying) Destroy(child); else DestroyImmediate(child);
            }
            var s = GameSettings.Current;
            float w = _content.rect.width, y = 0;
            void Row(Action<float> make) { make(y); y += RowGap; }
            switch (tab)
            {
                case Tab.Interaction:
                    Row(at => Choice("Interaction", new[] { "Menus", "Voice" }, (int)s.interactionMode, at, i => { InteractionMode.Set((InteractionModeSetting)i); if (_explain != null) _explain.text = ModeText(); }));
                    Row(at => _explain = Explain(ModeText(), at));
                    Row(at => Switch("Spoken feedback when voice fails", s.voiceFeedback, at, v => GameSettings.Update(d => d.voiceFeedback = v)));
                    break;
                case Tab.Audio:
                    Row(at => Volume("Master", s.masterVolume, at, v => GameSettings.Update(d => d.masterVolume = v)));
                    Row(at => Volume("Voices", s.voiceVolume, at, v => GameSettings.Update(d => d.voiceVolume = v)));
                    Row(at => Volume("Music", s.musicVolume, at, v => GameSettings.Update(d => d.musicVolume = v)));
                    Row(at => Volume("Sound effects", s.effectsVolume, at, v => GameSettings.Update(d => d.effectsVolume = v)));
                    Row(at => Volume("Ambience", s.ambienceVolume, at, v => GameSettings.Update(d => d.ambienceVolume = v)));
                    Row(at => Volume("Interface", s.uiVolume, at, v => GameSettings.Update(d => d.uiVolume = v)));
                    break;
                case Tab.Subtitles:
                    Row(at => Switch("Subtitles", s.subtitles, at, v => GameSettings.Update(d => d.subtitles = v)));
                    Row(at => Choice("Size", new[] { "Normal", "Large", "Largest" }, s.subtitleSize, at, i => GameSettings.Update(d => d.subtitleSize = i)));
                    Row(at => Switch("Dark plate behind subtitles", s.subtitleBackground, at, v => GameSettings.Update(d => d.subtitleBackground = v)));
                    break;
                case Tab.Comfort:
                    Row(at => Choice("Turning", new[] { "Snap", "Smooth" }, (int)s.turnStyle, at, i => GameSettings.Update(d => d.turnStyle = (TurnStyle)i)));
                    Row(at => Choice("Snap angle", new[] { "30°", "45°", "60°", "90°" }, Array.IndexOf(new[] { 30f, 45f, 60f, 90f }, s.snapAngle), at,
                                     i => GameSettings.Update(d => d.snapAngle = new[] { 30f, 45f, 60f, 90f }[i])));
                    Row(at => Volume("Turn speed", Mathf.InverseLerp(30f, 180f, s.turnSpeed), at, v => GameSettings.Update(d => d.turnSpeed = Mathf.Round(Mathf.Lerp(30f, 180f, v)))));
                    Row(at => Choice("Moving", new[] { "Teleport", "Continuous" }, (int)s.moveStyle, at, i => GameSettings.Update(d => d.moveStyle = (MoveStyle)i)));
                    Row(at => Switch("Comfort vignette while moving", s.comfortVignette, at, v => GameSettings.Update(d => d.comfortVignette = v)));
                    var controls = FindAnyObjectByType<ProcSense.VR.XR.ComfortControls>();   // the rig's seated mode (kept from the tablet's settings tab)
                    if (controls != null)
                        Row(at => Choice("Play", new[] { "Standing", "Seated" }, controls.Seated ? 1 : 0, at, i => { if (controls != null) controls.SetSeated(i == 1); }));
                    else Row(at => Explain("Seated play is set on the headset rig.", at));
                    break;
                case Tab.Display:
                    Row(at => Choice("Panel size", new[] { "Normal", "Large", "Largest" }, s.panelSize, at, i => GameSettings.Update(d => d.panelSize = i)));
                    Row(at => Switch("High contrast", s.highContrast, at, v => { GameSettings.Update(d => d.highContrast = v); Redraw(); }));
                    Row(at => Switch("Reduce motion", s.reduceMotion, at, v => GameSettings.Update(d => d.reduceMotion = v)));
                    Row(at => Choice("Dominant hand", new[] { "Right", "Left" }, s.leftHanded ? 1 : 0, at, i => GameSettings.Update(d => d.leftHanded = i == 1)));
                    Row(at => Kit.Kit.Button(_content, "Reset all settings", new Vector2(0, at), new Vector2(w, Kit.Kit.ButtonHeight), ButtonKind.Secondary, UiIcons.Retry,
                                             () => Confirm("Reset all settings?", "Every setting goes back to its default.", "Reset", false, () => { GameSettings.Reset(); Show(Tab.Display); })));
                    break;
                case Tab.Session:
                    Row(at => Kit.Kit.Button(_content, "Return to main menu", new Vector2(0, at), new Vector2(w, Kit.Kit.ButtonHeight), ButtonKind.Secondary, UiIcons.Back,
                                             () => Confirm("Return to the main menu?", "The case you are in ends here.", "Return", true, () => { Close(); ReturnToMenu?.Invoke(); })));
                    Row(at => Kit.Kit.Button(_content, "Quit ProcSense", new Vector2(0, at), new Vector2(w, Kit.Kit.ButtonHeight), ButtonKind.Danger, UiIcons.Quit,
                                             () => Confirm("Quit ProcSense?", "Your session is saved before the app closes.", "Quit", true, () => Quit?.Invoke())));
                    break;
            }
        }

        private bool _redrawQueued;

        /// <summary>Rebuilds the panel after a change of high contrast (outside the toggle's own callback), so its text and glass follow at once.</summary>
        private void Redraw()
        {
            if (_redrawQueued) return;
            _redrawQueued = true;
            void Rebuild()
            {
                _redrawQueued = false;
                if (this == null) return;
                var tab = Current;
                for (int i = transform.childCount - 1; i >= 0; i--) DestroyImmediate(transform.GetChild(i).gameObject);
                Canvas = null;
                Build(tab);
            }
            if (Application.isPlaying) StartCoroutine(Next(Rebuild)); else Rebuild();
        }

        private static System.Collections.IEnumerator Next(Action a) { yield return null; a(); }

        /// <summary>The open confirmation dialog, if any (tests press its buttons).</summary>
        public KitDialog Dialog { get; private set; }

        private void Confirm(string title, string body, string yes, bool danger, Action then)
        {
            if (Dialog != null) { if (Application.isPlaying) Destroy(Dialog.gameObject); else DestroyImmediate(Dialog.gameObject); }
            Dialog = Kit.Kit.Dialog(Canvas.transform, title, body, yes, "Cancel", new Vector2((Size.x - 640) / 2, (Size.y - 360) / 2), danger, then);
            Dialog.Open();
        }

        private void Choice(string label, string[] options, int selected, float y, Action<int> set)
        {
            float w = _content.rect.width, labelW = Mathf.Round(w * .4f);
            Kit.Kit.Text(_content, label, label, new Vector2(0, y), new Vector2(labelW, Kit.Kit.RowHeight), TextStyle.Body).verticalAlignment = VerticalAlignmentOptions.Middle;
            var seg = Kit.Kit.Segmented(_content, options, new Vector2(labelW, y - 4), new Vector2(w - labelW, 72), Mathf.Max(0, selected), set);
            seg.name = "Choice " + label;
        }

        private void Switch(string label, bool on, float y, Action<bool> set) => Kit.Kit.Toggle(_content, label, new Vector2(0, y), _content.rect.width, on, set);

        private void Volume(string label, float value, float y, Action<float> set) => Kit.Kit.Slider(_content, label, new Vector2(0, y), _content.rect.width, value, set);

        private TMP_Text _explain;

        /// <summary>The one-line explanation of the interaction mode (P3-SET-04).</summary>
        public static string ModeText() => InteractionMode.Voice
            ? "Voice: speak to the patient and the team; menus become reference cards. Your progress is not affected."
            : "Menus: point at panels and select actions. Your progress is not affected.";

        private TMP_Text Explain(string text, float y) =>
            Kit.Kit.Text(_content, "Explanation", text, new Vector2(0, y - 8), new Vector2(_content.rect.width, RowGap), TextStyle.Body, UiTheme.Current.TextSecondary);

        /// <summary>The controls of the current tab by name (tests drive them).</summary>
        public T Find<T>(string name) where T : Component
        {
            foreach (var c in _content.GetComponentsInChildren<T>(true)) if (c.name == name || c.name.EndsWith(" " + name)) return c;
            return null;
        }
    }
}
