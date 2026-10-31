using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace ProcSense.EditorTools
{
    /// <summary>
    /// Final-look material pass: imports the generated detail textures (Tools/Environment/build_detail_textures.py, make_foliage_textures.py) with mips, anisotropic filtering and ASTC on Android,
    /// and assigns them to the shared PatientBay materials. Run before baking so the lightmap sees the final albedo; idempotent.
    /// </summary>
    public static class FinalLookMaterials
    {
        const string Dst = "Assets/Textures/Hospital/FinalLook";

        static Texture2D Import(string src, string name, bool normal, int max, int astcBlock = 6, bool alpha = false)
        {
            if (!AssetDatabase.IsValidFolder(Dst)) AssetDatabase.CreateFolder("Assets/Textures/Hospital", "FinalLook");
            var dst = Dst + "/" + name;
            File.Copy(Path.GetFullPath(src), Path.GetFullPath(dst), true);
            AssetDatabase.ImportAsset(dst, ImportAssetOptions.ForceSynchronousImport);
            var ti = (TextureImporter)AssetImporter.GetAtPath(dst);
            ti.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default; ti.sRGBTexture = !normal;
            ti.mipmapEnabled = true; ti.anisoLevel = 8; ti.filterMode = FilterMode.Trilinear; ti.maxTextureSize = max; ti.isReadable = false; ti.wrapMode = TextureWrapMode.Repeat;
            ti.alphaIsTransparency = alpha; ti.mipMapsPreserveCoverage = alpha; ti.alphaTestReferenceValue = .5f;
            ti.textureCompression = TextureImporterCompression.CompressedHQ;
            var fmt = astcBlock == 4 ? TextureImporterFormat.ASTC_4x4 : astcBlock == 5 ? TextureImporterFormat.ASTC_5x5 : TextureImporterFormat.ASTC_6x6;
            ti.SetPlatformTextureSettings(new TextureImporterPlatformSettings { name = "Android", overridden = true, maxTextureSize = max, format = fmt, compressionQuality = 100 });
            ti.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(dst);
        }

        static Material Find(string name)
        {
            var g = AssetDatabase.FindAssets(name + " t:Material").Select(AssetDatabase.GUIDToAssetPath).FirstOrDefault(p => Path.GetFileNameWithoutExtension(p) == name);
            return g == null ? null : AssetDatabase.LoadAssetAtPath<Material>(g);
        }

        static void Set(string name, Texture2D albedo, Texture2D normal, Vector2 tiling, float bump = 1f, float? smooth = null, Color? tint = null)
        {
            var m = Find(name); if (m == null) { Debug.LogWarning("[FinalLookMaterials] missing " + name); return; }
            if (albedo != null) { m.SetTexture("_BaseMap", albedo); m.SetTextureScale("_BaseMap", tiling); }
            if (normal != null) { m.SetTexture("_BumpMap", normal); m.SetTextureScale("_BumpMap", tiling); m.SetFloat("_BumpScale", bump); m.EnableKeyword("_NORMALMAP"); }
            if (smooth.HasValue) m.SetFloat("_Smoothness", smooth.Value);
            if (tint.HasValue) m.SetColor("_BaseColor", tint.Value);
            EditorUtility.SetDirty(m);
        }

        public static void Apply()
        {
            const string T = "Logs/FinalLook/tex/";
            var vinA = Import(T + "vinyl_floor_albedo.png", "vinyl_floor_albedo.png", false, 2048, 6);
            var vinN = Import(T + "vinyl_floor_normal.png", "vinyl_floor_normal.png", true, 2048, 5);
            Set("PatientBay grey vinyl", vinA, vinN, new Vector2(5f, 4f), .22f, .30f, Color.white);
            var ctA = Import(T + "ceiling_tile_albedo.png", "ceiling_tile_albedo.png", false, 2048, 6);
            var ctN = Import(T + "ceiling_tile_normal.png", "ceiling_tile_normal.png", true, 2048, 5);
            Set("PatientBay ceiling tile", ctA, ctN, new Vector2(2.1f, 1.7f), .35f, .08f, new Color(1.25f, 1.25f, 1.25f));
            var wallN = Import(T + "wall_paint_normal.png", "wall_paint_normal.png", true, 1024, 5);
            Set("PatientBay wall paint", null, wallN, new Vector2(3.5f, 2.6f), .07f, .10f, new Color(.97f, .955f, .93f));
            var sofaA = Import(T + "sofa_weave_albedo.png", "sofa_weave_albedo.png", false, 1024, 5);
            var sofaN = Import(T + "sofa_weave_normal.png", "sofa_weave_normal.png", true, 1024, 5);
            Set("Sofa teal fabric", sofaA, sofaN, Vector2.one, 1.1f, .12f, new Color(.95f, 1.05f, 1.05f));
            var curA = Import(T + "curtain_albedo.png", "curtain_albedo.png", false, 1024, 5);
            Import(T + "curtain_normal.png", "curtain_normal.png", true, 1024, 5);
            foreach (var n in new[] { "PatientBay curtain teal", "PatientBay curtain privacy" })
            {
                var m = Find(n); if (m == null) continue;
                var ts = m.GetTextureScale("_BaseMap"); Set(n, curA, null, ts.sqrMagnitude < .01f ? Vector2.one : ts, 1f, .04f, Color.white);
            }
            var gownA = Import(T + "gown_albedo.png", "gown_albedo.png", false, 1024, 5);
            var gownN = Import(T + "gown_normal.png", "gown_normal.png", true, 1024, 5);
            Set("Bay woven PatientGown_Blue", gownA, gownN, new Vector2(2f, 2f), .8f, .0f, Color.white);
            var gm = Find("Bay woven PatientGown_Blue"); if (gm != null) { gm.SetFloat("_SpecularHighlights", 0f); gm.SetFloat("_EnvironmentReflections", 0f); gm.EnableKeyword("_SPECULARHIGHLIGHTS_OFF"); gm.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF"); EditorUtility.SetDirty(gm); }
            // ceiling diffuser: soft falloff + prism pattern, emission as an LDR map (the shipping Quest URP asset has HDR off, so a >1 emission clamps to a blank white rectangle)
            var dif = Import(T + "ceiling_diffuser_albedo.png", "ceiling_diffuser_albedo.png", false, 1024, 6);
            var dm = Find("PatientBay ceiling diffuser");
            if (dm != null)
            {
                dm.SetTexture("_BaseMap", dif); dm.SetColor("_BaseColor", Color.white); dm.SetTexture("_EmissionMap", dif); dm.SetColor("_EmissionColor", new Color(.80f, .80f, .79f));
                dm.EnableKeyword("_EMISSION"); EditorUtility.SetDirty(dm);
            }
            // patient: skin micro-detail (pores, fine lines, mottling), softer hair/brow edges, wet eyes
            var skinN = Import(T + "skin_detail_normal.png", "skin_detail_normal.png", true, 1024, 5);
            var skinA = Import(T + "skin_detail_albedo.png", "skin_detail_albedo.png", false, 1024, 5);
            var body = Find("ProcSensePatient_body");
            if (body != null)
            {
                body.SetTexture("_DetailAlbedoMap", skinA); body.SetTexture("_DetailNormalMap", skinN);
                foreach (var prop in new[] { "_DetailAlbedoMap", "_DetailNormalMap" }) body.SetTextureScale(prop, new Vector2(22f, 22f));
                body.SetFloat("_DetailAlbedoMapScale", .3f); body.SetFloat("_DetailNormalMapScale", .45f); body.EnableKeyword("_DETAIL_MULX2"); body.SetFloat("_Smoothness", .27f);
                EditorUtility.SetDirty(body);
            }
            foreach (var n in new[] { "ProcSensePatient_short02", "ProcSensePatient_eyebrow001", "ProcSensePatient_eyelashes01" })
            {
                var m = Find(n); if (m == null) continue;
                m.SetFloat("_AlphaToMask", 1f); m.SetFloat("_Cutoff", .42f); m.SetFloat("_AlphaClip", 1f); m.EnableKeyword("_ALPHATEST_ON");
                if (n.Contains("short02")) m.SetFloat("_Smoothness", .28f);
                if (n.Contains("eyebrow")) m.SetColor("_BaseColor", new Color(.58f, .44f, .34f));
                EditorUtility.SetDirty(m);
            }
            var brow = Import(T + "eyebrow_brown.png", "eyebrow_brown.png", false, 512, 6, true); var bm = Find("ProcSensePatient_eyebrow001");
            if (bm != null) { bm.SetTexture("_BaseMap", brow); bm.SetColor("_BaseColor", Color.white); EditorUtility.SetDirty(bm); }
            var eyes = Find("ProcSensePatient_high-poly"); if (eyes != null) { eyes.SetFloat("_Smoothness", .88f); EditorUtility.SetDirty(eyes); }
            // foliage: alpha-cutout, double sided, alpha-to-coverage under MSAA
            var frond = Import("Logs/ReferenceProps/foliage/palm_frond.png", "palm_frond.png", false, 1024, 6, true);
            var pothos = Import("Logs/ReferenceProps/foliage/pothos_leaf.png", "pothos_leaf.png", false, 512, 6, true);
            foreach (var (n, t) in new[] { ("Palm leaf dark", frond), ("Palm leaf light", frond), ("Pothos leaf", pothos) })
            {
                var m = Find(n); if (m == null) continue;
                m.SetTexture("_BaseMap", t); m.SetColor("_BaseColor", Color.white); m.SetFloat("_Cull", 0f); m.SetFloat("_AlphaClip", 1f); m.SetFloat("_Cutoff", .5f); m.SetFloat("_AlphaToMask", 1f);
                m.EnableKeyword("_ALPHATEST_ON"); m.SetFloat("_Smoothness", .35f); m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.AlphaTest; m.SetOverrideTag("RenderType", "TransparentCutout");
                EditorUtility.SetDirty(m);
            }
            AssetDatabase.SaveAssets();
            Debug.Log("[FinalLookMaterials] applied");
        }
    }
}
