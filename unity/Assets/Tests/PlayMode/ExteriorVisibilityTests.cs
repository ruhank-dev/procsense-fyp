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
    }
}
