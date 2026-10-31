using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ProcSense.EditorTools
{
    /// <summary>
    /// Imports the PatientBay wood floor set (unique 5 m x 4 m plank atlas: sRGB albedo with smoothness in alpha, plus a tangent-space normal map),
    /// creates the URP Lit material, and assigns it to PatientBay's Floor only. Corridor and OT floors are separate materials in their own scenes and are not touched.
    /// Args: -albedoMax 4096 -androidAlbedo 6 (ASTC block size 4|6|8) -bump 0.6
    /// </summary>
    public static class FloorImport
    {
        const string TexDir = "Assets/Textures/Hospital/PatientBayWood";
        const string MatPath = "Assets/Materials/Hospital/PatientBayLook/PatientBay wood floor.mat";
        static string Build => Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Logs", "FloorBuild", "out"));

        static string Arg(string key, string def) { var a = Environment.GetCommandLineArgs(); var i = Array.IndexOf(a, key); return i >= 0 && i + 1 < a.Length ? a[i + 1] : def; }

        static TextureImporterFormat Astc(int block) => block switch { 4 => TextureImporterFormat.ASTC_4x4, 5 => TextureImporterFormat.ASTC_5x5, 8 => TextureImporterFormat.ASTC_8x8, _ => TextureImporterFormat.ASTC_6x6 };

        static Texture2D Import(string file, bool normal, int maxSize, int androidBlock)
        {
            Directory.CreateDirectory(Path.GetFullPath(Path.Combine(Application.dataPath, "..", TexDir)));
            var dst = $"{TexDir}/{file}";
            File.Copy(Path.Combine(Build, file), Path.GetFullPath(Path.Combine(Application.dataPath, "..", dst)), true);
            AssetDatabase.ImportAsset(dst, ImportAssetOptions.ForceSynchronousImport);
            var ti = (TextureImporter)AssetImporter.GetAtPath(dst);
            ti.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
            ti.sRGBTexture = !normal;
            ti.alphaSource = normal ? TextureImporterAlphaSource.None : TextureImporterAlphaSource.FromInput;
            ti.alphaIsTransparency = false;                                  // the alpha channel holds smoothness, not opacity
            ti.mipmapEnabled = true; ti.streamingMipmaps = false; ti.anisoLevel = 8; ti.filterMode = FilterMode.Trilinear; ti.wrapMode = TextureWrapMode.Clamp;
            ti.npotScale = TextureImporterNPOTScale.None; ti.isReadable = false;
            ti.maxTextureSize = maxSize;
            ti.textureCompression = TextureImporterCompression.CompressedHQ;
            ti.SetPlatformTextureSettings(new TextureImporterPlatformSettings { name = "Android", overridden = true, maxTextureSize = maxSize, format = Astc(androidBlock), compressionQuality = 100 });
            ti.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(dst);
        }

        /// <summary>Parks the wood floor: puts the original vinyl material back on PatientBay's Floor (the wood assets stay in the project, unreferenced, for the floor task).</summary>
        public static void RevertToVinyl()
        {
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/PatientBay.unity", OpenSceneMode.Single);
            var floor = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<MeshRenderer>(true)).First(r => r.name == "Floor");
            floor.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Hospital/PatientBayLook/PatientBay vinyl floor.mat");
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
            Debug.Log("[FloorImport] Floor material reverted to " + floor.sharedMaterial.name);
        }

        public static void Run()
        {
            var max = int.Parse(Arg("-albedoMax", "4096")); var block = int.Parse(Arg("-androidAlbedo", "6"));
            var bump = float.Parse(Arg("-bump", "0.6"), System.Globalization.CultureInfo.InvariantCulture);
            var albedo = Import("floor_albedo_smooth.png", false, max, block);
            var normal = Import("floor_normal.png", true, max, 6);
            var m = AssetDatabase.LoadAssetAtPath<Material>(MatPath);
            if (m == null) { m = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "PatientBay wood floor" }; AssetDatabase.CreateAsset(m, MatPath); }
            m.enableInstancing = true;
            m.SetTexture("_BaseMap", albedo); m.SetTextureScale("_BaseMap", Vector2.one); m.SetTextureOffset("_BaseMap", Vector2.zero);
            m.SetColor("_BaseColor", Color.white);
            m.SetFloat("_Metallic", 0f);
            m.SetFloat("_Smoothness", 1f);                                                       // multiplier on the alpha channel
            m.SetFloat("_SmoothnessTextureChannel", 1f);                                         // albedo alpha
            m.EnableKeyword("_SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A");
            m.SetTexture("_BumpMap", normal); m.SetFloat("_BumpScale", bump); m.EnableKeyword("_NORMALMAP");
            m.SetFloat("_Surface", 0f); m.SetFloat("_AlphaClip", 0f); m.DisableKeyword("_ALPHATEST_ON");
            m.SetFloat("_SpecularHighlights", 1f); m.SetFloat("_EnvironmentReflections", 1f);
            EditorUtility.SetDirty(m); AssetDatabase.SaveAssets();

            var scene = EditorSceneManager.OpenScene("Assets/Scenes/PatientBay.unity", OpenSceneMode.Single);
            var floor = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<MeshRenderer>(true)).First(r => r.name == "Floor");
            floor.sharedMaterial = m;
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
            Debug.Log($"[FloorImport] albedo {albedo.width}x{albedo.height} max {max} android ASTC {block}x{block}; bump {bump}; material assigned to {floor.name}");
        }
    }
}
