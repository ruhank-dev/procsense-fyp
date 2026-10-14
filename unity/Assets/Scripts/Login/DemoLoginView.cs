using System;
using System.Collections;
using ProcSense.VR.Auth;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace ProcSense.VR.Login
{
    public sealed class DemoLoginView : MonoBehaviour
    {
        [SerializeField] private TMP_InputField email;
        [SerializeField] private TMP_InputField password;
        [SerializeField] private TMP_Text status;
        [SerializeField] private TMP_Text revealLabel;
        [SerializeField] private UnityEngine.UI.Button submit;
        [SerializeField] private UnityEngine.UI.Button reveal;
        [SerializeField] private RectTransform card;
        [SerializeField] private GameObject introduction;
        [SerializeField] private GameObject divider;
        private bool _narrow;
        private bool _busy;
        public bool Completed { get; private set; }
        public TMP_InputField Email => email;
        public TMP_InputField Password => password;
        public string Status => status.text;

        public void Configure(TMP_InputField emailField, TMP_InputField passwordField, TMP_Text statusLabel,
            TMP_Text passwordLabel, UnityEngine.UI.Button submitButton, UnityEngine.UI.Button revealButton,
            RectTransform cardRect, GameObject introductionObject, GameObject dividerObject)
        {
            email = emailField; password = passwordField; status = statusLabel; revealLabel = passwordLabel;
            submit = submitButton; reveal = revealButton; card = cardRect; introduction = introductionObject; divider = dividerObject;
        }

        private void Awake()
        {
            submit.onClick.AddListener(Submit);
            reveal.onClick.AddListener(TogglePassword);
            password.onSubmit.AddListener(_ => Submit());
            email.onValueChanged.AddListener(_ => ClearStatus());
            password.onValueChanged.AddListener(_ => ClearStatus());
            Resize();
        }

        private void Update()
        {
            Resize();
            var keyboard = Keyboard.current;
            if (keyboard == null || !keyboard.tabKey.wasPressedThisFrame || EventSystem.current == null) return;
            var selected = EventSystem.current.currentSelectedGameObject;
            if (selected == email.gameObject) password.Select();
            else email.Select();
        }

        private void Resize()
        {
            var canvas = GetComponent<Canvas>();
            bool narrow = canvas.renderMode != RenderMode.WorldSpace && Screen.width / (float)Mathf.Max(1, Screen.height) < 1.30f;
            if (_narrow == narrow && introduction.activeSelf == !narrow) return;
            _narrow = narrow;
            introduction.SetActive(!narrow); divider.SetActive(!narrow);
            card.anchorMin = card.anchorMax = new Vector2(narrow ? .5f : .74f, .5f);
        }

        public void Submit()
        {
            if (Completed || _busy) return;
            string address = email.text.Trim();
            if (address.Length == 0) { status.text = "Enter your email address."; email.Select(); return; }
            if (password.text.Length == 0) { status.text = "Enter your password."; password.Select(); return; }
            if (string.Equals(address, DemoLoginSettings.DemoEmail, StringComparison.OrdinalIgnoreCase))
            {
                // The fixed demo account never leaves the headset: it opens the workspace for practising offline, and nothing is sent to the service.
                if (!DemoLoginSettings.OfflineDemoAllowed || password.text != DemoLoginSettings.DemoPassword)
                {
                    status.text = DemoLoginSettings.OfflineDemoAllowed ? "Use the demo account shown below." : "Sign in with your ProcSense account.";
                    return;
                }
                AuthService.Current?.SignOut();
                Finish();
                return;
            }
            StartCoroutine(SignInOnline(address, password.text));
        }

        // A real account: the service and the account system decide (p1-backend-s10). The password is passed on once and never stored, shown or logged.
        private IEnumerator SignInOnline(string address, string typedPassword)
        {
            _busy = true;
            submit.interactable = false;
            status.text = "Connecting to the service. The first time it can take a minute to wake up...";
            SignInResult result = null;
            yield return AuthService.Factory().SignIn(address, typedPassword, r => result = r);
            _busy = false;
            if (result != null && result.Ok) { Finish(); yield break; }
            submit.interactable = true;
            string message = result != null ? result.Message : "Sign-in failed.";
            if (result != null && result.Outcome == SignInOutcome.Unreachable)
                message += DemoLoginSettings.OfflineDemoAllowed ? " Check the connection, or practise offline with the demo account shown below." : " Check the connection and try again.";
            status.text = message;
        }

        private void Finish()
        {
            Completed = true;
            password.SetTextWithoutNotify("");
            submit.interactable = false;
            status.text = "Opening your workspace...";
        }

        public void TogglePassword()
        {
            bool masked = password.inputType == TMP_InputField.InputType.Password;
            password.inputType = masked ? TMP_InputField.InputType.Standard : TMP_InputField.InputType.Password;
            revealLabel.text = masked ? "Hide" : "Show";
            password.ForceLabelUpdate();
        }

        private void ClearStatus() { if (!Completed && !_busy) status.text = ""; }
    }
}
