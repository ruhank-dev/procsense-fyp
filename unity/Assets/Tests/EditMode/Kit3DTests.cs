using NUnit.Framework;
using ProcSense.VR.UI.Kit;
using ProcSense.VR.UI.Theme;
using UnityEngine;

namespace ProcSense.Tests.EditMode
{
    /// <summary>P3-AST-07: the lightbulb, board icon, wrist button and submit button are in the kit, within the vertex budget, at their sizes, in the theme colours, with every face
    /// turned outward (so none disappears to back-face culling on the headset) and a collider to press.</summary>
    public class Kit3DTests
    {
        [Test]
        public void Each_element_is_a_small_mesh_at_its_size_with_faces_turned_outward()
        {
            foreach (Icon3D kind in System.Enum.GetValues(typeof(Icon3D)))
            {
                var m = Kit3D.MeshOf(kind);
                Assert.AreSame(m, Kit3D.MeshOf(kind), kind + ": built once and shared");
                Assert.LessOrEqual(m.vertexCount, Kit3D.VertexBudget, kind.ToString());
                Assert.AreEqual(2, m.subMeshCount, kind + ": body and coloured part");
                Assert.Greater(m.GetTriangles(1).Length, 0, kind + ": has a coloured part");
                var size = m.bounds.size;
                Assert.AreEqual(Kit3D.SizeOf(kind), Mathf.Max(size.x, size.y, size.z), .002f, kind + ": largest extent");

                var v = m.vertices;
                var n = m.normals;
                for (int s = 0; s < 2; s++)
                {
                    var t = m.GetTriangles(s);
                    for (int i = 0; i < t.Length; i += 3)
                    {
                        var face = Vector3.Cross(v[t[i + 1]] - v[t[i]], v[t[i + 2]] - v[t[i]]);
                        if (face.sqrMagnitude < 1e-14f) continue;   // the point at a lathe's pole
                        var avg = n[t[i]] + n[t[i + 1]] + n[t[i + 2]];
                        Assert.Greater(Vector3.Dot(face, avg), 0f, kind + ": triangle " + i / 3 + " of part " + s + " faces inward");
                    }
                }
            }
        }

        private static void Near(Color a, Color b, string why) => Assert.Less(((Vector4)a - (Vector4)b).magnitude, .002f, why + ": " + a + " vs " + b);

        [Test]
        public void Made_elements_carry_the_theme_colours_and_a_collider()
        {
            var root = new GameObject("kit3d");
            try
            {
                var t = UiTheme.Current;
                var bulb = Kit3D.Make(Icon3D.Lightbulb, root.transform);
                var submit = Kit3D.Make(Icon3D.SubmitButton, root.transform);
                var wrist = Kit3D.Make(Icon3D.WristButton, root.transform);
                Near(t.Hint, bulb.GetComponent<MeshRenderer>().sharedMaterials[1].color, "the hint bulb is hint amber");
                Near(t.Danger, submit.GetComponent<MeshRenderer>().sharedMaterials[1].color, "submit is danger red");
                Near(t.Action, wrist.GetComponent<MeshRenderer>().sharedMaterials[1].color, "wrist is action teal");
                Assert.AreSame(submit.GetComponent<MeshRenderer>().sharedMaterials[0], wrist.GetComponent<MeshRenderer>().sharedMaterials[0], "materials are shared");
                var box = submit.GetComponent<BoxCollider>();
                Assert.AreEqual(Kit3D.MeshOf(Icon3D.SubmitButton).bounds.size, box.size);
                Assert.AreEqual(UnityEngine.Rendering.ShadowCastingMode.Off, submit.GetComponent<MeshRenderer>().shadowCastingMode, "no shadow pass for interface parts");
            }
            finally { Object.DestroyImmediate(root); }
        }
    }
}
