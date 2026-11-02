using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace ProcSense.EditorTools
{
    /// <summary>
    /// The shared city outside every window (owner brief: corridor expansion). One world-space environment, authored around the hospital and placed once in the Corridor scene (all rooms load together,
    /// so PatientBay's window and both corridor glass walls look onto the same city): Layer A real near towers, Layer B simplified mid-distance towers, Layer C hazy skyline impostor rings,
    /// the hospital podium, streets with static rush-hour traffic, a real HDRI sky (Poly Haven 'Kloofendal 48d Partly Cloudy (Pure Sky)', CC0) and atmospheric haze.
    /// Import() brings in the generated models and textures; Build() assembles them; Atmosphere() sets sky + haze for every room scene.
    /// </summary>
    public static class CityExterior
    {
        const string Models = "Assets/Models/Environment/City", Tex = "Assets/Textures/Environment/City", Mats = "Assets/Materials/Environment/City", SkyDir = "Assets/Textures/Environment/Sky";
        const string Root = "City exterior";
        public static readonly Vector3 Centre = new Vector3(4f, -48f, 2.2f);
        static readonly Color Haze = new Color(.70f, .79f, .88f);

        static void Dir(string p) { if (!AssetDatabase.IsValidFolder(p)) { var parent = Path.GetDirectoryName(p).Replace('\\', '/'); Dir(parent); AssetDatabase.CreateFolder(parent, Path.GetFileName(p)); } }

        // ------------------------------------------------------------------------------------------------------------------------------------ import
        public static void Import()
        {
            Dir(Models); Dir(Tex); Dir(Mats); Dir(SkyDir);
            foreach (var n in new[] { "city_near", "city_mid", "city_ground", "city_traffic", "city_trees" })
            {
                var dst = Models + "/" + n + ".fbx"; File.Copy(Path.GetFullPath("Logs/CityBuild/" + n + ".fbx"), Path.GetFullPath(dst), true);
                AssetDatabase.ImportAsset(dst, ImportAssetOptions.ForceSynchronousImport);
                var mi = (ModelImporter)AssetImporter.GetAtPath(dst);
                mi.globalScale = 1; mi.useFileScale = true; mi.importAnimation = false; mi.importCameras = false; mi.importLights = false; mi.isReadable = false; mi.meshCompression = ModelImporterMeshCompression.Off;
                mi.generateSecondaryUV = false; mi.materialImportMode = ModelImporterMaterialImportMode.None; mi.indexFormat = ModelImporterIndexFormat.UInt32; mi.SaveAndReimport();
            }
            foreach (var f in Directory.GetFiles(Path.GetFullPath("Logs/CityArt"), "*.png").Where(f => !Path.GetFileName(f).StartsWith("_") && !f.EndsWith("overview.png")))
            {
                var name = Path.GetFileName(f); var dst = Tex + "/" + name; File.Copy(f, Path.GetFullPath(dst), true);
                AssetDatabase.ImportAsset(dst, ImportAssetOptions.ForceSynchronousImport);
                var ti = (TextureImporter)AssetImporter.GetAtPath(dst);
                bool normal = name.EndsWith("_normal.png"), data = name.EndsWith("_metal.png"), sky = name.StartsWith("skyline"), leaf = name.StartsWith("leaf");
                ti.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default; ti.sRGBTexture = !(normal || data);
                ti.mipmapEnabled = true; ti.anisoLevel = sky ? 1 : 8; ti.wrapMode = sky ? TextureWrapMode.Repeat : TextureWrapMode.Repeat; ti.alphaIsTransparency = sky || leaf; ti.textureCompression = TextureImporterCompression.Compressed;
                ti.maxTextureSize = sky ? 4096 : name.StartsWith("facade") ? 2048 : 1024; ti.filterMode = FilterMode.Trilinear;
                var android = ti.GetPlatformTextureSettings("Android"); android.overridden = true; android.maxTextureSize = sky ? 2048 : name.StartsWith("facade") ? 1024 : 1024; android.format = TextureImporterFormat.ASTC_6x6; ti.SetPlatformTextureSettings(android);
                ti.SaveAndReimport();
            }
            var sdst = SkyDir + "/sky_partly_cloudy_4k.hdr"; File.Copy(Path.GetFullPath("../build/sky/sky_4k.hdr"), Path.GetFullPath(sdst), true);
            AssetDatabase.ImportAsset(sdst, ImportAssetOptions.ForceSynchronousImport);
            var hi = (TextureImporter)AssetImporter.GetAtPath(sdst); hi.textureShape = TextureImporterShape.Texture2D; hi.sRGBTexture = false; hi.maxTextureSize = 4096; hi.mipmapEnabled = false; hi.wrapMode = TextureWrapMode.Repeat;
            hi.textureCompression = TextureImporterCompression.Compressed; hi.filterMode = FilterMode.Bilinear;
            var a = hi.GetPlatformTextureSettings("Android"); a.overridden = true; a.maxTextureSize = 2048; a.format = TextureImporterFormat.ASTC_HDR_6x6; hi.SetPlatformTextureSettings(a);
            hi.SaveAndReimport();
            AssetDatabase.SaveAssets();
            Debug.Log("[CityExterior] imported city models, textures and the HDRI sky");
        }

        // ------------------------------------------------------------------------------------------------------------------------------------ materials
        static Texture2D T(string n) => AssetDatabase.LoadAssetAtPath<Texture2D>(Tex + "/" + n);

        /// <summary>How much of its albedo each city material re-emits as daylight: glass facades carry the most (they reflect the sky), vehicles and street surfaces the least so their colours stay true.</summary>
        static float EmissionScale(string name)
        {
            if (name.StartsWith("Facade")) return 1.05f;
            if (name == "City palette") return .55f;
            if (name == "City bark") return .8f;
            if (name == "City lawn") return 1.1f;
            return 1.0f;
        }

        static Material Lit(string name, Texture2D albedo, float smooth, float metal = 0f, Texture2D normal = null, Texture2D metalMap = null, Color? tint = null)
        {
            var path = Mats + "/" + name + ".mat"; var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null) { m = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name }; AssetDatabase.CreateAsset(m, path); }
            m.enableInstancing = true; m.SetTexture("_BaseMap", albedo); m.SetColor("_BaseColor", tint ?? Color.white); m.SetFloat("_Smoothness", smooth); m.SetFloat("_Metallic", metal);
            if (normal != null) { m.SetTexture("_BumpMap", normal); m.SetFloat("_BumpScale", .8f); m.EnableKeyword("_NORMALMAP"); }
            if (metalMap != null) { m.SetTexture("_MetallicGlossMap", metalMap); m.SetFloat("_Smoothness", 1f); m.SetFloat("_Metallic", 1f); m.EnableKeyword("_METALLICSPECGLOSSMAP"); }
            bool glassy = name.StartsWith("Facade");                                               // only the glass facades reflect the sky; vehicles, paving and foliage are matte, or the bright sky veils their colour white
            m.SetFloat("_SpecularHighlights", glassy ? 1f : 0f); m.SetFloat("_EnvironmentReflections", glassy ? 1f : 0f);
            if (!glassy) { m.EnableKeyword("_SPECULARHIGHLIGHTS_OFF"); m.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF"); }
            // daylight on the city: the albedo re-emitted at a fixed fraction (Daylight), independent of every interior light
            m.SetTexture("_EmissionMap", albedo); m.SetColor("_EmissionColor", Color.white * Daylight * EmissionScale(name)); m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive; m.EnableKeyword("_EMISSION");
            EditorUtility.SetDirty(m); return m;
        }

        static Dictionary<string, Material> CityMaterials()
        {
            var d = new Dictionary<string, Material>();
            for (int i = 0; i < 6; i++) d["Facade" + i] = Lit("Facade" + i, T($"facade{i}_albedo.png"), 1f, 1f, T($"facade{i}_normal.png"), T($"facade{i}_metal.png"));
            d["Concrete"] = Lit("City concrete", T("concrete.png"), .15f); d["Roof"] = Lit("City roof", T("roof.png"), .1f); d["Palette"] = Lit("City palette", T("palette.png"), .35f);
            d["Road"] = Lit("City road", T("road.png"), .5f); d["Sidewalk"] = Lit("City sidewalk", T("sidewalk.png"), .2f); d["Terrace"] = Lit("City terrace paving", T("concrete.png"), .22f, 0f, null, null, new Color(.92f, .94f, .92f));
            d["Lawn"] = Lit("City lawn", T("lawn.png"), .05f);
            d["Bark"] = Lit("City bark", T("bark.png"), .08f, 0f, T("bark_normal.png"));
            d["Leaf"] = Leaf(); d["Ground"] = Lit("City ground", T("concrete.png"), .1f, 0f, null, null, new Color(.38f, .38f, .40f));
            return d;
        }

        /// <summary>Foliage: the leaf-cluster atlas, alpha-clipped and double-sided, with a little self-lit daylight so the canopy reads green under the interior-only lighting.</summary>
        static Material Leaf()
        {
            var m = Lit("City leaf", T("leaf_cluster.png"), .12f);
            m.SetFloat("_AlphaClip", 1f); m.SetFloat("_Cutoff", .5f); m.SetFloat("_Cull", 0f); m.EnableKeyword("_ALPHATEST_ON"); m.SetOverrideTag("RenderType", "TransparentCutout"); m.renderQueue = (int)RenderQueue.AlphaTest;
            m.SetColor("_EmissionColor", Color.white * 1.15f); EditorUtility.SetDirty(m); return m;
        }

        // ------------------------------------------------------------------------------------------------------------------------------------ build
        public static void Build()
        {
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/Corridor.unity", OpenSceneMode.Single);
            var old = scene.GetRootGameObjects().FirstOrDefault(g => g.name == Root); if (old != null) UnityEngine.Object.DestroyImmediate(old);
            var root = new GameObject(Root).transform; var mats = CityMaterials(); var report = new List<string>();
            foreach (var (n, layer, shadows) in new[] { ("city_near", "Layer A near towers", true), ("city_mid", "Layer B mid-distance towers", false), ("city_ground", "Ground, roads, podium terrace", false), ("city_traffic", "Rush-hour traffic and street furniture", false), ("city_trees", "Street and podium trees", false) })
            {
                var src = AssetDatabase.LoadAssetAtPath<GameObject>(Models + "/" + n + ".fbx");
                var go = (GameObject)PrefabUtility.InstantiatePrefab(src); go.transform.SetParent(root, false); go.name = layer; go.transform.position = Vector3.zero;
                foreach (var r in go.GetComponentsInChildren<MeshRenderer>(true))
                {
                    int i = r.name.LastIndexOf("_M_", StringComparison.Ordinal); var key = i >= 0 ? r.name.Substring(i + 3) : "";
                    r.sharedMaterial = mats.TryGetValue(key, out var m) ? m : mats["Concrete"];
                    r.shadowCastingMode = shadows ? ShadowCastingMode.On : ShadowCastingMode.Off; r.receiveShadows = false; r.lightProbeUsage = LightProbeUsage.Off; r.reflectionProbeUsage = ReflectionProbeUsage.Off;
                    GameObjectUtility.SetStaticEditorFlags(r.gameObject, StaticEditorFlags.BatchingStatic | StaticEditorFlags.ReflectionProbeStatic | StaticEditorFlags.OccluderStatic);
                }
                var b = new Bounds(go.transform.position, Vector3.zero); foreach (var r in go.GetComponentsInChildren<Renderer>()) b.Encapsulate(r.bounds);
                int tris = go.GetComponentsInChildren<MeshFilter>().Sum(f => f.sharedMesh.triangles.Length / 3);
                report.Add($"{layer}: {go.GetComponentsInChildren<Renderer>().Length} renderers, {tris} triangles, bounds x {b.min.x:F0}..{b.max.x:F0} y {b.min.y:F0}..{b.max.y:F0} z {b.min.z:F0}..{b.max.z:F0}");
            }
            Rings(root);
            foreach (var r in root.GetComponentsInChildren<MeshRenderer>(true)) r.renderingLayerMask = CityLayer;
            root.gameObject.AddComponent<ProcSense.VR.Views.ExteriorViewRange>();
            // no scene light for the city: an unshadowed realtime sun leaked into the interior (light layers did not isolate it), so the city materials carry their own daylight as emission
            foreach (var l in report) Debug.Log("[CityExterior] " + l);
            // the old billboard backdrop of PatientBay is replaced by this world
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); AssetDatabase.SaveAssets();
            Debug.Log("[CityExterior] built");
        }

        /// <summary>Layer C: three concentric, alpha-cut skyline strips around the hospital, far enough that head movement shows no parallax artefacts, each hazier than the one in front of it.</summary>
        static void Rings(Transform root)
        {
            var rings = new GameObject("Layer C skyline impostors").transform; rings.SetParent(root, false);
            foreach (var (name, tex, radius, height) in new[] { ("Skyline far", "skyline_c.png", 2300f, 420f), ("Skyline mid", "skyline_b.png", 1500f, 380f), ("Skyline near", "skyline_a.png", 900f, 320f) })
            {
                var go = new GameObject(name); go.transform.SetParent(rings, false); go.transform.position = new Vector3(Centre.x, Centre.y - 4f, Centre.z);
                var mesh = Cylinder(radius, height, 96, tex.Replace(".png", ""));
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                var r = go.AddComponent<MeshRenderer>(); r.sharedMaterial = Skyline(name, tex); r.shadowCastingMode = ShadowCastingMode.Off; r.receiveShadows = false; r.lightProbeUsage = LightProbeUsage.Off; r.reflectionProbeUsage = ReflectionProbeUsage.Off;
                GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.ReflectionProbeStatic);
            }
        }

        static Mesh Cylinder(float radius, float height, int seg, string name)
        {
            var path = Models + "/ring_" + name + "_" + (int)radius + ".asset"; var m = AssetDatabase.LoadAssetAtPath<Mesh>(path) ?? new Mesh { name = "ring_" + name };
            m.Clear(); var v = new List<Vector3>(); var uv = new List<Vector2>(); var tris = new List<int>();
            for (int i = 0; i <= seg; i++)
            {
                float a = i * Mathf.PI * 2f / seg; var p = new Vector3(Mathf.Sin(a) * radius, 0, Mathf.Cos(a) * radius);
                v.Add(p); uv.Add(new Vector2((float)i / seg * 2f, 0)); v.Add(p + Vector3.up * height); uv.Add(new Vector2((float)i / seg * 2f, 1));
            }
            for (int i = 0; i < seg; i++) { int a = i * 2; tris.AddRange(new[] { a, a + 1, a + 2, a + 1, a + 3, a + 2 }); }                  // facing inward
            m.SetVertices(v); m.SetUVs(0, uv); m.SetTriangles(tris, 0); m.RecalculateBounds(); m.RecalculateNormals();
            if (AssetDatabase.LoadAssetAtPath<Mesh>(path) == null) AssetDatabase.CreateAsset(m, path); else EditorUtility.SetDirty(m);
            return m;
        }

        static Material Skyline(string name, string tex)
        {
            var path = Mats + "/" + name.Replace(' ', '_') + ".mat"; var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null) { m = new Material(Shader.Find("Universal Render Pipeline/Unlit")) { name = name }; AssetDatabase.CreateAsset(m, path); }
            m.SetTexture("_BaseMap", T(tex)); m.SetColor("_BaseColor", Color.white); m.SetFloat("_AlphaClip", 1); m.SetFloat("_Cutoff", .5f); m.SetFloat("_Cull", 0); m.EnableKeyword("_ALPHATEST_ON");
            m.SetOverrideTag("RenderType", "TransparentCutout"); m.renderQueue = (int)RenderQueue.AlphaTest; EditorUtility.SetDirty(m); return m;
        }

        // ------------------------------------------------------------------------------------------------------------------------------------ sky and haze
        public static void Atmosphere()
        {
            var sky = AssetDatabase.LoadAssetAtPath<Texture2D>(SkyDir + "/sky_partly_cloudy_4k.hdr");
            var path = Mats + "/Sky partly cloudy.mat"; var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null) { m = new Material(Shader.Find("Skybox/Panoramic")) { name = "Sky partly cloudy" }; AssetDatabase.CreateAsset(m, path); }
            m.SetTexture("_MainTex", sky); m.SetFloat("_Mapping", 1); m.SetFloat("_ImageType", 0); m.SetFloat("_MirrorOnBack", 0); m.SetFloat("_Layout", 0);
            m.SetFloat("_Exposure", SkyExposure); m.SetFloat("_Rotation", SkyRotation); m.SetColor("_Tint", new Color(.5f, .5f, .5f, .5f)); EditorUtility.SetDirty(m);
            foreach (var s in new[] { "PatientBay", "Corridor", "OT" })
            {
                var scene = EditorSceneManager.OpenScene("Assets/Scenes/" + s + ".unity", OpenSceneMode.Single);
                var before = $"ambient {RenderSettings.ambientMode} int {RenderSettings.ambientIntensity:F2} skybox {(RenderSettings.skybox ? RenderSettings.skybox.name : "none")} fog {RenderSettings.fog} reflection {RenderSettings.defaultReflectionMode}";
                RenderSettings.skybox = m;
                RenderSettings.fog = true; RenderSettings.fogMode = FogMode.Linear; RenderSettings.fogColor = Haze; RenderSettings.fogStartDistance = 140f; RenderSettings.fogEndDistance = 2400f;
                EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
                Debug.Log($"[CityExterior] {s}: was ({before}); sky and haze set");
            }
            AssetDatabase.SaveAssets();
        }

        // ------------------------------------------------------------------------------------------------------------------------------------ photographic skybox (2 Nov 2026 direction: skybox first, no procedural city)
        const string PhotoSky = SkyDir + "/city_skyline_8k.hdr";

        /// <summary>Copies the processed Poly Haven 'Canary Wharf' skyline HDRI (CC0; Tools/Environment/make_city_skybox.py) into the project with 8K PC / 4K Android import.</summary>
        public static void ImportPhotoSky()
        {
            Dir(SkyDir); File.Copy(Path.GetFullPath("../build/sky/city_skyline_8k.hdr"), Path.GetFullPath(PhotoSky), true);
            AssetDatabase.ImportAsset(PhotoSky, ImportAssetOptions.ForceSynchronousImport);
            var ti = (TextureImporter)AssetImporter.GetAtPath(PhotoSky); ti.textureShape = TextureImporterShape.Texture2D; ti.mipmapEnabled = true; ti.filterMode = FilterMode.Trilinear; ti.anisoLevel = 4; ti.wrapMode = TextureWrapMode.Clamp; ti.maxTextureSize = 8192; ti.sRGBTexture = false;
            ti.SetPlatformTextureSettings(new TextureImporterPlatformSettings { name = "Android", overridden = true, maxTextureSize = 4096, format = TextureImporterFormat.RGB9E5 });
            ti.SaveAndReimport(); Debug.Log("[CityExterior] photographic sky imported");
        }

        /// <summary>Switches the exterior to the photographic skybox: the procedural city layers stay in the scene but inactive (kept as the fallback if headset testing shows the skybox alone is not enough).</summary>
        public static void UsePhotoSky()
        {
            var sky = AssetDatabase.LoadAssetAtPath<Texture2D>(PhotoSky);
            var path = Mats + "/Sky city photographic.mat"; var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null) { m = new Material(Shader.Find("Skybox/Panoramic")) { name = "Sky city photographic" }; AssetDatabase.CreateAsset(m, path); }
            m.SetTexture("_MainTex", sky); m.SetFloat("_Mapping", 1); m.SetFloat("_ImageType", 0); m.SetFloat("_MirrorOnBack", 0); m.SetFloat("_Layout", 0);
            m.SetFloat("_Exposure", PhotoExposure); m.SetFloat("_Rotation", PhotoRotation); m.SetColor("_Tint", new Color(.5f, .5f, .5f, .5f)); EditorUtility.SetDirty(m);
            foreach (var s in new[] { "PatientBay", "Corridor", "OT" })
            {
                var scene = EditorSceneManager.OpenScene("Assets/Scenes/" + s + ".unity", OpenSceneMode.Single);
                RenderSettings.skybox = m; RenderSettings.fog = KeepNearCity; RenderSettings.fogMode = FogMode.Linear; RenderSettings.fogColor = Haze; RenderSettings.fogStartDistance = 140f; RenderSettings.fogEndDistance = 2400f;
                var root = scene.GetRootGameObjects().FirstOrDefault(g => g.name == Root);
                if (root != null) foreach (Transform c in root.transform) c.gameObject.SetActive(KeepNearCity && !c.name.StartsWith("Layer C", StringComparison.Ordinal));   // the photographed skyline replaces the painted impostor rings
                EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
                Debug.Log($"[CityExterior] {s}: photographic skybox set, procedural city layers inactive (root found: {root != null})");
            }
            AssetDatabase.SaveAssets();
        }
        public static float PhotoExposure = 1.0f, PhotoRotation = 0f;
        public static bool KeepNearCity = true;                          // the skybox is the far backdrop; the near towers, streets, traffic and trees stay in front of it (2 Nov: a skybox alone has no streets below the window)

        public static void CalibratePhoto()
        {
            if (float.TryParse(Environment.GetEnvironmentVariable("SKY_ROTATION"), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var r)) PhotoRotation = r;
            if (float.TryParse(Environment.GetEnvironmentVariable("SKY_EXPOSURE"), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var e)) PhotoExposure = e;
            ImportPhotoSky(); UsePhotoSky();
        }

        public const uint CityLayer = 2u;
        public const float Daylight = 1.0f;                                             // emission fraction of the albedo: sunlit-overcast brightness of the facades
        public const float SunAzimuth = 280f, SunElevation = 50f;                       // west, as the baked PatientBay sun (travels toward yaw 100)
        public static float SkyExposure = .78f, SkyRotation = 153f;                     // 280 - 127 (measured HDRI sun azimuth at rotation 0); sign verified by capture

        /// <summary>Applies SKY_ROTATION / SKY_EXPOSURE from the environment (calibration of the sun against the baked sun).</summary>
        public static void Calibrate()
        {
            if (float.TryParse(Environment.GetEnvironmentVariable("SKY_ROTATION"), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var r)) SkyRotation = r;
            if (float.TryParse(Environment.GetEnvironmentVariable("SKY_EXPOSURE"), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var e)) SkyExposure = e;
            Atmosphere();
        }

        /// <summary>The corridor's first old billboard exterior in PatientBay is switched off: the world now provides the view.</summary>
        public static void RetireBackdrop()
        {
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/PatientBay.unity", OpenSceneMode.Single);
            int n = 0;
            foreach (var t in scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Transform>(true)).Where(t => t.name == "Window exterior backdrop" || t.name == "Sun glare shapers").ToArray()) { t.gameObject.SetActive(false); n++; }
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
            Debug.Log("[CityExterior] PatientBay billboard backdrop objects switched off: " + n);
        }
    }
}
