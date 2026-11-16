using UnityEditor;
using UnityEngine;

namespace ProcSense.EditorTools.UI
{
    /// <summary>
    /// Import settings of the UI icon atlas (P3-UIX-06, Assets/UI/Icons/Resources/UiIcons.png): straight alpha (white icons on clear cells, no dark fringe), mipmaps for panels read
    /// at a distance, clamped edges, no rescaling of the 1024 x 1024 atlas. UiIcons cuts the sprites at run time from the cell list beside it.
    /// </summary>
    public sealed class UiAssetImport : AssetPostprocessor
    {
        public const string IconAtlas = "Assets/UI/Icons/Resources/UiIcons.png";

        private void OnPreprocessTexture()
        {
            if (assetPath != IconAtlas) return;
            var t = (TextureImporter)assetImporter;
            t.textureType = TextureImporterType.Default;
            t.alphaSource = TextureImporterAlphaSource.FromInput;
            t.alphaIsTransparency = true;
            t.mipmapEnabled = true;
            t.wrapMode = TextureWrapMode.Clamp;
            t.npotScale = TextureImporterNPOTScale.None;
            t.maxTextureSize = 1024;
            t.sRGBTexture = true;
        }
    }
}
