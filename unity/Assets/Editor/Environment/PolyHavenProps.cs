using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace ProcSense.EditorTools
{
    /// <summary>
    /// 3 Nov 2026: imports CC0 Poly Haven models (converted by Tools/Environment/convert_ph_model.py: decimated FBX in Logs/PhModels, repacked PBR textures in Assets/Textures/PolyHaven) as prefabs with URP Lit materials.
    /// Leaves use alpha cut-out; every texture gets mipmaps, aniso 8 and a 1K ASTC Android override. Sources and licences: docs/research/asset_sources_and_licenses.md. Idempotent (re-run refreshes files and materials; prefab GUIDs stay).
    /// </summary>
    public static class PolyHavenProps
    {
        const string Models = "Assets/Models/Environment/PolyHaven", Prefabs = "Assets/Prefabs/Environment/PolyHaven", Mats = "Assets/Materials/Hospital/PolyHaven", Tex = "Assets/Textures/PolyHaven";
        public static readonly string[] Ids = { "potted_plant_01", "potted_plant_02", "potted_plant_04", "wheelchair_01", "fire_alarm", "korean_fire_extinguisher_01", "hanging_picture_frame_02", "wall_clock" };

        static void Folder(string path) { if (!AssetDatabase.IsValidFolder(path)) { var parent = Path.GetDirectoryName(path).Replace('\\', '/'); Folder(parent); AssetDatabase.CreateFolder(parent, Path.GetFileName(path)); } }

        static Texture2D Tx(string name, bool srgb, bool normal)
        {
            var path = Tex + "/" + name + ".png"; if (!File.Exists(path)) return null;
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var ti = (TextureImporter)AssetImporter.GetAtPath(path);
            ti.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default; ti.sRGBTexture = srgb; ti.alphaIsTransparency = srgb; ti.mipmapEnabled = true; ti.filterMode = FilterMode.Trilinear;
            ti.anisoLevel = 8; ti.maxTextureSize = 2048; ti.textureCompression = TextureImporterCompression.CompressedHQ;
            ti.SetPlatformTextureSettings(new TextureImporterPlatformSettings { name = "Android", overridden = true, maxTextureSize = 1024, format = TextureImporterFormat.ASTC_6x6 });
            ti.SaveAndReimport(); return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        static Material Mat(string name)
        {
            var path = Mats + "/" + name + ".mat"; var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null) { m = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(m, path); }
            var albedo = Tx(name + "_albedo", true, false); var normal = Tx(name + "_normal", false, true); var mask = Tx(name + "_mask", false, false); var ao = Tx(name + "_ao", false, false);
            m.SetTexture("_BaseMap", albedo); m.SetColor("_BaseColor", Color.white);
            m.SetTexture("_BumpMap", normal); m.SetFloat("_BumpScale", 1f); m.EnableKeyword("_NORMALMAP");
            m.SetTexture("_MetallicGlossMap", mask); m.SetFloat("_Metallic", 1f); m.SetFloat("_Smoothness", 1f); m.SetFloat("_SmoothnessTextureChannel", 0f); m.EnableKeyword("_METALLICSPECGLOSSMAP");
            m.SetTexture("_OcclusionMap", ao); m.SetFloat("_OcclusionStrength", .8f); m.EnableKeyword("_OCCLUSIONMAP");
            bool leaf = name.EndsWith("_leaves");
            m.SetFloat("_AlphaClip", leaf ? 1f : 0f); m.SetFloat("_Cutoff", .5f); m.SetFloat("_Cull", leaf ? 0f : 2f);
            if (leaf) m.EnableKeyword("_ALPHATEST_ON"); else m.DisableKeyword("_ALPHATEST_ON");
            m.renderQueue = leaf ? (int)RenderQueue.AlphaTest : -1;
            EditorUtility.SetDirty(m); return m;
        }

        public static void Import()
        {
            Folder(Models); Folder(Prefabs); Folder(Mats);
            foreach (var id in Ids)
            {
                var src = "Logs/PhModels/" + id + ".fbx"; if (!File.Exists(src)) { Debug.LogError("[PolyHaven] missing " + src); continue; }
                var fbx = Models + "/" + id + ".fbx"; File.Copy(src, fbx, true); AssetDatabase.ImportAsset(fbx, ImportAssetOptions.ForceSynchronousImport);
                var mi = (ModelImporter)AssetImporter.GetAtPath(fbx);
                mi.globalScale = 1; mi.useFileScale = true; mi.importAnimation = false; mi.importCameras = false; mi.importLights = false; mi.isReadable = false;
                mi.generateSecondaryUV = true; mi.materialImportMode = ModelImporterMaterialImportMode.ImportStandard; mi.materialLocation = ModelImporterMaterialLocation.InPrefab; mi.SaveAndReimport();      // keep the FBX material slots so each sub-mesh is mapped by its own material name
                var json = JsonUtility.FromJson<Names>(File.ReadAllText("Logs/PhModels/" + id + ".json"));
                var go = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(fbx));
                var cache = new Dictionary<string, Material>();
                Material For(string slotName)
                {
                    var n = System.Text.RegularExpressions.Regex.Replace(slotName ?? "", @"\.\d+$", "");
                    if (cache.TryGetValue(n, out var hit)) return hit;
                    Material m;
                    if (n.Contains("glass")) m = Glass(n);
                    else m = Mat(File.Exists(Tex + "/" + n + "_albedo.png") ? n : id);
                    return cache[n] = m;
                }
                try
                {
                    foreach (var r in go.GetComponentsInChildren<MeshRenderer>(true))
                    {
                        var slots = r.sharedMaterials; var mapped = new Material[slots.Length];
                        for (int i = 0; i < slots.Length; i++) mapped[i] = For(slots[i] != null ? slots[i].name : id);
                        r.sharedMaterials = mapped; r.receiveGI = ReceiveGI.LightProbes; r.shadowCastingMode = ShadowCastingMode.On;
                    }
                    PrefabUtility.SaveAsPrefabAsset(go, Prefabs + "/" + id + ".prefab");
                }
                finally { Object.DestroyImmediate(go); }
                Debug.Log($"[PolyHaven] {id}: {json.tris_out} triangles, {cache.Count} materials");
            }
            AssetDatabase.SaveAssets(); Debug.Log("[PolyHaven] imported");
        }

        static Material Glass(string name)
        {
            var path = Mats + "/" + name + ".mat"; var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null) { m = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(m, path); }
            m.SetFloat("_Surface", 1f); m.SetFloat("_Blend", 1f); m.SetFloat("_SrcBlend", 1f); m.SetFloat("_DstBlend", 10f); m.SetFloat("_ZWrite", 0f);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT"); m.EnableKeyword("_ALPHAPREMULTIPLY_ON"); m.SetOverrideTag("RenderType", "Transparent"); m.renderQueue = (int)RenderQueue.Transparent;
            m.SetColor("_BaseColor", new Color(.85f, .93f, .95f, .08f)); m.SetFloat("_Metallic", 0f); m.SetFloat("_Smoothness", .97f); EditorUtility.SetDirty(m); return m;
        }

        [System.Serializable] class Names { public string id; public int tris_out; public List<string> materials; }
    }
}
