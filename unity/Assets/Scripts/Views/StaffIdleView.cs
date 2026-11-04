using System;
using System.Collections.Generic;
using UnityEngine;

namespace ProcSense.VR.Views
{
    /// <summary>
    /// Presentation-only life for the hospital staff (receptionist and the two corridor nurses): breathing, blinking, small head movements, weight shifts, a restrained hand gesture, and for the
    /// nurses a silent conversation in which they take turns speaking (jaw and lip shapes, a nod from the listener). Everything is a small, smooth offset from the authored pose, evaluated from time
    /// alone (no random state), so it is deterministic and testable through <see cref="Evaluate"/>. It reads no clinical state and sends no actions.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class StaffIdleView : MonoBehaviour
    {
        public enum Role { Receptionist, NurseSpeakingFirst, NurseListeningFirst, CivilianWaiting, CivilianPhone, CivilianReading, CivilianTalking, CivilianCough, CivilianLookAround }

        /// <summary>Seconds between evaluations. 0 on desktop (every frame); the Quest build sets it to 0.1 so a crowd of idle people costs a tenth of the skeleton work (presentation only).</summary>
        public static float UpdateInterval =
#if UNITY_ANDROID && !UNITY_EDITOR
            0.1f;
#else
            0f;
#endif
        private float lastEval = -10f;
        [SerializeField, Tooltip("The Animator drives the skeleton (retargeted motion-capture clips); only blinking and speech blend shapes are added here.")] private bool facialOnly;
        public bool FacialOnly { get => facialOnly; set => facialOnly = value; }

        [SerializeField] private Role role = Role.NurseSpeakingFirst;
        [SerializeField, Tooltip("Seconds added to the clock so two people never move in step.")] private float phase;
        [SerializeField, Range(0f, 2f)] private float amount = 1f;

        private sealed class Bone { public Transform T; public Quaternion RestLocal; public int Depth; }
        private readonly List<Bone> bones = new List<Bone>();
        private readonly Dictionary<string, Bone> byName = new Dictionary<string, Bone>();
        private readonly List<(SkinnedMeshRenderer r, int left, int right, int jaw, int funnel, int smileL, int smileR)> faces = new List<(SkinnedMeshRenderer, int, int, int, int, int, int)>();
        private static readonly string[] Names =
        {
            "pelvis", "spine_01", "spine_02", "spine_03", "clavicle_l", "clavicle_r", "neck_01", "head", "upperarm_l", "upperarm_r", "lowerarm_l", "lowerarm_r", "hand_l", "hand_r",
        };
        private bool ready;

        public Role PersonRole { get => role; set => role = value; }
        public float Phase { get => phase; set => phase = value; }
        public int FaceSurfaces => faces.Count;

        private void Awake() => Cache();

        private void Cache()
        {
            if (ready) return;
            foreach (var t in GetComponentsInChildren<Transform>(true))
            {
                if (Array.IndexOf(Names, t.name) < 0 || byName.ContainsKey(t.name)) continue;
                int depth = 0; for (var p = t; p != transform && p != null; p = p.parent) depth++;
                var b = new Bone { T = t, RestLocal = t.localRotation, Depth = depth }; bones.Add(b); byName[t.name] = b;
            }
            bones.Sort((a, c) => a.Depth.CompareTo(c.Depth));
            foreach (var r in GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                var m = r.sharedMesh; if (m == null) continue;
                int l = m.GetBlendShapeIndex("eyeBlinkLeft"), rt = m.GetBlendShapeIndex("eyeBlinkRight");
                if (l < 0 && rt < 0) continue;
                faces.Add((r, l, rt, m.GetBlendShapeIndex("jawOpen"), m.GetBlendShapeIndex("mouthFunnel"), m.GetBlendShapeIndex("mouthSmileLeft"), m.GetBlendShapeIndex("mouthSmileRight")));
            }
            ready = true;
        }

        private void LateUpdate() { if (UpdateInterval > 0f && Time.time - lastEval < UpdateInterval) return; lastEval = Time.time; Evaluate(Time.time); }

        /// <summary>Largest rotation (degrees) the animation currently applies to any bone, for tests and reviews.</summary>
        public float MaxOffsetDegrees { get; private set; }
        /// <summary>Current eye-blink weight (0..100) on the first face surface.</summary>
        public float BlinkWeight { get; private set; }
        /// <summary>Current jaw-open weight (0..100) used for speech.</summary>
        public float JawWeight { get; private set; }

        private void Rot(string bone, Vector3 axis, float degrees)
        {
            if (facialOnly || !byName.TryGetValue(bone, out var b)) return;
            b.T.rotation = Quaternion.AngleAxis(degrees * amount, axis) * b.T.rotation;
        }

        private static float Pulse(float t, float start, float length) { float u = (t - start) / length; return u <= 0f || u >= 1f ? 0f : Mathf.Sin(u * Mathf.PI); }

        /// <summary>Applies the animation for clock time <paramref name="time"/> seconds on top of the authored pose.</summary>
        public void Evaluate(float time)
        {
            Cache(); float t = time + phase;
            if (!facialOnly) foreach (var b in bones) b.T.localRotation = b.RestLocal;
            Vector3 right = transform.right, up = transform.up, fwd = transform.forward;

            // breathing and a slow postural drift
            float breath = Mathf.Sin(t * Mathf.PI * 2f / 4.4f);
            Rot("spine_03", right, .7f * breath); Rot("spine_02", right, .3f * breath); Rot("clavicle_l", up, .35f * breath); Rot("clavicle_r", up, -.35f * breath);
            Rot("pelvis", fwd, .9f * Mathf.Sin(t * Mathf.PI * 2f / 12.5f)); Rot("spine_02", fwd, -.5f * Mathf.Sin(t * Mathf.PI * 2f / 12.5f));      // slow weight shift
            // head: small sway, never more than a couple of degrees
            Rot("head", up, 1.6f * Mathf.Sin(t * Mathf.PI * 2f / 7.3f)); Rot("head", right, 1.0f * Mathf.Sin(t * Mathf.PI * 2f / 5.1f + 1.3f)); Rot("neck_01", fwd, .8f * Mathf.Sin(t * Mathf.PI * 2f / 9.7f));

            float jaw = 0f, funnel = 0f, smile = 0f;
            if (role == Role.Receptionist)
            {
                float typing = Mathf.Clamp01(Mathf.Sin(t * Mathf.PI * 2f / 13f) * 1.6f);                                   // bursts of typing, then a pause
                Rot("hand_l", right, 1.6f * typing * Mathf.Sin(t * 33f)); Rot("hand_r", right, 1.6f * typing * Mathf.Sin(t * 29f + 1f));
                Rot("lowerarm_l", right, .5f * typing * Mathf.Sin(t * 11f)); Rot("lowerarm_r", right, .5f * typing * Mathf.Sin(t * 9f + 2f));
                float glance = Pulse(Mathf.Repeat(t, 17f), 12f, 2.2f);                                                      // now and then she looks up toward the corridor
                Rot("head", up, 7f * glance); Rot("head", right, -4f * glance); Rot("neck_01", up, 3f * glance);
                smile = 28f * Mathf.Max(glance, .25f);
            }
            else if (role >= Role.CivilianWaiting)
            {
                // civilians waiting in the corridor: each role adds one small, slow behaviour on top of breathing and sway; the per-person phase keeps them out of step
                float glance = Pulse(Mathf.Repeat(t, 19f), 11f, 3.2f);
                if (role == Role.CivilianPhone)
                {
                    Rot("head", right, 3f * Mathf.Sin(t * .9f) + 2f * Pulse(Mathf.Repeat(t, 14f), 9f, 1.2f)); Rot("hand_r", right, 2.2f * Mathf.Sin(t * 7.1f) * Mathf.Clamp01(Mathf.Sin(t * .4f) * 2f)); Rot("head", right, -14f * glance); smile = 12f;
                }
                else if (role == Role.CivilianReading)
                {
                    float line = Mathf.Repeat(t, 6f) / 6f; Rot("head", up, (line < .85f ? Mathf.Lerp(-5f, 5f, line / .85f) : Mathf.Lerp(5f, -5f, (line - .85f) / .15f))); Rot("head", right, -6f * glance);
                }
                else if (role == Role.CivilianTalking)
                {
                    float cycle = Mathf.Repeat(t, 11f); bool speaking = cycle < 6f; float env = speaking ? Mathf.Clamp01(Mathf.Min(cycle, 6f - cycle) * 2.2f) : 0f;
                    jaw = 20f * env * (.5f + .5f * Mathf.Sin(t * 12.3f) * Mathf.Sin(t * 5.7f + 1f)); funnel = 10f * env * Mathf.Clamp01(Mathf.Sin(t * 4.1f)); smile = speaking ? 16f : 30f;
                    float gesture = Mathf.Clamp01(Mathf.Sin(t * 2.4f) * 1.3f) * env; Rot("lowerarm_r", up, 5f * gesture * Mathf.Sin(t * 3.1f)); Rot("hand_r", fwd, 6f * gesture * Mathf.Sin(t * 4.2f)); Rot("head", right, 2f * env * Mathf.Sin(t * 5.3f));
                }
                else if (role == Role.CivilianCough)
                {
                    float c = Mathf.Repeat(t, 27f); float burst = c > 20f && c < 22.2f ? Mathf.Sin((c - 20f) * Mathf.PI * 3.2f) * Mathf.Sin((c - 20f) / 2.2f * Mathf.PI) : 0f;           // three short coughs every 27 s
                    Rot("spine_02", right, 5.5f * Mathf.Abs(burst)); Rot("head", right, 4f * Mathf.Abs(burst)); Rot("lowerarm_r", up, 3f * burst); jaw = 18f * Mathf.Abs(burst);
                }
                else if (role == Role.CivilianLookAround)
                {
                    Rot("head", up, 14f * Mathf.Sin(t * .27f) * Mathf.Clamp01(Mathf.Sin(t * .11f) * 3f)); Rot("neck_01", up, 5f * Mathf.Sin(t * .27f));
                }
                else { Rot("head", up, 5f * Pulse(Mathf.Repeat(t, 23f), 15f, 3f) - 4f * Pulse(Mathf.Repeat(t, 31f), 22f, 2.5f)); Rot("spine_03", fwd, 1.2f * Mathf.Sin(t * .31f)); }
            }
            else
            {
                // turn taking: 14 s cycle, each speaks for about 6.5 s, with a short pause between
                float cycle = Mathf.Repeat(t + (role == Role.NurseSpeakingFirst ? 0f : 7f), 14f);
                bool speaking = cycle < 6.5f;
                float env = speaking ? Mathf.Clamp01(Mathf.Min(cycle, 6.5f - cycle) * 2.2f) : 0f;
                float syll = .5f + .5f * Mathf.Sin(t * 13.1f) * Mathf.Sin(t * 5.3f + 1f);
                jaw = 22f * env * syll; funnel = 12f * env * Mathf.Clamp01(Mathf.Sin(t * 4.1f)); smile = speaking ? 18f : 34f;
                float gesture = speaking ? Mathf.Clamp01(Mathf.Sin(t * 2.6f) * 1.4f) * env : 0f;
                Rot("head", right, 2.2f * env * Mathf.Sin(t * 5.7f)); Rot("head", up, 1.4f * env * Mathf.Sin(t * 3.1f));
                float nod = !speaking ? Pulse(Mathf.Repeat(t + 2f, 5.5f), 0f, .9f) : 0f;                                     // the listener nods now and then
                Rot("head", right, 5.5f * nod); Rot("neck_01", right, 2f * nod);
                if (role == Role.NurseListeningFirst) { Rot("lowerarm_l", up, 4.5f * gesture * Mathf.Sin(t * 3.3f)); Rot("hand_l", fwd, 7f * gesture * Mathf.Sin(t * 4.4f + .5f)); Rot("upperarm_l", fwd, 2.2f * gesture); }
                else { Rot("clavicle_r", up, 1.2f * env * Mathf.Sin(t * 2.1f)); Rot("hand_r", right, 2.5f * gesture * Mathf.Sin(t * 3.7f)); }
            }

            // blinking: a quick close and open every 4-5 seconds, different per person
            float period = 4.3f + .9f * Mathf.Sin(phase * 1.7f + 1f); float bp = Mathf.Repeat(t + phase * 3.1f, period);
            float blink = 100f * Pulse(bp, 0f, .17f);
            BlinkWeight = blink; JawWeight = jaw;
            foreach (var f in faces)
            {
                if (f.left >= 0) f.r.SetBlendShapeWeight(f.left, blink);
                if (f.right >= 0) f.r.SetBlendShapeWeight(f.right, blink);
                if (f.jaw >= 0) f.r.SetBlendShapeWeight(f.jaw, jaw);
                if (f.funnel >= 0) f.r.SetBlendShapeWeight(f.funnel, funnel);
                if (f.smileL >= 0) f.r.SetBlendShapeWeight(f.smileL, smile); if (f.smileR >= 0) f.r.SetBlendShapeWeight(f.smileR, smile);
            }
            float max = 0f; if (!facialOnly) foreach (var b in bones) max = Mathf.Max(max, Quaternion.Angle(b.T.localRotation, b.RestLocal)); MaxOffsetDegrees = max;
        }

        private void OnDisable() { if (!ready || facialOnly) return; foreach (var b in bones) if (b.T != null) b.T.localRotation = b.RestLocal; foreach (var f in faces) if (f.r != null) for (int i = 0; i < f.r.sharedMesh.blendShapeCount; i++) f.r.SetBlendShapeWeight(i, 0f); }
    }
}
