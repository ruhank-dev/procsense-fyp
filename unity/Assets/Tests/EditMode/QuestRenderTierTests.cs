using System.IO;
using System.Linq;
using NUnit.Framework;
using ProcSense.VR.Diagnostics;
using ProcSense.VR.Views;
using ProcSense.VR.XR;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.XR.OpenXR;
using UnityEngine.XR.OpenXR.Features;

namespace ProcSense.Tests.EditMode
{
    /// <summary>
    /// The Quest render tier and the restored sunlight effects (P3-PRF-05, P3-PRF-06, P3-PRF-11; owner decision D7, 7 November 2026 UTC): Android has its own pipeline asset whose renderer lets URP
    /// render straight into the eye buffer, desktop keeps the approved asset unchanged, the two effect shaders are stereo-safe, the effects are visible on Android by default, foveation is available
    /// but off, and the diagnostic statistics are computed from frame times.
    /// </summary>
    public class QuestRenderTierTests
    {
        private const string Desktop = "Assets/Settings/URP_Quest.asset", Android = "Assets/Settings/URP_Quest_Android.asset";

        private static UniversalRendererData Renderer(string pipelinePath, int index)
        {
            var so = new SerializedObject(AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(pipelinePath));
            var list = so.FindProperty("m_RendererDataList");
            Assert.Greater(list.arraySize, index, pipelinePath + " renderer " + index);
            return (UniversalRendererData)list.GetArrayElementAtIndex(index).objectReferenceValue;
        }

        private static string QualityPipelineGuid(string levelName)
        {
            string text = File.ReadAllText("ProjectSettings/QualitySettings.asset");
            string block = text.Split(new[] { "  - serializedVersion:" }, System.StringSplitOptions.None).Single(b => b.Contains("name: " + levelName + "\n"));
            int i = block.IndexOf("customRenderPipeline:", System.StringComparison.Ordinal);
            int g = block.IndexOf("guid: ", i, System.StringComparison.Ordinal) + 6;
            return block.Substring(g, 32);
        }

        [Test]
        public void Android_quality_level_uses_the_quest_tier_and_desktop_keeps_the_approved_asset()
        {
            Assert.AreEqual(AssetDatabase.AssetPathToGUID(Android), QualityPipelineGuid("Medium"), "Android's default quality level (Medium) uses URP_Quest_Android");
            Assert.AreEqual(AssetDatabase.AssetPathToGUID(Desktop), QualityPipelineGuid("Ultra"), "desktop's default quality level (Ultra) keeps URP_Quest");
            Assert.AreEqual(Desktop, AssetDatabase.GetAssetPath(UnityEngine.Rendering.GraphicsSettings.defaultRenderPipeline), "the default pipeline is unchanged");
        }

        [Test]
        public void Quest_renderer_renders_straight_into_the_eye_buffer_and_the_desktop_renderer_is_unchanged()
        {
            Assert.AreEqual(IntermediateTextureMode.Auto, Renderer(Android, 0).intermediateTextureMode, "intermediate texture Auto: no forced copy, foveation possible");
            Assert.AreEqual(IntermediateTextureMode.Always, Renderer(Desktop, 0).intermediateTextureMode, "the approved desktop renderer is not touched");
            Assert.AreSame(Renderer(Desktop, 0), Renderer(Android, 1), "renderer 1 of the Quest tier is the original one, kept only for the diagnostic comparison (config B1/B2)");
        }

        [Test]
        public void Quest_tier_keeps_every_other_pipeline_setting_of_the_approved_asset()
        {
            var a = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(Android);
            var d = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(Desktop);
            Assert.AreEqual((d.msaaSampleCount, d.renderScale, d.supportsHDR, d.shadowDistance, d.mainLightShadowmapResolution, d.maxAdditionalLightsCount),
                (a.msaaSampleCount, a.renderScale, a.supportsHDR, a.shadowDistance, a.mainLightShadowmapResolution, a.maxAdditionalLightsCount));
        }

