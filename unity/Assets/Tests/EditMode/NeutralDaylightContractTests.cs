using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ProcSense.Tests.EditMode
{
    /// <summary>
    /// Guards PatientBay's neutral-daylight look (the owner's final reference, replacing the earlier golden-hour direction): a soft neutral Mixed sun whose shadows fall on the dynamic patient,
    /// baked (not realtime) room lights, exactly one shadow-casting light, a neutral trilight ambient, a clear window with the city skyline, the bake-only patient shadow proxy switched off at
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
        public void Sun_is_a_soft_neutral_mixed_directional_so_the_patient_casts_real_shadows()
        {
            var sun = Named("Sun through window");
            Assert.That(sun.type, Is.EqualTo(LightType.Directional));
            Assert.That(sun.lightmapBakeType, Is.EqualTo(LightmapBakeType.Mixed), "static GI is baked; dynamic objects (patient, gown) receive its realtime shadow");
            Assert.That(sun.transform.eulerAngles.x, Is.InRange(20f, 60f), "daytime elevation (3 Nov 2026: lowered to 24 degrees for the long window-frame streaks across the floor, owner approved)");
            Assert.That(sun.color.b / sun.color.r, Is.InRange(.8f, 1.1f), "neutral daylight, no orange");
            Assert.That(sun.shadows, Is.Not.EqualTo(LightShadows.None));
            Assert.That(sun.shadowAngle, Is.GreaterThanOrEqualTo(1.5f), "soft but readable penumbra (3 Nov 2026: owner asked for visible sun patches and window-frame shadows, so the 5 degree floor was lowered)");
            Assert.That(sun.intensity, Is.InRange(.2f, 3.5f), "direct sun visible on floors and walls (3 Nov 2026 owner request), still well below an HDR hot spot");
            var dir = sun.transform.forward;
            Assert.That(dir.y, Is.LessThan(0f)); Assert.That(dir.x, Is.GreaterThan(0f), "travels from the -X window into the room");
        }

        [Test]
        public void Room_lights_are_baked_and_exactly_one_light_casts_shadows()
        {
            foreach (var l in _lights.Where(l => l.enabled && l.gameObject.activeInHierarchy && l.name != "Soft clinical key" && l.name != "Sun through window"))
                Assert.That(l.lightmapBakeType, Is.Not.EqualTo(LightmapBakeType.Realtime), l.name + " must be baked on Quest");
            var key = Named("Soft clinical key");
            Assert.That(key.lightmapBakeType, Is.EqualTo(LightmapBakeType.Realtime));
            Assert.That(key.intensity, Is.LessThanOrEqualTo(.4f), "the key only gives dynamic objects form");
            Assert.That(key.shadows, Is.EqualTo(LightShadows.None), "the key must never cast shadows");
            var casters = _lights.Where(l => l.enabled && l.gameObject.activeInHierarchy && l.shadows != LightShadows.None && l.lightmapBakeType != LightmapBakeType.Baked).ToArray();
            Assert.That(casters.Length, Is.LessThanOrEqualTo(1), "Quest budget: one realtime shadow caster at most, got " + string.Join(", ", casters.Select(c => c.name)));
            foreach (var p in _lights.Where(l => l.name.StartsWith("Baked ceiling panel")))
            { Assert.That(p.color.r / p.color.b, Is.InRange(.9f, 1.25f), "neutral ceiling light"); Assert.That(p.lightmapBakeType, Is.EqualTo(LightmapBakeType.Baked)); }
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
            Assert.That(so.FindProperty("m_LightmapsBakeMode").intValue, Is.EqualTo(1), "directional lightmaps");
            Assert.That(so.FindProperty("m_BakeResolution").floatValue, Is.GreaterThanOrEqualTo(64f));
            Assert.That(so.FindProperty("m_PVRBounces").intValue, Is.GreaterThanOrEqualTo(4));
            Assert.That(so.FindProperty("m_PVRSampleCount").intValue, Is.GreaterThanOrEqualTo(512));
            Assert.That(so.FindProperty("m_PVRDirectSampleCount").intValue, Is.GreaterThanOrEqualTo(64));
            Assert.That(so.FindProperty("m_LightmapMaxSize").intValue, Is.LessThanOrEqualTo(2048), "memory bound for Quest");
        }
    }
}
