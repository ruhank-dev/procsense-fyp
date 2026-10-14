using System.Collections;
using System.Linq;
using NUnit.Framework;
using ProcSense.VR.Boot;
using ProcSense.VR.Bridge;
using ProcSense.VR.Login;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace ProcSense.Tests.PlayMode
{
    public class BakedRoomLightingTests
    {
        [TearDown] public void RestoreLogin() => DemoLoginSettings.LoginRequiredOverride = null;

        [UnityTearDown]
        public IEnumerator UnloadFixture()
        {
            if (SimulationHost.Instance != null) SimulationHost.Instance.SaveLogOnDestroy = false;
            var empty = SceneManager.CreateScene("Lighting fixture cleanup");
            SceneManager.SetActiveScene(empty);
            foreach (string name in new[] { "PatientBay", "Corridor", "OT", "Sys_Core", "DevOnly", "Boot" })
            {
                var scene = SceneManager.GetSceneByName(name);
                if (scene.isLoaded) yield return SceneManager.UnloadSceneAsync(scene);
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator Baked_surfaces_and_dynamic_probes_survive_additive_room_round_trip()
        {
            DemoLoginSettings.LoginRequiredOverride = false;
            yield return SceneManager.LoadSceneAsync("Boot");
            var deadline = Time.realtimeSinceStartup + 10;
            RoomLoader loader = null;
            while (Time.realtimeSinceStartup < deadline)
            {
                loader = Object.FindAnyObjectByType<RoomLoader>();
                if (loader != null && !loader.Busy && SimulationHost.Instance != null) break;
                yield return null;
            }
            Assert.IsNotNull(loader);
            Assert.IsNotNull(SimulationHost.Instance);
            SimulationHost.Instance.SaveLogOnDestroy = false;
            var session = SimulationHost.Instance.Session;
            Color? initialBayLight = null;
            foreach (string name in new[] { "PatientBay", "Corridor", "OT", "Corridor", "PatientBay" })
            {
                if (loader.CurrentRoom != name)
                {
                    Assert.IsTrue(loader.RequestRoom(name), loader.LastError);
                    deadline = Time.realtimeSinceStartup + 10;
                    while (loader.Busy && Time.realtimeSinceStartup < deadline) yield return null;
                    Assert.IsFalse(loader.Busy, "Lighting data must not stall room loading");
                }
                for (int frame = 0; frame < 3; frame++) yield return null;
                int expectedProbes = name == "PatientBay" ? 60 : 108;
                var scene = SceneManager.GetSceneByName(name);
                deadline = Time.realtimeSinceStartup + 2;
                while (LightProbes.GetSharedLightProbesForScene(scene) == null && Time.realtimeSinceStartup < deadline)
                    yield return null;
                Assert.AreEqual(name, loader.CurrentRoom, loader.LastError);
                Assert.AreEqual(name, SceneManager.GetActiveScene().name, "The visible room must own environment lighting");
                Assert.AreSame(session, SimulationHost.Instance.Session);
                Assert.That(LightmapSettings.lightmaps.Length, Is.GreaterThan(0), name);
                Assert.IsNotNull(LightmapSettings.lightProbes, name + " must load its baked probe data");
                var roomProbes = LightProbes.GetSharedLightProbesForScene(scene);
                Assert.IsNotNull(roomProbes, name);
                Assert.That(roomProbes.countSelf, Is.EqualTo(expectedProbes), name + ": room must load its own baked probe set");
                Assert.AreEqual(1, new[] { "PatientBay", "Corridor", "OT" }.Count(n => SceneManager.GetSceneByName(n).isLoaded), "Source room must be unloaded");
                var floor = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<MeshRenderer>()).Single(r => r.name == "Floor");
                Assert.That(floor.lightmapIndex, Is.InRange(0, LightmapSettings.lightmaps.Length - 1));
                Assert.IsNotNull(LightmapSettings.lightmaps[floor.lightmapIndex].lightmapColor);
                LightProbes.GetInterpolatedProbe(new Vector3(0, 1.3f, 0), null, out var sample);
                var colors = new Color[1]; sample.Evaluate(new[] { Vector3.up }, colors);
                Assert.IsFalse(float.IsNaN(colors[0].r) || float.IsInfinity(colors[0].r));
                Assert.That(colors[0].maxColorComponent, Is.GreaterThan(.01f), "Movable props must receive baked room illumination");
                if (name == "PatientBay")
                {
                    if (initialBayLight.HasValue)
                        Assert.That(Vector3.Distance(new Vector3(colors[0].r, colors[0].g, colors[0].b), new Vector3(initialBayLight.Value.r, initialBayLight.Value.g, initialBayLight.Value.b)), Is.LessThan(.03f), "Returning to the bay must restore the same lighting");
                    else initialBayLight = colors[0];
                }
            }
        }
    }
}
