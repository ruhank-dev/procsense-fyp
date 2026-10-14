using UnityEngine;
using UnityEngine.UI;

namespace ProcSense.VR.Login
{
    /// <summary>Code-native rounded panels, with no generated bitmap dependency.</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class RoundedGraphic : MaskableGraphic
    {
        public float Radius = 18;
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            var rect = rectTransform.rect;
            float radius = Mathf.Min(Radius, Mathf.Min(rect.width, rect.height) * .5f);
            vh.AddVert(rect.center, color, new Vector2(.5f, .5f));
            const int segments = 12;
            for (int corner = 0; corner < 4; corner++)
            {
                Vector2 centre = new Vector2(corner == 0 || corner == 3 ? rect.xMax - radius : rect.xMin + radius,
                    corner < 2 ? rect.yMax - radius : rect.yMin + radius);
                for (int step = 0; step <= segments; step++)
                {
                    float angle = (corner * 90f + step * 90f / segments) * Mathf.Deg2Rad;
                    Vector2 p = centre + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
                    vh.AddVert(p, color, new Vector2(Mathf.InverseLerp(rect.xMin, rect.xMax, p.x), Mathf.InverseLerp(rect.yMin, rect.yMax, p.y)));
                }
            }
            int count = 4 * (segments + 1);
            for (int i = 1; i <= count; i++) vh.AddTriangle(0, i, i == count ? 1 : i + 1);
        }
    }
}
