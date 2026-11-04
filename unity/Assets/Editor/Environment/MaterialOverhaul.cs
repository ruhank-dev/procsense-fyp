using System.IO;
using UnityEditor;
using UnityEngine;

namespace ProcSense.EditorTools
{
    /// <summary>
    /// 2 Nov 2026 quality overhaul, step 1: real PBR surfaces for the hospital shell (plaster wall paint, resilient floor). Maps come from Tools/Environment/make_pbr_surfaces.py (CC0 scans, hospital colours).
    /// Desktop imports at 2K with 8x anisotropy; the Android override drops to 1K ASTC. Idempotent: re-running reassigns the same maps.
    /// </summary>
    public static class MaterialOverhaul
    {
        const string Tex = "Assets/Textures/Hospital/PBR", Mats = "Assets/Materials/Hospital/PatientBayLook";

        static readonly System.Collections.Generic.Dictionary<string, Texture2D> Cache = new System.Collections.Generic.Dictionary<string, Texture2D>();

        static Texture2D Import(string file, bool srgb, bool normal)
        {
            if (Cache.TryGetValue(file, out var cached) && cached != null) return cached;
            var path = Tex + "/" + file; AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var ti = (TextureImporter)AssetImporter.GetAtPath(path);
            ti.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default; ti.sRGBTexture = srgb; ti.mipmapEnabled = true; ti.streamingMipmaps = false;
            ti.filterMode = FilterMode.Trilinear; ti.anisoLevel = 8; ti.maxTextureSize = 2048; ti.wrapMode = TextureWrapMode.Repeat; ti.textureCompression = TextureImporterCompression.CompressedHQ;
            ti.SetPlatformTextureSettings(new TextureImporterPlatformSettings { name = "Android", overridden = true, maxTextureSize = 1024, format = TextureImporterFormat.ASTC_6x6 });
            ti.SaveAndReimport(); return Cache[file] = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        static Material Find(string name)
        {
            foreach (var g in AssetDatabase.FindAssets(name + " t:Material")) { var p = AssetDatabase.GUIDToAssetPath(g); if (Path.GetFileNameWithoutExtension(p) == name) return AssetDatabase.LoadAssetAtPath<Material>(p); }
            return null;
        }

        /// <summary>tint null keeps the material's existing base colour (used for coloured cloth and metals); metallic is taken from the mask map's red channel.</summary>
        static void Surface(string material, string prefix, Vector2 tiling, float bump, float occlusion, Color? tint = null, bool keepColour = false, bool metal = false, float smooth = 1f)
        {
            var m = Find(material); if (m == null) { Debug.LogWarning("[MaterialOverhaul] missing " + material); return; }
            var albedo = Import(prefix + "_albedo.png", true, false); var normal = Import(prefix + "_normal.png", false, true); var mask = Import(prefix + "_mask.png", false, false); var ao = Import(prefix + "_ao.png", false, false);
            m.SetTexture("_BaseMap", albedo); m.SetTexture("_MainTex", albedo); if (!keepColour) m.SetColor("_BaseColor", tint ?? Color.white);
            m.SetTexture("_BumpMap", normal); m.SetFloat("_BumpScale", bump); m.EnableKeyword("_NORMALMAP");
            m.SetTexture("_MetallicGlossMap", mask); m.SetFloat("_Metallic", metal ? 1f : 0f); m.SetFloat("_Smoothness", smooth); m.SetFloat("_SmoothnessTextureChannel", 0f); m.EnableKeyword("_METALLICSPECGLOSSMAP");
            m.SetTexture("_OcclusionMap", ao); m.SetFloat("_OcclusionStrength", occlusion); m.EnableKeyword("_OCCLUSIONMAP");
            foreach (var p in new[] { "_BaseMap", "_BumpMap", "_MetallicGlossMap", "_OcclusionMap" }) { m.SetTextureScale(p, tiling); m.SetTextureOffset(p, Vector2.zero); }
            m.SetFloat("_SpecularHighlights", 1f); m.SetFloat("_EnvironmentReflections", 1f);
            EditorUtility.SetDirty(m); Debug.Log($"[MaterialOverhaul] {material}: {prefix} maps, tiling {tiling}");
        }

        // ---- pass 3 (3 Nov 2026): every remaining flat-colour material in PatientBay, Corridor and OT gets a real CC0 PBR set by category (Logs/material_audit.csv lists them).
        // Authored colour and smoothness are recorded once in Tools/Environment/data/material_originals.txt and always re-derived from there, so re-running never compounds.
        const string Originals = "Tools/Environment/data/material_originals.txt";
        static System.Collections.Generic.Dictionary<string, (Color c, float s)> orig;

        static (Color c, float s) Original(Material m)
        {
            if (orig == null)
            {
                orig = new System.Collections.Generic.Dictionary<string, (Color, float)>();
                if (File.Exists(Originals))
                    foreach (var l in File.ReadAllLines(Originals)) { var p = l.Split('|'); if (p.Length == 5) orig[p[0]] = (new Color(float.Parse(p[1], System.Globalization.CultureInfo.InvariantCulture), float.Parse(p[2], System.Globalization.CultureInfo.InvariantCulture), float.Parse(p[3], System.Globalization.CultureInfo.InvariantCulture)), float.Parse(p[4], System.Globalization.CultureInfo.InvariantCulture)); }
            }
            if (!orig.TryGetValue(m.name, out var o))
            {
                o = (m.HasProperty("_BaseColor") ? m.GetColor("_BaseColor") : Color.white, m.HasProperty("_Smoothness") ? m.GetFloat("_Smoothness") : .3f);
                orig[m.name] = o; Directory.CreateDirectory(Path.GetDirectoryName(Originals));
                File.AppendAllText(Originals, string.Format(System.Globalization.CultureInfo.InvariantCulture, "{0}|{1}|{2}|{3}|{4}" + System.Environment.NewLine, m.name, o.c.r, o.c.g, o.c.b, o.s));
            }
            return o;
        }

        static void Category(string[] names, string set, Vector2 tile, float bump, float ao, float brighten, bool metal, float meanAlpha)
        {
            foreach (var n in names)
            {
                var m = Find(n); if (m == null) continue;
                var o = Original(m);
                var col = new Color(Mathf.Clamp01(o.c.r * brighten), Mathf.Clamp01(o.c.g * brighten), Mathf.Clamp01(o.c.b * brighten), 1f);
                Surface(n, set, tile, bump, ao, col, false, metal, Mathf.Clamp01(o.s / meanAlpha));
            }
        }

        static void Pass3()
        {
            Category(new[] { "Brushed stainless hardware", "Glass wall mullion", "Board aluminium", "M_SteelBrushed", "M_MonSteel", "Door theatre steel", "Door steel" }, "brushed_steel", new Vector2(2, 2), .4f, 1f, 1.35f, true, .55f);
            Category(new[] { "Sofa steel leg", "Planter brushed grey", "M_Chrome", "PatientBay chrome", "M_MonChrome", "Brass screw", "Clipboard clip", "Badge clip" }, "satin_steel", new Vector2(2, 2), .4f, 1f, 1.3f, true, .55f);
            Category(new[] { "Satin ivory trim", "Clean ivory housing", "M_PlasticWhite", "Light housing", "Door architrave", "Ivory door frame", "Door frame steel", "Reception white laminate", "Frame mat", "PatientBay ceiling grid", "M_MonShell", "M_MonShell2", "M_MonPlastic", "BedFrame" }, "white_plastic", new Vector2(4, 4), .3f, 1f, 1.1f, false, .6f);
            Category(new[] { "Recesses and tablet bezel", "M_PlasticGrey", "Wall TV housing", "M_MonBezel", "M_MonKey", "Keyboard grey", "Reception kick", "Door seal" }, "dark_plastic", new Vector2(4, 4), .35f, 1f, 1f, false, .6f);
            Category(new[] { "Warm painted plaster", "Bay sage washable panels", "Pale oak laminate", "Sage clinical door", "Door diagnostic grey blue", "Theatre teal door", "Door laboratory white", "Charcoal skirting", "Teal wayfinding", "Reception blue-grey panel", "Reception teal accent", "Restricted access amber" }, "hosp_wall", new Vector2(3, 3), .3f, .5f, 1f, false, .2f);
            Category(new[] { "Door veneer", "Frame walnut", "Print frame oak" }, "oak_veneer", new Vector2(1, 1), .5f, 1f, 1.25f, false, .6f);
            Category(new[] { "Teal clinical upholstery", "Reception chair fabric", "Sage privacy fabric", "M_FabricNavy" }, "cotton_white", new Vector2(10, 10), .8f, .3f, 1.7f, false, .6f);
            Category(new[] { "Castor rubber", "M_RubberBlack" }, "rubber_black", new Vector2(3, 3), .6f, 1f, 1f, false, .5f);
            Category(new[] { "City road" }, "road_marked", new Vector2(6, 6), .8f, 1f, 1f, false, .6f);
            Category(new[] { "City sidewalk", "City terrace paving" }, "sidewalk", new Vector2(8, 8), .8f, .8f, 1f, false, .6f);
            Category(new[] { "City ground" }, "asphalt", new Vector2(10, 10), .8f, .8f, 1f, false, .6f);
            Category(new[] { "Reception quartz" }, "terrazzo", new Vector2(7, 7), .3f, 1f, 1f, false, .6f);
        }

        /// <summary>
        /// 3 Nov 2026 (owner: windows must look like real glass, transparent AND reflective): premultiplied-alpha transparency keeps the specular and Fresnel reflection at full strength while the pane stays clear (plain alpha blending
        /// multiplied the reflection by the 3 to 6 percent alpha, which is why the panes read as nothing). F0 stays the physical 4 percent; a faint float-glass waviness normal and dust-varied smoothness give the imperfections.
        /// </summary>
        static void Glass()
        {
            var normal = Import("glass_normal.png", false, true); var mask = Import("glass_mask.png", false, false);
            foreach (var (name, alpha, tint) in new[] { ("Corridor glass wall", .10f, new Color(.80f, .90f, .91f)), ("PatientBay window glass", .10f, new Color(.80f, .90f, .91f)), ("Frame glass", .12f, new Color(.80f, .90f, .91f)), ("Door vision glass", .22f, new Color(.74f, .88f, .90f)) })
            {
                var m = Find(name); if (m == null) continue;
                m.SetFloat("_Surface", 1f); m.SetFloat("_Blend", 1f); m.SetFloat("_SrcBlend", 1f); m.SetFloat("_DstBlend", 10f); m.SetFloat("_SrcBlendAlpha", 1f); m.SetFloat("_DstBlendAlpha", 10f); m.SetFloat("_ZWrite", 0f);
                m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT"); m.EnableKeyword("_ALPHAPREMULTIPLY_ON"); m.DisableKeyword("_ALPHABLEND_ON"); m.SetOverrideTag("RenderType", "Transparent"); m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
                m.SetColor("_BaseColor", new Color(tint.r, tint.g, tint.b, alpha)); m.SetFloat("_Metallic", 0f); m.SetFloat("_Smoothness", 1f); m.SetFloat("_SmoothnessTextureChannel", 0f);
                m.SetTexture("_BumpMap", normal); m.SetFloat("_BumpScale", 1f); m.EnableKeyword("_NORMALMAP"); m.SetTexture("_MetallicGlossMap", mask); m.EnableKeyword("_METALLICSPECGLOSSMAP");
                foreach (var pr in new[] { "_BumpMap", "_MetallicGlossMap" }) m.SetTextureScale(pr, new Vector2(1.5f, 1.5f));
                m.SetFloat("_SpecularHighlights", 1f); m.SetFloat("_EnvironmentReflections", 1f); EditorUtility.SetDirty(m); Debug.Log("[MaterialOverhaul] glass " + name);
            }
        }

        public static void Apply()
        {
            // wall paint: fine plaster grain, almost flat; floor: tile seams and satin response at the existing room tilings
            Surface("PatientBay wall paint", "hosp_wall", new Vector2(3f, 2f), .35f, .8f);
            Surface("PatientBay grey vinyl", "hosp_floor", new Vector2(5f, 4f), .9f, .9f, null, false, false, .45f);
            Surface("OT vinyl", "hosp_floor", new Vector2(7f, 7f), .9f, .9f, null, false, false, .45f);
            // pass 2 (3 Nov): ceiling, metal, wood, cloth; ambientCG / Poly Haven CC0 scans (docs/research/asset_sources_and_licenses.md)
            var ceil = new Color(1.25f, 1.25f, 1.25f);
            Surface("PatientBay ceiling tile", "acoustic_ceiling", new Vector2(.28f, .28f), .3f, .15f, ceil);
            Surface("OT ceiling tile", "acoustic_ceiling", new Vector2(.28f, .28f), .3f, .15f, ceil);
            Surface("PatientBay satin steel", "satin_steel", new Vector2(1, 1), .5f, 1f, null, true, true);
            Surface("PatientBay door veneer", "oak_veneer", new Vector2(1, 1), .6f, 1f, new Color(.95f, .82f, .62f));
            Surface("PatientBay curtain privacy", "curtain_weave", new Vector2(2.5f, 5f), 1f, 1f, new Color(.72f, .83f, .86f));
            Surface("PatientBay curtain teal", "curtain_weave", new Vector2(2.5f, 5f), 1f, 1f, new Color(.35f, .62f, .62f));
            Surface("NurseFemale_lab_coat_mat", "cotton_white", new Vector2(14, 14), .5f, .3f, new Color(.95f, .95f, .94f));
            Surface("NurseMale_lab_coat_mat", "cotton_white", new Vector2(14, 14), .5f, .3f, new Color(.95f, .95f, .94f));
            Surface("PatientGown_Blue", "cotton_white", new Vector2(14, 14), .5f, .3f, null, true);
            Surface("BedLinen", "cotton_white", new Vector2(8, 8), .5f, .3f, null, true);
            Surface("NurseFemale_scrub_top_mat", "cotton_white", new Vector2(14, 14), .5f, .3f, null, true);
            Surface("NurseFemale_scrub_trousers_mat", "cotton_white", new Vector2(14, 14), .5f, .3f, null, true);
            Pass3();
            Glass();
            AssetDatabase.SaveAssets(); Debug.Log("[MaterialOverhaul] applied");
        }
    }
}
