using UnityEngine;
using UnityEngine.UI;

namespace ProcSense.VR.Login
{
    /// <summary>Quiet abstract signal ribbons for the launch screen, not patient readings.</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class LoginArtwork : MaskableGraphic
    {
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            var rect = rectTransform.rect;
            const int count = 160;
            for (int layer = 0; layer < 5; layer++)
            {
                for (int i = 0; i < count; i++)
                {
                    Vector2 Point(int sample)
                    {
                        float t = sample / (float)count;
                        float wave = Mathf.Sin(t * 7.5f + layer * .21f) * .22f + Mathf.Sin(t * 15f + 1) * .09f;
                        return new Vector2(rect.xMin + t * rect.width, rect.center.y + (wave + (layer - 2) * .085f) * rect.height);
                    }
                    Vector2 p = Point(i), q = Point(i + 1);
                    var normal = new Vector2(-(q - p).y, (q - p).x).normalized * 1.25f;
                    int start = vh.currentVertCount;
                    var tint = color; tint.a *= 1 - Mathf.Abs(layer - 2) * .22f;
                    vh.AddVert(p - normal, tint, Vector2.zero); vh.AddVert(p + normal, tint, Vector2.zero);
                    vh.AddVert(q + normal, tint, Vector2.zero); vh.AddVert(q - normal, tint, Vector2.zero);
                    vh.AddTriangle(start, start + 1, start + 2); vh.AddTriangle(start, start + 2, start + 3);
                }
            }
        }
    }
}
