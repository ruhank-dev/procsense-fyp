using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ProcSense.VR.Views;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace ProcSense.EditorTools
{
    /// <summary>
    /// Quest "Impostor" exterior (overhaul, 14 Nov 2026). The headset is GPU bound and the city is only seen through windows, so on Android it is drawn with the cheapest practical surface: every URP Lit
    /// material of the city becomes a "ProcSense/Exterior Unlit" copy (one texture sample; sun/shade and street-canyon darkening computed per vertex), and the 8k HDR sky becomes a 2048 x 1024 LDR panorama
    /// (the camera has HDR off, so the picture is the same). Geometry, placement and chunk culling are those of the Optimised variant. Nothing is deleted: QuestExterior keeps the original materials and sky and
    /// shows either set (diagnostic variant Original / Optimised / Impostor). Re-runnable. Batch: -executeMethod ProcSense.EditorTools.ExteriorImpostorBuild.Run
    /// </summary>
    public static class ExteriorImpostorBuild
    {
        private const string MatDir = "Assets/Materials/Environment/City/Impostor";
        private const string SkyPng = "Assets/Textures/Environment/Sky/city_skyline_ldr_2048.png";

        public static void Run()
        {
            bool ok = false;
            try { Build(); ok = true; } catch (Exception e) { Debug.LogError("[ExteriorImpostorBuild] " + e); }
            EditorApplication.Exit(ok ? 0 : 1);
        }

        private static void Build()
        {
            AssetDatabase.ImportAsset(SkyPng, ImportAssetOptions.ForceSynchronousImport);
            var ti = (TextureImporter)AssetImporter.GetAtPath(SkyPng);
            ti.textureType = TextureImporterType.Default; ti.sRGBTexture = true; ti.mipmapEnabled = false; ti.wrapMode = TextureWrapMode.Repeat; ti.filterMode = FilterMode.Bilinear; ti.maxTextureSize = 2048;
            ti.SetPlatformTextureSettings(new TextureImporterPlatformSettings { name = "Android", overridden = true, maxTextureSize = 2048, format = TextureImporterFormat.ASTC_6x6, compressionQuality = 100, textureCompression = TextureImporterCompression.Compressed });
            ti.SaveAndReimport();

            Directory.CreateDirectory(MatDir);
            var shader = Shader.Find("ProcSense/Exterior Unlit") ?? throw new Exception("shader missing");
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/Corridor.unity", OpenSceneMode.Single);
            var root = scene.GetRootGameObjects().First(g => g.name == "City exterior");
            var q = root.GetComponent<QuestExterior>() ?? throw new Exception("QuestExterior missing");

            var sun = UnityEngine.Object.FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None).Where(l => l.type == LightType.Directional).OrderByDescending(l => l.intensity).FirstOrDefault();
            Vector3 toSun = sun != null ? -sun.transform.forward : new Vector3(-0.88f, 0.2f, 0.43f);

            var holderNames = new HashSet<string> { "Quest variant" };
            var allRenderers = root.GetComponentsInChildren<MeshRenderer>(true);
            var replaced = new HashSet<Renderer>(OriginalsOf(q));
            var targets = new List<Renderer>(); var origSets = new List<QuestExterior.MaterialSet>(); var impSets = new List<QuestExterior.MaterialSet>();
            var cache = new Dictionary<Material, Material>();
            foreach (var r in allRenderers)
            {
                if (replaced.Contains(r)) continue;                                           // hidden whenever the Optimised geometry is shown
                var mats = r.sharedMaterials; var converted = new Material[mats.Length]; bool any = false;
                for (int i = 0; i < mats.Length; i++)
                {
                    var m = mats[i];
                    if (m != null && m.shader != null && m.shader.name == "Universal Render Pipeline/Lit") { converted[i] = Convert(m, shader, toSun, cache); any = true; } else converted[i] = m;
                }
                if (!any) continue;
                targets.Add(r); origSets.Add(new QuestExterior.MaterialSet { materials = mats }); impSets.Add(new QuestExterior.MaterialSet { materials = converted });
            }
            var skyOrig = RenderSettings.skybox;
            var skyPath = MatDir + "/Sky city LDR.mat";
            var sky = AssetDatabase.LoadAssetAtPath<Material>(skyPath);
            if (sky == null) { sky = new Material(skyOrig); AssetDatabase.CreateAsset(sky, skyPath); }
            sky.SetTexture("_MainTex", AssetDatabase.LoadAssetAtPath<Texture2D>(SkyPng)); EditorUtility.SetDirty(sky);
            q.ConfigureImpostor(targets.ToArray(), origSets.ToArray(), impSets.ToArray(), skyOrig, sky);
            EditorUtility.SetDirty(q);
            Debug.Log($"[ExteriorImpostorBuild] {targets.Count} renderers, {cache.Count} unlit materials, toSun {toSun}, sky {skyOrig.name} -> {sky.name}");
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); AssetDatabase.SaveAssets();
        }

        private static IEnumerable<Renderer> OriginalsOf(QuestExterior q)
        {
            var so = new SerializedObject(q); var p = so.FindProperty("originals");
            for (int i = 0; i < p.arraySize; i++) yield return p.GetArrayElementAtIndex(i).objectReferenceValue as Renderer;
        }

        private static Material Convert(Material src, Shader shader, Vector3 toSun, Dictionary<Material, Material> cache)
        {
            if (cache.TryGetValue(src, out var done)) return done;
            var path = MatDir + "/" + src.name.Replace(' ', '_') + "_unlit.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null) { m = new Material(shader); AssetDatabase.CreateAsset(m, path); }
            m.shader = shader;
            var tex = src.HasProperty("_BaseMap") ? src.GetTexture("_BaseMap") : null; if (tex == null && src.HasProperty("_MainTex")) tex = src.GetTexture("_MainTex");
            m.SetTexture("_BaseMap", tex);
            if (src.HasProperty("_BaseMap")) { m.SetTextureScale("_BaseMap", src.GetTextureScale("_BaseMap")); m.SetTextureOffset("_BaseMap", src.GetTextureOffset("_BaseMap")); }
            m.SetColor("_BaseColor", src.HasProperty("_BaseColor") ? src.GetColor("_BaseColor") : Color.white);
            bool clip = src.HasProperty("_AlphaClip") && src.GetFloat("_AlphaClip") > 0.5f;
            m.SetFloat("_Cutoff", clip ? src.GetFloat("_Cutoff") : 0f);
            m.renderQueue = clip ? (int)RenderQueue.AlphaTest : (int)RenderQueue.Geometry;
            m.SetVector("_SunDir", new Vector4(toSun.x, toSun.y, toSun.z, 0));
            m.SetVector("_Shade", new Vector4(0.6f, 0.55f, 0.55f, 0)); m.SetVector("_CanyonY", new Vector4(-48f, 30f, 0, 0));
            if (clip) m.SetInt("_Cull", (int)CullMode.Off);
            EditorUtility.SetDirty(m); cache[src] = m; return m;
        }
    }
}