        [TestCase("Assets/Shaders/SunShaft.shader")]
        [TestCase("Assets/Shaders/SunDustMote.shader")]
        public void Sunlight_effect_shaders_are_stereo_safe(string path)
        {
            string s = File.ReadAllText(path);
            foreach (var m in new[] { "#pragma multi_compile_instancing", "UNITY_VERTEX_INPUT_INSTANCE_ID", "UNITY_VERTEX_OUTPUT_STEREO", "UNITY_SETUP_INSTANCE_ID(", "UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(", "UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(" })
                StringAssert.Contains(m, s, path);
        }

        [Test]
        public void Sunlight_effects_draw_on_android_by_default_and_always_on_desktop()
        {
            Assert.IsTrue(DesktopOnly.ShowOnAndroid, "D7: the approved look includes the shafts and dust on Quest");
            Assert.IsTrue(DesktopOnly.Visible(RuntimePlatform.Android, true));
            Assert.IsFalse(DesktopOnly.Visible(RuntimePlatform.Android, false), "the diagnostic toggle can hide them");
            Assert.IsTrue(DesktopOnly.Visible(RuntimePlatform.WindowsPlayer, false) && DesktopOnly.Visible(RuntimePlatform.WindowsEditor, false), "desktop never loses them");
        }

        [Test]
        public void Foveation_is_available_on_android_without_eye_tracking_and_off_by_default()
        {
            var feature = OpenXRSettings.GetSettingsForBuildTargetGroup(BuildTargetGroup.Android).GetFeature<FoveatedRenderingFeature>();
            Assert.IsNotNull(feature);
            Assert.IsTrue(feature.enabled, "the OpenXR Foveated Rendering feature is on for Android");
            Assert.IsFalse(new SerializedObject(feature).FindProperty("useEyeTracking").boolValue, "Quest 2 has no eye tracking: no permission request");
            Assert.AreEqual(0f, QuestRenderTier.DefaultFoveation, "production foveation stays off until measured and approved");
            Assert.AreEqual(1f, QuestRenderTier.DefaultEyeScale);
            Assert.AreEqual((0f, 1f, 0.7f, 1.3f), (QuestRenderTier.ClampFoveation(-1f), QuestRenderTier.ClampFoveation(3f), QuestRenderTier.ClampEyeScale(0.1f), QuestRenderTier.ClampEyeScale(5f)));
        }

        [Test]
        public void Perf_window_derives_fps_and_one_percent_low_from_frame_times()
        {
            var w = new PerfWindow();
            for (int i = 0; i < 99; i++) w.Add(1000.0 / 72.0, 9, 11);
            w.Add(50.0);                    // one 50 ms hitch, no GPU time reported
            w.Add(0); w.Add(double.PositiveInfinity);   // not frames
            var s = w.Summarise();
            Assert.AreEqual(100, s.Frames);
            Assert.AreEqual(1000.0 * 100 / (99 * 1000.0 / 72.0 + 50.0), s.AvgFps, 1e-9);
            Assert.AreEqual(20.0, s.OnePercentLowFps, 1e-9, "the slowest 1% is the 50 ms frame");
            Assert.AreEqual((9.0, 11.0, true, true), (s.AvgCpuMs, s.AvgGpuMs, s.HasCpu, s.HasGpu));
            StringAssert.StartsWith("100,", s.Csv());
            Assert.AreEqual(0, new PerfWindow().Summarise().Frames);
        }

        [Test]
        public void Diagnostic_configurations_are_unique_and_B0_changes_nothing()
        {
            var all = PerfPresets.All;
            Assert.AreEqual(all.Count, all.Select(p => p.Id).Distinct().Count());
            Assert.AreEqual("B0", all[0].Id);
            Assert.IsFalse(all[0].ChangesLook || all[0].LegacyRenderer, "B0 is the production default");
            Assert.IsTrue(all.Single(p => p.Id == "B1").LegacyRenderer && all.Single(p => p.Id == "B1").ShaftsOff, "B1 reproduces the 7 Nov rendering");
        }
    }
}
