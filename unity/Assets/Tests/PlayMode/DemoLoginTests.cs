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
            Assert.IsFalse(view.Completed); StringAssert.Contains("email", view.EmailError);
            StringAssert.Contains("password", view.PasswordError);
            view.Email.text = DemoLoginSettings.DemoEmail;
            view.Submit();
            StringAssert.Contains("password", view.PasswordError);
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
        public IEnumerator Field_errors_clear_on_correction_and_password_spaces_are_not_trimmed()
        {
            DemoLoginSettings.LoginRequiredOverride = true;
            yield return SceneManager.LoadSceneAsync("Boot");
            for (int i = 0; i < 20; i++) yield return null;
            var view = Object.FindFirstObjectByType<DemoLoginView>();
            view.Email.text = "bad-address";
            view.Password.text = "   ";
            view.Submit();
            StringAssert.Contains("valid email", view.EmailError);
            StringAssert.Contains("password", view.PasswordError);
            Assert.IsFalse(view.Completed);
            Assert.IsNull(SimulationHost.Instance);
            view.Email.text = "  DEMO@PROCSENSE.APP  ";
            Assert.AreEqual("", view.EmailError);
            view.Password.text = " " + DemoLoginSettings.DemoPassword + " ";
            view.Submit();
            Assert.AreEqual("", view.PasswordError);
            StringAssert.Contains("Incorrect", view.Status);
            Assert.IsFalse(view.Completed, "Do not silently change a entered password");
            view.Password.text = DemoLoginSettings.DemoPassword;
            Assert.AreEqual("", view.Status);
            view.Submit();
            Assert.IsTrue(view.Completed);
            for (int i = 0; i < 60; i++) yield return null;
            Assert.IsNotNull(SimulationHost.Instance);
            SimulationHost.Instance.SaveLogOnDestroy = false;
        }

        [UnityTest]
        public IEnumerator Incomplete_form_configuration_stays_locked_without_null_exceptions()
        {
            DemoLoginSettings.LoginRequiredOverride = true;
            yield return SceneManager.LoadSceneAsync("Boot");
            for (int i = 0; i < 20; i++) yield return null;
            var container = new GameObject("Incomplete login fixture");
            container.SetActive(false);
            var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/UI/LoginScreen.prefab");
            var clone = Object.Instantiate(prefab, container.transform).GetComponent<DemoLoginView>();
            clone.ConfigureValidation(null, null);
            LogAssert.Expect(LogType.Error, "Login UI references are incomplete; workspace remains locked.");
            container.SetActive(true);
            yield return null;
            Assert.IsFalse(clone.IsReady);
            clone.Submit();
            clone.TogglePassword();
            Assert.IsFalse(clone.Completed);
            StringAssert.Contains("unavailable", clone.Status);
            Assert.IsNull(SimulationHost.Instance);
            Object.Destroy(container);
            yield return null;
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
