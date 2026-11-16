using System.Collections.Generic;
using ProcSense.VR.UI.Theme;
using UnityEngine;

namespace ProcSense.VR.UI.Kit
{
    /// <summary>The 3D elements of the kit (P3-AST-07).</summary>
    public enum Icon3D { Lightbulb, BoardIcon, WristButton, SubmitButton }

    /// <summary>
    /// The 3D elements of the interface (P3-AST-07): the hint lightbulb, the training-board icon, the wrist button and the big submit button. Each is a small low-poly mesh made in
    /// code (a lathe or an extruded rounded rectangle, no imported model, nothing to license), built once and shared, coloured from the theme tokens (hint amber, action teal,
    /// danger red) on the pipeline's lit shader, with a box collider so a ray or a finger can press it. Sizes are in metres and every mesh stays under <see cref="VertexBudget"/>.
    /// </summary>
    public static class Kit3D
    {
        public const int VertexBudget = 600;
        private const int Segments = 24;

        /// <summary>The size of each element in metres (its largest extent).</summary>
        public static float SizeOf(Icon3D kind)
        {
            switch (kind)
            {
                case Icon3D.Lightbulb: return .06f;
                case Icon3D.BoardIcon: return .06f;
                case Icon3D.WristButton: return .035f;
                default: return .16f;
            }
        }

        private static readonly Dictionary<Icon3D, Mesh> Meshes = new Dictionary<Icon3D, Mesh>();
        private static readonly Dictionary<Color, Material> Materials = new Dictionary<Color, Material>();

        /// <summary>The shared mesh of an element (two sub-meshes: the body, then the coloured part).</summary>
        public static Mesh MeshOf(Icon3D kind)
        {
            if (Meshes.TryGetValue(kind, out var m) && m != null) return m;
            switch (kind)
            {
                case Icon3D.Lightbulb: m = Lightbulb(); break;
                case Icon3D.BoardIcon: m = Board(); break;
                case Icon3D.WristButton: m = Puck(); break;
                default: m = Submit(); break;
            }
            m.name = "Kit3D " + kind;
            Meshes[kind] = m;
            return m;
        }

        /// <summary>The colours of an element: the body, then the coloured part.</summary>
        public static (Color body, Color accent) ColoursOf(Icon3D kind)
        {
            var t = UiTheme.Current;
            var body = UiTheme.Hex(0x3A4652);
            switch (kind)
            {
                case Icon3D.Lightbulb: return (UiTheme.Hex(0x9AA4AE), t.Hint);
                case Icon3D.BoardIcon: return (body, t.Action);
                case Icon3D.WristButton: return (body, t.Action);
                default: return (body, t.Danger);
            }
        }

        /// <summary>Makes the element under a parent, centred on its origin, with a collider for pressing.</summary>
        public static GameObject Make(Icon3D kind, Transform parent, string name = null)
        {
            var go = new GameObject(name ?? kind.ToString(), typeof(MeshFilter), typeof(MeshRenderer), typeof(BoxCollider));
            go.transform.SetParent(parent, false);
            var mesh = MeshOf(kind);
            go.GetComponent<MeshFilter>().sharedMesh = mesh;
            var (body, accent) = ColoursOf(kind);
            var r = go.GetComponent<MeshRenderer>();
            r.sharedMaterials = new[] { MaterialOf(body, false), MaterialOf(accent, kind == Icon3D.Lightbulb) };
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            var box = go.GetComponent<BoxCollider>();
            box.center = mesh.bounds.center;
            box.size = mesh.bounds.size;
            return go;
        }

        private static Material MaterialOf(Color c, bool glow)
        {
            var key = glow ? new Color(c.r, c.g, c.b, .5f) : c;
            if (Materials.TryGetValue(key, out var m) && m != null) return m;
            var shader = Shader.Find("Universal Render Pipeline/Simple Lit") ?? Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
            m = new Material(shader) { name = "Kit3D " + ColorUtility.ToHtmlStringRGB(c) + (glow ? " glow" : "") };
            m.color = c;
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", .55f);
            if (glow && m.HasProperty("_EmissionColor")) { m.EnableKeyword("_EMISSION"); m.SetColor("_EmissionColor", c * .8f); }
            Materials[key] = m;
            return m;
        }

