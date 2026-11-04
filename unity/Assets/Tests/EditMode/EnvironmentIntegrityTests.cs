using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace ProcSense.Tests.EditMode
{
    /// <summary>
    /// Environment handoff gate (3 Nov 2026): every scene of the build opens, and nothing the scene needs is missing: scripts, prefabs, meshes, materials, shaders (no pink), textures, the baked lightmaps and probes,
    /// the characters' rigs. A clean clone that passes this and then <c>tools/setup/restore_environment.ps1</c> has the same assets as the working environment.
    /// </summary>
    public sealed class EnvironmentIntegrityTests
    {
        private static readonly string[] RoomScenes = { "PatientBay", "Corridor", "OT" };

        public static IEnumerable<string> BuildScenes() => EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path);

        private static IEnumerable<GameObject> All(Scene s) => s.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true)).Select(t => t.gameObject);

        [TestCaseSource(nameof(BuildScenes))]
        public void Scene_opens_and_has_no_missing_scripts_prefabs_meshes_or_materials(string path)
        {
            var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            var problems = new List<string>();
            foreach (var go in All(scene))
            {
                if (GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(go) > 0) problems.Add("missing script on " + Name(go));
                if (PrefabUtility.IsPrefabAssetMissing(go)) problems.Add("missing prefab asset for " + Name(go));
                foreach (var mf in go.GetComponents<MeshFilter>()) if (mf.sharedMesh == null && go.GetComponent<MeshRenderer>() != null) problems.Add("MeshFilter without mesh on " + Name(go));
                foreach (var smr in go.GetComponents<SkinnedMeshRenderer>()) if (smr.sharedMesh == null) problems.Add("SkinnedMeshRenderer without mesh on " + Name(go));
                foreach (var r in go.GetComponents<Renderer>())
                {
                    if (r is ParticleSystemRenderer || r is TrailRenderer || r is LineRenderer) continue;
                    var mats = r.sharedMaterials;
                    if (mats.Length == 0) { problems.Add("renderer without materials on " + Name(go)); continue; }
                    foreach (var m in mats)
                    {
                        if (m == null) { problems.Add("null material slot on " + Name(go)); continue; }
                        if (m.shader == null || m.shader.name == "Hidden/InternalErrorShader" || !m.shader.isSupported) problems.Add("pink/unsupported shader '" + (m.shader ? m.shader.name : "null") + "' in material " + m.name + " on " + Name(go));
                    }
                }
            }
            Assert.IsEmpty(problems, path + ": " + string.Join("\n", problems.Take(25)));
        }

        [TestCaseSource(nameof(RoomScenes))]
        public void Room_has_baked_lighting_probes_and_a_volume(string room)
        {
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/" + room + ".unity", OpenSceneMode.Single);
            var lm = Lightmapping.lightingDataAsset;
            Assert.NotNull(lm, room + ": no LightingData asset (baked lighting lost: run git lfs pull)");
            Assert.Greater(LightmapSettings.lightmaps.Length, 0, room + ": no baked lightmaps");
            foreach (var l in LightmapSettings.lightmaps) Assert.NotNull(l.lightmapColor, room + ": a lightmap texture is missing (LFS pointer?)");
            Assert.Greater(LightmapSettings.lightProbes != null ? LightmapSettings.lightProbes.count : 0, 0, room + ": no baked light probes");
            Assert.Greater(Object.FindObjectsByType<ReflectionProbe>(FindObjectsInactive.Include).Length, 0, room + ": no reflection probes");
            Assert.IsNotEmpty(System.IO.Directory.GetFiles("Assets/Scenes/PatientBay", "ReflectionProbe-*.exr"), "baked reflection probe data is missing (Git LFS pointer files are smaller than 200 bytes)");
            foreach (var f in System.IO.Directory.GetFiles("Assets/Scenes/PatientBay", "ReflectionProbe-*.exr")) Assert.Greater(new System.IO.FileInfo(f).Length, 1000, f + " looks like a Git LFS pointer, run git lfs pull");
            // exposure and contrast live in the project's default Volume profile; PatientBay adds a local trim Volume on top of it
            Assert.IsInstanceOf<UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset>(GraphicsSettings.defaultRenderPipeline, room + ": the default render pipeline is not the URP asset");
            if (room == "PatientBay") Assert.Greater(Object.FindObjectsByType<Volume>(FindObjectsInactive.Include).Length, 0, room + ": no local exposure trim Volume");
        }

        [Test]
        public void Rocketbox_characters_have_rigs_materials_and_animation_controllers()
        {
            var dir = "Assets/Prefabs/Characters";
            var prefabs = AssetDatabase.FindAssets("RB_ t:Prefab", new[] { dir });
            Assert.GreaterOrEqual(prefabs.Length, 17, "expected the 17 Rocketbox prefabs");
            foreach (var g in prefabs)
            {
                var path = AssetDatabase.GUIDToAssetPath(g); var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                var a = go.GetComponent<Animator>();
                Assert.NotNull(a, path + ": no Animator"); Assert.NotNull(a.avatar, path + ": no Avatar"); Assert.IsTrue(a.avatar.isHuman, path + ": avatar is not humanoid");
                var smr = go.GetComponentsInChildren<SkinnedMeshRenderer>(true); Assert.IsNotEmpty(smr, path + ": no skinned mesh");
                foreach (var r in smr) foreach (var m in r.sharedMaterials) { Assert.NotNull(m, path + ": null material"); Assert.AreNotEqual("Hidden/InternalErrorShader", m.shader.name, path); }
            }
            foreach (var clip in new[] { "Idle_Loop", "Sitting_Idle_Loop", "Walk_Loop", "Idle_Talking_Loop", "Sitting_Talking_Loop", "Walk_Formal_Loop" })
                Assert.NotNull(AssetDatabase.LoadAssetAtPath<UnityEditor.Animations.AnimatorController>("Assets/Animations/Characters/RB_" + clip + ".controller"), "controller RB_" + clip);
        }

        [Test]
        public void Every_guid_referenced_by_a_material_resolves_to_an_asset()
        {
            var bad = new List<string>(); var rx = new System.Text.RegularExpressions.Regex(@"guid: ([0-9a-f]{32})");
            foreach (var g in AssetDatabase.FindAssets("t:Material", new[] { "Assets/Materials", "Assets/Characters", "Assets/Models" }))
            {
                var path = AssetDatabase.GUIDToAssetPath(g); if (!path.EndsWith(".mat")) continue;
                foreach (System.Text.RegularExpressions.Match m in rx.Matches(System.IO.File.ReadAllText(path)))
                {
                    var guid = m.Groups[1].Value; if (guid.Trim('0').Length == 0) continue;                        // built-in resources have all-zero or fixed guids
                    if (string.IsNullOrEmpty(AssetDatabase.GUIDToAssetPath(guid)) && !guid.StartsWith("0000000000000000")) bad.Add(path + " -> unknown guid " + guid);
                }
            }
            Assert.IsEmpty(bad, string.Join(System.Environment.NewLine, bad.Take(25)));
        }

        [Test]
        public void Sun_shaft_and_dust_shaders_compile_and_are_supported()
        {
            foreach (var n in new[] { "ProcSense/Sun Shaft", "ProcSense/Sun Dust Mote" })
            {
                var s = Shader.Find(n); Assert.NotNull(s, n + " not found"); Assert.IsTrue(s.isSupported, n + " unsupported"); Assert.IsFalse(ShaderUtil.ShaderHasError(s), n + " has compile errors");
            }
        }

        private static string Name(GameObject go) { var n = go.name; for (var t = go.transform.parent; t != null; t = t.parent) n = t.name + "/" + n; return n; }
    }
}
