using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace ProcSense.Tests.EditMode
{
    /// <summary>
    /// Guards the cheap precomputed sun shafts (15 Nov 2026 regression: on Quest 2 the dust stayed but the shafts vanished). The Quest overhaul moved the shafts to "Sun Shaft Baked", which culled
    /// back faces (the far wall of every prism and every face when the eye is inside a beam) and sampled the red channel of a noise texture imported as Alpha only, so the density term collapsed.
    /// Measured in the Editor (SunShaftReview): about a quarter of the original contribution, zero in most PatientBay views. These tests keep the fix: both faces drawn, a red-channel noise,
    /// additive unlit transparent, stereo macros, shafts present and active in the bay and the corridor, aligned with the baked sun and starting at the window glass, shipped in the build.
    /// </summary>
    public class SunShaftContractTests
    {
        const string Baked = "ProcSense/Sun Shaft Baked";
        const string NoisePath = "Assets/Textures/Effects/sunshaft_noise.png";

        static MeshRenderer[] Shafts(UnityEngine.SceneManagement.Scene s) => s.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<MeshRenderer>(true))
            .Where(r => r.sharedMaterial != null && r.sharedMaterial.shader != null && r.sharedMaterial.shader.name.StartsWith("ProcSense/Sun Shaft")).ToArray();

        [Test]
        public void Baked_shaft_shader_draws_both_faces_additively_without_depth_write_and_is_stereo_safe()
        {
            var sh = Shader.Find(Baked);
            Assert.IsNotNull(sh, "shader present");
            Assert.IsTrue(sh.isSupported, "supported on the current graphics API");
            Assert.IsFalse(ShaderUtil.ShaderHasError(sh), "compiles without errors");
            string src = File.ReadAllText("Assets/Shaders/SunShaftBaked.shader");
            StringAssert.Contains("Blend One One", src); StringAssert.Contains("ZWrite Off", src); StringAssert.Contains("Cull [_Cull]", src);
            StringAssert.Contains("_Cull (\"Cull\", Float) = 0", src, "default Off: a back-face cull hides the beam's far wall and the whole beam when the eye is inside it");
            StringAssert.DoesNotContain("_CameraDepthTexture", src, "no depth texture: nothing per eye to sample, no extra pass on Quest");
            foreach (var m in new[] { "#pragma multi_compile_instancing", "UNITY_VERTEX_INPUT_INSTANCE_ID", "UNITY_VERTEX_OUTPUT_STEREO", "UNITY_SETUP_INSTANCE_ID(", "UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(", "UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(" })
                StringAssert.Contains(m, src);
        }

        [Test]
        public void Noise_texture_is_imported_as_the_red_channel_the_shader_samples()
        {
            var ti = (TextureImporter)AssetImporter.GetAtPath(NoisePath);
            var ts = new TextureImporterSettings(); ti.ReadTextureSettings(ts);
            Assert.That(ti.textureType, Is.EqualTo(TextureImporterType.SingleChannel));
            Assert.That(ts.singleChannelComponent, Is.EqualTo(TextureImporterSingleChannelComponent.Red), "the shader reads .r; an Alpha-only import gives r = 0 and a near-black density");
            Assert.IsFalse(ti.sRGBTexture, "data, not colour");
            Assert.That(ti.wrapMode, Is.EqualTo(TextureWrapMode.Repeat));
            StringAssert.Contains("SAMPLE_TEXTURE2D(_NoiseTex, sampler_NoiseTex, nuv).r", File.ReadAllText("Assets/Shaders/SunShaftBaked.shader"));
        }

        [TestCase("Assets/Scenes/PatientBay.unity", "Sun through window", 1)]
        [TestCase("Assets/Scenes/Corridor.unity", "Corridor sun", 3)]
        public void Room_has_active_shafts_aligned_with_the_baked_sun(string path, string sunName, int minPrisms)
        {
            var s = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            try
            {
                var prisms = Shafts(s).Where(r => r.name.StartsWith("Shafts")).ToArray();
                Assert.That(prisms.Length, Is.GreaterThanOrEqualTo(minPrisms), "shaft prisms in " + s.name);
                var sun = s.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Light>(true)).First(l => l.name == sunName);
                foreach (var r in prisms)
                {
                    Assert.IsTrue(r.gameObject.activeInHierarchy && r.enabled && !r.forceRenderingOff, r.name + " is drawn");
                    Assert.IsNotNull(r.GetComponentInParent<ProcSense.VR.Views.DesktopOnly>(), r.name + " sits under the sunlight-effects host (shown on Android by default)");
                    var m = r.sharedMaterial;
                    Assert.That(m.shader.name, Is.EqualTo(Baked), r.name);
                    Assert.That(m.renderQueue, Is.GreaterThanOrEqualTo((int)RenderQueue.Transparent), "drawn after opaques");
                    Assert.That(m.GetFloat("_Cull"), Is.EqualTo((float)CullMode.Off), "material keeps both faces");
                    Assert.That(m.GetFloat("_Intensity"), Is.GreaterThan(0.02f), "visible");
                    Assert.IsNotNull(m.GetTexture("_NoiseTex"), "noise assigned");
                    Assert.That(Vector3.Dot(((Vector3)m.GetVector("_ShaftDir")).normalized, sun.transform.forward), Is.GreaterThan(0.99f), "beams follow the baked sun");
                    var mesh = r.GetComponent<MeshFilter>().sharedMesh;
                    Assert.That(mesh.bounds.size.magnitude, Is.GreaterThan(1f), "real beams, not a collapsed mesh");
                    Assert.That(mesh.colors.Length, Is.EqualTo(mesh.vertexCount), "per-prism strength in the vertex colour");
                }
                Assert.IsTrue(ProcSense.VR.Views.DesktopOnly.ShowOnAndroid, "Quest draws them by default");
            }
            finally { EditorSceneManager.CloseScene(s, true); }
        }

        [Test]
        public void Bay_shafts_start_at_the_window_glass()
        {
            var s = EditorSceneManager.OpenScene("Assets/Scenes/PatientBay.unity", OpenSceneMode.Additive);
            try
            {
                var glass = s.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<MeshRenderer>(true)).Where(r => r.name == "Window glass pane" && r.gameObject.activeInHierarchy).Select(r => r.bounds).ToArray();
                Assert.That(glass, Is.Not.Empty);
                foreach (var r in Shafts(s).Where(r => r.name.StartsWith("Shafts")))
                {
                    var mesh = r.GetComponent<MeshFilter>().sharedMesh; var uv = mesh.uv; var v = mesh.vertices;
                    var starts = Enumerable.Range(0, v.Length).Where(i => uv[i].y < 0.01f).Select(i => r.transform.TransformPoint(v[i])).ToArray();
                    foreach (var p in starts)
                        Assert.That(glass.Min(b => Vector3.Distance(b.ClosestPoint(p), p)), Is.LessThan(0.15f), "each beam begins on a window pane, got " + p);
                }
            }
            finally { EditorSceneManager.CloseScene(s, true); }
        }

        [Test]
        public void Shaft_shader_and_materials_ship_through_the_build_scenes()
        {
            var scenes = EditorBuildSettings.scenes.Where(e => e.enabled).Select(e => e.path).ToArray();
            Assert.That(scenes, Does.Contain("Assets/Scenes/PatientBay.unity").And.Contain("Assets/Scenes/Corridor.unity"));
            var deps = AssetDatabase.GetDependencies(new[] { "Assets/Scenes/PatientBay.unity", "Assets/Scenes/Corridor.unity" }, true);
            Assert.That(deps, Does.Contain("Assets/Shaders/SunShaftBaked.shader"), "referenced by a build scene, so never stripped as unused");
            Assert.That(deps, Does.Contain(NoisePath));
        }
    }
}
