using System;
using UnityEngine;

namespace ProcSense.VR.Views
{
    /// <summary>
    /// Cosmetic secondary motion for the patient's gown (skirt hem and sleeve cuffs). The gown mesh keeps its normal skeletal skinning; extra "secondary" bones, authored once by the editor
    /// (GownRigBuilder), carry the weight of the loose cloth. Each node simulates one point of the garment with a damped spring toward the place the skeleton would carry it, gravity,
    /// a push-out from the leg capsules and a floor at the mattress surface, so a bending or rotating leg makes the fabric lag, swing, fold over and settle.
    /// It is deliberately not a full cloth solver (cheap, stable and deterministic on Quest-class CPUs): it reads no clinical state and sends no actions.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-10)]
    public sealed class GownSecondaryMotion : MonoBehaviour
    {
        [Serializable]
        public sealed class Node
        {
            public Transform Bone;                 // the secondary skin bone (child of this object)
            public Transform Driver;               // original skeleton bone that would carry this point
            public Vector3 DriverLocalPoint;       // rest position of the point in the driver's space
            [Range(0.02f, 1f)] public float Looseness = .5f;   // 0 = follows the skeleton rigidly, 1 = free hem
            [NonSerialized] public Vector3 Pos, Prev;
            [NonSerialized] public bool Ready;
            [NonSerialized] public float[] Slack;     // per capsule: how far inside it the accepted rest fit already sits (never pushed out at rest)
            [NonSerialized] public float FloorSlack;  // how far below the mattress limit the accepted rest fit sits
        }

        [Serializable]
        public sealed class Capsule { public Transform A, B; public float Radius; }

        [SerializeField] private Node[] nodes = Array.Empty<Node>();
        [SerializeField] private Capsule[] capsules = Array.Empty<Capsule>();
        [SerializeField, Tooltip("World height of the mattress top; cloth cannot sink below it.")] private float surfaceY = .70f;
        [SerializeField, Min(0f)] private float stiffness = 38f;
        [SerializeField, Range(0f, .5f)] private float damping = .16f;
        [SerializeField, Min(0f)] private float gravity = 2.6f;
        [SerializeField, Min(0f)] private float maxOffset = .20f;
        [SerializeField] private Mesh originalMesh;
        [SerializeField] private Transform[] originalBones;

        /// <summary>World height below which cloth cannot sink: the mattress while the patient lies on the bed, the floor when the patient stands (tests and reviews set it).</summary>
        public float SurfaceY { get => surfaceY; set => surfaceY = value; }
        public Node[] Nodes => nodes;
        public Mesh OriginalMesh => originalMesh;
        public Transform[] OriginalBones => originalBones;

        public void Configure(Node[] n, Capsule[] c, float mattressTop, Mesh original, Transform[] bones)
        {
            nodes = n; capsules = c; surfaceY = mattressTop; originalMesh = original; originalBones = bones;
        }

        /// <summary>Largest distance (m) any node currently sits from where the skeleton alone would put it; used by tests and reviews.</summary>
        public float MaxLag()
        {
            float m = 0;
            foreach (var n in nodes) if (n.Ready && n.Driver != null) m = Mathf.Max(m, Vector3.Distance(n.Pos, n.Driver.TransformPoint(n.DriverLocalPoint)));
            return m;
        }

        /// <summary>Fastest node speed (m/s) over the last step; near zero means the cloth has settled, wherever it has come to rest.</summary>
        public float MaxSpeed(float dt)
        {
            float m = 0; foreach (var n in nodes) if (n.Ready) m = Mathf.Max(m, (n.Pos - n.Prev).magnitude / Mathf.Max(dt, 1e-4f));
            return m;
        }

        private void OnEnable() { foreach (var n in nodes) n.Ready = false; }

        private void LateUpdate() => Step(Mathf.Min(Time.deltaTime, .04f));

        /// <summary>Advances the simulation (two sub-steps). Public so edit/play tests can drive it with a fixed step.</summary>
        public void Step(float dt)
        {
            if (dt <= 0f) return;
            const int sub = 2; float h = dt / sub;
            foreach (var n in nodes)
            {
                if (n.Bone == null || n.Driver == null) continue;
                var rest = n.Driver.TransformPoint(n.DriverLocalPoint);
                if (!n.Ready)
                {
                    n.Pos = n.Prev = rest; n.Ready = true;
                    n.Slack = new float[capsules.Length];
                    for (int c = 0; c < capsules.Length; c++) n.Slack[c] = Mathf.Max(0f, capsules[c].Radius - Distance(rest, capsules[c]));
                    n.FloorSlack = Mathf.Max(0f, surfaceY + .004f - rest.y);
                }
                for (int s = 0; s < sub; s++)
                {
                    rest = n.Driver.TransformPoint(n.DriverLocalPoint);
                    var vel = (n.Pos - n.Prev) * (1f - damping);
                    n.Prev = n.Pos;
                    n.Pos += vel + Vector3.down * (gravity * n.Looseness * h * h);
                    n.Pos += (rest - n.Pos) * (1f - Mathf.Exp(-stiffness * (1.15f - n.Looseness) * h));
                    var off = n.Pos - rest;
                    if (off.sqrMagnitude > maxOffset * maxOffset) n.Pos = rest + off.normalized * maxOffset;
                    for (int c = 0; c < capsules.Length; c++) PushOut(ref n.Pos, capsules[c], n.Slack[c]);
                    float floor = surfaceY + .004f - n.FloorSlack; if (n.Pos.y < floor) n.Pos.y = floor;
                }
                n.Bone.position = n.Driver.position + (n.Pos - rest);
                n.Bone.rotation = n.Driver.rotation;
            }
        }

        private static float Distance(Vector3 p, Capsule c)
        {
            if (c.A == null || c.B == null) return float.MaxValue;
            Vector3 a = c.A.position, ab = c.B.position - a;
            float t = ab.sqrMagnitude < 1e-8f ? 0f : Mathf.Clamp01(Vector3.Dot(p - a, ab) / ab.sqrMagnitude);
            return (p - (a + ab * t)).magnitude;
        }

        private static void PushOut(ref Vector3 p, Capsule c, float slack)
        {
            if (c.A == null || c.B == null) return;
            Vector3 a = c.A.position, b = c.B.position, ab = b - a;
            float t = ab.sqrMagnitude < 1e-8f ? 0f : Mathf.Clamp01(Vector3.Dot(p - a, ab) / ab.sqrMagnitude);
            var nearest = a + ab * t; var d = p - nearest; float dist = d.magnitude; float r = c.Radius - slack;
            if (dist < r) p = nearest + (dist < 1e-5f ? Vector3.up : d / dist) * r;
        }
    }
}