        // ------------------------------------------------------------- shapes

        private sealed class Builder
        {
            public readonly List<Vector3> V = new List<Vector3>();
            public readonly List<Vector3> N = new List<Vector3>();
            public readonly List<int>[] T = { new List<int>(), new List<int>() };

            public int Add(Vector3 v, Vector3 n) { V.Add(v); N.Add(n); return V.Count - 1; }
            public void Quad(int sub, int a, int b, int c, int d) { T[sub].AddRange(new[] { a, b, c, a, c, d }); }

            public Mesh Build()
            {
                var m = new Mesh();
                m.SetVertices(V);
                m.SetNormals(N);
                m.subMeshCount = 2;
                m.SetTriangles(T[0], 0);
                m.SetTriangles(T[1], 1);
                m.RecalculateBounds();
                return m;
            }

            /// <summary>Turns a profile of (radius, height) points around the Y axis; smooth normals from the profile.</summary>
            public void Lathe(int sub, Vector2[] profile)
            {
                int rows = profile.Length, start = V.Count;
                for (int i = 0; i < rows; i++)
                {
                    var prev = profile[Mathf.Max(0, i - 1)];
                    var next = profile[Mathf.Min(rows - 1, i + 1)];
                    var tangent = (next - prev).normalized;
                    var n2 = new Vector2(tangent.y, -tangent.x);   // outward for a profile running bottom to top
                    for (int s = 0; s <= Segments; s++)
                    {
                        float a = s * Mathf.PI * 2f / Segments, cos = Mathf.Cos(a), sin = Mathf.Sin(a);
                        Add(new Vector3(profile[i].x * cos, profile[i].y, profile[i].x * sin), new Vector3(n2.x * cos, n2.y, n2.x * sin).normalized);
                    }
                }
                for (int i = 0; i < rows - 1; i++)
                    for (int s = 0; s < Segments; s++)
                    {
                        int a = start + i * (Segments + 1) + s, b = a + Segments + 1;
                        Quad(sub, a, b, b + 1, a + 1);
                    }
            }

            /// <summary>An extruded rounded rectangle in X/Y from z0 (back) to z1 (front): the sides go to sub-mesh 0, the front face to <paramref name="frontSub"/>.</summary>
            public void Slab(float w, float h, float radius, float z0, float z1, int frontSub, int sideSub = 0)
            {
                var ring = RoundedRect(w, h, radius);
                int n = ring.Count;
                // sides: two vertices per corner point, normals outward
                int sides = V.Count;
                for (int i = 0; i < n; i++)
                {
                    var p = ring[i];
                    var prev = ring[(i - 1 + n) % n];
                    var next = ring[(i + 1) % n];
                    var edge = (next - prev).normalized;
                    var nrm = new Vector3(edge.y, -edge.x, 0f);
                    Add(new Vector3(p.x, p.y, z0), nrm);
                    Add(new Vector3(p.x, p.y, z1), nrm);
                }
                for (int i = 0; i < n; i++)
                {
                    int a = sides + i * 2, b = sides + ((i + 1) % n) * 2;
                    Quad(sideSub, a, b, b + 1, a + 1);
                }
                Cap(ring, z1, Vector3.forward, frontSub, false);
                Cap(ring, z0, Vector3.back, sideSub, true);
            }

            private void Cap(List<Vector2> ring, float z, Vector3 normal, int sub, bool flip)
            {
                int centre = Add(new Vector3(0, 0, z), normal), first = V.Count;
                foreach (var p in ring) Add(new Vector3(p.x, p.y, z), normal);
                for (int i = 0; i < ring.Count; i++)
                {
                    int a = first + i, b = first + (i + 1) % ring.Count;
                    if (flip) T[sub].AddRange(new[] { centre, b, a }); else T[sub].AddRange(new[] { centre, a, b });   // clockwise as seen along the cap's normal
                }
            }
        }

