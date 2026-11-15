using UnityEngine;
using UnityEngine.UI;

namespace ProcSense.VR.UI.Kit
{
    /// <summary>
    /// A ring that fills clockwise from twelve o'clock (P3-UIX-04): the full ring drawn faintly as the track, the filled part in the graphic's colour. A plain mesh with the
    /// default UI material, so it batches with the rest of the canvas. Value 0 shows the track only (the open-step ring of a list row).
    /// </summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class ProgressRing : MaskableGraphic
    {
        public const int Segments = 72;
        public const float TrackAlpha = .22f;

        [SerializeField, Range(0, 1)] private float _value;
        [SerializeField] private float _thickness = 10f;

        public float Value { get => _value; set { _value = Mathf.Clamp01(value); SetVerticesDirty(); } }
        public float Thickness { get => _thickness; set { _thickness = Mathf.Max(1f, value); SetVerticesDirty(); } }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            var r = GetPixelAdjustedRect();
            float outer = Mathf.Min(r.width, r.height) / 2f, inner = Mathf.Max(0, outer - _thickness);
            Vector2 c = r.center;
            var track = color; track.a *= TrackAlpha;
            Arc(vh, c, inner, outer, 0f, 1f, track);
            if (_value > 0) Arc(vh, c, inner, outer, 0f, _value, color);
        }

        private static void Arc(VertexHelper vh, Vector2 c, float inner, float outer, float from, float to, Color32 colour)
        {
            int steps = Mathf.Max(1, Mathf.CeilToInt(Segments * (to - from)));
            int start = vh.currentVertCount;
            for (int i = 0; i <= steps; i++)
            {
                float a = Mathf.PI / 2 - Mathf.Lerp(from, to, i / (float)steps) * Mathf.PI * 2;   // from the top, clockwise
                var d = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                vh.AddVert(c + d * outer, colour, Vector2.zero);
                vh.AddVert(c + d * inner, colour, Vector2.zero);
            }
            for (int i = 0; i < steps; i++)
            {
                int o = start + i * 2;
                vh.AddTriangle(o, o + 2, o + 1);
                vh.AddTriangle(o + 1, o + 2, o + 3);
            }
        }
    }
}
