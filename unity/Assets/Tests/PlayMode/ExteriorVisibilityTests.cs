using System.Collections;
using NUnit.Framework;
using ProcSense.VR.Views;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace ProcSense.Tests.PlayMode
{
    /// <summary>The exterior city may only be switched off where no window or door can show it (P3-PRF-21): it must be hidden deep in the windowless theatre and in the bay when looking at the wall, and drawn wherever a window or door is in view.</summary>
    public class ExteriorVisibilityTests
    {
        [SetUp] public void BypassLogin() => ProcSense.VR.Login.DemoLoginSettings.LoginRequiredOverride = false;
        [TearDown] public void Restore() { ProcSense.VR.Login.DemoLoginSettings.LoginRequiredOverride = null; ExteriorVisibility.ResetToDefault(); }
        [UnityTearDown] public IEnumerator Cleanup() => EditorXRTestEnvironment.Cleanup();

        private static IEnumerator Boot()
        {
            LogAssert.ignoreFailingMessages = true;
            yield return SceneManager.LoadSceneAsync("Boot");
            yield return EditorXRTestEnvironment.EnsureSimulator();
            for (int i = 0; i < 120; i++) yield return null;
        }

        private static bool HiddenFrom(Camera cam, Vector3 pos, Vector3 look)
        {
            cam.transform.position = pos; cam.transform.LookAt(look);
            cam.Render();
            return ExteriorVisibility.CityHiddenNow;
        }

        [TearDown] public void RestoreVariant() { QuestExterior.ResetToDefault(); ExteriorVisibility.PortalCullEnabled = true; }

        /// <summary>P3-EXT-04/08: the original city is the default everywhere, the Quest variant sits beside it switched off, either can be shown, and every original renderer is still present.</summary>
        [UnityTest]
        public IEnumerator Quest_variant_is_isolated_and_reversible()
        {
            yield return Boot();
            var q = Object.FindAnyObjectByType<QuestExterior>(); Assert.IsNotNull(q, "QuestExterior on the City exterior root");
            Assert.AreEqual(QuestExterior.DefaultVariant, QuestExterior.Current, "the platform default: optimised on Android (approved 9 Nov 2026), the untouched original on desktop and in the Editor");
            Assert.Greater(q.OriginalCount, 0); Assert.Greater(q.OptimisedCount, 0);
            int OnCount(string layer) { int n = 0; foreach (var r in q.GetComponentsInChildren<Renderer>(true)) if (r.enabled && r.transform.parent != null && r.transform.parent.name == layer) n++; return n; }
            int chunksOn() { int n = 0; foreach (var r in q.transform.Find("Quest variant").GetComponentsInChildren<Renderer>(true)) if (r.enabled) n++; return n; }
            Assert.AreEqual(0, chunksOn(), "no Quest chunk draws while the original is selected");
            QuestExterior.Current = QuestExterior.Variant.Optimised;
            Assert.AreEqual(q.OptimisedCount, chunksOn(), "every Quest chunk is enabled when the optimised variant is selected");
            QuestExterior.Current = QuestExterior.Variant.Original;
            Assert.AreEqual(0, chunksOn()); foreach (var r in q.transform.Find("Quest variant").GetComponentsInChildren<MeshRenderer>(true)) Assert.IsNotNull(r.sharedMaterial, r.name + " has a material");
        }

        [UnityTest]
        public IEnumerator Portal_culling_draws_only_the_chunks_seen_through_a_window()
        {
            yield return Boot();
            foreach (var c in Object.FindObjectsByType<Camera>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)) c.enabled = false;
            var go = new GameObject("TestCamera"); var cam = go.AddComponent<Camera>(); cam.fieldOfView = 96f; go.AddComponent<UniversalAdditionalCameraData>();
            cam.targetTexture = new RenderTexture(256, 256, 24);
            ExteriorVisibility.Enabled = true; QuestExterior.Current = QuestExterior.Variant.Optimised;
            HiddenFrom(cam, new Vector3(1.8f, 1.5f, -0.6f), new Vector3(-2.45f, 1.6f, 0f));      // bay, facing the window
            Assert.Greater(ExteriorVisibility.ChunksDrawn, 0, "the window shows part of the city");
            Assert.Less(ExteriorVisibility.ChunksDrawn, ExteriorVisibility.ChunksTotal / 2, "most chunks lie outside the cone through the window; decision " + ExteriorVisibility.LastDecision + " " + ExteriorVisibility.Report);
            HiddenFrom(cam, new Vector3(4.5f, 1.7f, 13.6f), new Vector3(4.5f, 1.5f, -9f));      // corridor, glass walls in view
            Assert.Greater(ExteriorVisibility.ChunksDrawn, 0);
            Assert.IsTrue(HiddenFrom(cam, new Vector3(9.5f, 1.7f, 5.2f), new Vector3(13f, 1.3f, 5.4f)), "theatre: the whole city is off");
            ExteriorVisibility.PortalCullEnabled = false;
            HiddenFrom(cam, new Vector3(1.8f, 1.5f, -0.6f), new Vector3(-2.45f, 1.6f, 0f));
            int drawnWithout = 0; foreach (var r in Object.FindAnyObjectByType<QuestExterior>().transform.Find("Quest variant").GetComponentsInChildren<Renderer>()) if (r.enabled && !r.forceRenderingOff) drawnWithout++;
            Assert.AreEqual(Object.FindAnyObjectByType<QuestExterior>().OptimisedCount, drawnWithout, "portal culling off: every chunk is drawn");
            cam.targetTexture.Release(); Object.Destroy(go);
        }

        [UnityTest]
        public IEnumerator City_is_hidden_only_where_no_window_or_door_can_show_it()
        {
            yield return Boot();
            foreach (var c in Object.FindObjectsByType<Camera>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)) c.enabled = false;
            var go = new GameObject("TestCamera"); var cam = go.AddComponent<Camera>(); cam.fieldOfView = 96f; go.AddComponent<UniversalAdditionalCameraData>();
            cam.targetTexture = new RenderTexture(256, 256, 24);
            ExteriorVisibility.Enabled = true;
            Assert.IsTrue(HiddenFrom(cam, new Vector3(9.5f, 1.7f, 5.2f), new Vector3(13f, 1.3f, 5.4f)), "theatre, looking away from the door");
            Assert.IsTrue(HiddenFrom(cam, new Vector3(0f, 1.6f, -1.4f), new Vector3(0f, 1.6f, -1.95f)), "bay, facing the wall");
            Assert.IsFalse(HiddenFrom(cam, new Vector3(1.8f, 1.5f, -0.6f), new Vector3(-2.45f, 1.6f, 0f)), "bay, facing the window");
            Assert.IsFalse(HiddenFrom(cam, new Vector3(4.5f, 1.7f, 13.6f), new Vector3(4.5f, 1.5f, -9f)), "corridor, glass walls in view");
            Assert.IsFalse(HiddenFrom(cam, new Vector3(7f, 1.7f, 5.2f), new Vector3(11f, 1.2f, 5.4f)), "theatre doorway");
            ExteriorVisibility.Enabled = false;
            Assert.IsFalse(HiddenFrom(cam, new Vector3(9.5f, 1.7f, 5.2f), new Vector3(13f, 1.3f, 5.4f)), "disabled: always drawn");
            cam.targetTexture.Release(); Object.Destroy(go);
        }

        /// <summary>Round 3 CPU fast path (cached flags, managed plane tests, pose cache) must give exactly the cull states of the original per-frame path, on a pose the first time and again when the identical pose is rendered a second time (cache hit).</summary>
        [UnityTest, Timeout(1200000)]
        public IEnumerator Fast_path_gives_identical_cull_states_to_the_original_path()
        {
            yield return Boot();
            foreach (var c in Object.FindObjectsByType<Camera>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)) c.enabled = false;
            var go = new GameObject("TestCamera"); var cam = go.AddComponent<Camera>(); cam.fieldOfView = 96f; go.AddComponent<UniversalAdditionalCameraData>();
            cam.targetTexture = new RenderTexture(128, 128, 24);
            ExteriorVisibility.Enabled = true; RoomVisibility.Enabled = true; QuestExterior.Current = QuestExterior.Variant.Optimised;
            var all = Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            var anchors = new[] { new Vector3(1.2f, 1.6f, -0.2f), new Vector3(1.8f, 1.5f, -0.6f), new Vector3(3.6f, 1.6f, -0.8f), new Vector3(4.5f, 1.6f, 3f), new Vector3(4.5f, 1.6f, 13.3f), new Vector3(2.4f, 1.6f, 1f), new Vector3(7f, 1.7f, 5.2f), new Vector3(8.8f, 1.6f, 5.2f), new Vector3(9.5f, 1.7f, 5.2f) };
            var a = new bool[all.Length]; int mismatches = 0, poses = 0, hidden = 0; string first = null;
            foreach (var p in anchors)
                for (int yaw = 0; yaw < 360; yaw += 30)
                {
                    cam.transform.position = p; cam.transform.rotation = Quaternion.Euler(yaw % 60 == 0 ? 8f : -6f, yaw, 0);
                    ExteriorVisibility.FastPath = false; RoomVisibility.FastPath = false; cam.Render();
                    for (int i = 0; i < all.Length; i++) a[i] = all[i] != null && all[i].forceRenderingOff;
                    ExteriorVisibility.FastPath = true; RoomVisibility.FastPath = true;
                    for (int pass = 0; pass < 2; pass++)                                          // pass 0 computes, pass 1 is the cache hit
                    {
                        cam.Render(); int bad = 0;
                        for (int i = 0; i < all.Length; i++) if (all[i] != null && all[i].forceRenderingOff != a[i]) { bad++; if (first == null) first = all[i].name + " at " + p + " yaw " + yaw + " pass " + pass; }
                        mismatches += bad;
                    }
                    for (int i = 0; i < all.Length; i++) if (a[i]) hidden++;
                    poses++; if (poses % 10 == 0) yield return null;
                }
            Assert.AreEqual(0, mismatches, "fast path differs from the original path; first: " + first);
            Assert.Greater(hidden, poses * 10, "the comparison saw real culling (hidden renderers summed over poses " + hidden + ")");
            cam.targetTexture.Release(); Object.Destroy(go);
        }
    }
}