        /// <summary>The outline of a rounded rectangle centred on the origin, counter-clockwise.</summary>
        internal static List<Vector2> RoundedRect(float w, float h, float radius, int perCorner = 6)
        {
            radius = Mathf.Min(radius, Mathf.Min(w, h) * .5f);
            var pts = new List<Vector2>();
            var centres = new[] { new Vector2(w / 2 - radius, h / 2 - radius), new Vector2(-w / 2 + radius, h / 2 - radius), new Vector2(-w / 2 + radius, -h / 2 + radius), new Vector2(w / 2 - radius, -h / 2 + radius) };
            for (int c = 0; c < 4; c++)
                for (int i = 0; i <= perCorner; i++)
                {
                    float a = (c * 90f + i * 90f / perCorner) * Mathf.Deg2Rad;
                    pts.Add(centres[c] + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius);
                }
            return pts;
        }

        // the hint lightbulb, 6 cm tall, standing on its screw base: the glass (amber, glowing) and the metal base
        private static Mesh Lightbulb()
        {
            var b = new Builder();
            b.Lathe(0, new[] { new Vector2(0f, -.03f), new Vector2(.007f, -.03f), new Vector2(.009f, -.027f), new Vector2(.0095f, -.022f), new Vector2(.0085f, -.019f), new Vector2(.0095f, -.016f), new Vector2(.0085f, -.013f), new Vector2(.0095f, -.01f) });
            var glass = new List<Vector2> { new Vector2(.0095f, -.01f), new Vector2(.011f, -.004f) };
            for (int i = 0; i <= 8; i++)
            {
                float a = Mathf.Lerp(-55f, 90f, i / 8f) * Mathf.Deg2Rad;
                glass.Add(new Vector2(Mathf.Cos(a) * .019f, .012f + Mathf.Sin(a) * .018f));
            }
            glass[glass.Count - 1] = new Vector2(0f, .03f);
            b.Lathe(1, glass.ToArray());
            return b.Build();
        }

        // the training-board icon: a rounded board facing the user (+Z) with a teal face and three lines of "text" raised a little
        private static Mesh Board()
        {
            var b = new Builder();
            b.Slab(.06f, .045f, .006f, -.003f, .003f, 0);
            for (int i = 0; i < 3; i++)
            {
                float y = .011f - i * .011f, w = i == 2 ? .028f : .042f;
                int start = b.V.Count;
                foreach (var p in RoundedRect(w, .005f, .0025f, 3))
                    b.Add(new Vector3(p.x - (.042f - w) / 2f, p.y + y, .0036f), Vector3.forward);
                int count = b.V.Count - start;
                int centre = b.Add(new Vector3(-(.042f - w) / 2f, y, .0036f), Vector3.forward);
                for (int k = 0; k < count; k++) b.T[1].AddRange(new[] { centre, start + k, start + (k + 1) % count });
            }
            return b.Build();
        }

        // the wrist button: a 3.5 cm puck with a bevelled teal top
        private static Mesh Puck()
        {
            var b = new Builder();
            b.Lathe(0, new[] { new Vector2(0f, 0f), new Vector2(.0175f, 0f), new Vector2(.0175f, .006f), new Vector2(.016f, .0075f) });
            b.Lathe(1, new[] { new Vector2(.016f, .0075f), new Vector2(.014f, .009f), new Vector2(0f, .0095f) });
            return b.Build();
        }

        // the submit button: a 16 x 6 cm housing with a red pill-shaped key standing 1.2 cm proud, facing +Z
        private static Mesh Submit()
        {
            var b = new Builder();
            b.Slab(.16f, .06f, .01f, -.01f, 0f, 0);
            b.Slab(.14f, .044f, .022f, 0f, .012f, 1, 1);
            return b.Build();
        }
    }
}
