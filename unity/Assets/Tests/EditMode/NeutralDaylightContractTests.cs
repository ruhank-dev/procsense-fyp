using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ProcSense.Tests.EditMode
{
    /// <summary>
    /// Guards PatientBay's neutral-daylight look (the owner's final reference, replacing the earlier golden-hour direction): a neutral baked sun,
    /// all lights baked (P253, 14 Nov 2026), no realtime shadow casters, a neutral trilight ambient, a clear window with the city skyline, the bake-only patient shadow proxy switched off at
    /// runtime, and the shipping bake quality (so look-development preview settings can never be left in the asset).
    /// </summary>
    public class NeutralDaylightContractTests
    {
        UnityEngine.SceneManagement.Scene _scene;
        Light[] _lights;

        [OneTimeSetUp]
        public void Open()
        {
            _scene = EditorSceneManager.OpenScene("Assets/Scenes/PatientBay.unity", OpenSceneMode.Additive);
            _lights = _scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Light>(true)).ToArray();
        }

        [OneTimeTearDown] public void Close() => EditorSceneManager.CloseScene(_scene, true);

        Light Named(string n) => _lights.Single(l => l.name == n);

        [Test]
        public void Sun_is_a_neutral_baked_directional_with_no_realtime_shadow()
        {
            // P253 (14 Nov 2026) superseded the mixed-sun look-development contract: everything is baked for Quest.
            var sun = Named("Sun through window");
            Assert.That(sun.type, Is.EqualTo(LightType.Directional));
            Assert.That(sun.lightmapBakeType, Is.EqualTo(LightmapBakeType.Baked), "static GI only; no realtime sun");
            Assert.That(sun.transform.eulerAngles.x, Is.InRange(20f, 60f), "daytime elevation (lowered to 24 degrees for the window-frame streaks, owner approved)");
            Assert.That(sun.color.b / sun.color.r, Is.InRange(.8f, 1.1f), "neutral daylight, no orange");
            Assert.That(sun.shadowAngle, Is.GreaterThanOrEqualTo(1.5f), "soft but readable baked penumbra");
            Assert.That(sun.intensity, Is.InRange(.2f, 3.5f), "direct sun visible on floors and walls, still well below an HDR hot spot");
            var dir = sun.transform.forward;
            Assert.That(dir.y, Is.LessThan(0f)); Assert.That(dir.x, Is.GreaterThan(0f), "travels from the -X window into the room");
        }

        [Test]
        public void All_lights_are_baked_and_none_casts_a_realtime_shadow()
        {
            var live = _lights.Where(l => l.enabled && l.gameObject.activeInHierarchy).ToArray();
            foreach (var l in live)
                Assert.That(l.lightmapBakeType, Is.EqualTo(LightmapBakeType.Baked), l.name + " must be baked on Quest");
            var casters = live.Where(l => l.shadows != LightShadows.None && l.lightmapBakeType != LightmapBakeType.Baked).ToArray();
            Assert.That(casters, Is.Empty, "no realtime shadow casters, got " + string.Join(", ", casters.Select(c => c.name)));
            var key = _lights.SingleOrDefault(l => l.name == "Soft clinical key");
            if (key != null) { Assert.That(key.lightmapBakeType, Is.EqualTo(LightmapBakeType.Baked)); Assert.That(key.shadows, Is.EqualTo(LightShadows.None)); }
            foreach (var p in _lights.Where(l => l.name.StartsWith("Baked ceiling panel")))
            { Assert.That(p.color.r / p.color.b, Is.InRange(.9f, 1.25f), "neutral ceiling light"); Assert.That(p.lightmapBakeType, Is.EqualTo(LightmapBakeType.Baked)); }
        }

        [Test]
        public void The_shared_bake_has_lightmaps()
        {
            var maps = AssetDatabase.FindAssets("Lightmap- t:Texture2D", new[] { "Assets/Scenes/PatientBay" });
            Assert.That(maps.Length, Is.GreaterThan(0), "baked lightmaps present");
            Assert.That(AssetDatabase.LoadAssetAtPath<LightingDataAsset>("Assets/Scenes/PatientBay/LightingData.asset"), Is.Not.Null);
        }

        [Test]
        public void Window_has_a_clear_glass_pane_and_the_city_skyline_exterior()
        {
            var all = _scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<MeshRenderer>(true)).ToArray();
            var pane = all.Single(r => r.name == "Window glass pane");
            Assert.That(pane.shadowCastingMode, Is.EqualTo(UnityEngine.Rendering.ShadowCastingMode.Off));
            Assert.IsFalse(GameObjectUtility.GetStaticEditorFlags(pane.gameObject).HasFlag(StaticEditorFlags.ContributeGI));
            Assert.That(pane.sharedMaterial.GetFloat("_Surface"), Is.EqualTo(1f), "transparent");
            Assert.That(pane.sharedMaterial.GetColor("_BaseColor").a, Is.InRange(.02f, .15f), "clear glass");
            Assert.That(pane.transform.lossyScale.z, Is.GreaterThan(2.0f), "the wide window opening (the rebuilt bay window is 2.08 m between its frames)");
            var ext = all.Single(r => r.name == "Window exterior backdrop");
            Assert.That(ext.sharedMaterial.GetTexture("_BaseMap").name, Does.Contain("skyline"), "the high-rise city view");
            Assert.That(all.Any(r => r.name == "Window blinds"), "horizontal blinds");
        }

        [Test]
        public void Ambient_is_neutral_trilight()
        {
            EditorSceneManager.SetActiveScene(_scene);
            Assert.That(RenderSettings.ambientMode, Is.EqualTo(UnityEngine.Rendering.AmbientMode.Trilight));
            Assert.That(RenderSettings.ambientSkyColor.b / RenderSettings.ambientSkyColor.r, Is.InRange(.95f, 1.5f), "neutral to slightly cool sky");
        }

        [Test]
        public void Patient_shadow_proxy_is_bake_only_and_costs_nothing_at_runtime()
        {
            var go = _scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<MeshRenderer>(true)).SingleOrDefault(r => r.name == "Patient baked shadow proxy");
            Assert.IsNotNull(go, "proxy exists");
            Assert.IsFalse(go.enabled, "switched off after the bake: no shadow-pass cost at runtime");
            Assert.That(go.shadowCastingMode, Is.EqualTo(UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly));
        }

        [Test]
        public void Bake_settings_are_the_shipping_quality_not_a_look_development_preview()
        {
            var s = AssetDatabase.LoadAssetAtPath<LightingSettings>("Assets/Settings/HospitalLighting/PatientBay.lighting");
            var so = new SerializedObject(s);
            Assert.That(so.FindProperty("m_LightmapsBakeMode").intValue, Is.EqualTo(0), "non-directional lightmaps (P253: one texture fetch less per pixel)");
            Assert.That(so.FindProperty("m_BakeResolution").floatValue, Is.GreaterThanOrEqualTo(64f));
            Assert.That(so.FindProperty("m_PVRBounces").intValue, Is.GreaterThanOrEqualTo(4));
            Assert.That(so.FindProperty("m_PVRSampleCount").intValue, Is.GreaterThanOrEqualTo(512));
            Assert.That(so.FindProperty("m_PVRDirectSampleCount").intValue, Is.GreaterThanOrEqualTo(64));
            Assert.That(so.FindProperty("m_LightmapMaxSize").intValue, Is.LessThanOrEqualTo(2048), "memory bound for Quest");
        }
    }
}
