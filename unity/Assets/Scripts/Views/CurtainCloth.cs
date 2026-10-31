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

        public Node[] Nodes => nodes;
        public float Open { get => open; set => open = Mathf.Clamp01(value); }
        public Mesh OriginalMesh => originalMesh;

        public void Configure(Node[] n, Vector3 gatherDir, float length, Mesh original) { nodes = n; gatherDirectionLocal = gatherDir.normalized; trackLength = length; originalMesh = original; }

        public float MaxLag()
        {
            float m = 0;
            foreach (var n in nodes) if (n.Ready) m = Mathf.Max(m, (n.Pos - RestWorld(n)).magnitude);
            return m;
        }

        // Opening moves each column toward the gathered end in proportion to its distance from it, so the spacing compresses and the folds bunch up; the top row follows the track exactly.
        private Vector3 SlideOffset(Node n) => gatherDirectionLocal * (open * trackLength * .82f * n.Column);
        private Vector3 RestWorld(Node n) => transform.TransformPoint(n.RestLocal + SlideOffset(n));

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
                n.Bone.position = n.Pos; n.Bone.rotation = transform.rotation;
            }
        }
    }
}
