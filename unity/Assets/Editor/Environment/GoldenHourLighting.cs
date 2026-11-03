using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace ProcSense.EditorTools
{
    /// <summary>
    /// Golden-hour lighting for PatientBay, lighting only: lights, ambient, the exterior backdrop's tint, bake quality and the connected three-room bake.
    /// Nothing about objects, geometry, textures or floors is touched. Values come from unity/Tools/Lighting/golden.json (tracked, so a clone rebakes with the same sun) so a look can be iterated without recompiling.
    /// Colours in the JSON are sRGB (as the Light colour field shows them). Stages: Apply -> Bake (connected bake) -> Reflections (room cube + monitor local probe).
    /// </summary>
    public static class GoldenHourLighting
    {
        const string LightingPath = "Assets/Settings/HospitalLighting/PatientBay.lighting";
        static string CfgPath => Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Tools", "Lighting", "golden.json"));

        [Serializable] public class SunCfg { public float pitch = 20f, yaw = 135f, intensity = 2.4f, shadowAngle = 2f; public float[] color = { 1f, .80f, .58f }; public bool cookie = false; public bool mixed = false; }
        [Serializable] public class LowSunCfg { public float pitch = 6f, yaw = 108f, intensity = 0f, shadowAngle = .5f; public float[] color = { 1f, .62f, .30f }; public bool cookie = false; }
        [Serializable] public class RectCfg { public float intensity; public float[] color; }
        [Serializable] public class KeyCfg { public float intensity = .3f; public float[] color = { 1f, .84f, .66f }; public bool followSun = true; }
        [Serializable] public class AmbientCfg { public float[] sky = { .40f, .34f, .27f }, equator = { .27f, .22f, .17f }, ground = { .14f, .11f, .08f }; }
        [Serializable] public class ExteriorCfg { public float[] tint = { 1f, .86f, .70f }; public string texture = ""; public float glassAlpha = .05f; public float glassSmooth = .96f; public float glare = .0f; public bool shapers = false; }
        [Serializable] public class BarsCfg { public float mullion = .06f, transom = 0f; }
        [Serializable] public class WashCfg { public float intensity = 0f; public float[] color = { 1f, .86f, .66f }; }
        [Serializable] public class BakeCfg { public string quality = "preview"; public int bounces = 4; public float resolution = 64f; public int direct = 64, indirect = 512, env = 128; public int denoiseDirect = 2, denoiseIndirect = 2; }
        [Serializable] public class Cfg
        {
            public SunCfg sun = new SunCfg();
            public LowSunCfg lowSun = new LowSunCfg();
            public RectCfg window = new RectCfg { intensity = 4f, color = new[] { 1f, .84f, .66f } };
            public RectCfg panels = new RectCfg { intensity = 5.5f, color = new[] { 1f, .92f, .80f } };
            public RectCfg headwall = new RectCfg { intensity = 2f, color = new[] { 1f, .90f, .75f } };
            public KeyCfg key = new KeyCfg();
            public AmbientCfg ambient = new AmbientCfg();
            public ExteriorCfg exterior = new ExteriorCfg();
            public BarsCfg bars = new BarsCfg();
            public WashCfg wash = new WashCfg();
            public float curtainShift = 0f;
            public BakeCfg bake = new BakeCfg();
        }

        static Cfg Load() => File.Exists(CfgPath) ? JsonUtility.FromJson<Cfg>(File.ReadAllText(CfgPath)) : new Cfg();
        static Color C(float[] c) => new Color(c[0], c[1], c[2]);

        public static void WriteDefault() { Directory.CreateDirectory(Path.GetDirectoryName(CfgPath)); File.WriteAllText(CfgPath, JsonUtility.ToJson(new Cfg(), true)); }

        public static void Apply()
        {
            var cfg = Load();
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/PatientBay.unity", OpenSceneMode.Single);
            var lights = UnityEngine.Object.FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            var sun = lights.Single(l => l.name == "Sun through window");
            sun.transform.rotation = Quaternion.Euler(cfg.sun.pitch, cfg.sun.yaw, 0f);
            sun.color = C(cfg.sun.color); sun.intensity = cfg.sun.intensity; sun.lightmapBakeType = cfg.sun.mixed ? LightmapBakeType.Mixed : LightmapBakeType.Baked; sun.shadows = LightShadows.Soft; sun.shadowStrength = .85f; sun.shadowBias = .02f; sun.shadowNormalBias = .4f; sun.shadowAngle = cfg.sun.shadowAngle;
            ApplyCookie(sun, "cookie_main.png", cfg.sun.cookie);
            // Second baked sun at grazing elevation: it is what throws the window-frame patterns onto the far walls (a 6 degree sun carries 5 m across the room
            // before it drops 0.5 m), while the main sun throws the stripes across the floor. Both come through the same real window, so their shadows agree.
            var lowGo = GameObject.Find("Low sun through window");
            if (lowGo != null) UnityEngine.Object.DestroyImmediate(lowGo);
            if (cfg.lowSun.intensity > 0f)
            {
                lowGo = new GameObject("Low sun through window", typeof(Light));
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(lowGo, scene);
                lowGo.transform.rotation = Quaternion.Euler(cfg.lowSun.pitch, cfg.lowSun.yaw, 0f);
                var ll = lowGo.GetComponent<Light>(); ll.type = LightType.Directional; ll.lightmapBakeType = LightmapBakeType.Baked; ll.shadows = LightShadows.Soft;
                ll.shadowAngle = cfg.lowSun.shadowAngle; ll.color = C(cfg.lowSun.color); ll.intensity = cfg.lowSun.intensity;
                ApplyCookie(ll, "cookie_low.png", cfg.lowSun.cookie);
            }
            lights = UnityEngine.Object.FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None);   // the low sun may have just been recreated
            foreach (var l in lights)
            {
                if (l.name == "Baked window daylight") { l.color = C(cfg.window.color); l.intensity = cfg.window.intensity; }
                else if (l.name.StartsWith("Baked ceiling panel")) { l.color = C(cfg.panels.color); l.intensity = cfg.panels.intensity; }
                else if (l.name == "Baked headwall reading light") { l.color = C(cfg.headwall.color); l.intensity = cfg.headwall.intensity; }
                else if (l.name == "Soft clinical key")
                {
                    l.color = C(cfg.key.color); l.intensity = cfg.key.intensity; l.shadows = LightShadows.None;   // Quest budget: the Mixed sun is the one shadow caster
                    if (cfg.key.followSun) l.transform.rotation = Quaternion.Euler(cfg.sun.pitch, cfg.sun.yaw, 0f);
                }
            }
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = C(cfg.ambient.sky); RenderSettings.ambientEquatorColor = C(cfg.ambient.equator); RenderSettings.ambientGroundColor = C(cfg.ambient.ground);
            var ext = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Hospital/PatientBayLook/PatientBay window exterior view.mat");
            if (ext != null)
            {
                ext.SetColor("_BaseColor", C(cfg.exterior.tint));
                if (!string.IsNullOrEmpty(cfg.exterior.texture))
                {
                    var tex = ImportExterior(cfg.exterior.texture);
                    ext.SetTexture("_BaseMap", tex); ext.mainTexture = tex;
                }
                EditorUtility.SetDirty(ext);
            }
            AddWindowGlass(scene, cfg);
            AddSunShapers(scene, cfg.exterior.shapers);
            ApplyWindowBars(scene, cfg.bars);
            ApplyHeadwallWash(scene, cfg.wash);
            ShiftCurtains(cfg.curtainShift);
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); AssetDatabase.SaveAssets();
            Debug.Log($"[GoldenHour] applied: sun ({cfg.sun.pitch},{cfg.sun.yaw}) I={cfg.sun.intensity}; exterior material {(ext ? "tinted" : "NOT FOUND")}");
        }

        /// <summary>
        /// Real, visible glazing bars: the centre mullion is widened and a transom is added across the opening, in the existing satin ivory trim, so the sun's frame shadows come from
        /// geometry you can see in the window (no invisible shadow casters). Baselines of the original frame scale are kept so a re-run never compounds.
        /// </summary>
        static void ApplyWindowBars(UnityEngine.SceneManagement.Scene scene, BarsCfg bars)
        {
            var all = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Transform>(true)).ToArray();
            var baseline = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Logs", "Lighting", "window_baseline.json"));
            var mull = all.Where(t => t.name == "Vertical window frame" && Mathf.Abs(t.GetComponent<MeshRenderer>().bounds.center.z) < .02f).FirstOrDefault();
            if (mull != null)
            {
                if (!File.Exists(baseline)) File.WriteAllText(baseline, JsonUtility.ToJson(new Vector3Wrap { v = mull.localScale }));
                var orig = JsonUtility.FromJson<Vector3Wrap>(File.ReadAllText(baseline)).v;
                var ratio = Mathf.Max(1f, bars.mullion / .06f);
                var axis = mull.InverseTransformDirection(Vector3.forward).normalized;                 // the local axis that points along world Z
                var ls = orig; var k = Mathf.Abs(axis.x) > .7f ? 0 : Mathf.Abs(axis.y) > .7f ? 1 : 2; ls[k] *= ratio; mull.localScale = ls;
            }
            foreach (var old in all.Where(t => t != null && t.name == "Window glazing bar").ToArray()) UnityEngine.Object.DestroyImmediate(old.gameObject);
            if (bars.transom <= 0f) return;
            var frame = all.First(t => t.name == "Horizontal window frame" && t.GetComponent<MeshRenderer>() != null);
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube); go.name = "Window glazing bar";
            UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(go, scene);
            go.transform.SetParent(frame.parent, true);
            go.transform.position = new Vector3(-2.34f, 1.92f, 0f); go.transform.rotation = Quaternion.identity; go.transform.localScale = new Vector3(.06f, bars.transom, 2.04f);
            var mr = go.GetComponent<MeshRenderer>(); mr.sharedMaterial = frame.GetComponent<MeshRenderer>().sharedMaterial; mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            GameObjectUtility.SetStaticEditorFlags(go, GameObjectUtility.GetStaticEditorFlags(frame.gameObject)); mr.receiveGI = ReceiveGI.Lightmaps;
        }

        [Serializable] class Vector3Wrap { public Vector3 v; }

        /// <summary>A warm wall-washer on the headwall: the reading-light fixture lights the wall above it (a lamp, not sun, so no direction contradiction with the window).</summary>
        static void ApplyHeadwallWash(UnityEngine.SceneManagement.Scene scene, WashCfg wash)
        {
            var old = GameObject.Find("Baked headwall wash");
            if (old != null) UnityEngine.Object.DestroyImmediate(old);
            if (wash.intensity <= 0f) return;
            var go = new GameObject("Baked headwall wash", typeof(Light));
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(go, scene);
            go.transform.position = new Vector3(0f, 1.96f, 1.73f); go.transform.rotation = Quaternion.identity;                         // faces +Z, toward the headwall
            var l = go.GetComponent<Light>(); l.type = LightType.Rectangle; l.areaSize = new Vector2(1.5f, .12f); l.lightmapBakeType = LightmapBakeType.Baked;
            l.shadows = LightShadows.None; l.color = C(wash.color); l.intensity = wash.intensity;
        }

        /// <summary>
        /// Experiment/option only: slides the two window curtains apart along the wall (their only change is world Z). 0 restores the original positions. The original pose is recorded the first
        /// time so repeated calls never accumulate.
        /// </summary>
        static void ShiftCurtains(float shift)
        {
            var baseline = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Logs", "Lighting", "curtain_baseline.json"));
            var cur = UnityEngine.Object.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Include, FindObjectsSortMode.None).Where(r => r.name == "Gathered sage curtain").OrderBy(r => r.bounds.center.z).ToArray();
            if (cur.Length != 2) { Debug.Log("[GoldenHour] curtains not found: " + cur.Length); return; }
            if (!File.Exists(baseline)) File.WriteAllText(baseline, JsonUtility.ToJson(new CurtainBase { a = cur[0].transform.position, b = cur[1].transform.position }));
            var b = JsonUtility.FromJson<CurtainBase>(File.ReadAllText(baseline));
            cur[0].transform.position = b.a + new Vector3(0, 0, -shift); cur[1].transform.position = b.b + new Vector3(0, 0, shift);
        }

        [Serializable] class CurtainBase { public Vector3 a, b; }

        /// <summary>The golden-hour exterior photograph (derived from the CC0 'Stone Pines' panorama by Tools/Lighting/golden_exterior.py).</summary>
        static Texture2D ImportExterior(string file)
        {
            var src = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Logs", "Exterior2", file));
            var dst = "Assets/Textures/Hospital/PatientBayLook/patientbay_window_exterior_" + Path.GetFileNameWithoutExtension(file).Replace("golden_final", "golden") + ".jpg";   // named after its content (skyline_city, golden...)
            File.Copy(src, Path.GetFullPath(Path.Combine(Application.dataPath, "..", dst)), true);
            AssetDatabase.ImportAsset(dst, ImportAssetOptions.ForceSynchronousImport);
            var ti = (TextureImporter)AssetImporter.GetAtPath(dst);
            ti.sRGBTexture = true; ti.mipmapEnabled = true; ti.anisoLevel = 4; ti.wrapMode = TextureWrapMode.Clamp; ti.filterMode = FilterMode.Trilinear;
            ti.maxTextureSize = 4096; ti.textureCompression = TextureImporterCompression.CompressedHQ;
            ti.SetPlatformTextureSettings(new TextureImporterPlatformSettings { name = "Android", overridden = true, maxTextureSize = 2048, format = TextureImporterFormat.ASTC_6x6, compressionQuality = 100 });
            ti.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(dst);
        }

        /// <summary>A thin clear pane in the existing window opening: a 4 percent Fresnel reflection of the room, no GI contribution, no shadow, so the baked sun passes through it.</summary>
        static void AddWindowGlass(UnityEngine.SceneManagement.Scene scene, Cfg cfg)
        {
            var old = GameObject.Find("Window glass pane");
            if (old != null) UnityEngine.Object.DestroyImmediate(old);
            var path = "Assets/Materials/Hospital/PatientBayLook/PatientBay window glass.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null) { m = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "PatientBay window glass" }; AssetDatabase.CreateAsset(m, path); }
            m.SetFloat("_Surface", 1f); m.SetFloat("_Blend", 0f); m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha); m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_ZWrite", 0f); m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT"); m.SetOverrideTag("RenderType", "Transparent"); m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            m.SetColor("_BaseColor", new Color(.92f, .95f, .96f, cfg.exterior.glassAlpha));
            m.SetFloat("_Metallic", 0f); m.SetFloat("_Smoothness", cfg.exterior.glassSmooth);
            m.SetShaderPassEnabled("ShadowCaster", false);
            EditorUtility.SetDirty(m);
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube); go.name = "Window glass pane";
            UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(go, scene);
            go.transform.position = new Vector3(-2.405f, 1.92f, 0f); go.transform.localScale = new Vector3(.004f, 1.22f, 2.08f);
            var mr = go.GetComponent<MeshRenderer>(); mr.sharedMaterial = m; mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; mr.receiveShadows = false;
            GameObjectUtility.SetStaticEditorFlags(go, 0);
            AddWindowGlare(scene, cfg.exterior.glare);
        }

        /// <summary>
        /// A soft additive glow just inside the opening: the bloom a bright window gets against a darker room, without any post-processing (one unlit quad, one draw call,
        /// no overdraw beyond the window area). Intensity 0 removes it.
        /// </summary>
        static void AddWindowGlare(UnityEngine.SceneManagement.Scene scene, float strength)
        {
            var old = GameObject.Find("Window glare");
            if (old != null) UnityEngine.Object.DestroyImmediate(old);
            if (strength <= 0f) return;
            var dst = "Assets/Textures/Hospital/PatientBayLook/patientbay_window_glare.png";
            File.Copy(Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Logs", "Lighting", "window_glare.png")), Path.GetFullPath(Path.Combine(Application.dataPath, "..", dst)), true);
            AssetDatabase.ImportAsset(dst, ImportAssetOptions.ForceSynchronousImport);
            var ti = (TextureImporter)AssetImporter.GetAtPath(dst); ti.alphaIsTransparency = true; ti.wrapMode = TextureWrapMode.Clamp; ti.mipmapEnabled = true; ti.SaveAndReimport();
            var path = "Assets/Materials/Hospital/PatientBayLook/PatientBay window glare.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null) { m = new Material(Shader.Find("Universal Render Pipeline/Unlit")) { name = "PatientBay window glare" }; AssetDatabase.CreateAsset(m, path); }
            m.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(dst));
            m.SetColor("_BaseColor", new Color(1f, 1f, 1f, Mathf.Clamp01(strength)));
            m.SetFloat("_Surface", 1f); m.SetFloat("_Blend", 0f);
            m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha); m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.One);   // additive glow weighted by alpha
            m.SetFloat("_ZWrite", 0f); m.SetFloat("_Cull", 0f); m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT"); m.SetOverrideTag("RenderType", "Transparent");
            m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent + 5; m.SetShaderPassEnabled("ShadowCaster", false);
            EditorUtility.SetDirty(m);
            var q = GameObject.CreatePrimitive(PrimitiveType.Quad); q.name = "Window glare";
            UnityEngine.Object.DestroyImmediate(q.GetComponent<Collider>());
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(q, scene);
            q.transform.position = new Vector3(-2.385f, 1.92f, 0f); q.transform.rotation = Quaternion.Euler(0f, 90f, 0f); q.transform.localScale = new Vector3(2.5f, 1.55f, 1f);
            var mr = q.GetComponent<MeshRenderer>(); mr.sharedMaterial = m; mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; mr.receiveShadows = false;
            mr.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off; mr.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
            GameObjectUtility.SetStaticEditorFlags(q, 0);
        }

        /// <summary>
        /// Window-pane cookie (made by Tools/Lighting/sun_cookie.py for this exact sun direction): the baked sun is projected through the window's heavy mullion and transom pattern,
        /// so the floor and walls get the clearly separated frame patches of the reference. The light sits at the window centre so the cookie plane lines up with the opening.
        /// </summary>
        static void ApplyCookie(Light l, string file, bool on)
        {
            if (!on) { l.cookie = null; return; }
            var dst = "Assets/Textures/Hospital/PatientBayLook/patientbay_" + file;
            File.Copy(Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Logs", "Lighting", file)), Path.GetFullPath(Path.Combine(Application.dataPath, "..", dst)), true);
            AssetDatabase.ImportAsset(dst, ImportAssetOptions.ForceSynchronousImport);
            var ti = (TextureImporter)AssetImporter.GetAtPath(dst);
            ti.textureType = TextureImporterType.Cookie; ti.alphaSource = TextureImporterAlphaSource.FromGrayScale; ti.alphaIsTransparency = false;
            ti.wrapMode = TextureWrapMode.Clamp; ti.mipmapEnabled = false; ti.isReadable = false; ti.maxTextureSize = 1024;
            ti.SetPlatformTextureSettings(new TextureImporterPlatformSettings { name = "Android", overridden = true, maxTextureSize = 1024, format = TextureImporterFormat.ASTC_6x6 });
            ti.SaveAndReimport();
            l.transform.position = new Vector3(-2.4f, 1.9f, 0f);
            l.cookie = AssetDatabase.LoadAssetAtPath<Texture2D>(dst); l.cookieSize2D = new Vector2(3.2f, 3.2f);
        }

        /// <summary>
        /// Invisible light shapers in the window plane: two wide mullions and a transom that only cast shadows (ShadowsOnly), so the baked suns throw the clearly separated
        /// window-frame patches of the reference onto the floor and walls. Nothing is rendered, only the bake and the baked lightmaps see them.
        /// </summary>
        static void AddSunShapers(UnityEngine.SceneManagement.Scene scene, bool on)
        {
            foreach (var old in GameObject.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None).Where(t => t.name == "Sun shaper").ToArray()) UnityEngine.Object.DestroyImmediate(old.gameObject);
            if (!on) return;
            var parts = new (Vector3 c, Vector3 size)[]
            {
                (new Vector3(-2.396f, 1.92f, -.52f), new Vector3(.01f, 1.18f, .16f)),
                (new Vector3(-2.396f, 1.92f, .52f), new Vector3(.01f, 1.18f, .16f)),
                (new Vector3(-2.396f, 1.95f, 0f), new Vector3(.01f, .10f, 2.06f)),
            };
            foreach (var (c, size) in parts)
            {
                var g = GameObject.CreatePrimitive(PrimitiveType.Cube); g.name = "Sun shaper";
                UnityEngine.Object.DestroyImmediate(g.GetComponent<Collider>());
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(g, scene);
                g.transform.position = c; g.transform.localScale = size;
                var mr = g.GetComponent<MeshRenderer>(); mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly; mr.receiveShadows = false;
                GameObjectUtility.SetStaticEditorFlags(g, StaticEditorFlags.ContributeGI);
            }
        }

        static void Quality(BakeCfg b)
        {
            var settings = AssetDatabase.LoadAssetAtPath<LightingSettings>(LightingPath);
            var so = new SerializedObject(settings);
            so.FindProperty("m_BakeResolution").floatValue = b.resolution;
            so.FindProperty("m_PVRDirectSampleCount").intValue = b.direct;
            so.FindProperty("m_PVRSampleCount").intValue = b.indirect;
            so.FindProperty("m_PVREnvironmentSampleCount").intValue = b.env;
            so.FindProperty("m_PVRBounces").intValue = b.bounces;
            so.FindProperty("m_PVRDenoiserTypeDirect").intValue = b.denoiseDirect; so.FindProperty("m_PVRDenoiserTypeIndirect").intValue = b.denoiseIndirect;
            so.ApplyModifiedProperties(); EditorUtility.SetDirty(settings); AssetDatabase.SaveAssets();
            Lightmapping.lightingSettings = settings;
        }

        /// <summary>Connected bake: PatientBay active, Corridor and OT additive (their scene files are never saved), as in the established procedure.</summary>
        public static void Bake()
        {
            var cfg = Load();
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-final") >= 0) cfg.bake = new BakeCfg { quality = "final", bounces = 4, resolution = 64f, direct = 64, indirect = 512, env = 128, denoiseDirect = 2, denoiseIndirect = 2 };   // the project's established quality
            Quality(cfg.bake);
            var bay = EditorSceneManager.OpenScene("Assets/Scenes/PatientBay.unity", OpenSceneMode.Single);
            EditorSceneManager.OpenScene("Assets/Scenes/Corridor.unity", OpenSceneMode.Additive);
            EditorSceneManager.OpenScene("Assets/Scenes/OT.unity", OpenSceneMode.Additive);
            UnityEngine.SceneManagement.SceneManager.SetActiveScene(bay);
            Lightmapping.lightingSettings = AssetDatabase.LoadAssetAtPath<LightingSettings>(LightingPath);
            var t0 = DateTime.UtcNow;
            if (!Lightmapping.Bake()) throw new InvalidOperationException("Bake failed");
            EditorSceneManager.MarkSceneDirty(bay); EditorSceneManager.SaveScene(bay); AssetDatabase.SaveAssets();
            Debug.Log($"[GoldenHour] bake ({cfg.bake.quality}) done in {(DateTime.UtcNow - t0).TotalMinutes:F1} min: lightmaps={LightmapSettings.lightmaps.Length} probes={LightmapSettings.lightProbes?.count}");
        }

        /// <summary>Refreshes the room HDR cube and the monitor's local probe so reflections show the new lighting (lightmaps are not touched).</summary>
        public static void Reflections()
        {
            RoomReflectionUpgrade.Run();
            RoomReflectionUpgrade.LocalProbe();
            RoomReflectionUpgrade.TrimLocalProbe();
            Debug.Log("[GoldenHour] reflection captures refreshed");
        }
    }
}
