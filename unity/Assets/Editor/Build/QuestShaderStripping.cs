// Android-only shader variant stripping for the Quest 2 build. Strips ONLY keywords that cannot occur in this project; every rule cites its evidence.
// Never runs for desktop/Editor (checks the active build target, and the whole thing is a no-op unless the player build target is Android).
// Writes counts per shader to <project>/Logs/shader_stripping.txt (UTC timestamp).
//
// Project facts (verified 9 Nov 2026 from the assets, not assumed):
//  * Renderer: URP_Quest_Android_Renderer and URP_Quest_Renderer both m_RenderingMode 0 (Forward), m_RendererFeatures [] -> no Forward+, no Deferred, no SSAO,
//    no Screen Space Shadows, no Decal (DBuffer) feature. URP asset m_LightProbeSystem 0 (legacy probes, Adaptive Probe Volumes NOT used).
//  * Baked/mixed rooms use lightmaps (Corridor/OT non-directional, PatientBay directional) with Mixed shadowmask-mode lighting settings (m_MixedBakeMode 2): LIGHTMAP_ON,
//    DIRLIGHTMAP_COMBINED, LIGHTMAP_SHADOW_MIXING, SHADOWS_SHADOWMASK, _MIXED_LIGHTING_SUBTRACTIVE are all KEPT. Light cookies (_LIGHT_COOKIES) and light layers (_LIGHT_LAYERS) KEPT.
//    Main light shadows (_MAIN_LIGHT_SHADOWS, _MAIN_LIGHT_SHADOWS_CASCADE, _SHADOWS_SOFT*) KEPT; additional-light shadows are left to the URP prefilter (asset has them off).
//  * m_EnableRealtimeLightmaps: 0 in every .lighting asset and every scene -> no realtime-GI lightmap, DYNAMICLIGHTMAP_ON cannot occur.
//  * Fog: Corridor, OT and PatientBay have fog on with m_FogMode 1 (Linear); all other scenes have fog off (their stored mode 3 is irrelevant when off). The only
//    RenderSettings.fogMode writes are in Editor scripts (CityExterior.cs, FogMode.Linear). So FOG_EXP and FOG_EXP2 cannot occur; FOG_LINEAR and no-fog are KEPT.
//  * LODGroups: 0 in all 10 scenes and 0 in any prefab (no "!u!205" documents), audit says 0; no script uses LODGroup/crossfade at runtime -> LOD_FADE_CROSSFADE stripped.
//    (If an imported model ever gains a LODGroup, remove the LOD_FADE_CROSSFADE rule.)
//  * Debug display: Rendering Debugger variants (DEBUG_DISPLAY) are never used in a player build; EDITOR_VISUALIZATION is editor-only.
// If a rule is ever wrong the symptom is a pink/missing material only on Quest, so the lead should run the on-device smoke list in docs/qa/quest2_shader_notes.md.
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.Rendering;
using UnityEngine;
using UnityEngine.Rendering;

namespace ProcSense.EditorTools.Build
{
    public sealed class QuestShaderStripping : IPreprocessShaders, IPreprocessBuildWithReport, IPostprocessBuildWithReport
    {
        public int callbackOrder => 100; // after the URP stripper, so we only see what it kept

        // keyword -> why it is safe (kept next to the list so the evidence travels with the code)
        static readonly string[] StripKeywords =
        {
            "DEBUG_DISPLAY",               // Rendering Debugger: not available in player builds of this project
            "EDITOR_VISUALIZATION",        // editor-only scene-view visualisation
            "PROBE_VOLUMES_L1",            // APV: URP asset m_LightProbeSystem 0
            "PROBE_VOLUMES_L2",
            "_SCREEN_SPACE_OCCLUSION",     // SSAO: renderer features empty
            "_MAIN_LIGHT_SHADOWS_SCREEN",  // screen space shadows: renderer features empty
            "_DBUFFER_MRT1",               // decals: renderer features empty
            "_DBUFFER_MRT2",
            "_DBUFFER_MRT3",
            "_FORWARD_PLUS",               // renderers are Forward (m_RenderingMode 0)
            "_CLUSTER_LIGHT_LOOP",
            "_GBUFFER_NORMALS_OCT",        // Deferred not used
            "FOG_EXP",                     // fog is Linear only (see header)
            "FOG_EXP2",
            "LOD_FADE_CROSSFADE",          // 0 LODGroups anywhere
            "DYNAMICLIGHTMAP_ON",          // realtime lightmaps off everywhere
        };

