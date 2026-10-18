#if UNITY_EDITOR
using System.Collections;
using System.Linq;
using NUnit.Framework;
using ProcSense.VR.Bridge;
using ProcSense.VR.Login;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.XR.Interaction.Toolkit.Locomotion;

namespace ProcSense.Tests.PlayMode
{
    public class LoginPresentationTests
    {
        [TearDown] public void RestoreGate() => DemoLoginSettings.LoginRequiredOverride = null;

        private static IEnumerator Open()
        {
            DemoLoginSettings.LoginRequiredOverride = true;
            yield return SceneManager.LoadSceneAsync("Boot");
            for (int i = 0; i < 20; i++) yield return null;
        }

        private static DemoLoginView View => Object.FindFirstObjectByType<DemoLoginView>();
        private static LoginPresentation HeadsetForm()
        {
            var presentation = View.GetComponent<LoginPresentation>();
            if (View.GetComponent<LoginKeyboard>() == null) presentation.Configure(Camera.main, View, true);
            return presentation;
        }

        [UnityTest]
        public IEnumerator Launch_rig_cannot_fall_or_locomote_while_head_and_hand_tracking_are_retained()
        {
            yield return Open();
            var rig = Camera.main.GetComponentInParent<Unity.XR.CoreUtils.XROrigin>();
            Assert.IsNotNull(rig);
            Assert.IsTrue(rig.GetComponentsInChildren<LocomotionProvider>(true).All(p => !p.enabled));
            Assert.IsTrue(rig.GetComponentsInChildren<XRBodyTransformer>(true).All(p => !p.enabled));
            Assert.IsTrue(rig.GetComponentsInChildren<CharacterController>(true).All(p => !p.enabled));
            var drivers = rig.GetComponentsInChildren<UnityEngine.InputSystem.XR.TrackedPoseDriver>(true);
            Assert.IsTrue(drivers.Length >= 1 && drivers.All(d => d.enabled));
            var start = rig.transform.position;
            yield return new WaitForSeconds(2);
            Assert.That(Vector3.Distance(start, rig.transform.position), Is.LessThan(.0001f));
            Assert.IsNull(SimulationHost.Instance);
        }

        [UnityTest]
        public IEnumerator Panel_uses_current_eye_height_yaw_and_stays_world_anchored_until_repositioned()
        {
            yield return Open();
            var presentation = HeadsetForm();
            var go = new GameObject("Tracked pose fixture");
            try
            {
                foreach (float height in new[] { 1.1f, 1.75f })
                {
                    go.transform.SetPositionAndRotation(new Vector3(3, height, -2), Quaternion.Euler(35, 123, 22));
                    presentation.PlaceInFront(go.transform);
                    Vector3 expectedForward = Vector3.ProjectOnPlane(go.transform.forward, Vector3.up).normalized;
                    Assert.That(Vector3.Distance(presentation.transform.position, go.transform.position + expectedForward * 1.6f + Vector3.down * .12f), Is.LessThan(.0001f));
                    Assert.That(Vector3.Angle(presentation.transform.up, Vector3.up), Is.LessThan(.01f));
                    var placed = presentation.transform.position;
                    go.transform.position += Vector3.up * .3f;
                    yield return null;
                    Assert.That(Vector3.Distance(placed, presentation.transform.position), Is.LessThan(.0001f));
                }
                Assert.AreEqual(RenderMode.WorldSpace, View.GetComponent<Canvas>().renderMode);
                Assert.IsNotNull(View.GetComponent<LoginKeyboard>().ActiveField);
            }
            finally { Object.Destroy(go); }
        }

        [UnityTest]
        public IEnumerator Controller_keyboard_retains_field_focus_edits_selection_and_honours_limits()
        {
            yield return Open(); HeadsetForm(); yield return null;
            var keyboard = View.GetComponent<LoginKeyboard>();
            var field = View.Email;
            Assert.AreSame(field, keyboard.ActiveField);
            field.text = "abc"; field.caretPosition = 1;
            var key = View.transform.Find("Controller keyboard/Key q").gameObject;
            var pointer = new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left };
            ExecuteEvents.Execute(key, pointer, ExecuteEvents.pointerDownHandler);
            ExecuteEvents.Execute(key, pointer, ExecuteEvents.pointerClickHandler);
            Assert.AreEqual("aqbc", field.text);
            Assert.AreSame(field.gameObject, EventSystem.current.currentSelectedGameObject);
            field.selectionAnchorPosition = 1; field.selectionFocusPosition = 3;
            keyboard.Insert("@"); Assert.AreEqual("a@c", field.text);
            keyboard.Backspace(); Assert.AreEqual("ac", field.text);
            keyboard.Clear(); keyboard.Backspace(); Assert.AreEqual("", field.text);
            keyboard.ToggleShift();
            ExecuteEvents.Execute(key, pointer, ExecuteEvents.pointerClickHandler);
            Assert.AreEqual("Q", field.text);
            keyboard.ToggleShift(); keyboard.ToggleSymbols();
            ExecuteEvents.Execute(key, pointer, ExecuteEvents.pointerClickHandler);
            Assert.AreEqual("Q!", field.text);
            keyboard.ToggleSymbols(); keyboard.Clear();
            field.text = new string('a', field.characterLimit - 1); field.caretPosition = field.text.Length;
            keyboard.Insert("xyz"); Assert.AreEqual(field.characterLimit, field.text.Length);
            keyboard.Insert("x"); Assert.AreEqual(field.characterLimit, field.text.Length);
            keyboard.NextField(); Assert.AreSame(View.Password, keyboard.ActiveField);
            Assert.AreEqual(TMPro.TMP_InputField.InputType.Password, View.Password.inputType);
            Assert.IsTrue(field.shouldHideSoftKeyboard && View.Password.shouldHideSoftKeyboard);
            Assert.IsNull(SimulationHost.Instance);
        }

        [UnityTest]
        public IEnumerator Keyboard_types_credentials_validates_and_is_destroyed_before_the_app_rig_loads()
        {
            yield return Open(); HeadsetForm(); yield return null;
            var view = View; var keyboard = view.GetComponent<LoginKeyboard>();
            view.Submit(); Assert.IsFalse(view.Completed);
            keyboard.SelectField(view.Email); keyboard.Clear(); keyboard.Insert(DemoLoginSettings.DemoEmail);
            keyboard.NextField(); keyboard.Clear(); keyboard.Insert("wrong"); view.Submit(); Assert.IsFalse(view.Completed);
            keyboard.Clear(); keyboard.Insert(DemoLoginSettings.DemoPassword);
            view.Submit(); Assert.IsTrue(view.Completed); Assert.AreEqual("", view.Password.text);
            keyboard.Insert("ignored"); Assert.AreEqual("", view.Password.text);
            for (int i = 0; i < 60; i++) yield return null;
            Assert.IsNull(Object.FindFirstObjectByType<LoginKeyboard>());
            Assert.AreEqual(1, Camera.allCamerasCount);
            Assert.AreEqual(1, Object.FindObjectsByType<EventSystem>(FindObjectsSortMode.None).Length);
            Assert.IsNotNull(SimulationHost.Instance); SimulationHost.Instance.SaveLogOnDestroy = false;
        }
    }
}
#endif
