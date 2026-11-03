using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ProcSense.Tests.EditMode
{
    /// <summary>
    /// Guards the monitor's imported-material standard against the regressions found while matching Blender: a white specular map
    /// (a mirror instead of anti-reflective glass), lifted display blacks, near-black stand plastic, a metal that lost its smoothness, an LDR room reflection,
    /// and a local reflection probe whose cube map would not survive a save.
    /// </summary>
    public class MonitorMaterialContractTests
    {
        static Material Mat(string name) => AssetDatabase.LoadAssetAtPath<Material>($"Assets/Materials/Equipment/M_{name}.mat");
        static float Lum(Color c) => .2126f * c.linear.r + .7152f * c.linear.g + .0722f * c.linear.b;

        [Test]
        public void Screen_glass_is_anti_reflective_not_a_mirror()
        {
            var m = Mat("MonGlass");
            Assert.That(m, Is.Not.Null);
            Assert.That(m.GetFloat("_WorkflowMode"), Is.EqualTo(0f), "specular workflow");
            Assert.That(m.GetFloat("_Surface"), Is.EqualTo(1f), "transparent layer in front of the display");
            var map = m.GetTexture("_SpecGlossMap");
            Assert.That(map, Is.Not.Null);
            // URP reads the map's RGB directly as reflectance; a white map once turned the screen into a mirror. 12/255 = 4.7% is already bare glass.
            var tex = new Texture2D(2, 2);
            Assert.IsTrue(tex.LoadImage(File.ReadAllBytes(AssetDatabase.GetAssetPath(map))));
            var px = tex.GetPixels32();
            Assert.That(px.Max(p => p.r), Is.LessThanOrEqualTo(12), "specular reflectance (red) of the glass map");
            Assert.That(px.Min(p => p.a), Is.GreaterThanOrEqualTo(200), "smoothness stays glass-like (>= .78)");
            Object.DestroyImmediate(tex);
        }

        [Test]
        public void Display_is_emissive_on_a_dark_panel_so_blacks_are_not_lifted()
        {
            var m = Mat("MonLCD");
            Assert.IsTrue(m.IsKeywordEnabled("_EMISSION"));
            Assert.That(m.GetTexture("_EmissionMap"), Is.Not.Null);
            Assert.That(Lum(m.GetColor("_BaseColor")), Is.LessThan(.01f));
        }

        [Test]
        public void Bezel_is_satin_and_the_housing_carries_baked_occlusion()
        {
            var bezel = Mat("MonBezel");                                                                                    // after the 2 Nov material overhaul the bezel is dark plastic whose smoothness comes from the packed mask map (the slider is its multiplier)
            if (bezel.GetTexture("_MetallicGlossMap") == null) Assert.That(bezel.GetFloat("_Smoothness"), Is.InRange(.6f, .88f), "satin black, not piano black");
            else Assert.That(bezel.GetFloat("_Smoothness"), Is.InRange(.6f, 1f), "mask-driven smoothness: the slider only scales the map");
            foreach (var n in new[] { "MonShell", "MonShell2", "MonBezel", "MonKey" })
                Assert.That(Mat(n).GetTexture("_OcclusionMap"), Is.Not.Null, n + " occlusion map");
            var key = Mat("MonKey");                                                                                        // soft-touch keys: the slider is the whole story only when no mask map drives smoothness (2 Nov overhaul: dark plastic mask)
            if (key.GetTexture("_MetallicGlossMap") == null) Assert.That(key.GetFloat("_Smoothness"), Is.LessThan(.5f), "soft-touch keys");
            else Assert.That(key.GetFloat("_Smoothness"), Is.LessThan(.8f), "soft-touch keys (mask-driven)");
        }

        [Test]
        public void Polished_steel_and_stand_plastic_keep_their_authored_values()
        {
            Assert.That(Mat("MonChrome").GetFloat("_Metallic"), Is.EqualTo(1f));
            Assert.That(Mat("MonChrome").GetFloat("_Smoothness"), Is.GreaterThanOrEqualTo(.9f));
            Assert.That(Mat("MonSteel").GetFloat("_Metallic"), Is.EqualTo(1f));
            Assert.That(Lum(Mat("MonPlastic").GetColor("_BaseColor")), Is.InRange(.03f, .25f), "mid-grey satin plastic, not near-black and not white");
        }

        [Test]
        public void Room_reflection_is_high_dynamic_range_and_the_local_probe_serialises_its_cube()
        {
            const string room = "Assets/Settings/HospitalLighting/PatientBay reflection.exr";
            var ti = (TextureImporter)AssetImporter.GetAtPath(room);
            Assert.That(ti.textureShape, Is.EqualTo(TextureImporterShape.TextureCube));
            Assert.That(ti.GetPlatformTextureSettings("Android").format, Is.EqualTo(TextureImporterFormat.ASTC_HDR_6x6), "Quest: HDR format, not ETC2 RGB");
            Assert.That(ti.GetPlatformTextureSettings("Standalone").format, Is.EqualTo(TextureImporterFormat.RGBAHalf));
            Assert.That(AssetDatabase.LoadAssetAtPath<Cubemap>(room).width, Is.GreaterThanOrEqualTo(512));

            var scene = EditorSceneManager.OpenScene("Assets/Scenes/PatientBay.unity", OpenSceneMode.Additive);
            try
            {
                var probe = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<ReflectionProbe>(true)).Single(p => p.name == "Monitor local reflections");
                Assert.That(probe.mode, Is.EqualTo(UnityEngine.Rendering.ReflectionProbeMode.Custom), "a Baked-mode texture is not stored with the scene");
                Assert.That(probe.customBakedTexture, Is.Not.Null);
                Assert.That(probe.boxProjection, Is.True);
                // Corridor's door leaf and frame sit at x ~ 2.45; the probe's box must stop short of them
                var maxX = probe.transform.position.x + probe.center.x + probe.size.x / 2;
                Assert.That(maxX, Is.LessThan(2.40f), "local probe box must not reach the neighbouring room's door");
            }
            finally { EditorSceneManager.CloseScene(scene, true); }
        }
    }
}
