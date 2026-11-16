using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace ProcSense.EditorTools
{
    /// <summary>
    /// Quest overhaul, lighting (Agent B): every stationary light becomes Baked so no room needs a realtime shadow map or a realtime light. Applies the conversion, swaps the sun shafts for the cheap
    /// texture-driven shader, trims dust, and (Bake) runs the established connected bake: PatientBay active, Corridor and OT additive, one Lightmapping.Bake(), PatientBay saved.
    /// Batch: -executeMethod ProcSense.EditorTools.BakeEverything.Apply | Bake | Report | MaterialsDryRun | MaterialsApply. Changes only scene/light/material/lighting-settings data (git reverts it).
    /// </summary>
    public static class BakeEverything
    {
        static readonly string[] Rooms = { "PatientBay", "Corridor", "OT" };
        const string LightingPath = "Assets/Settings/HospitalLighting/PatientBay.lighting";
        const string NoisePath = "Assets/Textures/Effects/sunshaft_noise.png";
        const string ShaftShader = "ProcSense/Sun Shaft", ShaftBaked = "ProcSense/Sun Shaft Baked", DustShader = "ProcSense/Sun Dust Mote";

        public static void Apply() { RunApply(); Finish(); }
        public static void ApplyAndAudit() { RunApply(); OpenRooms(); Materials(false); Finish(); }
        public static void Bake() { RunApply(); RunBake(); Finish(); }
        /// <summary>Static renderers that receive lightmaps but got none in the bake (too small for a lightmap chart, or excluded) would show only flat ambient now that nothing is realtime: they are lit by the baked light probes instead (Receive GI = Light Probes), which needs no re-bake.</summary>
        public static void FixUnlit()
        {
            OpenRooms(); int fixedCount = 0;
            foreach (var r in UnityEngine.Object.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (!r.enabled || !GameObjectUtility.GetStaticEditorFlags(r.gameObject).HasFlag(StaticEditorFlags.ContributeGI)) continue;
                if (r.lightmapIndex >= 0) continue;                                    // lightmapped, or 65534 = emissive and unlit by design
                var so = new SerializedObject(r); var gi = so.FindProperty("m_ReceiveGI");
                if (gi == null || gi.intValue != 1) continue;
                gi.intValue = 2; so.ApplyModifiedProperties(); EditorUtility.SetDirty(r); fixedCount++;
                Log($"probe-lit: {r.gameObject.scene.name}/{r.transform.root.name}/{r.name}");
            }
            foreach (var n in Rooms) EditorSceneManager.MarkSceneDirty(SceneManager.GetSceneByName(n));
            EditorSceneManager.SaveOpenScenes(); AssetDatabase.SaveAssets();
            Log($"renderers switched to Receive GI = Light Probes: {fixedCount}"); Finish();
        }

        public static void UnlitStatics()
        {
            OpenRooms();
            foreach (var r in UnityEngine.Object.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (!r.enabled || !GameObjectUtility.GetStaticEditorFlags(r.gameObject).HasFlag(StaticEditorFlags.ContributeGI)) continue;
                if (r.lightmapIndex >= 0 && r.lightmapIndex < 65534) continue;
                var lod = r.GetComponentInParent<LODGroup>(true);
                Log($"UNLIT {r.gameObject.scene.name}: {r.transform.root.name}/{r.name} idx={r.lightmapIndex} lod={(lod != null ? lod.name : "-")} scaleInLm={GameObjectUtility.GetStaticEditorFlags(r.gameObject)}");
            }
            Finish();
        }
        public static void Report() { OpenRooms(); Log(Stats()); Finish(); }
        public static void MaterialsDryRun() { OpenRooms(); Materials(false); Finish(); }
        public static void MaterialsApply() { OpenRooms(); Materials(true); Finish(); }

        static void Finish() { if (Application.isBatchMode) EditorApplication.Exit(0); }
        static void Log(string s) => Debug.Log("[BakeEverything] " + s);

        static Scene OpenRooms()
        {
            var bay = EditorSceneManager.OpenScene("Assets/Scenes/PatientBay.unity", OpenSceneMode.Single);
            EditorSceneManager.OpenScene("Assets/Scenes/Corridor.unity", OpenSceneMode.Additive);
            EditorSceneManager.OpenScene("Assets/Scenes/OT.unity", OpenSceneMode.Additive);
            SceneManager.SetActiveScene(bay);
            return bay;
        }

        static string Stats()
        {
            var lights = UnityEngine.Object.FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            int enabled = lights.Count(l => l.enabled && l.gameObject.activeInHierarchy);
            int realtime = lights.Count(l => l.enabled && l.lightmapBakeType != LightmapBakeType.Baked);
            int shadowed = lights.Count(l => l.enabled && l.lightmapBakeType != LightmapBakeType.Baked && l.shadows != LightShadows.None);
            var rends = UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            int dynCasters = rends.Count(r => r.shadowCastingMode != ShadowCastingMode.Off && !GameObjectUtility.GetStaticEditorFlags(r.gameObject).HasFlag(StaticEditorFlags.ContributeGI));
            return $"lights total {lights.Length}, enabled {enabled}, enabled non-baked {realtime}, of which with shadows {shadowed}; dynamic (non-static) renderers still casting shadows {dynCasters} of {rends.Count(r => !GameObjectUtility.GetStaticEditorFlags(r.gameObject).HasFlag(StaticEditorFlags.ContributeGI))}";
        }

        static void RunApply()
        {
            ImportNoise();
            var settings = AssetDatabase.LoadAssetAtPath<LightingSettings>(LightingPath);
            var so = new SerializedObject(settings);
            so.FindProperty("m_RealtimeEnvironmentLighting").boolValue = false;      // no per-frame ambient probe / skybox GI update
            so.FindProperty("m_LightmapsBakeMode").intValue = 0;                      // non-directional lightmaps: one texture fetch less per pixel
            so.ApplyModifiedProperties(); EditorUtility.SetDirty(settings); AssetDatabase.SaveAssets();

            OpenRooms();
            Log("before: " + Stats());
            int converted = 0, offKey = 0;
            foreach (var l in UnityEngine.Object.FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (l.lightmapBakeType == LightmapBakeType.Baked) continue;
                bool key = l.name == "Soft clinical key";                               // the weak overhead key existed only for realtime contact shadows
                l.lightmapBakeType = LightmapBakeType.Baked;
                if (key) { l.enabled = false; l.shadows = LightShadows.None; offKey++; }
                else if (l.type != LightType.Directional) l.shadows = LightShadows.None;   // baked fills need no baked shadows
                converted++; EditorUtility.SetDirty(l);
            }
            int noCast = 0;
            foreach (var r in UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (GameObjectUtility.GetStaticEditorFlags(r.gameObject).HasFlag(StaticEditorFlags.ContributeGI)) continue;   // static casters are needed for the baked shadows
                if (r.shadowCastingMode == ShadowCastingMode.Off && !r.receiveShadows) continue;
                r.shadowCastingMode = ShadowCastingMode.Off; r.receiveShadows = false; noCast++; EditorUtility.SetDirty(r);
            }
            var noise = AssetDatabase.LoadAssetAtPath<Texture2D>(NoisePath);
            var baked = Shader.Find(ShaftBaked);
            int shafts = 0, dust = 0;
            foreach (var guid in AssetDatabase.FindAssets("t:Material"))
            {
                var m = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid));
                if (m == null || m.shader == null || m.shader.name != ShaftShader || baked == null) continue;
                m.shader = baked; m.SetTexture("_NoiseTex", noise); EditorUtility.SetDirty(m); shafts++;
            }
            foreach (var r in UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                foreach (var m in r.sharedMaterials)
                {
                    if (m == null || m.shader == null || baked == null) continue;
                    if (m.shader.name == ShaftShader) { m.shader = baked; m.SetTexture("_NoiseTex", noise); EditorUtility.SetDirty(m); shafts++; }
                }
                if (r is ParticleSystemRenderer pr && pr.sharedMaterial != null && pr.sharedMaterial.shader != null && pr.sharedMaterial.shader.name == DustShader)
                {
                    var ps = pr.GetComponent<ParticleSystem>(); if (ps == null) continue;
                    var main = ps.main; int max = main.maxParticles, cap = 60;
                    if (max > cap) { var em = ps.emission; em.rateOverTimeMultiplier *= (float)cap / max; main.maxParticles = cap; dust++; EditorUtility.SetDirty(ps); }
                }
            }
            foreach (var s in Rooms.Select(n => SceneManager.GetSceneByName(n))) { EditorSceneManager.MarkSceneDirty(s); }
            EditorSceneManager.SaveOpenScenes(); AssetDatabase.SaveAssets();
            Log($"converted {converted} lights to Baked ({offKey} soft-key lights switched off), {noCast} dynamic renderers set to no shadows, {shafts} shaft materials moved to the baked shader, {dust} dust systems trimmed");
            Log("after: " + Stats());
        }

        static void ImportNoise()
        {
            AssetDatabase.ImportAsset(NoisePath, ImportAssetOptions.ForceSynchronousImport);
            var ti = AssetImporter.GetAtPath(NoisePath) as TextureImporter; if (ti == null) return;
            ti.textureType = TextureImporterType.SingleChannel; var ts = new TextureImporterSettings(); ti.ReadTextureSettings(ts);
            ts.singleChannelComponent = TextureImporterSingleChannelComponent.Red; ti.SetTextureSettings(ts);   // the shader samples .r; the Alpha default left r = 0 (15 Nov 2026 shaft regression)
            ti.sRGBTexture = false; ti.wrapMode = TextureWrapMode.Repeat; ti.mipmapEnabled = true; ti.filterMode = FilterMode.Bilinear;
            ti.textureCompression = TextureImporterCompression.Compressed;
            ti.SetPlatformTextureSettings(new TextureImporterPlatformSettings { name = "Android", overridden = true, maxTextureSize = 128, format = TextureImporterFormat.ASTC_6x6 });
            ti.SaveAndReimport();
        }

        /// <summary>Reflection probes are rendered by the GPU: under -nographics (Null device) the baked cubemaps come out as uninitialised memory (magenta/blue noise up to 65504), which showed as pink noise on glass, chrome and posters and blew out glossy floors. Never bake without a graphics device.</summary>
        static void RequireGpu()
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
                throw new InvalidOperationException("Bake needs a graphics device: run batch mode WITHOUT -nographics (reflection probes render garbage on the Null device)");
        }

        /// <summary>Re-renders only the baked reflection probes of the connected bake into their existing ReflectionProbe-N.exr files (same GUIDs, LightingData and lightmaps untouched).</summary>
        public static void BakeReflectionProbes()
        {
            RequireGpu(); OpenRooms(); int n = 0;
            var probes = UnityEngine.Object.FindObjectsByType<ReflectionProbe>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            // two passes: glossy surfaces seen by one probe reflect the other probes, so a second pass removes anything stale from before
            foreach (var p in probes.Concat(probes))
            {
                if (p.mode != ReflectionProbeMode.Baked || !p.enabled || !p.gameObject.activeInHierarchy) continue;
                string path = p.bakedTexture != null ? AssetDatabase.GetAssetPath(p.bakedTexture) : null;
                if (string.IsNullOrEmpty(path)) { Log($"probe {p.gameObject.scene.name}/{p.name}: no baked texture, skipped"); continue; }
                if (!Lightmapping.BakeReflectionProbe(p, path)) throw new InvalidOperationException("probe bake failed: " + p.name);
                n++; Log($"probe {p.gameObject.scene.name}/{p.name} -> {path}");
            }
            AssetDatabase.Refresh(); Log($"reflection probes re-rendered: {n}"); Finish();
        }

        static void RunBake()
        {
            RequireGpu();
            var bay = OpenRooms();
            Lightmapping.lightingSettings = AssetDatabase.LoadAssetAtPath<LightingSettings>(LightingPath);
            var t0 = DateTime.UtcNow;
            if (!Lightmapping.Bake()) throw new InvalidOperationException("Bake failed");
            EditorSceneManager.MarkSceneDirty(bay); EditorSceneManager.SaveScene(bay); AssetDatabase.SaveAssets();
            Log($"bake done in {(DateTime.UtcNow - t0).TotalMinutes:F1} min: lightmaps={LightmapSettings.lightmaps.Length} probes={LightmapSettings.lightProbes?.count}");
            Log("final: " + Stats());
        }

        // ---- material simplification: Lit -> Simple Lit where nothing is lost that a baked-only scene shows ----
        static readonly string[] Keep = { "glass", "monitor", "screen", "floor", "vinyl", "metal", "steel", "chrome", "mirror", "skin", "eye", "cornea", "lens", "hair", "lash", "teeth", "tooth", "tongue", "nail", "gown", "scrub", "uniform", "_body", "_head", "_opacity", "receptionist", "patient", "shorts", "shoes", "badge", "m_mon", "city" };   // characters belong to Agent D, the city to Agent C, monitor screens stay Lit

        static void Materials(bool apply)
        {
            var simple = Shader.Find("Universal Render Pipeline/Simple Lit");
            var setKw = Type.GetType("UnityEditor.Rendering.Universal.ShaderGUI.SimpleLitShader, Unity.RenderPipelines.Universal.Editor")?.GetMethod("SetMaterialKeywords", BindingFlags.Public | BindingFlags.Static);
            var mats = new HashSet<Material>();
            foreach (var r in UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                foreach (var m in r.sharedMaterials) if (m != null) mats.Add(m);
            var sb = new StringBuilder("material,path,reason\n"); int total = 0, ok = 0, kept = 0;
            foreach (var m in mats)
            {
                if (m.shader == null || m.shader.name != "Universal Render Pipeline/Lit") continue;
                string path = AssetDatabase.GetAssetPath(m); if (string.IsNullOrEmpty(path) || !path.StartsWith("Assets/")) continue;
                total++;
                string why = null; string lower = (m.name + path).ToLowerInvariant();
                if (Keep.Any(k => lower.Contains(k))) why = "name keeps Lit";
                else if (m.HasProperty("_Surface") && m.GetFloat("_Surface") > 0.5f) why = "transparent";
                else if (m.HasProperty("_Metallic") && m.GetFloat("_Metallic") > 0.15f) why = "metallic";
                else if (m.HasProperty("_Smoothness") && m.GetFloat("_Smoothness") > 0.6f) why = "glossy";
                else if (m.HasProperty("_MetallicGlossMap") && m.GetTexture("_MetallicGlossMap") != null) why = "metallic/gloss map";
                else if (m.HasProperty("_ParallaxMap") && m.GetTexture("_ParallaxMap") != null) why = "height map";
                if (why != null) { kept++; sb.AppendLine($"{m.name},{path},keep: {why}"); continue; }
                ok++; sb.AppendLine($"{m.name},{path},{(apply ? "to Simple Lit" : "candidate")}");
                if (apply && simple != null) { m.shader = simple; setKw?.Invoke(null, new object[] { m }); EditorUtility.SetDirty(m); }
            }
            if (apply) AssetDatabase.SaveAssets();
            Directory.CreateDirectory("D:/ProcSenseWork/reports");
            File.WriteAllText("D:/ProcSenseWork/reports/quest_materials_" + (apply ? "applied" : "dryrun") + ".csv", sb.ToString());
            Log($"Lit materials in the three rooms {total}: {(apply ? "switched" : "candidates")} {ok}, kept {kept}");
        }
    }
}
