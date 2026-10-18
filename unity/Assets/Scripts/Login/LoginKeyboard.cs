using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace ProcSense.VR.Login
{
    /// <summary>Controller-ray text entry for the launch form; never echoes a password.</summary>
    public sealed class LoginKeyboard : MonoBehaviour
    {
        private DemoLoginView _view;
        private TMP_InputField _field;
        private TMP_Text _heading;
        private bool _shift, _symbols;
        private readonly List<KeyValuePair<UnityEngine.UI.Button, string>> _keys = new List<KeyValuePair<UnityEngine.UI.Button, string>>();
        public TMP_InputField ActiveField => _field;

        public void Configure(DemoLoginView view)
        {
            _view = view;
            // Own the VR keyboard explicitly instead of relying on an unavailable
            // platform overlay or allowing two keyboards to open at once.
            view.Email.shouldHideSoftKeyboard = view.Password.shouldHideSoftKeyboard = true;
            view.Email.shouldHideMobileInput = view.Password.shouldHideMobileInput = true;
            view.Email.onSelect.AddListener(SelectEmail);
            view.Password.onSelect.AddListener(SelectPassword);
            var root = new GameObject("Controller keyboard", typeof(RectTransform), typeof(RoundedGraphic));
            root.transform.SetParent(transform, false);
            var rect = (RectTransform)root.transform;
            rect.sizeDelta = new Vector2(940, 390);
            rect.anchoredPosition = new Vector2(0, -380);
            var background = root.GetComponent<RoundedGraphic>();
            background.Radius = 22; background.color = new Color(.07f, .14f, .17f); background.raycastTarget = false;
            _heading = Label(rect, "Input heading", "", new Vector2(0, 164), new Vector2(900, 34), 22);
            string[] rows = { "1234567890", "qwertyuiop", "asdfghjkl", "zxcvbnm" };
            for (int row = 0; row < rows.Length; row++)
            {
                string letters = rows[row];
                for (int col = 0; col < letters.Length; col++)
                {
                    string value = letters[col].ToString();
                    var button = Key(rect, value, new Vector2((col - (letters.Length - 1) * .5f) * 87, 112 - row * 60), new Vector2(79, 52), () => Type(value));
                    _keys.Add(new KeyValuePair<UnityEngine.UI.Button, string>(button, value));
                }
            }
            float y = -140;
            Key(rect, "Shift", new Vector2(-391, y), new Vector2(106, 54), ToggleShift);
            Key(rect, "Symbols", new Vector2(-273, y), new Vector2(116, 54), ToggleSymbols);
            Key(rect, "@", new Vector2(-166, y), new Vector2(80, 54), () => Insert("@"));
            Key(rect, ".", new Vector2(-78, y), new Vector2(80, 54), () => Insert("."));
            Key(rect, "Space", new Vector2(35, y), new Vector2(130, 54), () => Insert(" "));
            Key(rect, "Backspace", new Vector2(180, y), new Vector2(144, 54), Backspace);
            Key(rect, "Clear", new Vector2(299, y), new Vector2(78, 54), Clear);
            Key(rect, "Next field", new Vector2(397, y), new Vector2(108, 54), NextField);
            SelectField(view.Email);
        }

        private void SelectEmail(string value) => SelectField(_view.Email);
        private void SelectPassword(string value) => SelectField(_view.Password);

        public void SelectField(TMP_InputField field)
        {
            if (_view == null || _view.Completed || (field != _view.Email && field != _view.Password)) return;
            _field = field;
            if (_heading != null) _heading.text = field == _view.Email ? "EMAIL ADDRESS  /  Point and pull the trigger to type" : "PASSWORD  /  Point and pull the trigger to type";
            // Preserve a controller-selected caret; only explicitly move focus
            // when switching fields, not after each keyboard button click.
            if (EventSystem.current != null && EventSystem.current.currentSelectedGameObject != field.gameObject)
            {
                field.Select(); field.ActivateInputField();
                field.caretPosition = field.text.Length;
            }
        }

        private bool Editable => _field != null && _field.interactable && _view != null && !_view.Completed;

        public void Insert(string value)
        {
            if (!Editable || string.IsNullOrEmpty(value)) return;
            int start = Mathf.Min(_field.selectionAnchorPosition, _field.selectionFocusPosition);
            int end = Mathf.Max(_field.selectionAnchorPosition, _field.selectionFocusPosition);
            start = Mathf.Clamp(start, 0, _field.text.Length); end = Mathf.Clamp(end, start, _field.text.Length);
            int available = _field.characterLimit > 0 ? _field.characterLimit - (_field.text.Length - (end - start)) : value.Length;
            if (available <= 0) return;
            if (value.Length > available) value = value.Substring(0, available);
            _field.text = _field.text.Remove(start, end - start).Insert(start, value);
            _field.caretPosition = start + value.Length;
            _field.ForceLabelUpdate();
        }

        public void Backspace()
        {
            if (!Editable) return;
            int start = Mathf.Clamp(Mathf.Min(_field.selectionAnchorPosition, _field.selectionFocusPosition), 0, _field.text.Length);
            int end = Mathf.Clamp(Mathf.Max(_field.selectionAnchorPosition, _field.selectionFocusPosition), start, _field.text.Length);
            if (start == end) { if (start == 0) return; start--; }
            _field.text = _field.text.Remove(start, end - start);
            _field.caretPosition = start;
            _field.ForceLabelUpdate();
        }

        public void Clear() { if (Editable) { _field.text = ""; _field.caretPosition = 0; } }
        public void NextField() => SelectField(_field == _view.Email ? _view.Password : _view.Email);
        public void ToggleShift() { _shift = !_shift; RefreshKeys(); }
        public void ToggleSymbols() { _symbols = !_symbols; RefreshKeys(); }

        private string Display(string key)
        {
            const string letters = "qwertyuiopasdfghjklzxcvbnm";
            const string symbols = "!#$%&*()-_=+[]{};:'\",/?\\|<>`~^";
            int i = letters.IndexOf(key, System.StringComparison.Ordinal);
            if (_symbols && i >= 0) return symbols[(i + (_shift ? 15 : 0)) % symbols.Length].ToString();
            int digit = "1234567890".IndexOf(key, System.StringComparison.Ordinal);
            if (_shift && digit >= 0) return "!@#$%^&*()"[digit].ToString();
            return _shift ? key.ToUpperInvariant() : key;
        }
        private void Type(string key) => Insert(Display(key));
        private void RefreshKeys()
        {
            foreach (var pair in _keys) pair.Key.GetComponentInChildren<TMP_Text>().text = Display(pair.Value);
        }

        private TMP_Text Label(Transform parent, string name, string value, Vector2 position, Vector2 size, float fontSize)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI)); go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform; rect.sizeDelta = size; rect.anchoredPosition = position;
            var text = go.GetComponent<TextMeshProUGUI>(); text.font = _view.Email.textComponent.font;
            text.fontSize = fontSize; text.text = value; text.alignment = TextAlignmentOptions.Center;
            text.color = new Color(.93f, .97f, .95f); text.raycastTarget = false;
            return text;
        }

        private UnityEngine.UI.Button Key(Transform parent, string text, Vector2 position, Vector2 size, UnityEngine.Events.UnityAction action)
        {
            var go = new GameObject("Key " + text, typeof(RectTransform), typeof(RoundedGraphic), typeof(UnityEngine.UI.Button));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform; rect.sizeDelta = size; rect.anchoredPosition = position;
            var graphic = go.GetComponent<RoundedGraphic>(); graphic.Radius = 8; graphic.color = Color.white; graphic.raycastTarget = true;
            var button = go.GetComponent<UnityEngine.UI.Button>(); button.targetGraphic = graphic;
            var colors = button.colors; colors.normalColor = new Color(.14f, .29f, .30f); colors.highlightedColor = new Color(.21f, .47f, .44f);
            colors.pressedColor = new Color(.30f, .64f, .56f); colors.selectedColor = colors.highlightedColor; button.colors = colors;
            // Button.OnPointerDown must not steal the TMP field's selection/caret.
            button.navigation = new UnityEngine.UI.Navigation { mode = UnityEngine.UI.Navigation.Mode.None };
            button.onClick.AddListener(action);
            Label(rect, "Label", text, Vector2.zero, size, text.Length > 3 ? 16 : 25);
            return button;
        }

        private void OnDestroy()
        {
            if (_view == null) return;
            _view.Email.onSelect.RemoveListener(SelectEmail);
            _view.Password.onSelect.RemoveListener(SelectPassword);
        }
    }
}
