using System.Collections.Generic;
using ProcSense.Core.Content;
using UnityEngine;

namespace ProcSense.VR.UI.Theme
{
    /// <summary>
    /// The UI icons (P3-UIX-06): Lucide outline icons (ISC licence) drawn white in one atlas, Resources/UiIcons.png, built by tools/ui/build_icon_atlas.py (task ui-icons) with the
    /// cell order in Resources/UiIcons.json. Get(name) cuts the sprite for one cell once and keeps it; colour it with Image.color. The constants name the icon of each use, so a screen
    /// asks for <see cref="Hint"/>, not for "lightbulb"; the build's --check fails when a constant names an icon the atlas does not have.
    /// </summary>
    public static class UiIcons
    {
        // modes and procedures
        public const string Training = "graduation-cap", Test = "clipboard-check", Procedure = "stethoscope", Theatre = "scissors", Hospital = "hospital", Records = "file-text";
        // people
        public const string Trainee = "user", Patient = "bed", Nurse = "heart-pulse", Anaesthetist = "syringe", Surgeon = "scissors", Team = "users", Person = "user-round";
        // actions and flow
        public const string Play = "play", Pause = "pause", Retry = "rotate-ccw", NewScenario = "shuffle", Submit = "send", Confirm = "check", Cancel = "x", Back = "chevron-left", Quit = "log-out";
        // training, voice and settings
        public const string Hint = "lightbulb", Board = "clipboard-list", Mic = "mic", MicOff = "mic-off", VoiceMode = "message-square", MenuMode = "hand", Settings = "settings", Volume = "volume-2", Timer = "timer";
        // feedback
        public const string Info = "info", Warning = "triangle-alert", Done = "circle-check", Failed = "circle-x";

        public const string Atlas = "UiIcons";

        private static Texture2D _texture;
        private static Dictionary<string, Rect> _cells;
        private static readonly Dictionary<string, Sprite> Sprites = new Dictionary<string, Sprite>();

        /// <summary>Every icon name in the atlas, in cell order (empty when the atlas is missing).</summary>
        public static IReadOnlyCollection<string> Names { get { Load(); return _cells != null ? (IReadOnlyCollection<string>)_cells.Keys : new string[0]; } }

        /// <summary>The sprite of an icon, or null when the name is unknown or the atlas is missing (a screen then shows its text label only).</summary>
        public static Sprite Get(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            if (Sprites.TryGetValue(name, out var cached) && cached != null) return cached;
            Load();
            if (_texture == null || _cells == null || !_cells.TryGetValue(name, out var rect)) return null;
            var sprite = Sprite.Create(_texture, rect, new Vector2(.5f, .5f), rect.width, 0, SpriteMeshType.FullRect);
            sprite.name = name;
            Sprites[name] = sprite;
            return sprite;
        }

        private static void Load()
        {
            if (_cells != null) return;
            _texture = Resources.Load<Texture2D>(Atlas);
            var index = Resources.Load<TextAsset>(Atlas);
            if (_texture == null || index == null) return;
            if (!(MiniJson.Parse(index.text) is Dictionary<string, object> d) || !(d.TryGetValue("names", out var n) && n is List<object> names)) return;
            int cell = System.Convert.ToInt32(d["cell"]), cols = System.Convert.ToInt32(d["columns"]);
            _cells = new Dictionary<string, Rect>();
            for (int i = 0; i < names.Count; i++)
            {
                int col = i % cols, row = i / cols;
                _cells[(string)names[i]] = new Rect(col * cell, _texture.height - (row + 1) * cell, cell, cell);   // rows count down from the top of the image; texture y is up
            }
        }
    }
}
