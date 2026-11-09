using System.Collections.Generic;
using UnityEngine;

namespace ProcSense.VR.Views
{
    /// <summary>
    /// Idle life for the clothed Rocketbox patient lying on the bed (the avatar's own bones and ARKit-style blend shapes, no animation clip needed): the chest rises and falls with the breath, both eyelids blink on a random
    /// rhythm, the eyes drift a little, the head turns slightly now and then and a foot shifts once in a while. Everything is a small offset on top of the pose the avatar was laid in, so the body stays on the mattress.
    /// To pose him by hand: set <see cref="gazeOffset"/> (eye direction, -1..1), <see cref="headEuler"/>, <see cref="blinkHold"/> (keeps the eyes shut) or call <see cref="SetBoneEuler"/> for any bone by name; <see cref="autoMotion"/> off freezes the automatic part.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PatientMotion : MonoBehaviour
    {
        [Header("Automatic")]
        public bool autoMotion = true;
        [Min(4)] public float breathsPerMinute = 14f;
        [Range(0f, 4f)] public float breathDegrees = 1.0f;
        [Range(0f, .1f), Tooltip("Chest depth change per breath (front to back, i.e. up toward the ceiling when lying): the chest wall itself rises and falls.")] public float breathDepth = .05f;
        [Range(0f, 8f)] public float headDriftDegrees = 3f;

        [Header("Manual (added on top)")]
        public Vector2 gazeOffset;
        public Vector3 headEuler;
        [Range(0f, 1f)] public float blinkHold;

        private SkinnedMeshRenderer face; private int blinkL = -1, blinkR = -1, lookInL = -1, lookOutL = -1, lookInR = -1, lookOutR = -1, lookUpL = -1, lookUpR = -1, lookDownL = -1, lookDownR = -1;
        private Transform spine1, spine2, head, neck, footL, footR;
        private Quaternion spine1Rest, spine2Rest, headRest, footLRest, footRRest;
        private Vector3 spine1Scale, spine2Scale; private int apAxis1, apAxis2; private Vector3 breathAxis1, breathAxis2, headAxisYaw, headAxisRoll, footAxis;
        private float breathSign = 1f, nextBlink, blinkStart = -10f, seed, nextFoot = 12f, footStart = -10f; private int footSide;
        private readonly Dictionary<Transform, (Quaternion rest, Vector3 euler)> manual = new Dictionary<Transform, (Quaternion, Vector3)>();

        private static int Dominant(Vector3 v) { v = new Vector3(Mathf.Abs(v.x), Mathf.Abs(v.y), Mathf.Abs(v.z)); return v.x >= v.y && v.x >= v.z ? 0 : v.y >= v.z ? 1 : 2; }

        private static Transform Find(Transform root, string n) { foreach (var t in root.GetComponentsInChildren<Transform>(true)) if (t.name == n) return t; return null; }
        private static int Shape(SkinnedMeshRenderer r, string suffix) { for (int i = 0; i < r.sharedMesh.blendShapeCount; i++) if (r.sharedMesh.GetBlendShapeName(i).EndsWith(suffix)) return i; return -1; }

        private void Awake()
        {
            seed = Random.value * 100f;
            foreach (var r in GetComponentsInChildren<SkinnedMeshRenderer>(true)) if (r.sharedMesh != null && Shape(r, "EyeBlinkLeft") >= 0) face = r;
            if (face != null)
            {
                blinkL = Shape(face, "EyeBlinkLeft"); blinkR = Shape(face, "EyeBlinkRight"); lookInL = Shape(face, "EyeLookInLeft"); lookOutL = Shape(face, "EyeLookOutLeft"); lookInR = Shape(face, "EyeLookInRight"); lookOutR = Shape(face, "EyeLookOutRight");
                lookUpL = Shape(face, "EyeLookUpLeft"); lookUpR = Shape(face, "EyeLookUpRight"); lookDownL = Shape(face, "EyeLookDownLeft"); lookDownR = Shape(face, "EyeLookDownRight");
            }
            spine1 = Find(transform, "Bip01 Spine1"); spine2 = Find(transform, "Bip01 Spine2"); head = Find(transform, "Bip01 Head"); neck = Find(transform, "Bip01 Neck"); footL = Find(transform, "Bip01 L Foot"); footR = Find(transform, "Bip01 R Foot");
            var thighL = Find(transform, "Bip01 L Thigh"); var thighR = Find(transform, "Bip01 R Thigh");
            if (spine1 == null || spine2 == null || head == null || neck == null) { enabled = false; return; }
            spine1Rest = spine1.localRotation; spine2Rest = spine2.localRotation; headRest = head.localRotation; if (footL) footLRest = footL.localRotation; if (footR) footRRest = footR.localRotation;
            spine1Scale = spine1.localScale; spine2Scale = spine2.localScale;
            apAxis1 = Dominant(spine1.InverseTransformDirection(Vector3.up)); apAxis2 = Dominant(spine2.InverseTransformDirection(Vector3.up));                  // lying on the back: the body's front-to-back axis is world up
            var lateral = thighL != null && thighR != null ? (thighR.position - thighL.position).normalized : transform.right;
            breathAxis1 = spine1.InverseTransformDirection(lateral); breathAxis2 = spine2.InverseTransformDirection(lateral);
            // which way is "chest up"? rotate the upper body by +1 degree about the lateral axis and see whether the head goes up or down (world +y)
            float y0 = head.position.y; spine1.localRotation = spine1Rest * Quaternion.AngleAxis(1f, breathAxis1); float y1 = head.position.y; spine1.localRotation = spine1Rest; breathSign = y1 >= y0 ? 1f : -1f;
            headAxisYaw = head.InverseTransformDirection((head.position - neck.position).normalized); headAxisRoll = head.InverseTransformDirection(lateral);
            footAxis = footL != null ? footL.InverseTransformDirection(lateral) : Vector3.right;
            nextBlink = Time.time + Random.Range(1.5f, 4f);
        }

        /// <summary>Rotates a bone (looked up by name, e.g. "Bip01 L Hand") by the given Euler offset on top of its resting pose.</summary>
        public void SetBoneEuler(string boneName, Vector3 euler)
        {
            var t = Find(transform, boneName); if (t == null) return;
            if (!manual.ContainsKey(t)) manual[t] = (t.localRotation, euler); else manual[t] = (manual[t].rest, euler);
        }

        private void SetShape(int idx, float w) { if (face != null && idx >= 0) CharacterPerfBlend.Set(face, idx, Mathf.Clamp(w, 0f, 100f)); }

        private void LateUpdate()
        {
            float t = Time.time;
            float breath = autoMotion ? Mathf.Sin(t * breathsPerMinute / 60f * Mathf.PI * 2f) : 0f;
            float rise = breath * (.75f + .25f * Mathf.Sin(t * .31f + seed));                                              // depth varies a little from breath to breath
            spine1.localRotation = spine1Rest * Quaternion.AngleAxis(breathSign * breathDegrees * .6f * rise, breathAxis1);
            spine2.localRotation = spine2Rest * Quaternion.AngleAxis(breathSign * breathDegrees * rise, breathAxis2);

            var sc1 = spine1Scale; sc1[apAxis1] *= 1f + breathDepth * .6f * rise; spine1.localScale = sc1;                                    // the chest wall rises and falls
            var sc2 = spine2Scale; sc2[apAxis2] *= 1f + breathDepth * rise; spine2.localScale = sc2;

            float yaw = autoMotion ? (Mathf.PerlinNoise(seed, t * .05f) - .5f) * 2f * headDriftDegrees : 0f, roll = autoMotion ? (Mathf.PerlinNoise(seed + 9f, t * .04f) - .5f) * headDriftDegrees * .6f : 0f;
            head.localRotation = headRest * Quaternion.AngleAxis(yaw, headAxisYaw) * Quaternion.AngleAxis(roll, headAxisRoll) * Quaternion.Euler(headEuler);

            if (autoMotion && t >= nextBlink) { blinkStart = t; nextBlink = t + Random.Range(2.2f, 6.5f); if (Random.value < .15f) nextBlink = t + .35f; }       // now and then a quick double blink
            float b = 0f; float k = t - blinkStart; if (k >= 0f && k < .18f) b = k < .06f ? k / .06f : 1f - (k - .06f) / .12f;
            b = Mathf.Max(b, blinkHold); SetShape(blinkL, b * 100f); SetShape(blinkR, b * 100f);

            float gx = (autoMotion ? (Mathf.PerlinNoise(seed + 3f, t * .12f) - .5f) * 1.2f : 0f) + gazeOffset.x, gy = (autoMotion ? (Mathf.PerlinNoise(seed + 5f, t * .1f) - .5f) * .8f : 0f) + gazeOffset.y;
            SetShape(lookOutL, Mathf.Max(0f, -gx) * 40f); SetShape(lookInL, Mathf.Max(0f, gx) * 40f); SetShape(lookInR, Mathf.Max(0f, -gx) * 40f); SetShape(lookOutR, Mathf.Max(0f, gx) * 40f);
            SetShape(lookUpL, Mathf.Max(0f, gy) * 40f); SetShape(lookUpR, Mathf.Max(0f, gy) * 40f); SetShape(lookDownL, Mathf.Max(0f, -gy) * 40f); SetShape(lookDownR, Mathf.Max(0f, -gy) * 40f);

            if (autoMotion && footL != null && footR != null)                                                               // a foot shifts every 15 to 40 seconds, slowly out and back
            {
                if (t >= nextFoot) { footStart = t; nextFoot = t + Random.Range(15f, 40f); footSide = Random.value < .5f ? 0 : 1; }
                float f = t - footStart; float w = f >= 0f && f < 3f ? Mathf.Sin(f / 3f * Mathf.PI) : 0f; var tr = footSide == 0 ? footL : footR; var rest = footSide == 0 ? footLRest : footRRest;
                (footSide == 0 ? footR : footL).localRotation = footSide == 0 ? footRRest : footLRest; tr.localRotation = rest * Quaternion.AngleAxis(7f * w, footAxis);
            }
            foreach (var kv in manual) kv.Key.localRotation = kv.Value.rest * Quaternion.Euler(kv.Value.euler);
        }
    }
}
