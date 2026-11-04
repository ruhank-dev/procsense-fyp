using System;
using UnityEngine;

namespace ProcSense.VR.Views
{
    /// <summary>
    /// Lightweight curtain cloth. A curtain mesh is skinned to a grid of secondary bones (authored once by CurtainRigBuilder); each bone is a damped-spring pendulum hanging from the track,
    /// so a nudge, a hand brushing the fabric or an open/close slide makes the folds swing and settle under gravity. Open (0 closed .. 1 fully gathered) slides the columns toward the
    /// gathered end along the track. Cosmetic only: no clinical state, no actions; a handful of vector operations per bone, no allocations.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-10)]
    public sealed class CurtainCloth : MonoBehaviour
    {
        [Serializable]
        public sealed class Node
        {
            public Transform Bone;
            public Vector3 RestLocal;              // rest position in this object's space
            [Range(0f, 1f)] public float Column;    // 0..1 along the track, from the gathered end
            [Range(0f, 1f)] public float Looseness; // 0 at the track, 1 at the hem
            public float Pleat;                     // path mode: +1 / -1 alternating by column, the sign of the accordion fold when the curtain is gathered
            [NonSerialized] public Vector3 Pos, Prev;
            [NonSerialized] public bool Ready;
        }

        [SerializeField] private Node[] nodes = Array.Empty<Node>();
        [SerializeField] private Vector3 gatherDirectionLocal = Vector3.right;   // toward the gathered end
        [SerializeField, Min(0f)] private float trackLength = 1.2f;
        [SerializeField, Range(0f, 1f)] private float open;
        [SerializeField, Min(0f)] private float stiffness = 14f;
        [SerializeField, Range(0f, .5f)] private float damping = .07f;
        [SerializeField, Min(0f)] private float maxOffset = .30f;
        [SerializeField] private Mesh originalMesh;
        [SerializeField] private Vector3[] pathPoints = Array.Empty<Vector3>();   // path mode: the track centre line in this object's space (bed privacy curtain: a U)
        [SerializeField] private float[] pathArc = Array.Empty<float>();
        [SerializeField, Range(0f, 1f)] private float gatherFraction = .80f;      // how much of the track length the fabric gives up when fully gathered
        [SerializeField, Min(0f)] private float accordion = .085f;                // metres each fold swings out of the track line when gathered

        public Node[] Nodes => nodes;
        public float Open { get => open; set => open = Mathf.Clamp01(value); }
        public Mesh OriginalMesh => originalMesh;
        public float GatherFraction { get => gatherFraction; set => gatherFraction = Mathf.Clamp(value, 0f, .97f); }
        public float Accordion { get => accordion; set => accordion = Mathf.Max(0f, value); }

        public void Configure(Node[] n, Vector3 gatherDir, float length, Mesh original) { nodes = n; gatherDirectionLocal = gatherDir.normalized; trackLength = length; originalMesh = original; }

        public bool UsesPath => pathPoints.Length > 1;
        public float TrackLength => trackLength;

        /// <summary>Switches to path mode: column S of the closed curtain lies at arc length S along <paramref name="points"/>; gathering slides the columns back along the same path toward its start.</summary>
        public void ConfigurePath(Vector3[] points)
        {
            pathPoints = points; pathArc = new float[points.Length];
            for (int i = 1; i < points.Length; i++) pathArc[i] = pathArc[i - 1] + Vector3.Distance(points[i], points[i - 1]);
            trackLength = pathArc[points.Length - 1];
        }

        private void SamplePath(float s, out Vector3 p, out Vector3 tangent)
        {
            s = Mathf.Clamp(s, 0f, pathArc[pathArc.Length - 1]);
            int i = Array.BinarySearch(pathArc, s); if (i < 0) i = ~i - 1; i = Mathf.Clamp(i, 0, pathPoints.Length - 2);
            float seg = Mathf.Max(1e-5f, pathArc[i + 1] - pathArc[i]);
            p = Vector3.Lerp(pathPoints[i], pathPoints[i + 1], (s - pathArc[i]) / seg);
            tangent = (pathPoints[i + 1] - pathPoints[i]).normalized;
        }

        private float PathS(Node n) => n.Column * trackLength * (1f - gatherFraction * open);

        private Vector3 PathRestLocal(Node n)
        {
            SamplePath(PathS(n), out var p, out var t);
            var left = new Vector3(-t.z, 0f, t.x);                                      // toward the bed, the inside of the U
            p += left * (n.Pleat * open * accordion / Mathf.Max(1e-4f, transform.lossyScale.x));
            p.y = n.RestLocal.y; return p;
        }

        private Quaternion PathRestRotation(Node n)
        {
            SamplePath(n.Column * trackLength, out _, out var t0); SamplePath(PathS(n), out _, out var t1);
            return transform.rotation * Quaternion.AngleAxis(Vector3.SignedAngle(t0, t1, Vector3.up), Vector3.up);
        }

        public float MaxLag()
        {
            float m = 0;
            foreach (var n in nodes) if (n.Ready) m = Mathf.Max(m, (n.Pos - RestWorld(n)).magnitude);
            return m;
        }

        // Opening moves each column toward the gathered end in proportion to its distance from it, so the spacing compresses and the folds bunch up; the top row follows the track exactly.
        private Vector3 SlideOffset(Node n) => gatherDirectionLocal * (open * trackLength * .82f * n.Column);
        private Vector3 RestWorld(Node n) => UsesPath ? transform.TransformPoint(PathRestLocal(n)) : transform.TransformPoint(n.RestLocal + SlideOffset(n));

        /// <summary>Pushes the fabric near <paramref name="worldPoint"/> (hand, passing person, wind); strength in metres of initial displacement.</summary>
        public void Nudge(Vector3 worldPoint, float radius, Vector3 worldDirection, float strength)
        {
            foreach (var n in nodes)
            {
                if (!n.Ready) continue;
                float d = Vector3.Distance(n.Pos, worldPoint); if (d > radius) continue;
                float k = (1f - d / radius) * n.Looseness * strength;
                n.Pos += worldDirection.normalized * k;
            }
        }

        private void OnEnable() { foreach (var n in nodes) n.Ready = false; }
        private void LateUpdate() => Step(Mathf.Min(Time.deltaTime, .04f));

        public void Step(float dt)
        {
            if (dt <= 0f) return;
            const int sub = 2; float h = dt / sub;
            foreach (var n in nodes)
            {
                if (n.Bone == null) continue;
                var rest = RestWorld(n);
                if (!n.Ready) { n.Pos = n.Prev = rest; n.Ready = true; }
                for (int s = 0; s < sub; s++)
                {
                    rest = RestWorld(n);
                    var vel = (n.Pos - n.Prev) * (1f - damping); n.Prev = n.Pos;
                    n.Pos += vel;
                    n.Pos += (rest - n.Pos) * (1f - Mathf.Exp(-stiffness * (1.05f - .75f * n.Looseness) * h));
                    var off = n.Pos - rest; if (off.sqrMagnitude > maxOffset * maxOffset) n.Pos = rest + off.normalized * maxOffset;
                }
                n.Bone.position = n.Pos; n.Bone.rotation = UsesPath ? PathRestRotation(n) : transform.rotation;
            }
        }
    }
}
