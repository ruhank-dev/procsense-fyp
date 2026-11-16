using NUnit.Framework;
using ProcSense.EditorTools.UI;
using ProcSense.VR.Settings;
using ProcSense.VR.UI.Theme;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace ProcSense.Tests.EditMode
{
    /// <summary>P3-UIX-09: the desktop-only blur behind the glass. The shader declares the variant and still compiles, the Android build strips it, the desktop turns it on with a
    /// per-camera opaque texture request (no pipeline asset changed), and high contrast turns it off.</summary>
    public class GlassBlurTests
    {
        private sealed class MemoryStore : ISettingsStore
        {
            private readonly System.Collections.Generic.Dictionary<string, string> _v = new System.Collections.Generic.Dictionary<string, string>();
            public string GetString(string key) => _v.TryGetValue(key, out var s) ? s : "";
            public bool HasKey(string key) => _v.ContainsKey(key);
            public int GetInt(string key, int fallback) => fallback;
            public void SetString(string key, string value) => _v[key] = value;
            public void DeleteKey(string key) => _v.Remove(key);
            public void Save() { }
        }

        private ISettingsStore _store;

        [SetUp] public void SetUp() { _store = GameSettings.Store; GameSettings.Store = new MemoryStore(); GameSettings.Reset(); GlassBlur.Reset(); }
        [TearDown] public void TearDown() { GlassBlur.Reset(); GameSettings.Store = _store; GameSettings.Reset(); }

        [Test]
        public void The_shader_declares_the_blur_variant_and_compiles()
        {
            var shader = Shader.Find(GlassPanel.ShaderName);
            Assert.IsNotNull(shader);
            Assert.IsFalse(ShaderUtil.ShaderHasError(shader), "shader errors: " + ShaderUtil.GetShaderMessageCount(shader));
            CollectionAssert.Contains(System.Array.ConvertAll(shader.keywordSpace.keywordNames, k => k), GlassBlur.Keyword);
        }

        [Test]
        public void The_android_build_strips_the_blur_and_desktop_builds_keep_it()
        {
            Assert.IsFalse(GlassBlurStripper.Keep(BuildTarget.Android, true), "never on the headset");
            Assert.IsTrue(GlassBlurStripper.Keep(BuildTarget.Android, false), "the plain glass stays");
            Assert.IsTrue(GlassBlurStripper.Keep(BuildTarget.StandaloneWindows64, true));
            Assert.IsTrue(GlassBlurStripper.Keep(BuildTarget.StandaloneOSX, true));
        }

        [Test]
        public void On_the_desktop_the_camera_asks_for_its_opaque_texture_and_high_contrast_turns_it_off()
        {
            var cam = new GameObject("eyes", typeof(Camera)).GetComponent<Camera>();
            try
            {
                Assert.IsTrue(GlassBlur.Supported, "the editor is a desktop");
                GlassBlur.Apply(cam);
                Assert.IsTrue(GlassBlur.On);
                Assert.IsTrue(Shader.IsKeywordEnabled(GlassBlur.Keyword));
                Assert.AreEqual(CameraOverrideOption.On, cam.GetComponent<UniversalAdditionalCameraData>().requiresColorOption);

                GameSettings.Update(s => s.highContrast = true);
                GlassBlur.Apply(cam);
                Assert.IsFalse(GlassBlur.On, "solid plates: nothing to blur");
                Assert.IsFalse(Shader.IsKeywordEnabled(GlassBlur.Keyword));
                Assert.AreEqual(CameraOverrideOption.UsePipelineSettings, cam.GetComponent<UniversalAdditionalCameraData>().requiresColorOption, "back to the pipeline's own setting");
            }
            finally { Object.DestroyImmediate(cam.gameObject); }
        }
    }
}
