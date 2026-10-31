using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace ProcSense.EditorTools
{
    /// <summary>
    /// Controlled material test: an empty scene with only the monitor prefab (or mirror balls), a floor and an environment, rendered from the
    /// same cameras as the Blender studio set. Variants isolate one cause at a time.
    /// Args: -mode ball|full  -envs name:file:flip:rot[,...]  -steel smooth  -hdr 0/1  -tone 0/1  -res reflectionResolution  -tag text
    /// </summary>
    public static class MaterialParityTest
    {
        static string Root => Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Logs", "MonitorBuild", "parity"));
        const int W = 1600, H = 1200;
        const float Fov = 18.06f;                                   // 85 mm lens, 36 mm sensor fit to width, 4:3 -> vertical 27 mm

        static string Arg(string key, string def)
        {
            var a = Environment.GetCommandLineArgs(); var i = Array.IndexOf(a, key);
            return i >= 0 && i + 1 < a.Length ? a[i + 1] : def;
        }

        static Texture2D Latlong(string file)
        {
            var dir = "Assets/Textures/MaterialTest"; Directory.CreateDirectory(Path.GetFullPath(Path.Combine(Application.dataPath, "..", dir)));
            var dst = $"{dir}/{Path.GetFileName(file)}";
            File.Copy(file, Path.GetFullPath(Path.Combine(Application.dataPath, "..", dst)), true);
            AssetDatabase.ImportAsset(dst, ImportAssetOptions.ForceSynchronousImport);
            var ti = (TextureImporter)AssetImporter.GetAtPath(dst);
            ti.textureShape = TextureImporterShape.Texture2D; ti.sRGBTexture = false; ti.mipmapEnabled = true; ti.wrapMode = TextureWrapMode.Repeat;
            ti.maxTextureSize = 2048; ti.textureCompression = TextureImporterCompression.Uncompressed; ti.filterMode = FilterMode.Trilinear;
            ti.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(dst);
        }

        public static void Run()
        {
            Directory.CreateDirectory(Root);
            var mode = Arg("-mode", "full");
            var tag = Arg("-tag", "t");
            var envs = Arg("-envs", "std:studio_env.exr:0:0").Split(',');
            var steel = float.Parse(Arg("-steel", "-1"), System.Globalization.CultureInfo.InvariantCulture);
            bool hdr = Arg("-hdr", "0") == "1"; int tone = int.Parse(Arg("-tone", "0"));
            int res = int.Parse(Arg("-res", "1024"));
            var urp = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            var oldHdr = urp.supportsHDR; urp.supportsHDR = hdr;
            try
            {
                foreach (var e in envs)
                {
                    var f = e.Split(':');
                    var tex = Latlong(Path.Combine(Root, f[1]));
                    BuildScene(mode, tex, int.Parse(f[3]), res, steel, tone);
                    foreach (var view in Views(mode))
                        Render(Path.Combine(Root, $"unity_{tag}_{f[0]}_{view.n}.png"), view.pos, view.look, view.fov, tone > 0);
                    Debug.Log($"[Parity] rendered {tag}_{f[0]}");
                }
            }
            finally { urp.supportsHDR = oldHdr; }
        }

        /// <summary>The lat-long capture imported as a cube map with the specular (glossy reflection) convolution Unity needs for rough metals.</summary>
        /// <summary>Does a probe bind in a batch camera render? A mirror ball in an empty scene with black default reflection and one probe holding the studio cube.</summary>
        public static void ProbeBinding()
        {
            var tex = Latlong(Path.Combine(Root, "studio_env_fq1.exr"));
            var cube = CubeAsset(tex);
            foreach (var variant in new[] { "custom", "baked" })
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                RenderSettings.defaultReflectionMode = DefaultReflectionMode.Custom; RenderSettings.customReflectionTexture = null; RenderSettings.ambientMode = AmbientMode.Flat; RenderSettings.ambientLight = Color.gray;
                var ball = GameObject.CreatePrimitive(PrimitiveType.Sphere); ball.transform.position = new Vector3(0, 1.3f, 0);
                var m = new Material(Shader.Find("Universal Render Pipeline/Lit")); m.SetFloat("_Metallic", 1); m.SetFloat("_Smoothness", .95f); ball.GetComponent<MeshRenderer>().sharedMaterial = m;
                var go = new GameObject("probe"); go.transform.position = new Vector3(0, 1.3f, 0);
                var pr = go.AddComponent<ReflectionProbe>(); pr.size = new Vector3(8, 8, 8); pr.importance = 2; pr.boxProjection = false; pr.intensity = 1f;
                if (variant == "custom") { pr.mode = ReflectionProbeMode.Custom; pr.customBakedTexture = cube; } else { pr.mode = ReflectionProbeMode.Baked; pr.bakedTexture = cube; }
                Render(Path.Combine(Root, $"unity_probebind_{variant}.png"), new Vector3(0, 1.3f, 2.5f), new Vector3(0, 1.3f, 0), 40f, false);
            }
        }

        static Cubemap CubeAsset(Texture2D latlong)
        {
            var src = AssetDatabase.GetAssetPath(latlong);
            var dst = src.Replace(".exr", "_cube.exr");
            AssetDatabase.CopyAsset(src, dst);
            var ti = (TextureImporter)AssetImporter.GetAtPath(dst);
            ti.textureShape = TextureImporterShape.TextureCube; ti.generateCubemap = TextureImporterGenerateCubemap.AutoCubemap;
            ti.sRGBTexture = false; ti.mipmapEnabled = true; ti.maxTextureSize = 1024; ti.textureCompression = TextureImporterCompression.Uncompressed;
            var so = new SerializedObject(ti); var conv = so.FindProperty("m_CubemapConvolution"); if (conv != null) { conv.intValue = 1; so.ApplyModifiedProperties(); }
            ti.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Cubemap>(dst);
        }

        static void BuildScene(string mode, Texture2D latlong, int rotation, int reflRes, float steelSmooth, int tone)
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var sky = new Material(Shader.Find("Skybox/Panoramic"));
            sky.SetTexture("_MainTex", latlong); sky.SetFloat("_Mapping", 1); sky.SetFloat("_ImageType", 0); sky.SetFloat("_MirrorOnBack", 0);
            sky.SetFloat("_Rotation", rotation); sky.SetFloat("_Exposure", .55f);
            RenderSettings.skybox = sky;
            RenderSettings.ambientMode = AmbientMode.Skybox; RenderSettings.ambientIntensity = 1f;
            var cm = CubeAsset(latlong);
            RenderSettings.defaultReflectionMode = DefaultReflectionMode.Custom; RenderSettings.customReflectionTexture = cm; RenderSettings.reflectionIntensity = 1f;   // the .55 world strength is already inside the capture
            var skyc = new Material(Shader.Find("Skybox/Cubemap")); skyc.SetTexture("_Tex", cm); skyc.SetFloat("_Exposure", 1f); RenderSettings.skybox = skyc;
            DynamicGI.UpdateEnvironment();

            var fm = new Material(Shader.Find("Universal Render Pipeline/Lit")); fm.SetColor("_BaseColor", new Color(.55f, .56f, .57f)); fm.SetFloat("_Smoothness", .35f);
            var floor = GameObject.CreatePrimitive(PrimitiveType.Plane); floor.transform.localScale = Vector3.one * 2f; floor.GetComponent<MeshRenderer>().sharedMaterial = fm;
            var lg = new GameObject("key", typeof(Light)); lg.transform.rotation = Quaternion.LookRotation(new Vector3(-1.5f, -1.0f, 1.3f).normalized);
            var l = lg.GetComponent<Light>(); l.type = LightType.Directional; l.intensity = 0f; l.color = new Color(1f, .97f, .93f); l.shadows = LightShadows.Soft;

            if (mode == "ball")
            {
                foreach (var (x, smooth) in new[] { (0f, .94f), (.3f, .75f), (-.3f, .4f) })
                {
                    var s = GameObject.CreatePrimitive(PrimitiveType.Sphere); s.transform.localScale = Vector3.one * .2f;
                    s.transform.position = new Vector3(-x, 1.29f, 0);                       // Blender (x,0,1.29) -> Unity (-x, 1.29, 0)
                    var m = new Material(Shader.Find("Universal Render Pipeline/Lit")); m.SetColor("_BaseColor", new Color(.85f, .86f, .88f)); m.SetFloat("_Metallic", 1); m.SetFloat("_Smoothness", smooth);
                    s.GetComponent<MeshRenderer>().sharedMaterial = m;
                }
                return;
            }
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Equipment/vital_signs_monitor.prefab");
            var inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            var rb = inst.GetComponent<Rigidbody>(); if (rb) rb.isKinematic = true;
            foreach (var r in inst.GetComponentsInChildren<MeshRenderer>().Where(r => r.sharedMaterials.Any(m => m && m.name == "M_Chrome")))
            {
                var copy = new Material(r.sharedMaterial) { name = "M_Chrome(test)" };
                if (steelSmooth >= 0) { copy.SetFloat("_Smoothness", steelSmooth); copy.SetFloat("_Metallic", 1f); copy.SetColor("_BaseColor", new Color(.85f, .86f, .88f)); }
                r.sharedMaterial = copy;
            }
            if (tone > 0)
            {
                var volGo = new GameObject("vol"); var vol = volGo.AddComponent<Volume>(); vol.isGlobal = true;
                var prof = ScriptableObject.CreateInstance<VolumeProfile>(); var tm = prof.Add<Tonemapping>(); tm.mode.Override(tone == 2 ? TonemappingMode.ACES : TonemappingMode.Neutral); vol.sharedProfile = prof;
            }
        }

        // Blender cameras (render_monitor.py), converted to Unity axes: Unity = (-x, z, -y); the monitor sits at the origin as in Blender
        static IEnumerable<(string n, Vector3 pos, Vector3 look, float fov)> Views(string mode)
        {
            Vector3 U(float x, float y, float z) => new Vector3(-x, z, -y);
            if (mode == "ball") { yield return ("ball", U(0, -1.6f, 1.30f), U(0, 0, 1.29f), 2 * Mathf.Atan(13.5f / 50f) * Mathf.Rad2Deg); yield break; }
            yield return ("front", U(0, -1.6f, 1.30f), U(0, 0, 1.29f), Fov);
            yield return ("threequarter", U(-.95f, -1.25f, 1.55f), U(0, 0, 1.30f), Fov);
            yield return ("bezel_closeup", U(-.12f, -.55f, 1.34f), U(-.01f, -.09f, 1.26f), Fov);
            yield return ("keys_closeup", U(.08f, -.40f, 1.28f), U(0f, -.095f, 1.18f), Fov);
            yield return ("glass_closeup", U(-.50f, -.50f, 1.50f), U(-.008f, -.10f, 1.316f), Fov);
            yield return ("housing_closeup", U(-.45f, -.45f, 1.55f), U(-.16f, -.05f, 1.40f), Fov);
            yield return ("stand", U(-1.8f, -3.0f, 1.35f), U(0, .04f, .75f), 25.36f);
            yield return ("pole_closeup", U(-.55f, -.55f, .75f), U(0, .045f, .70f), Fov);
        }

        static void Render(string file, Vector3 pos, Vector3 look, float fov, bool tonemap)
        {
            var go = new GameObject("ParityCam") { hideFlags = HideFlags.HideAndDontSave };
            var cam = go.AddComponent<Camera>();
            cam.transform.position = pos; cam.transform.LookAt(look); cam.fieldOfView = fov; cam.nearClipPlane = .02f; cam.farClipPlane = 40f;
            cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(.60f, .61f, .62f);
            var ad = cam.gameObject.AddComponent<UniversalAdditionalCameraData>(); ad.renderPostProcessing = tonemap; ad.antialiasing = AntialiasingMode.None;
            var rt = new RenderTexture(W, H, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB) { antiAliasing = 8 };
            cam.targetTexture = rt; cam.Render(); cam.Render();
            var prev = RenderTexture.active; RenderTexture.active = rt;
            var tex = new Texture2D(W, H, TextureFormat.RGB24, false, false); tex.ReadPixels(new Rect(0, 0, W, H), 0, 0); tex.Apply();
            File.WriteAllBytes(file, tex.EncodeToPNG());
            RenderTexture.active = prev; cam.targetTexture = null;
            UnityEngine.Object.DestroyImmediate(tex); UnityEngine.Object.DestroyImmediate(rt); UnityEngine.Object.DestroyImmediate(go);
        }
    }
}
