using UnityEngine;

namespace ProcSense.VR.Views
{
    /// <summary>
    /// P3-ANI-07: two hand actions for any Rocketbox staff member, laid over the Animator's pose in LateUpdate: <see cref="HandOver"/> (the right hand reaches out in front, holds, comes back) and <see cref="Write"/>
    /// (the right hand scribbles low in front of the body). Cosmetic: it only moves the right arm and decides nothing clinical.
    /// </summary>
    [DefaultExecutionOrder(150)]
    public sealed class StaffArmGesture : MonoBehaviour
    {
        public enum Kind { None, HandOver, Write }
        [SerializeField] private Vector3 handOverSpot = new Vector3(.12f, 1.2f, .5f);   // body space: x right, y up, z forward (metres)
        [SerializeField] private Vector3 writeSpot = new Vector3(.08f, 1.0f, .36f);
        [Tooltip("Writes when the engine accepts this action (e.g. escalate, request_transport). Empty: never.")] public string writeOn = "";
        [Tooltip("Hands something over when the engine accepts this action. Empty: never.")] public string handOverOn = "";
        [Tooltip("Sitting at a desk: the hand works lower.")] public bool seated;
        private ProcSense.VR.Bridge.SimulationHost host;
        private Transform upper, fore, hand, root;
        private Kind kind; private float length, clock;

        public Kind Current => kind;
        public bool Active => kind != Kind.None;

        public void HandOver(float seconds = 2.5f) => Begin(Kind.HandOver, seconds);
        public void Write(float seconds = 3f) => Begin(Kind.Write, seconds);
        private void Begin(Kind k, float seconds) { kind = k; length = Mathf.Max(.5f, seconds); clock = 0; }

        /// <summary>How far into the gesture the arm is, 0 to 1: eases in over half a second, holds, eases out over the last half second.</summary>
        public static float Weight(float clock, float length)
        {
            float edge = Mathf.Min(.5f, length / 3f);                        // seconds to raise and to lower the arm
            float w = Mathf.Clamp01(Mathf.Min(clock / edge, (length - clock) / edge));
            return w * w * (3f - 2f * w);
        }

        private static Transform Find(Transform r, string n) { foreach (var t in r.GetComponentsInChildren<Transform>(true)) if (t.name == n) return t; return null; }

        private void Awake()
        {
            root = transform;
            upper = Find(transform, "Bip01 R UpperArm"); fore = Find(transform, "Bip01 R Forearm"); hand = Find(transform, "Bip01 R Hand");
        }

        private void OnEnable() { host = ProcSense.VR.Bridge.SimulationHost.Instance; if (host != null) host.Applied += OnApplied; }
        private void OnDisable() { if (host != null) host.Applied -= OnApplied; host = null; }

        private void Update() { if (host == null && ProcSense.VR.Bridge.SimulationHost.Instance != null) OnEnable(); }

        /// <summary>An accepted action may start a gesture. Public for tests.</summary>
        public void OnApplied(ProcSense.Core.Actions.SimAction action, ProcSense.Core.Actions.ActionOutcome outcome)
        {
            if (outcome == null || !outcome.Accepted || Active) return;
            if (writeOn.Length > 0 && action.Verb == writeOn) Write(); else if (handOverOn.Length > 0 && action.Verb == handOverOn) HandOver();
        }

        private void LateUpdate()
        {
            if (kind == Kind.None || upper == null || fore == null || hand == null) return;
            clock += Time.deltaTime;
            if (clock >= length) { kind = Kind.None; return; }
            var spot = kind == Kind.HandOver ? handOverSpot : writeSpot;
            if (seated) spot.y -= .25f;
            if (kind == Kind.Write) { spot.x += Mathf.Sin(clock * 14f) * .02f; spot.z += Mathf.Sin(clock * 9f + 1f) * .012f; }
            var target = root.TransformPoint(spot);
            TwoBoneArm.Reach(upper, fore, hand, target, root.right + Vector3.down * .5f, Weight(clock, length));
        }
    }
}
