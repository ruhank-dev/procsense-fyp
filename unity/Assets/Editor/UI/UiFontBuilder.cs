using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

namespace ProcSense.EditorTools.UI
{
    /// <summary>
    /// Builds the TextMeshPro font assets of Inter (P3-UIX-02; SIL Open Font License 1.1, Assets/UI/Fonts/Inter/OFL.txt) and makes Inter the project's default font.
    /// Three SDF assets (Regular, SemiBold, Bold) with a static atlas of the characters the app writes (Latin, Latin-1 and the punctuation and symbols below), so no glyph is
    /// rasterised on the headset. Regular is the default; its weight table sends SemiBold (600) and Bold (700, the &lt;b&gt; tag) to the other two, and LiberationSans SDF stays
    /// as the fallback for any character outside the set. Run from the menu ProcSense/UI/Build Inter font assets, or by eval: ProcSense.EditorTools.UI.UiFontBuilder.Build().
    /// </summary>
    public static class UiFontBuilder
    {
        public const string Source = "Assets/UI/Fonts/Inter/";
        public const string Output = "Assets/TextMesh Pro/Resources/Fonts & Materials/";
        public const string Regular = "Inter SDF", SemiBold = "Inter SemiBold SDF", Bold = "Inter Bold SDF";
        public const string Fallback = "Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset";
        public const int SamplingSize = 48, Padding = 6, AtlasSize = 1024;

        /// <summary>The characters baked into every atlas.</summary>
        public static string Characters()
        {
            var sb = new System.Text.StringBuilder();
            for (int c = 32; c < 127; c++) sb.Append((char)c);
            for (int c = 160; c < 256; c++) sb.Append((char)c);
            sb.Append("–—‘’“”•…←↑→↓≤≥−×✓✕€™‰′″");
            return sb.ToString();
        }

        [MenuItem("ProcSense/UI/Build Inter font assets")]
        public static void Build()
        {
            var regular = Make("Inter-Regular.ttf", Regular);
            var semi = Make("Inter-SemiBold.ttf", SemiBold);
            var bold = Make("Inter-Bold.ttf", Bold);
            var fallback = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(Fallback);
            foreach (var f in new[] { regular, semi, bold })
            {
                f.fallbackFontAssetTable = new System.Collections.Generic.List<TMP_FontAsset>();
                if (fallback != null) f.fallbackFontAssetTable.Add(fallback);
                EditorUtility.SetDirty(f);
            }
            var weights = regular.fontWeightTable;
            weights[6].regularTypeface = semi;
            weights[7].regularTypeface = bold;   // the table is the asset's own array: setting its elements is enough
            EditorUtility.SetDirty(regular);

            var settings = Resources.Load<TMP_Settings>("TMP Settings");
            var so = new SerializedObject(settings);
            so.FindProperty("m_defaultFontAsset").objectReferenceValue = regular;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();
            Debug.Log("[ProcSense] Inter font assets built; Inter SDF is the default font");
        }

        private static TMP_FontAsset Make(string file, string name)
        {
            var font = AssetDatabase.LoadAssetAtPath<Font>(Source + file);
            if (font == null) throw new FileNotFoundException(Source + file);
            string path = Output + name + ".asset";
            if (AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path) != null) AssetDatabase.DeleteAsset(path);   // rebuilt from scratch, so the atlas never keeps stale glyphs
            var asset = TMP_FontAsset.CreateFontAsset(font, SamplingSize, Padding, GlyphRenderMode.SDFAA, AtlasSize, AtlasSize, AtlasPopulationMode.Dynamic, false);
            asset.name = name;
            AssetDatabase.CreateAsset(asset, path);
            asset.atlasTexture.name = name + " Atlas";
            asset.material.name = name + " Material";
            AssetDatabase.AddObjectToAsset(asset.atlasTexture, asset);
            AssetDatabase.AddObjectToAsset(asset.material, asset);
            if (!asset.TryAddCharacters(Characters(), out string missing) && !string.IsNullOrEmpty(missing))
                Debug.LogWarning("[ProcSense] " + name + " lacks " + missing.Length + " character(s); the fallback draws them");
            asset.atlasPopulationMode = AtlasPopulationMode.Static;   // nothing is rasterised at run time
            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssets();
            return asset;
        }
    }
}
