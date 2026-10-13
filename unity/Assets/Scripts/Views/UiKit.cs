using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ProcSense.VR.Views
{
    /// <summary>
    /// Small helpers that build greybox UI in code (world-space panels, labels, buttons). Building it in code keeps scenes free of
    /// hand-wired references; the real art pass replaces these panels later (plan: greybox first).
    /// </summary>
    public static class UiKit
    {
        public static readonly Color PanelColor = new Color(0.08f, 0.10f, 0.15f, 0.96f);
        public static readonly Color ButtonColor = new Color(0.16f, 0.36f, 0.58f, 1f);
        public static readonly Color ButtonDisabledColor = new Color(0.24f, 0.26f, 0.30f, 1f);
        public static readonly Color TextColor = new Color(0.93f, 0.95f, 0.98f, 1f);
        public static readonly Color DimTextColor = new Color(0.62f, 0.67f, 0.74f, 1f);
        public static readonly Color WarnColor = new Color(1f, 0.78f, 0.30f, 1f);

        /// <summary>Raised for every canvas made here; the XR layer uses it to add its pointer raycaster.</summary>
        public static event Action<Canvas> CanvasCreated;

        public static Canvas WorldCanvas(string name, Transform parent, Vector2 sizePixels, float metresPerPixel)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            var rect = (RectTransform)go.transform;
            rect.sizeDelta = sizePixels;
            rect.localScale = Vector3.one * metresPerPixel;
            go.AddComponent<CanvasScaler>().dynamicPixelsPerUnit = 8f;
            go.AddComponent<GraphicRaycaster>();
            Panel(go.transform, "Background", Vector2.zero, sizePixels, PanelColor);
            CanvasCreated?.Invoke(canvas);
            return canvas;
        }

        /// <summary>Positions use the top-left corner of the parent as origin, y growing downwards (like a web page).</summary>
        public static RectTransform Panel(Transform parent, string name, Vector2 topLeft, Vector2 size, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            go.GetComponent<Image>().color = color;
            go.GetComponent<Image>().raycastTarget = false;
            return Place((RectTransform)go.transform, topLeft, size);
        }

        public static TMP_Text Label(Transform parent, string name, string text, Vector2 topLeft, Vector2 size, float fontSize,
                                     TextAlignmentOptions align = TextAlignmentOptions.TopLeft, Color? color = null)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var label = go.AddComponent<TextMeshProUGUI>();
            label.text = text;
            label.fontSize = fontSize;
            label.alignment = align;
            label.color = color ?? TextColor;
            label.textWrappingMode = TextWrappingModes.Normal;
            label.overflowMode = TextOverflowModes.Ellipsis;
            label.raycastTarget = false;
            Place((RectTransform)go.transform, topLeft, size);
            return label;
        }

        public static Button MakeButton(Transform parent, string label, Vector2 topLeft, Vector2 size, float fontSize = 24f)
        {
            var go = new GameObject("Button - " + label, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var image = go.GetComponent<Image>();
            image.color = Color.white;
            var button = go.GetComponent<Button>();
            button.targetGraphic = image;
            var colors = button.colors;
            colors.normalColor = ButtonColor;
            colors.highlightedColor = ButtonColor * 1.25f;
            colors.pressedColor = ButtonColor * 0.8f;
            colors.disabledColor = ButtonDisabledColor;
            colors.fadeDuration = 0.05f;
            button.colors = colors;
            Place((RectTransform)go.transform, topLeft, size);
            var text = Label(go.transform, "Label", label, new Vector2(8, 0), size - new Vector2(16, 0), fontSize, TextAlignmentOptions.Center);
            text.verticalAlignment = VerticalAlignmentOptions.Middle;
            return button;
        }

        private static RectTransform Place(RectTransform rect, Vector2 topLeft, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(topLeft.x, -topLeft.y);
            rect.sizeDelta = size;
            return rect;
        }
    }
}