        sealed class Counter { public int In, Stripped, Kept; }
        static readonly Dictionary<string, Counter> Counts = new Dictionary<string, Counter>();
        static readonly Dictionary<string, int> StrippedByKeyword = new Dictionary<string, int>();
        static readonly Dictionary<string, ShaderKeyword[]> KeywordCache = new Dictionary<string, ShaderKeyword[]>();

        static bool Active => EditorUserBuildSettings.activeBuildTarget == BuildTarget.Android;
        static string LogPath => Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Logs", "shader_stripping.txt"));

        public void OnPreprocessBuild(BuildReport report)
        {
            Counts.Clear(); StrippedByKeyword.Clear(); KeywordCache.Clear();
        }

        public void OnProcessShader(Shader shader, ShaderSnippetData snippet, IList<ShaderCompilerData> data)
        {
            if (!Active || shader == null || data == null || data.Count == 0) return;

            var kws = GetKeywords(shader);
            string key = shader.name;
            if (!Counts.TryGetValue(key, out var c)) Counts[key] = c = new Counter();
            c.In += data.Count;

            for (int i = data.Count - 1; i >= 0; i--)
            {
                var set = data[i].shaderKeywordSet;
                string hit = null;
                for (int k = 0; k < kws.Length; k++)
                {
                    if (kws[k].IsValid() && set.IsEnabled(kws[k])) { hit = StripKeywords[k]; break; }
                }
                if (hit == null) { c.Kept++; continue; }
                data.RemoveAt(i);
                c.Stripped++;
                StrippedByKeyword.TryGetValue(hit, out int n); StrippedByKeyword[hit] = n + 1;
            }
        }

        static ShaderKeyword[] GetKeywords(Shader shader)
        {
            string id = shader.name;
            if (KeywordCache.TryGetValue(id, out var arr)) return arr;
            arr = new ShaderKeyword[StripKeywords.Length];
            for (int i = 0; i < arr.Length; i++) arr[i] = new ShaderKeyword(shader, StripKeywords[i]);
            KeywordCache[id] = arr;
            return arr;
        }

        public void OnPostprocessBuild(BuildReport report)
        {
            if (report.summary.platform != BuildTarget.Android) return;
            try
            {
                var sb = new StringBuilder();
                sb.AppendLine("Quest shader stripping (UTC " + DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss") + ")");
                int tin = 0, tstr = 0, tkept = 0;
                var names = new List<string>(Counts.Keys); names.Sort(StringComparer.Ordinal);
                sb.AppendLine("shader | variants in (after URP prefilter) | stripped here | kept");
                foreach (var n in names)
                {
                    var c = Counts[n]; tin += c.In; tstr += c.Stripped; tkept += c.Kept;
                    sb.AppendLine($"{n} | {c.In} | {c.Stripped} | {c.Kept}");
                }
                sb.AppendLine($"TOTAL | {tin} | {tstr} | {tkept}");
                sb.AppendLine("stripped by keyword:");
                foreach (var kv in StrippedByKeyword) sb.AppendLine($"  {kv.Key}: {kv.Value}");
                Directory.CreateDirectory(Path.GetDirectoryName(LogPath));
                File.WriteAllText(LogPath, sb.ToString());
                Debug.Log($"[QuestShaderStripping] stripped {tstr} of {tin} variants; details in {LogPath}");
            }
            catch (Exception e) { Debug.LogWarning("[QuestShaderStripping] could not write log: " + e.Message); }
        }
    }
}
