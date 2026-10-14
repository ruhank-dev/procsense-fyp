#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using ProcSense.VR.Bridge;
using ProcSense.VR.Views;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace ProcSense.Tests.PlayMode
{
    public class WristbandRayTests
    {
        [SetUp] public void BypassLoginForInteractionChecks() => ProcSense.VR.Login.DemoLoginSettings.LoginRequiredOverride = false;
        [TearDown] public void RestoreLogin() => ProcSense.VR.Login.DemoLoginSettings.LoginRequiredOverride = null;
        [UnityTearDown] public IEnumerator CleanupSimulatedInput() => EditorXRTestEnvironment.Cleanup();
        [UnityTest] public IEnumerator App_band_reads_from_controller_without_feedback_errors() => Read(false);
        [UnityTest] public IEnumerator Direct_bay_band_reads_from_controller_without_feedback_errors() => Read(true);

        private IEnumerator Read(bool review)
        {
            var errors = new List<string>();
            void Logged(string message, string trace, LogType type)
            {
                if (type == LogType.Error || type == LogType.Exception || message.Contains("already registered with another") || message.Contains("haptic capabilities")) errors.Add(message);
            }
            Application.logMessageReceived += Logged;
            try
            {
                if (review) yield return EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/Scenes/PatientBay.unity", new LoadSceneParameters(LoadSceneMode.Single));
                else yield return SceneManager.LoadSceneAsync("Boot");
                for (int i = 0; i < 60; i++) yield return null;
                yield return EditorXRTestEnvironment.EnsureSimulator();
                var host = SimulationHost.Instance;
                Assert.IsNotNull(host);
                host.AutoTick = false;
                host.SaveLogOnDestroy = false;
                Assert.IsNull(Object.FindFirstObjectByType<IdentityCardView>());
                var band = GameObject.Find("Patient wristband").GetComponent<XRSimpleInteractable>();
                XRBaseInteractor ray = null;
                float controllerDeadline = Time.realtimeSinceStartup + 3;
                while (ray == null && Time.realtimeSinceStartup < controllerDeadline)
                {
                    ray = Object.FindObjectsByType<XRBaseInteractor>().FirstOrDefault(r => r.isActiveAndEnabled && (r is NearFarInteractor || r is XRRayInteractor));
                    if (ray == null) yield return null;
                }
                Assert.IsNotNull(ray, "The simulated controller did not become ready within 3 seconds. Devices: " +
                    string.Join("; ", UnityEngine.InputSystem.InputSystem.devices.Select(d => d.name + ":" + d.layout + ":enabled=" + d.enabled)) + ". Interactors: " +
                    string.Join("; ", Object.FindObjectsByType<XRBaseInteractor>(FindObjectsInactive.Include, FindObjectsSortMode.None).Select(r => r.name + ":active=" + r.gameObject.activeInHierarchy + ":enabled=" + r.enabled)));
                var manager = band.interactionManager;
                var collider = band.GetComponent<Collider>();
                Assert.IsTrue(manager.TryGetInteractableForCollider(collider, out var mapped));
                Assert.AreSame(band, mapped);
                var button = GameObject.Find("Start BP cycle").GetComponent<XRSimpleInteractable>();
                Assert.IsTrue(manager.TryGetInteractableForCollider(button.GetComponent<Collider>(), out mapped));
                Assert.AreSame(button, mapped, "Machine grab must not steal the BP control collider");
                int frame = Time.frameCount;
                manager.HoverEnter((IXRHoverInteractor)ray, (IXRHoverInteractable)band);
                yield return null;
                manager.SelectEnter((IXRSelectInteractor)ray, (IXRSelectInteractable)band);
                for (int i = 0; i < 5; i++) yield return null;
                Assert.IsNotNull(host.Observable.Wristband);
                Assert.AreEqual(1, host.Session.Log.Count);
                Assert.That(Time.frameCount, Is.GreaterThan(frame));
                Assert.AreEqual(1, Camera.allCamerasCount);
                manager.SelectExit((IXRSelectInteractor)ray, (IXRSelectInteractable)band);
                Assert.IsEmpty(errors, string.Join("\n", errors));
            }
            finally { Application.logMessageReceived -= Logged; }
        }
    }
}
#endif
