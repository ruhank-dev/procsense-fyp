using System.Collections;
using System.Linq;
using NUnit.Framework;
using ProcSense.VR.Views;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace ProcSense.Tests.PlayMode
{
    /// <summary>
    /// 15 Nov 2026 shaft regression: with the Android culling on (ExteriorVisibility + RoomVisibility, as on Quest), the sun-shaft prisms of the bay and the corridor stay active, enabled, not
    /// force-hidden and inside the camera's visible set from a standing position, from inside a beam and after turning the head through a full circle. The camera is an ordinary mono camera:
    /// single-pass instanced / multiview stereo is covered by the shader contract (EditMode) and the headset.
    /// </summary>
    public class SunShaftVisibilityTests
    {
        [SetUp] public void BypassLogin() => ProcSense.VR.Login.DemoLoginSettings.LoginRequiredOverride = false;
        [TearDown] public void Restore() { ProcSense.VR.Login.DemoLoginSettings.LoginRequiredOverride = null; ExteriorVisibility.ResetToDefault(); RoomVisibility.ResetToDefault(); DesktopOnly.ShowOnAndroid = true; }
        [UnityTearDown] public IEnumerator Cleanup() => EditorXRTestEnvironment.Cleanup();

        [UnityTest]
        public IEnumerator Shafts_stay_drawn_with_android_culling_while_the_player_moves_and_turns()
        {
            LogAssert.ignoreFailingMessages = true;
            yield return SceneManager.LoadSceneAsync("Boot");
            yield return EditorXRTestEnvironment.EnsureSimulator();
            for (int i = 0; i < 120; i++) yield return null;
            foreach (var c in Object.FindObjectsByType<Camera>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)) c.enabled = false;
            var go = new GameObject("ShaftTestCamera"); var cam = go.AddComponent<Camera>(); cam.fieldOfView = 96f; cam.nearClipPlane = 0.05f; go.AddComponent<UniversalAdditionalCameraData>();
            cam.targetTexture = new RenderTexture(256, 256, 24);
            ExteriorVisibility.Enabled = true; RoomVisibility.Enabled = true; DesktopOnly.ShowOnAndroid = true;

            var prisms = Object.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .Where(r => r.name.StartsWith("Shafts") && r.sharedMaterial != null && r.sharedMaterial.shader.name == "ProcSense/Sun Shaft Baked").ToArray();
            Assert.That(prisms.Select(r => r.gameObject.scene.name).Distinct(), Is.SupersetOf(new[] { "PatientBay", "Corridor" }), "bay and corridor shafts are loaded");
            foreach (var r in prisms)
            {
                var d = ((Vector3)r.sharedMaterial.GetVector("_ShaftDir")).normalized; var c = r.bounds.center;
                var stand = c + d * 0.6f; stand.y = 1.6f;                                                  // inside the beam, eye height
                for (int yaw = 0; yaw < 360; yaw += 45)
                {
                    cam.transform.SetPositionAndRotation(stand, Quaternion.Euler(10f, yaw, 0f));
                    cam.Render(); yield return null;
                    Assert.IsTrue(r.gameObject.activeInHierarchy && r.enabled, $"{r.gameObject.scene.name}/{r.name} active at yaw {yaw}");
                    Assert.IsFalse(r.forceRenderingOff, $"{r.gameObject.scene.name}/{r.name} not culled by the room/exterior systems at yaw {yaw}");
                }
                cam.transform.position = stand; cam.transform.LookAt(c - d * 1.5f);                     // looking back at the window through the beam
                cam.Render(); yield return null;
                Assert.IsTrue(r.isVisible, $"{r.gameObject.scene.name}/{r.name} is in the visible set when facing the window");
            }
            Object.Destroy(cam.targetTexture); Object.Destroy(go);
        }
    }
}
