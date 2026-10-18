using System;
using System.Collections;
using ProcSense.VR.Auth;
using ProcSense.VR.Sync;
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
        [SerializeField] private TMP_Text emailError;
        [SerializeField] private TMP_Text passwordError;
        private UnityEngine.UI.ColorBlock _emailColors, _passwordColors;
        private bool _validateEmail, _validatePassword;
        private bool _narrow;
        private bool _busy;
        public bool IsReady { get; private set; }
        public bool Completed { get; private set; }
        public TMP_InputField Email => email;
        public TMP_InputField Password => password;
        public string Status => status != null ? status.text : "";
        public string EmailError => emailError != null ? emailError.text : "";
        public string PasswordError => passwordError != null ? passwordError.text : "";
        public void ConfigureValidation(TMP_Text emailLabel, TMP_Text passwordLabel) { emailError = emailLabel; passwordError = passwordLabel; }

        public void Configure(TMP_InputField emailField, TMP_InputField passwordField, TMP_Text statusLabel,
            TMP_Text passwordLabel, UnityEngine.UI.Button submitButton, UnityEngine.UI.Button revealButton,
            RectTransform cardRect, GameObject introductionObject, GameObject dividerObject)
        {
            email = emailField; password = passwordField; status = statusLabel; revealLabel = passwordLabel;
            submit = submitButton; reveal = revealButton; card = cardRect; introduction = introductionObject; divider = dividerObject;
        }

        private void Awake()
        {
            IsReady = email != null && password != null && status != null && revealLabel != null && submit != null && reveal != null && card != null && introduction != null && divider != null && emailError != null && passwordError != null && GetComponent<Canvas>() != null;
            if (!IsReady)
            {
                if (status != null) status.text = "Sign-in is unavailable. Please restart.";
                if (submit != null) submit.interactable = false;
                if (reveal != null) reveal.interactable = false;
                if (email != null) email.interactable = false;
                if (password != null) password.interactable = false;
                Debug.LogError("Login UI references are incomplete; workspace remains locked.");
                enabled = false;
                return;
            }
            _emailColors = email.colors; _passwordColors = password.colors;
            submit.onClick.AddListener(Submit);
            reveal.onClick.AddListener(TogglePassword);
            password.onSubmit.AddListener(_ => Submit());
            email.onValueChanged.AddListener(_ => { ClearStatus(); if (_validateEmail) ValidateEmail(); });
            password.onValueChanged.AddListener(_ => { ClearStatus(); if (_validatePassword) ValidatePassword(); });
            email.onEndEdit.AddListener(_ => { _validateEmail = email.text.Length > 0 || _validateEmail; if (_validateEmail) ValidateEmail(); });
            password.onEndEdit.AddListener(_ => { _validatePassword = password.text.Length > 0 || _validatePassword; if (_validatePassword) ValidatePassword(); });
            Resize();
        }

        private void Update()
        {
            if (Completed) return;
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
            bool headset = canvas.renderMode == RenderMode.WorldSpace;
            bool narrow = headset || Screen.width / (float)Mathf.Max(1, Screen.height) < 1.30f;
            if (_narrow == narrow && introduction.activeSelf == !narrow) return;
            _narrow = narrow;
            introduction.SetActive(!narrow); divider.SetActive(!narrow);
            card.anchorMin = card.anchorMax = new Vector2(narrow ? .5f : .74f, .5f);
            card.anchoredPosition = headset ? new Vector2(0, 180) : Vector2.zero;
        }

        public void Submit()
        {
            if (!IsReady || Completed || _busy) return;
            _validateEmail = _validatePassword = true;
            ClearStatus();
            bool emailValid = ValidateEmail(), passwordValid = ValidatePassword();
            if (!emailValid || !passwordValid)
            {
                if (!emailValid) email.Select(); else password.Select();
                return;
            }
            string address = email.text.Trim();
            if (string.Equals(address, DemoLoginSettings.DemoEmail, StringComparison.OrdinalIgnoreCase))
            {
                // The offline demo never sends credentials to the service.
                if (!DemoLoginSettings.OfflineDemoAllowed || password.text != DemoLoginSettings.DemoPassword)
                {
                    status.text = DemoLoginSettings.OfflineDemoAllowed ? "Incorrect email or password. Use the demo account shown below." : "Sign in with your ProcSense account.";
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
            email.SetTextWithoutNotify(email.text.Trim());
            password.SetTextWithoutNotify("");
            submit.interactable = false;
            reveal.interactable = false;
            email.interactable = password.interactable = false;
            status.color = new Color(.10f, .49f, .45f);
            status.text = "Opening your workspace...";
            SessionUploader.Instance?.Kick();   // sessions saved earlier by this person can go now
        }

        public void TogglePassword()
        {
            if (!IsReady || Completed) return;
            bool masked = password.inputType == TMP_InputField.InputType.Password;
            password.inputType = masked ? TMP_InputField.InputType.Standard : TMP_InputField.InputType.Password;
            revealLabel.text = masked ? "Hide" : "Show";
            password.ForceLabelUpdate();
        }

        private void ClearStatus() { if (!Completed && !_busy) status.text = ""; }
        private bool ValidateEmail() => FieldError(email, emailError, LoginInputValidation.EmailError(email.text), _emailColors);
        private bool ValidatePassword() => FieldError(password, passwordError, LoginInputValidation.PasswordError(password.text), _passwordColors);
        private static bool FieldError(TMP_InputField field, TMP_Text label, string message, UnityEngine.UI.ColorBlock original)
        {
            label.text = message;
            if (message.Length > 0)
            {
                var red = new Color(.70f, .22f, .22f);
                original.normalColor = original.selectedColor = original.highlightedColor = red;
            }
            field.colors = original;
            return message.Length == 0;
        }

    }
}
