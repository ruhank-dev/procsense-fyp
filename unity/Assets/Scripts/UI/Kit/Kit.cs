using System;
using ProcSense.VR.UI.Accessibility;
using ProcSense.VR.UI.Theme;
using ProcSense.VR.Views;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ProcSense.VR.UI.Kit
{
    public enum TextStyle { Title, Heading, Body, Caption }
    public enum ButtonKind { Primary, Secondary, Danger, Icon }
    /// <summary>The state icon of a list row (the training board's steps use the same four).</summary>
    public enum RowState { Open, Done, NotNeeded, Missed }

    /// <summary>
    /// The ProcSense component kit (P3-UIX-04): every screen is built from these factory methods, all reading UiTheme (colours, type, radius, spacing, targets, motion)
    /// and drawing their shapes with GlassPanel (three shared materials, so the SRP batcher draws a whole screen in a few batches). Positions are top-left in canvas units,
    /// y down, like UiKit. Built in code like the rest of the app's UI (no prefabs: one source of truth for the token values); KitSample shows every component.
    /// Components: Canvas, Panel, Text, Icon, Header, Button (primary, secondary, danger, icon), Toggle, Slider, Segmented, ListRow, Card, Toast, Dialog, Badge, ProgressRing, Tooltip.
    /// </summary>
    public static class Kit
    {
        private static UiTheme T => UiTheme.Current;
        public const float RowHeight = 64f, ButtonHeight = 72f;

        // ------------------------------------------------------------------ base

        /// <summary>A world-space canvas read from <paramref name="distance"/> metres, scaled by the theme so text keeps its headset size at any distance.</summary>
        public static Canvas Canvas(string name, Transform parent, Vector2 size, float distance)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.additionalShaderChannels |= AdditionalCanvasShaderChannels.TexCoord1;
            ((RectTransform)go.transform).sizeDelta = size;
            go.AddComponent<CanvasScaler>().dynamicPixelsPerUnit = 8f;
            go.AddComponent<GraphicRaycaster>();
            var a = AccessibleCanvas.Attach(canvas, T.MetresPerUnit(distance));
            a.ViewingDistance = distance;
            a.Apply();
            UiKit.Announce(canvas);
            return canvas;
        }

        public static RectTransform Place(RectTransform r, Vector2 topLeft, Vector2 size)
        {
            r.anchorMin = r.anchorMax = r.pivot = new Vector2(0, 1);
            r.anchoredPosition = new Vector2(topLeft.x, -topLeft.y);
            r.sizeDelta = size;
            return r;
        }

        private static RectTransform Node(Transform parent, string name, params Type[] components)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            foreach (var c in components) go.AddComponent(c);
            return (RectTransform)go.transform;
        }

        public static GlassPanel Panel(Transform parent, string name, Vector2 topLeft, Vector2 size, GlassPanel.Style style = GlassPanel.Style.Dark, Color? fill = null, float radius = -1f)
        {
            var p = GlassPanel.Add(parent, name, topLeft, size, style);
            if (fill.HasValue) p.color = fill.Value;
            if (radius >= 0) p.Radius = radius;
            return p;
        }

        public static float SizeOf(TextStyle s) => s == TextStyle.Title ? T.Title : s == TextStyle.Heading ? T.Heading : s == TextStyle.Body ? T.Body : T.Caption;

        public static TMP_Text Text(Transform parent, string name, string text, Vector2 topLeft, Vector2 size, TextStyle style = TextStyle.Body, Color? color = null,
                                    TextAlignmentOptions align = TextAlignmentOptions.TopLeft)
        {
            var label = Node(parent, name).gameObject.AddComponent<TextMeshProUGUI>();
            if (label.font == null) label.font = TMP_Settings.defaultFontAsset;
            label.text = text;
            var readable = parent.GetComponentInParent<AccessibleCanvas>(true);
            label.fontSize = readable != null ? Mathf.Max(SizeOf(style), readable.MinFontSize) : SizeOf(style);
            label.fontWeight = style == TextStyle.Title ? FontWeight.Bold : style == TextStyle.Heading ? FontWeight.SemiBold : FontWeight.Regular;
            label.color = color ?? T.TextPrimary;
            label.alignment = align;
            label.textWrappingMode = TextWrappingModes.Normal;
            label.overflowMode = TextOverflowModes.Ellipsis;
            label.raycastTarget = false;
            Place(label.rectTransform, topLeft, size);
            return label;
        }

        public static Image Icon(Transform parent, string icon, Vector2 topLeft, float size, Color? color = null)
        {
            var img = Node(parent, "Icon " + icon, typeof(Image)).GetComponent<Image>();
            img.sprite = UiIcons.Get(icon);
            img.color = color ?? T.TextPrimary;
            img.raycastTarget = false;
            img.enabled = img.sprite != null;
            Place(img.rectTransform, topLeft, new Vector2(size, size));
            return img;
        }

        /// <summary>A screen header: optional icon, the title, an optional subtitle and a thin divider under them. Returns the header's rect (its height is what it used).</summary>
        public static RectTransform Header(Transform parent, string title, string subtitle, Vector2 topLeft, float width, string icon = null)
        {
            float h = T.Title * 1.4f + (string.IsNullOrEmpty(subtitle) ? 0 : T.Body * 1.6f) + T.Spacing[1];
            var r = Place(Node(parent, "Header"), topLeft, new Vector2(width, h));
            float x = 0;
            if (!string.IsNullOrEmpty(icon)) { Icon(r, icon, new Vector2(0, 4), T.Title, T.ActionText); x = T.Title + T.Spacing[1]; }
            Text(r, "Title", title, new Vector2(x, 0), new Vector2(width - x, T.Title * 1.4f), TextStyle.Title);
            if (!string.IsNullOrEmpty(subtitle)) Text(r, "Subtitle", subtitle, new Vector2(x, T.Title * 1.4f), new Vector2(width - x, T.Body * 1.6f), TextStyle.Body, T.TextSecondary);
            var line = Node(r, "Divider", typeof(Image)).GetComponent<Image>();
            line.color = new Color(1, 1, 1, .12f);
            line.raycastTarget = false;
            Place(line.rectTransform, new Vector2(0, h - 2), new Vector2(width, 2));
            return r;
        }

        // ------------------------------------------------------------------ controls

        /// <summary>A button. Primary: teal fill, dark label. Danger: red fill, dark label. Secondary: light glass. Icon: a square light-glass button with the icon only (at least the target size).</summary>
        public static Button Button(Transform parent, string label, Vector2 topLeft, Vector2 size, ButtonKind kind = ButtonKind.Secondary, string icon = null, Action onClick = null)
        {
            size = new Vector2(Mathf.Max(size.x, T.MinTarget), Mathf.Max(size.y, T.MinTarget));
            bool solid = kind == ButtonKind.Primary || kind == ButtonKind.Danger;
            var bg = Panel(parent, "Button " + (label ?? icon), topLeft, size, solid ? GlassPanel.Style.Solid : GlassPanel.Style.Light,
                           kind == ButtonKind.Primary ? T.Action : kind == ButtonKind.Danger ? T.Danger : (Color?)null, Mathf.Min(T.Radius, size.y / 2));
            bg.raycastTarget = true;
            var button = bg.gameObject.AddComponent<Button>();
            button.targetGraphic = bg;
            var c = button.colors;
            c.normalColor = new Color(.9f, .9f, .9f, 1f);
            c.highlightedColor = c.selectedColor = Color.white;
            c.pressedColor = new Color(.75f, .75f, .75f, 1f);
            c.disabledColor = new Color(.7f, .7f, .7f, T.TextDisabledAlpha);
            c.fadeDuration = T.Hover;
            button.colors = c;
            bg.gameObject.AddComponent<KitHover>();
            Color ink = solid ? T.TextOnAccent : T.TextPrimary;
            float iconSize = Mathf.Min(40f, size.y - 24f);
            if (kind == ButtonKind.Icon || string.IsNullOrEmpty(label))
                Icon(bg.transform, icon, new Vector2((size.x - iconSize) / 2, (size.y - iconSize) / 2), iconSize, ink);
            else
            {
                float x = T.Spacing[2];
                if (!string.IsNullOrEmpty(icon)) { Icon(bg.transform, icon, new Vector2(x, (size.y - iconSize) / 2), iconSize, ink); x += iconSize + T.Spacing[1]; }
                var text = Text(bg.transform, "Label", label, new Vector2(x, 0), new Vector2(size.x - x - T.Spacing[2], size.y), TextStyle.Body, ink,
                                string.IsNullOrEmpty(icon) ? TextAlignmentOptions.Center : TextAlignmentOptions.Left);
                text.verticalAlignment = VerticalAlignmentOptions.Middle;
                text.fontWeight = FontWeight.SemiBold;
            }
            if (onClick != null) button.onClick.AddListener(() => onClick());
            return button;
        }

        /// <summary>A labelled on/off switch; the whole row is the target.</summary>
        public static KitToggle Toggle(Transform parent, string label, Vector2 topLeft, float width, bool on, Action<bool> changed = null)
        {
            var row = Place(Node(parent, "Toggle " + label, typeof(Image)), topLeft, new Vector2(width, RowHeight));
            var hit = row.GetComponent<Image>();
            hit.color = new Color(0, 0, 0, 0);
            Text(row, "Label", label, Vector2.zero, new Vector2(width - 112, RowHeight), TextStyle.Body).verticalAlignment = VerticalAlignmentOptions.Middle;
            var track = Panel(row, "Track", new Vector2(width - 96, 10), new Vector2(88, 44), GlassPanel.Style.Solid, null, 22);
            var knob = Panel(track.transform, "Knob", new Vector2(4, 4), new Vector2(36, 36), GlassPanel.Style.Solid, Color.white, 18);
            var t = row.gameObject.AddComponent<UnityEngine.UI.Toggle>();
            t.targetGraphic = hit;
            t.transition = Selectable.Transition.None;
            var view = row.gameObject.AddComponent<KitToggle>();
            view.Init(t, track, knob);
            t.isOn = on;
            view.Show(on, false);
            if (changed != null) t.onValueChanged.AddListener(v => changed(v));
            return view;
        }

        /// <summary>A labelled slider from 0 to 1 (volumes, sizes). The label is on the left, the track on the right; the handle is the target size.</summary>
        public static Slider Slider(Transform parent, string label, Vector2 topLeft, float width, float value, Action<float> changed = null)
        {
            var row = Place(Node(parent, "Slider " + label), topLeft, new Vector2(width, RowHeight));
            float labelW = Mathf.Round(width * .4f);
            Text(row, "Label", label, Vector2.zero, new Vector2(labelW - T.Spacing[1], RowHeight), TextStyle.Body).verticalAlignment = VerticalAlignmentOptions.Middle;
            float trackW = width - labelW - 32;
            var area = Place(Node(row, "Track area", typeof(Image)), new Vector2(labelW + 16, 0), new Vector2(trackW, RowHeight));
            var hit = area.GetComponent<Image>();
            hit.color = new Color(0, 0, 0, 0);
            Panel(area, "Track", new Vector2(0, 28), new Vector2(trackW, 8), GlassPanel.Style.Solid, new Color(1, 1, 1, .2f), 4);
            var fillArea = Place(Node(area, "Fill area"), new Vector2(0, 28), new Vector2(trackW, 8));
            var fill = Panel(fillArea, "Fill", Vector2.zero, new Vector2(0, 8), GlassPanel.Style.Solid, T.Action, 4).rectTransform;
            fill.anchorMin = new Vector2(0, 0); fill.anchorMax = new Vector2(0, 1); fill.sizeDelta = Vector2.zero; fill.anchoredPosition = Vector2.zero;
            var handleArea = Place(Node(area, "Handle area"), new Vector2(0, 0), new Vector2(trackW, RowHeight));
            var handle = Panel(handleArea, "Handle", Vector2.zero, new Vector2(T.MinTarget, T.MinTarget), GlassPanel.Style.Solid, Color.white, T.MinTarget / 2).rectTransform;
            handle.anchorMin = new Vector2(0, 0); handle.anchorMax = new Vector2(0, 1); handle.pivot = new Vector2(.5f, .5f); handle.sizeDelta = new Vector2(T.MinTarget, 0); handle.anchoredPosition = Vector2.zero;
            var knobDot = Panel(handle, "Dot", new Vector2(18, 18), new Vector2(28, 28), GlassPanel.Style.Solid, Color.white, 14).rectTransform;
            knobDot.anchorMin = knobDot.anchorMax = knobDot.pivot = new Vector2(.5f, .5f); knobDot.anchoredPosition = Vector2.zero;
            handle.GetComponent<GlassPanel>().color = new Color(1, 1, 1, 0);   // the handle is a large invisible target around a visible dot
            handle.GetComponent<GlassPanel>().raycastTarget = true;
            var s = area.gameObject.AddComponent<UnityEngine.UI.Slider>();
            s.fillRect = fill;
            s.handleRect = handle;
            s.targetGraphic = handle.GetComponent<GlassPanel>();
            s.direction = UnityEngine.UI.Slider.Direction.LeftToRight;
            s.minValue = 0; s.maxValue = 1;
            s.value = Mathf.Clamp01(value);
            if (changed != null) s.onValueChanged.AddListener(v => changed(v));
            return s;
        }

        /// <summary>A row of mutually exclusive options; the selected one is filled teal.</summary>
        public static KitSegmented Segmented(Transform parent, string[] options, Vector2 topLeft, Vector2 size, int selected, Action<int> changed = null)
        {
            size.y = Mathf.Max(size.y, T.MinTarget + 8);   // each option keeps the full target height inside the 4-unit frame
            var bg = Panel(parent, "Segmented", topLeft, size, GlassPanel.Style.Light, null, Mathf.Min(T.Radius, size.y / 2));
            var seg = bg.gameObject.AddComponent<KitSegmented>();
            float w = (size.x - 8) / options.Length;
            var buttons = new Button[options.Length];
            for (int i = 0; i < options.Length; i++)
            {
                int index = i;
                var b = Panel(bg.transform, "Option " + options[i], new Vector2(4 + i * w, 4), new Vector2(w, size.y - 8), GlassPanel.Style.Solid, null, Mathf.Min(T.Radius, size.y / 2) - 4);
                b.raycastTarget = true;
                var btn = b.gameObject.AddComponent<Button>();
                btn.targetGraphic = b;
                btn.transition = Selectable.Transition.None;
                btn.onClick.AddListener(() => seg.Select(index));
                var label = Text(b.transform, "Label", options[i], Vector2.zero, new Vector2(w, size.y - 8), TextStyle.Body, null, TextAlignmentOptions.Center);
                label.verticalAlignment = VerticalAlignmentOptions.Middle;
                label.fontWeight = FontWeight.SemiBold;
                buttons[i] = btn;
            }
            seg.Init(buttons, selected);
            if (changed != null) seg.Changed += changed;
            return seg;
        }

        /// <summary>A list row with a state icon on the left (open: an empty ring; done: green tick; not needed: dimmed; missed: red cross).</summary>
        public static GlassPanel ListRow(Transform parent, string text, Vector2 topLeft, float width, RowState state)
        {
            var row = Panel(parent, "Row " + text, topLeft, new Vector2(width, RowHeight), GlassPanel.Style.Light, null, 16);
            const float icon = 36;
            var at = new Vector2(T.Spacing[1], (RowHeight - icon) / 2);
            switch (state)
            {
                case RowState.Open: Ring(row.transform, at, icon, 0f, T.TextSecondary, 3f); break;
                case RowState.Done: Icon(row.transform, UiIcons.Done, at, icon, T.DoneText); break;
                case RowState.NotNeeded: Icon(row.transform, UiIcons.Cancel, at, icon, T.TextDisabled); break;
                default: Icon(row.transform, UiIcons.Failed, at, icon, T.DangerText); break;
            }
            var label = Text(row.transform, "Text", text, new Vector2(T.Spacing[1] * 2 + icon, 0), new Vector2(width - icon - T.Spacing[1] * 3, RowHeight), TextStyle.Body,
                             state == RowState.NotNeeded ? T.TextDisabled : T.TextPrimary);
            label.verticalAlignment = VerticalAlignmentOptions.Middle;
            if (state == RowState.NotNeeded) label.fontStyle = FontStyles.Strikethrough;
            return row;
        }

        // ------------------------------------------------------------------ containers and feedback

        /// <summary>A card on a panel: light glass, an optional icon and heading, and body text.</summary>
        public static GlassPanel Card(Transform parent, string title, string body, Vector2 topLeft, Vector2 size, string icon = null, Color? iconColor = null)
        {
            var card = Panel(parent, "Card " + title, topLeft, size, GlassPanel.Style.Light);
            float pad = T.Spacing[2], x = pad;
            if (!string.IsNullOrEmpty(icon)) { Icon(card.transform, icon, new Vector2(pad, pad), 48, iconColor ?? T.ActionText); x += 48 + T.Spacing[1]; }
            Text(card.transform, "Title", title, new Vector2(x, pad + 4), new Vector2(size.x - x - pad, T.Heading * 1.4f), TextStyle.Heading);
            Text(card.transform, "Body", body, new Vector2(pad, pad + 64), new Vector2(size.x - 2 * pad, size.y - pad * 2 - 64), TextStyle.Body);
            return card;
        }

        /// <summary>A small solid label (status, count, verdict).</summary>
        public static GlassPanel Badge(Transform parent, string text, Vector2 topLeft, Color fill)
        {
            float h = 40;
            var b = Panel(parent, "Badge " + text, topLeft, new Vector2(100, h), GlassPanel.Style.Solid, fill, h / 2);
            var label = Text(b.transform, "Text", text, Vector2.zero, new Vector2(100, h), TextStyle.Caption, T.TextOnAccent, TextAlignmentOptions.Center);
            label.verticalAlignment = VerticalAlignmentOptions.Middle;
            label.fontWeight = FontWeight.Bold;
            float w = Mathf.Ceil(label.GetPreferredValues(text).x) + 2 * T.Spacing[1];
            b.rectTransform.sizeDelta = new Vector2(Mathf.Max(h, w), h);
            label.rectTransform.sizeDelta = b.rectTransform.sizeDelta;
            return b;
        }

        /// <summary>A ring that fills clockwise from the top (score, timer, upload progress).</summary>
        public static ProgressRing Ring(Transform parent, Vector2 topLeft, float diameter, float value, Color color, float thickness = 10f)
        {
            var ring = Node(parent, "Ring", typeof(ProgressRing)).GetComponent<ProgressRing>();
            ring.color = color;
            ring.Thickness = thickness;
            ring.Value = value;
            ring.raycastTarget = false;
            Place(ring.rectTransform, topLeft, new Vector2(diameter, diameter));
            return ring;
        }

        /// <summary>A short caption on dark glass next to a control.</summary>
        public static GlassPanel Tooltip(Transform parent, string text, Vector2 topLeft, float width)
        {
            var tip = Panel(parent, "Tooltip", topLeft, new Vector2(width, 56), GlassPanel.Style.Dark, null, 12);
            var label = Text(tip.transform, "Text", text, new Vector2(T.Spacing[1], 0), new Vector2(width - 2 * T.Spacing[1], 56), TextStyle.Caption, T.TextPrimary);
            label.verticalAlignment = VerticalAlignmentOptions.Middle;
            return tip;
        }

        /// <summary>A toast: icon and one line on dark glass. Show() rises and fades it in, holds it and fades it out (UiMotion.Toast).</summary>
        public static KitToast Toast(Transform parent, string text, Vector2 topLeft, float width, string icon = UiIcons.Info, Color? iconColor = null)
        {
            var bg = Panel(parent, "Toast", topLeft, new Vector2(width, 88), GlassPanel.Style.Dark);
            Icon(bg.transform, icon, new Vector2(T.Spacing[2], 24), 40, iconColor ?? T.ActionText);
            var label = Text(bg.transform, "Text", text, new Vector2(T.Spacing[2] + 56, 0), new Vector2(width - 56 - 2 * T.Spacing[2], 88), TextStyle.Body);
            label.verticalAlignment = VerticalAlignmentOptions.Middle;
            var toast = bg.gameObject.AddComponent<KitToast>();
            toast.Init(bg.gameObject.AddComponent<CanvasGroup>(), label);
            return toast;
        }

        /// <summary>A confirmation dialog: title, body and two buttons (the confirm button is red when <paramref name="danger"/>). Hidden until Open().</summary>
        public static KitDialog Dialog(Transform parent, string title, string body, string confirm, string cancel, Vector2 topLeft, bool danger = false, Action onConfirm = null, Action onCancel = null, float width = 640)
        {
            var size = new Vector2(width, 360);
            var bg = Panel(parent, "Dialog " + title, topLeft, size, GlassPanel.Style.Dark);
            bg.raycastTarget = true;   // blocks the controls behind it
            float pad = T.Spacing[3];
            Text(bg.transform, "Title", title, new Vector2(pad, pad), new Vector2(size.x - 2 * pad, T.Heading * 1.4f), TextStyle.Heading);
            Text(bg.transform, "Body", body, new Vector2(pad, pad + 60), new Vector2(size.x - 2 * pad, 150), TextStyle.Body, T.TextSecondary);
            float bw = (size.x - 2 * pad - T.Spacing[1]) / 2, by = size.y - pad - ButtonHeight;
            var d = bg.gameObject.AddComponent<KitDialog>();
            var no = Button(bg.transform, cancel, new Vector2(pad, by), new Vector2(bw, ButtonHeight), ButtonKind.Secondary, null, () => { onCancel?.Invoke(); d.Close(); });
            var yes = Button(bg.transform, confirm, new Vector2(pad + bw + T.Spacing[1], by), new Vector2(bw, ButtonHeight), danger ? ButtonKind.Danger : ButtonKind.Primary, null,
                             () => { onConfirm?.Invoke(); d.Close(); });
            d.Init(bg.gameObject.AddComponent<CanvasGroup>(), yes, no);
            bg.gameObject.SetActive(false);
            return d;
        }
    }

    /// <summary>Hover lift and press dip of a kit button (UiMotion presets; none with reduce motion).</summary>
    public sealed class KitHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler
    {
        public void OnPointerEnter(PointerEventData e) { if (Interactable) UiMotion.Hover((RectTransform)transform, true); }
        public void OnPointerExit(PointerEventData e) => UiMotion.Hover((RectTransform)transform, false);
        public void OnPointerDown(PointerEventData e) { if (Interactable) UiMotion.Press(transform); }
        private bool Interactable { get { var s = GetComponent<Selectable>(); return s == null || s.IsInteractable(); } }
    }

    /// <summary>The look of a kit toggle: the track teal and the knob on the right when on.</summary>
    public sealed class KitToggle : MonoBehaviour
    {
        public UnityEngine.UI.Toggle Toggle { get; private set; }
        private GlassPanel _track, _knob;

        public bool IsOn { get => Toggle.isOn; set => Toggle.isOn = value; }

        public void Init(UnityEngine.UI.Toggle toggle, GlassPanel track, GlassPanel knob)
        {
            Toggle = toggle; _track = track; _knob = knob;
            toggle.onValueChanged.AddListener(v => Show(v, true));
        }

        public void Show(bool on, bool animate)
        {
            var t = UiTheme.Current;
            _track.color = on ? t.Action : new Color(1, 1, 1, .2f);
            float from = _knob.rectTransform.anchoredPosition.x, to = on ? 48f : 4f;
            if (!animate) { _knob.rectTransform.anchoredPosition = new Vector2(to, -4); return; }
            UiMotion.Play(_knob, "toggle", t.Hover * 2, UiMotion.EaseOut, v => _knob.rectTransform.anchoredPosition = new Vector2(Mathf.Lerp(from, to, v), -4));
        }
    }

    /// <summary>The selection of a segmented control.</summary>
    public sealed class KitSegmented : MonoBehaviour
    {
        public int Selected { get; private set; } = -1;
        public event Action<int> Changed;
        private Button[] _options;
        public int Count => _options.Length;
        public Button Option(int i) => _options[i];

        public void Init(Button[] options, int selected) { _options = options; Select(selected, false); }

        public void Select(int index) => Select(index, true);

        private void Select(int index, bool notify)
        {
            index = Mathf.Clamp(index, 0, _options.Length - 1);
            bool changed = index != Selected;
            Selected = index;
            var t = UiTheme.Current;
            for (int i = 0; i < _options.Length; i++)
            {
                bool on = i == index;
                ((GlassPanel)_options[i].targetGraphic).color = on ? t.Action : new Color(1, 1, 1, 0);
                var label = _options[i].GetComponentInChildren<TMP_Text>();
                if (label != null) label.color = on ? t.TextOnAccent : t.TextPrimary;
            }
            if (changed && notify) Changed?.Invoke(index);
        }
    }

    /// <summary>A toast's show and hide.</summary>
    public sealed class KitToast : MonoBehaviour
    {
        public CanvasGroup Group { get; private set; }
        public TMP_Text Label { get; private set; }
        public void Init(CanvasGroup group, TMP_Text label) { Group = group; Label = label; group.alpha = 0; group.blocksRaycasts = false; }
        public void Show(string text = null, float hold = 2.5f)
        {
            if (text != null) Label.text = text;
            UiMotion.Toast((RectTransform)transform, Group, hold);
        }
    }

    /// <summary>A dialog's open and close (UiMotion presets).</summary>
    public sealed class KitDialog : MonoBehaviour
    {
        public Button Confirm { get; private set; }
        public Button Cancel { get; private set; }
        private CanvasGroup _group;
        public bool IsOpen => gameObject.activeSelf;
        public void Init(CanvasGroup group, Button confirm, Button cancel) { _group = group; Confirm = confirm; Cancel = cancel; }
        public void Open() => UiMotion.Open(transform, _group);
        public void Close() => UiMotion.Close(transform, _group);
    }
}
