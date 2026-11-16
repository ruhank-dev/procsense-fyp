using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace ProcSense.Tests.EditMode
{
    /// <summary>
    /// Static checks of the Quest 2 GPU overhaul (owner decisions of 14 Nov 2026): the hospital is lit by baked light only, nothing casts a realtime shadow, MSAA is off and the eye buffer is reduced.
    /// Each check opens the saved scene (no play mode), so it is fast and deterministic. Thresholds are the owner's requirements, not tuned to pass: a failing test means the overhaul is not finished.
    /// The three environment scenes share one lighting bake (PatientBay/LightingData), so the lightmap and probe checks need the bake to have run.
    /// </summary>
    public sealed class QuestOverhaulSceneTests
    {
        private const string QuestAsset = "Assets/Settings/URP_Quest_Android.asset";
        private static readonly string[] Environment = { "PatientBay", "Corridor", "OT" };
        private static readonly string[] AllScenes = { "Boot", "Sys_Core", "MainMenu", "PatientBay", "Corridor", "OT", "Recovery", "DevOnly" };

        private static string Path(string scene) => "Assets/Scenes/" + scene + ".unity";

        private static IEnumerable<T> In<T>(Scene scene) where T : Component =>
            scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<T>(true));

        /// <summary>Opens each scene in turn and always restores the editor's own scene setup, so a test never leaves another scene open or dirty.</summary>
        private static void WithScenes(IEnumerable<string> names, System.Action<string, Scene> check)
        {
            var setup = EditorSceneManager.GetSceneManagerSetup();
            try
            {
                foreach (var name in names) check(name, EditorSceneManager.OpenScene(Path(name), OpenSceneMode.Single));
            }
            finally
            {
                if (setup.Length > 0) EditorSceneManager.RestoreSceneManagerSetup(setup); else EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
            }
        }

        [Test]
        public void Quest_URP_asset_is_the_aggressive_profile()
        {
            var urp = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(QuestAsset);
            Assert.NotNull(urp, QuestAsset);
            Assert.AreEqual(1, urp.msaaSampleCount, "MSAA off (owner decision 14 Nov 2026; a 2x fallback is a runtime option, not the default)");
            // The asset keeps renderScale 1.0 on purpose: the draw scale is applied at runtime by QuestPerformanceProfile (eye buffer 1.0, viewport 0.9 to start), so the two scales must not multiply
            // twice. What the headset renders is therefore the asset scale times the profile's render scale, and that is what is held to the owner's range (sharpness revision 15 Nov 2026: 0.85-1.0).
            Assert.AreEqual(1f, urp.renderScale, "URP render scale stays 1.0: the profile is the single source of the scale");
            float effective = urp.renderScale * ProcSense.VR.Views.QuestPerformanceProfile.RenderScale;
            Assert.That(ProcSense.VR.Views.QuestPerformanceProfile.RenderScale, Is.InRange(0.85f, 1f), "profile start scale: 0.9 (0.8 looked grainy and blurry on the headset footage)");
            Assert.That(effective, Is.InRange(0.85f, 1f), "effective render scale = asset renderScale x profile RenderScale");
            Assert.That(ProcSense.VR.Views.QuestPerformanceProfile.MinScale, Is.GreaterThanOrEqualTo(0.85f), "dynamic resolution never drops below 0.85");
            Assert.AreEqual(1, ProcSense.VR.Views.QuestPerformanceProfile.Msaa, "profile MSAA off");
            Assert.AreEqual(0.5f, ProcSense.VR.Views.QuestPerformanceProfile.Foveation, "fixed foveated rendering Medium (High looked pixelated in the periphery)");
            Assert.IsFalse(urp.supportsMainLightShadows, "zero realtime shadow maps");
            Assert.IsFalse(urp.supportsAdditionalLightShadows, "zero realtime additional-light shadow maps");
            Assert.IsFalse(urp.supportsHDR, "no HDR render target");
            Assert.IsFalse(urp.supportsCameraDepthTexture, "no depth copy");
            Assert.IsFalse(urp.supportsCameraOpaqueTexture, "no colour copy");
            Assert.IsTrue(urp.useSRPBatcher, "SRP Batcher on");
            Assert.IsFalse(urp.supportsDynamicBatching, "dynamic batching off (SRP Batcher replaces it)");
        }

        [Test]
        public void Environment_scenes_have_no_realtime_lights()
        {
            var bad = new List<string>();
            WithScenes(Environment, (name, scene) =>
            {
                foreach (var l in In<Light>(scene))
                    if (l.enabled && l.gameObject.activeInHierarchy && l.lightmapBakeType != LightmapBakeType.Baked) bad.Add($"{name}/{l.name} ({l.type}, {l.lightmapBakeType}, shadows {l.shadows})");
            });
            Assert.IsEmpty(bad, "every light must be Baked (no realtime or mixed light on Quest): " + string.Join("; ", bad));
        }

        [Test]
        public void Environment_scenes_have_no_realtime_shadow_casters_that_move()
        {
            var bad = new List<string>();
            WithScenes(Environment, (name, scene) =>
            {
                foreach (var r in In<Renderer>(scene))
                {
                    if (r.shadowCastingMode == ShadowCastingMode.Off) continue;
                    bool moves = r is SkinnedMeshRenderer || r.GetComponentInParent<Animator>() != null || r.GetComponentInParent<ProcSense.VR.Boot.SwingDoor>() != null
                                 || r.GetComponentInParent<Rigidbody>() != null;
                    if (moves) bad.Add($"{name}/{r.name}");
                }
            });
            Assert.IsEmpty(bad, "players, hands, staff, patients, doors and moving equipment must not cast shadows: " + string.Join("; ", bad.Take(20)));
        }

        [Test]
        public void Static_surfaces_are_lightmapped_after_the_bake()
        {
            var report = new List<string>();
            WithScenes(Environment, (name, scene) =>
            {
                // only surfaces that receive lightmaps count: emissive downlight discs (lightmap index 65534) and parts set to light probes are lit another way on purpose
                var gi = In<MeshRenderer>(scene).Where(r => r.enabled && GameObjectUtility.GetStaticEditorFlags(r.gameObject).HasFlag(StaticEditorFlags.ContributeGI)
                    && new SerializedObject(r).FindProperty("m_ReceiveGI").intValue == 1 && r.lightmapIndex != 65534).ToArray();
                if (gi.Length == 0) { report.Add($"{name}: no contribute-GI renderers"); return; }
                int lit = gi.Count(r => r.lightmapIndex >= 0 && r.lightmapIndex < 65534);
                if (lit < gi.Length * 0.95) report.Add($"{name}: {lit}/{gi.Length} lightmapped (need 95%)");
            });
            Assert.IsEmpty(report, string.Join("; ", report));
        }

        [Test]
        public void Environment_scenes_have_baked_light_probes_for_moving_characters()
        {
            var report = new List<string>();
            WithScenes(Environment, (name, scene) =>
            {
                var probes = LightmapSettings.lightProbes;
                if (probes == null || probes.count < 4) report.Add($"{name}: {(probes == null ? 0 : probes.count)} baked probes");
            });
            Assert.IsEmpty(report, string.Join("; ", report));
        }

        [Test]
        public void Reflection_probes_are_baked_or_custom_never_realtime()
        {
            var bad = new List<string>();
            WithScenes(Environment, (name, scene) =>
            {
                foreach (var p in In<ReflectionProbe>(scene))
                    if (p.mode == ReflectionProbeMode.Realtime) bad.Add($"{name}/{p.name}");
            });
            Assert.IsEmpty(bad, string.Join("; ", bad));
        }

        [Test]
        public void No_scene_has_a_missing_script_or_a_missing_prefab()
        {
            var bad = new List<string>();
            WithScenes(AllScenes, (name, scene) =>
            {
                foreach (var root in scene.GetRootGameObjects())
                    foreach (var t in root.GetComponentsInChildren<Transform>(true))
                    {
                        int missing = GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject);
                        if (missing > 0) bad.Add($"{name}/{t.name}: {missing} missing script(s)");
                        if (PrefabUtility.IsPrefabAssetMissing(t.gameObject)) bad.Add($"{name}/{t.name}: missing prefab asset");
                    }
            });
            Assert.IsEmpty(bad, string.Join("; ", bad.Take(30)));
        }
    }
}
