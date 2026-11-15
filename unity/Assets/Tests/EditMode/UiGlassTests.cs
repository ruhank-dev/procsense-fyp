using NUnit.Framework;
using ProcSense.VR.UI.Accessibility;
using ProcSense.VR.UI.Theme;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace ProcSense.Tests.EditMode
{
    /// <summary>
    /// P3-UIX-03: the glass shader compiles without errors and is supported, both shared materials use it (and keep it in player builds by sitting in Resources), a glass panel
    /// sends its size and radius in uv1 and turns that channel on for its canvas, and text on the glass keeps 4.5:1 contrast even over a white room.
    /// </summary>
    public class UiGlassTests
    {
        [Test]
        public void The_glass_shader_compiles_and_is_supported()
        {
            var shader = Shader.Find(GlassPanel.ShaderName);
            Assert.IsNotNull(shader);
            Assert.IsFalse(ShaderUtil.ShaderHasError(shader), "shader errors: " + ShaderUtil.GetShaderMessageCount(shader));
            Assert.IsTrue(shader.isSupported);
            Assert.GreaterOrEqual(shader.passCount, 1);
        }

        [Test]
        public void Both_shared_materials_use_the_glass_shader()
        {
            foreach (var style in new[] { GlassPanel.Style.Dark, GlassPanel.Style.Light })
            {
                var m = GlassPanel.MaterialFor(style);
                Assert.IsNotNull(m, style.ToString());
                Assert.AreEqual(GlassPanel.ShaderName, m.shader.name);
                Assert.IsTrue(m.enableInstancing);
            }
            Assert.AreNotSame(GlassPanel.Dark, GlassPanel.Light);
        }

        [Test]
        public void A_panel_sends_its_size_and_radius_and_enables_uv1_on_its_canvas()
        {
            var root = new GameObject("canvas", typeof(RectTransform), typeof(Canvas));
            try
            {
                var canvas = root.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.WorldSpace;
                canvas.additionalShaderChannels = AdditionalCanvasShaderChannels.None;
                var panel = GlassPanel.Add(root.transform, "glass", new Vector2(10, 20), new Vector2(300, 120));
                Assert.AreSame(GlassPanel.Dark, panel.material);
                Assert.IsTrue((canvas.additionalShaderChannels & AdditionalCanvasShaderChannels.TexCoord1) != 0);
                panel.Radius = 16;
                var mesh = new Mesh();
                using (var vh = new VertexHelper())
                {
                    typeof(GlassPanel).GetMethod("OnPopulateMesh", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance, null, new[] { typeof(VertexHelper) }, null).Invoke(panel, new object[] { vh });
                    Assert.AreEqual(4, vh.currentVertCount);
                    Assert.AreEqual(6, vh.currentIndexCount);
                    var v = new UIVertex();
                    vh.PopulateUIVertex(ref v, 2);
                    Assert.AreEqual(new Vector4(300, 120, 16, 0), v.uv1);
                    Assert.AreEqual(new Vector4(1, 1, 0, 0), v.uv0);
                }
                Object.DestroyImmediate(mesh);
                var plain = GlassPanel.Add(root.transform, "default radius", Vector2.zero, new Vector2(100, 100));
                Assert.AreEqual(UiTheme.Current.Radius, plain.Radius, "the theme's radius unless set");
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test]
        public void Text_on_the_dark_glass_keeps_its_contrast_over_a_white_room()
        {
            var t = UiTheme.Current;
            var glass = GlassPanel.Dark.GetColor("_Color");
            foreach (var room in new[] { Color.white, new Color(.8f, .8f, .75f), Color.black })
            {
                var behind = UiTheme.Over(glass, room);
                Assert.GreaterOrEqual(Readability.Contrast(t.TextPrimary, behind), 4.5f, "primary text over " + room);
                Assert.GreaterOrEqual(Readability.Contrast(UiTheme.Over(t.TextSecondary, behind), behind), 4.5f, "secondary text over " + room);
            }
        }
    }
}
