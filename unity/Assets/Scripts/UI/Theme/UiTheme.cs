using UnityEngine;

namespace ProcSense.VR.UI.Theme
{
    /// <summary>
    /// The design tokens of every ProcSense screen (P3-UIX-01; the table is docs/plans/p3/ui-design-tokens.md). One asset, read by the UiKit factory
    /// and the glass shader, so a colour or a size changes in one place. <see cref="Default"/> gives the shipped values when no asset is loaded
    /// (tests, and the Editor before the asset exists). Sizes are canvas units at 0.0006 m per unit, the scale of the tablet and the menus.
    /// </summary>
    [CreateAssetMenu(menuName = "ProcSense/UI theme")]
    public sealed class UiTheme : ScriptableObject
    {
        public const string ResourceName = "UiTheme";

        [Header("Glass")]
        [Tooltip("Panel tint. 85 % opaque, not the plan's first 62 %: at 62 % accent text fails WCAG AA in front of a bright window (ui-design-tokens.md).")]
        public Color GlassTint = Hex(0x0F1A24, .85f);
        public Color GlassTintLight = Hex(0xE8F0F6, .18f);
        public Color Edge = new Color(1f, 1f, 1f, .35f);
        public float EdgeWidth = 1.5f;

        [Header("Accents (fills: buttons, ticks, rings)")]
        public Color Action = Hex(0x2EC4B6);
        public Color Hint = Hex(0xF2B33D);
        public Color Danger = Hex(0xE64A4F);   // #E5484D gave 4.496:1 under dark labels; one step lighter passes 4.5:1 (P3-UIX-04)
        public Color Done = Hex(0x3CCB7F);

        [Header("Text")]
        public Color TextPrimary = Hex(0xF4F7FA);
        [Range(0, 1)] public float TextSecondaryAlpha = .72f;
        [Range(0, 1)] public float TextDisabledAlpha = .45f;
        [Tooltip("Text on an accent fill (dark, so it reads on every accent).")]
        public Color TextOnAccent = Hex(0x0F1A24);
        [Tooltip("Danger as text on glass: lighter than the fill so it passes AA on glass over a bright background.")]
        public Color DangerText = Hex(0xFF8589);

        [Header("Type (canvas units, as seen from the reference distance)")]
        public float Title = 48, Heading = 32, Body = 24, Caption = 18;
        [Tooltip("The type sizes are for a panel this far away at 0.0006 m per unit; a panel further away gets a larger scale (MetresPerUnit) so its text looks the same size.")]
        public float ReferenceDistance = .6f;
        public float ReferenceMetresPerUnit = .0006f;

        [Header("Shape and spacing (canvas units)")]
        public float Radius = 24;
        public float[] Spacing = { 8, 16, 24, 32 };
        [Tooltip("Smallest button, and the gap between buttons (about 2.2 degrees at 1 m).")]
        public float MinTarget = 64, TargetGap = 12;

        [Header("Motion (seconds)")]
        public float Open = .18f, Close = .12f, Hover = .06f, Tick = .25f;

        [Header("Distances (metres)")]
        public Vector2 MenuDistance = new Vector2(1.2f, 1.6f);
        public Vector2 ContextDistance = new Vector2(.6f, 1.2f);
        public float WristDistance = .35f, NearestDistance = .3f;

        /// <summary>The canvas scale for a panel read from this far away: text keeps the same angular size at every distance (24 headset pixels and up for Caption).</summary>
        public float MetresPerUnit(float distance) => ReferenceMetresPerUnit * Mathf.Max(NearestDistance, distance) / ReferenceDistance;

        public Color TextSecondary => WithAlpha(TextPrimary, TextSecondaryAlpha);
        public Color TextDisabled => WithAlpha(TextPrimary, TextDisabledAlpha);
        public Color HintText => Hint;
        public Color DoneText => Done;
        public Color ActionText => Action;

        private static UiTheme _loaded;

        /// <summary>The project's theme asset (Resources/UiTheme), or the shipped values when there is none.</summary>
        public static UiTheme Current
        {
            get
            {
                if (_loaded == null) _loaded = Resources.Load<UiTheme>(ResourceName);
                if (_loaded == null) _loaded = Default();
                return ProcSense.VR.Settings.GameSettings.Current.highContrast ? HighContrastOf(_loaded) : _loaded;
            }
        }

        private static UiTheme _contrast, _contrastOf;

        /// <summary>
        /// The high-contrast variant (P3-SET-06): near-opaque darker glass, a brighter rim, and secondary and disabled text closer to full white. Accent colours stay, so their
        /// meaning does not change. Built once from the base theme.
        /// </summary>
        public static UiTheme HighContrastOf(UiTheme b)
        {
            if (_contrast != null && _contrastOf == b) return _contrast;
            _contrast = Instantiate(b);
            _contrast.name = b.name + " (high contrast)";
            _contrast.GlassTint = Hex(0x070C11, .97f);
            _contrast.GlassTintLight = new Color(1f, 1f, 1f, .26f);
            _contrast.Edge = new Color(1f, 1f, 1f, .75f);
            _contrast.EdgeWidth = Mathf.Max(b.EdgeWidth, 3f);
            _contrast.TextSecondaryAlpha = .92f;
            _contrast.TextDisabledAlpha = .62f;
            _contrastOf = b;
            return _contrast;
        }

        public static UiTheme Default() => CreateInstance<UiTheme>();

        public static Color Hex(int rgb, float a = 1f) => new Color(((rgb >> 16) & 255) / 255f, ((rgb >> 8) & 255) / 255f, (rgb & 255) / 255f, a);
        private static Color WithAlpha(Color c, float a) { c.a = a; return c; }

        /// <summary>What a translucent colour looks like over a background (straight alpha), for the contrast checks.</summary>
        public static Color Over(Color fg, Color bg) => new Color(fg.r * fg.a + bg.r * (1 - fg.a), fg.g * fg.a + bg.g * (1 - fg.a), fg.b * fg.a + bg.b * (1 - fg.a), 1f);
    }
}
