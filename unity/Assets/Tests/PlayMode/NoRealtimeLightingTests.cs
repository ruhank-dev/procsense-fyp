using System.Collections;
using System.Linq;
using NUnit.Framework;
using ProcSense.VR.Views;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace ProcSense.Tests.PlayMode
{
    /// <summary>Quest overhaul (Agent B): the three rooms need zero realtime shadow maps and zero realtime lights; moving objects cast no shadows; the guard keeps it so.</summary>
    public class NoRealtimeLightingTests
    {
        static readonly string[] Rooms = { "PatientBay", "Corridor", "OT" };

        IEnumerator Load()
        {
            yield return TestHostHygiene.ResetToEmptyScene();                   // only the three authored rooms: no Boot/RoomLoader or earlier room copy whose runtime state (a re-enabled clinical key) would be counted
            foreach (var r in Rooms) yield return SceneManager.LoadSceneAsync(r, LoadSceneMode.Additive);
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator Unload()
        {
            QuestLightingGuard.ResetToDefault();
            var empty = SceneManager.GetSceneByName("lighting cleanup"); if (!empty.IsValid()) empty = SceneManager.CreateScene("lighting cleanup"); SceneManager.SetActiveScene(empty);
            foreach (var r in Rooms) { var s = SceneManager.GetSceneByName(r); if (s.isLoaded) yield return SceneManager.UnloadSceneAsync(s); }
        }

        [UnityTest, Timeout(300000)]
        public IEnumerator Every_enabled_light_is_baked_and_nothing_casts_a_realtime_shadow()
        {
            yield return Load();
            var lights = Object.FindObjectsByType<Light>(FindObjectsInactive.Exclude, FindObjectsSortMode.None).Where(l => l.enabled).ToArray();
            Assert.IsNotEmpty(lights);
            var notBaked = lights.Where(l => l.bakingOutput.lightmapBakeType != LightmapBakeType.Baked).Select(l => l.name + " (" + l.gameObject.scene.name + ")").ToArray();
            Assert.IsEmpty(notBaked, "every enabled light must be baked: " + string.Join(", ", notBaked));
            var casters = Object.FindObjectsByType<Renderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)
                .Where(r => !r.gameObject.isStatic && r.shadowCastingMode != ShadowCastingMode.Off).Select(r => r.name).Take(10).ToArray();
            Assert.IsEmpty(casters, "moving objects must not cast shadows: " + string.Join(", ", casters));
        }

        [UnityTest, Timeout(300000)]
        public IEnumerator The_guard_removes_shadows_from_anything_added_later()
        {
            yield return Load();
            var go = new GameObject("late light"); var l = go.AddComponent<Light>(); l.type = LightType.Point; l.shadows = LightShadows.Soft;
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube); cube.name = "late mover";
            Assert.Greater(QuestLightingGuard.Sweep(default), 0);
            Assert.AreEqual(LightShadows.None, l.shadows);
            Assert.AreEqual(ShadowCastingMode.Off, cube.GetComponent<Renderer>().shadowCastingMode);
            Object.Destroy(go); Object.Destroy(cube);
        }
    }
}
