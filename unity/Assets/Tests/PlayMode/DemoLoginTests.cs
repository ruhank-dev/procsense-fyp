#if UNITY_EDITOR
using System.Collections;
using NUnit.Framework;
using ProcSense.VR.Bridge;
using ProcSense.VR.Login;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace ProcSense.Tests.PlayMode
{
    public class DemoLoginTests
    {
        [TearDown] public void RestoreGate() => DemoLoginSettings.LoginRequiredOverride = null;

        [UnityTest]
        public IEnumerator Demo_login_gates_the_session_rejects_invalid_input_and_releases_one_rig()
        {
            DemoLoginSettings.LoginRequiredOverride = true;
            yield return SceneManager.LoadSceneAsync("Boot");
            for (int i = 0; i < 20; i++) yield return null;
            var view = Object.FindFirstObjectByType<DemoLoginView>();
            Assert.IsNotNull(view);
            Assert.IsNull(SimulationHost.Instance, "Clinical time must not start behind login");
            Assert.AreEqual(1, Camera.allCamerasCount);
            Assert.AreEqual(1, Object.FindObjectsByType<EventSystem>().Length);
            view.Submit();
            Assert.IsFalse(view.Completed); StringAssert.Contains("email", view.Status);
            view.Email.text = DemoLoginSettings.DemoEmail;
            view.Submit();
            StringAssert.Contains("password", view.Status);
            view.Password.text = "wrong-password";
            view.Submit();
            Assert.IsFalse(view.Completed);
            Assert.IsNull(SimulationHost.Instance);
            Assert.AreEqual(TMPro.TMP_InputField.InputType.Password, view.Password.inputType);
            view.TogglePassword();
            Assert.AreEqual(TMPro.TMP_InputField.InputType.Standard, view.Password.inputType);
            view.TogglePassword();
            Assert.AreEqual(TMPro.TMP_InputField.InputType.Password, view.Password.inputType);
            view.Password.text = DemoLoginSettings.DemoPassword;
            var button = view.transform.Find("Sign-in card/Sign in").gameObject;
            ExecuteEvents.Execute(button, new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left }, ExecuteEvents.pointerClickHandler);
            Assert.IsTrue(view.Completed);
            Assert.AreEqual("", view.Password.text, "Do not retain the entered password after success");
            view.Submit(); // Repeated click must not start a second workspace.
            for (int i = 0; i < 60; i++) yield return null;
            Assert.IsNotNull(SimulationHost.Instance);
            SimulationHost.Instance.SaveLogOnDestroy = false;
            Assert.IsNull(Object.FindFirstObjectByType<DemoLoginView>());
            Assert.AreEqual(1, Camera.allCamerasCount);
            Assert.AreEqual(1, Object.FindObjectsByType<EventSystem>().Length);
            Assert.IsTrue(SceneManager.GetSceneByName("PatientBay").isLoaded);
            Assert.IsNotNull(GameObject.Find("MPFB patient"), "Sign-in must open the permanent rigged patient");
            Assert.IsNull(GameObject.Find("Patient model trial"));
        }

        [UnityTest]
        public IEnumerator Development_bypass_loads_the_workspace_without_login()
        {
            DemoLoginSettings.LoginRequiredOverride = false;
            yield return SceneManager.LoadSceneAsync("Boot");
            for (int i = 0; i < 60; i++) yield return null;
            Assert.IsNotNull(SimulationHost.Instance);
            SimulationHost.Instance.SaveLogOnDestroy = false;
            Assert.IsNull(Object.FindFirstObjectByType<DemoLoginView>());
            Assert.AreEqual(1, Camera.allCamerasCount);
            Assert.AreEqual(1, Object.FindObjectsByType<EventSystem>().Length);
        }
    }
}
#endif
