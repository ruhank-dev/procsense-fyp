using System.IO;
using System.Linq;
using NUnit.Framework;
using ProcSense.VR.UI.Theme;
using ProcSense.VR.Views;
using TMPro;
using UnityEngine;

namespace ProcSense.Tests.EditMode
{
    /// <summary>
    /// P3-UIX-02 and P3-UIX-06: Inter is the default font with its two heavier weights and the LiberationSans fallback, its atlas is static and holds every character the app writes,
    /// the licence and provenance notes are present, and every icon constant names a sprite cut from the atlas at the cell's size.
    /// </summary>
    public class UiFontAndIconTests
    {
        private static string Repo => Path.GetFullPath(Path.Combine(Application.dataPath, "..", ".."));

        [Test]
        public void Inter_is_the_default_font_with_its_weights_and_a_fallback()
        {
            var font = TMP_Settings.defaultFontAsset;
            Assert.IsNotNull(font);
            Assert.AreEqual("Inter SDF", font.name);
            Assert.AreEqual(AtlasPopulationMode.Static, font.atlasPopulationMode, "no glyph is rasterised on the headset");
            Assert.AreEqual("Inter SemiBold SDF", font.fontWeightTable[6].regularTypeface.name);
            Assert.AreEqual("Inter Bold SDF", font.fontWeightTable[7].regularTypeface.name);
            Assert.IsTrue(font.fallbackFontAssetTable.Any(f => f != null && f.name == "LiberationSans SDF"));
        }

        [Test]
        public void The_atlas_holds_the_characters_the_app_writes()
        {
            var font = TMP_Settings.defaultFontAsset;
            // letters, digits, punctuation, and the symbols the panels use: degree, times, dashes, curly quotes, bullet, ellipsis, arrow, plus-minus, e-acute
            const string symbols = "\u00B0\u00D7\u2014\u2013\u201C\u201D\u2018\u2019\u2022\u2026\u2192\u00B1\u00E9";
            foreach (char c in "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789.,;:!?'\"()[]/%+-=<>&@#*" + symbols)
                Assert.IsTrue(font.characterLookupTable.ContainsKey(c), "missing U+" + ((int)c).ToString("X4"));
        }

        [Test]
        public void A_kit_label_uses_Inter()
        {
            var go = new GameObject("panel", typeof(RectTransform));
            try
            {
                var label = UiKit.Label(go.transform, "L", "Hello", Vector2.zero, new Vector2(200, 40), 24f);
                Assert.AreEqual("Inter SDF", (label.font ?? TMP_Settings.defaultFontAsset).name);
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void Every_icon_constant_has_a_sprite_of_one_cell()
        {
            var names = typeof(UiIcons).GetFields().Where(f => f.IsLiteral && f.FieldType == typeof(string) && f.Name != nameof(UiIcons.Atlas)).Select(f => (string)f.GetRawConstantValue()).Distinct().ToList();
            Assert.Greater(names.Count, 25);
            foreach (var n in names)
            {
                var s = UiIcons.Get(n);
                Assert.IsNotNull(s, n);
                Assert.AreEqual(128f, s.rect.width, n);
                Assert.AreSame(s, UiIcons.Get(n), "cut once and kept");
            }
            Assert.IsNull(UiIcons.Get("no-such-icon"));
            Assert.AreEqual(34, UiIcons.Names.Count);
        }

        [Test]
        public void The_licences_and_provenance_notes_are_present()
        {
            StringAssert.Contains("SIL OPEN FONT LICENSE", File.ReadAllText(Path.Combine(Repo, "unity/Assets/UI/Fonts/Inter/OFL.txt")).ToUpperInvariant());
            StringAssert.Contains("ISC License", File.ReadAllText(Path.Combine(Repo, "tools/ui/icons/lucide/LICENSE")));
            StringAssert.Contains("SIL OFL", File.ReadAllText(Path.Combine(Repo, "unity/Assets/UI/Fonts/Inter/README.md")));
            StringAssert.Contains("ISC licence", File.ReadAllText(Path.Combine(Repo, "unity/Assets/UI/Icons/README.md")));
        }
    }
}
