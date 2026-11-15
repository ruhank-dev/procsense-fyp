using UnityEngine;
using UnityEngine.UI;

namespace ProcSense.VR.UI.Theme
{
    /// <summary>
    /// A frosted glass panel (P3-UIX-03): one quad drawn with the "ProcSense/UI Glass" shader. The quad carries its size in canvas units and the corner radius in the second UV
    /// channel, so the shader cuts the same rounded corner at any panel size, and the edge stays one pixel soft at any distance. All panels share one material per style
    /// (<see cref="Dark"/> for menus and cards, <see cref="Light"/> for highlights), so the SRP batcher draws them together. Use it in place of an Image for any panel background.
    /// </summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class GlassPanel : MaskableGraphic
    {
        public const string ShaderName = "ProcSense/UI Glass";
        public const string DarkMaterial = "UiGlass", LightMaterial = "UiGlassLight", SolidMaterial = "UiGlassSolid";

        /// <summary>Dark: panels and dialogs. Light: cards and secondary buttons on a panel. Solid: an opaque fill coloured by <c>color</c> (primary and danger buttons, badges), same shape and rim.</summary>
        public enum Style { Dark, Light, Solid }

        [SerializeField] private Style _style = Style.Dark;
        [SerializeField] private float _radius = -1f;   // negative: the theme's radius

        public Style PanelStyle { get => _style; set { _style = value; material = MaterialFor(value); SetVerticesDirty(); } }

        /// <summary>The corner radius in canvas units (the theme's radius unless set).</summary>
        public float Radius { get => _radius >= 0 ? _radius : UiTheme.Current.Radius; set { _radius = value; SetVerticesDirty(); } }

        private static Material _dark, _light, _solid;

        /// <summary>The shared material of a style (from Resources), or null when it is missing; the panel then draws as a plain tinted quad.</summary>
        public static Material MaterialFor(Style style)
        {
            if (style == Style.Light) return _light != null ? _light : (_light = Resources.Load<Material>(LightMaterial));
            if (style == Style.Solid) return _solid != null ? _solid : (_solid = Resources.Load<Material>(SolidMaterial));
            return _dark != null ? _dark : (_dark = Resources.Load<Material>(DarkMaterial));
        }

        public static Material Dark => MaterialFor(Style.Dark);
        public static Material Light => MaterialFor(Style.Light);
        public static Material Solid => MaterialFor(Style.Solid);

        /// <summary>Adds a glass panel under <paramref name="parent"/> (top-left origin, y down, like UiKit).</summary>
        public static GlassPanel Add(Transform parent, string name, Vector2 topLeft, Vector2 size, Style style = Style.Dark)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var panel = go.AddComponent<GlassPanel>();
            panel.PanelStyle = style;
            panel.raycastTarget = false;
            var rect = (RectTransform)go.transform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = new Vector2(topLeft.x, -topLeft.y);
            rect.sizeDelta = size;
            return panel;
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            if (material == defaultMaterial || material == null) material = MaterialFor(_style);
            var c = canvas;
            if (c != null) c.additionalShaderChannels |= AdditionalCanvasShaderChannels.TexCoord1;   // the size and radius travel in uv1
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            var r = GetPixelAdjustedRect();
            var shape = new Vector4(r.width, r.height, Radius, 0);
            var c = (Color32)color;
            vh.AddVert(new UIVertex { position = new Vector3(r.xMin, r.yMin), color = c, uv0 = new Vector4(0, 0), uv1 = shape });
            vh.AddVert(new UIVertex { position = new Vector3(r.xMin, r.yMax), color = c, uv0 = new Vector4(0, 1), uv1 = shape });
            vh.AddVert(new UIVertex { position = new Vector3(r.xMax, r.yMax), color = c, uv0 = new Vector4(1, 1), uv1 = shape });
            vh.AddVert(new UIVertex { position = new Vector3(r.xMax, r.yMin), color = c, uv0 = new Vector4(1, 0), uv1 = shape });
            vh.AddTriangle(0, 1, 2);
            vh.AddTriangle(2, 3, 0);
        }

#if UNITY_EDITOR
        protected override void OnValidate()
        {
            base.OnValidate();
            material = MaterialFor(_style);
        }
#endif
    }
}
