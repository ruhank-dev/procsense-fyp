using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using ProcSense.VR.Boot;
using ProcSense.VR.Bridge;
using ProcSense.VR.Views;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace ProcSense.Tests.PlayMode
{
    /// <summary>
    /// Smoke tests of the Quest 2 GPU overhaul (owner decisions of 14 Nov 2026) in a running simulation: the whole game still loads clean, the doors still work, nothing renders a realtime shadow, the
    /// exterior default is the intended one and Agent A's runtime profile (QuestPerformanceProfile) is aggressive. The static scene and asset checks are in EditMode (QuestOverhaulSceneTests);
    /// skinning at the Quest's two-bone quality is SkinWeightTests; doors and rooms in depth are ModeledDoorTests and EnvironmentIntegrationTests.
    /// QuestExterior.DefaultVariant is asserted against the rule in force today (impostor on Android, original elsewhere): if the exterior rebuild changes that default, update the expectation
    /// here in the same change, deliberately.
    /// </summary>
    public sealed class QuestOverhaulSmokeTests
    {
        [SetUp] public void Setup() => ProcSense.VR.Login.DemoLoginSettings.LoginRequiredOverride = false;

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            ProcSense.VR.Login.DemoLoginSettings.LoginRequiredOverride = null;
            yield return EditorXRTestEnvironment.Cleanup();
        }

        [UnityTest, Timeout(300000)]
        public IEnumerator Boot_loads_clean_has_no_realtime_light_or_shadow_and_a_door_still_opens()
        {
            var errors = new List<string>();
            void Capture(string condition, string stack, LogType type)
            { if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) errors.Add(condition); }
            Application.logMessageReceived += Capture;
            try
            {
                yield return SceneManager.LoadSceneAsync("Boot"); yield return EditorXRTestEnvironment.EnsureSimulator();
                var host = SimulationHost.Instance; host.AutoTick = false; host.SaveLogOnDestroy = false;
                for (int i = 0; i < 10; i++) yield return null;

                var realtime = UnityEngine.Object.FindObjectsByType<Light>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)
                    .Where(l => l.enabled && l.lightmapBakeType != LightmapBakeType.Baked).Select(l => l.name + " (" + l.type + ", shadows " + l.shadows + ")").ToArray();
                Assert.IsEmpty(realtime, "no realtime or mixed light may be active: " + string.Join("; ", realtime));

                var shadowed = UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)
                    .Where(r => r.shadowCastingMode != ShadowCastingMode.Off && (r is SkinnedMeshRenderer || r.GetComponentInParent<SwingDoor>() != null)).Select(r => r.name).Distinct().Take(20).ToArray();
                Assert.IsEmpty(shadowed, "people and doors must not cast shadows: " + string.Join("; ", shadowed));

                var door = UnityEngine.Object.FindObjectsByType<SwingDoor>(FindObjectsSortMode.None).FirstOrDefault(d => d.Ready);
                Assert.NotNull(door, "at least one door is ready");
                Assert.IsTrue(door.Toggle()); yield return new WaitForSeconds(1);
                Assert.Greater(Mathf.Abs(door.Angle), 90, "the door opens");
                Assert.IsTrue(door.Toggle()); yield return new WaitForSeconds(1);
                Assert.Less(Mathf.Abs(door.Angle), 1, "the door closes");

                Assert.IsEmpty(errors, "errors logged while loading and playing: " + string.Join(" | ", errors.Take(10)));
            }
            finally { Application.logMessageReceived -= Capture; }
        }

        [Test]
        public void The_exterior_default_is_the_intended_one()
        {
            var expected = Application.platform == RuntimePlatform.Android ? QuestExterior.Variant.Impostor : QuestExterior.Variant.Original;
            Assert.AreEqual(expected, QuestExterior.DefaultVariant);
        }

        /// <summary>
        /// Agent A's runtime profile. Looked up by name so this file compiles before the profile exists; it then fails clearly until it does. Recognised static members (any one family is enough
        /// for each setting): RenderScale / EyeScale (float, 0.6 to 0.85), Msaa / MsaaSamples (int, 1), Foveation / FoveationLevel (a level above 0).
        /// </summary>
        [Test]
        public void The_quest_performance_profile_is_aggressive()
        {
            var type = AppDomain.CurrentDomain.GetAssemblies().SelectMany(a => { try { return a.GetTypes(); } catch { return Type.EmptyTypes; } }).FirstOrDefault(t => t.Name == "QuestPerformanceProfile");
            Assert.NotNull(type, "QuestPerformanceProfile does not exist yet: Agent A has not delivered the Quest performance profile");
            const BindingFlags F = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;
            object Read(params string[] names)
            {
                foreach (var n in names)
                {
                    var p = type.GetProperty(n, F); if (p != null) return p.GetValue(null);
                    var f = type.GetField(n, F); if (f != null) return f.GetValue(null);
                }
                return null;
            }
            string members = string.Join(", ", type.GetMembers(F).Select(m => m.Name).Distinct());
            var scale = Read("RenderScale", "EyeScale", "EyeTextureResolutionScale");
            var msaa = Read("Msaa", "MsaaSamples", "MsaaSampleCount");
            var fov = Read("Foveation", "FoveationLevel", "FoveatedRenderingLevel");
            Assert.NotNull(scale, "no render scale member; members: " + members);
            Assert.NotNull(msaa, "no MSAA member; members: " + members);
            Assert.NotNull(fov, "no foveation member; members: " + members);
            float s = Convert.ToSingle(scale);
            Assert.That(s, Is.InRange(0.85f, 1f), "render scale (sharpness revision 15 Nov 2026: start 0.9)");
            Assert.AreEqual(1, Convert.ToInt32(msaa), "MSAA off");
            Assert.That(Convert.ToSingle(fov), Is.InRange(0.375f, 0.75f), "fixed foveated rendering is on at Medium");
        }
    }
}
