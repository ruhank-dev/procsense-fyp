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
    [DefaultExecutionOrder(-20)]
    public sealed class PatientMotion : MonoBehaviour
    {
        public Transform Head => head;
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
        private Vector3 spine1Scale, spine2Scale, neckScale, neckRestPos, neckWrotePos; private Quaternion neckRestRot, neckWroteRot; private int apAxis1, apAxis2, apAxisNeck; private Vector3 breathAxis1, breathAxis2, headAxisYaw, headAxisRoll, headAxisTilt, footAxis;
        private Transform armUpper, armFore, armHand, armLeft, pelvis; private Quaternion armUpperRest, armForeRest;
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
            var thighL = Find(transform, "Bip01 L Thigh"); var thighR = Find(transform, "Bip01 R Thigh"); thighLeft = thighL; thighRight = thighR;
            if (spine1 == null || spine2 == null || head == null || neck == null) { enabled = false; return; }
            spine1Rest = spine1.localRotation; spine2Rest = spine2.localRotation; headRest = head.localRotation; if (footL) footLRest = footL.localRotation; if (footR) footRRest = footR.localRotation;
            spine1Scale = spine1.localScale; spine2Scale = spine2.localScale; neckScale = neck.localScale; neckRestPos = neck.localPosition; neckRestRot = neck.localRotation; apAxisNeck = Dominant(neck.InverseTransformDirection(Vector3.up));
            apAxis1 = Dominant(spine1.InverseTransformDirection(Vector3.up)); apAxis2 = Dominant(spine2.InverseTransformDirection(Vector3.up));                  // lying on the back: the body's front-to-back axis is world up
            var lateral = thighL != null && thighR != null ? (thighR.position - thighL.position).normalized : transform.right;
            breathAxis1 = spine1.InverseTransformDirection(lateral); breathAxis2 = spine2.InverseTransformDirection(lateral);
            // which way is "chest up"? rotate the upper body by +1 degree about the lateral axis and see whether the head goes up or down (world +y)
            float y0 = head.position.y; spine1.localRotation = spine1Rest * Quaternion.AngleAxis(1f, breathAxis1); float y1 = head.position.y; spine1.localRotation = spine1Rest; breathSign = y1 >= y0 ? 1f : -1f;
            spineBase = Find(transform, "Bip01 Spine");
            if (spineBase != null)
            {
                spineBaseRest = spineBase.localRotation; sitAxis = spineBase.InverseTransformDirection(lateral);
                float h0 = head.position.y; spineBase.localRotation = spineBaseRest * Quaternion.AngleAxis(1f, sitAxis); float h1 = head.position.y; spineBase.localRotation = spineBaseRest; sitSign = h1 >= h0 ? 1f : -1f;
            }
            headAxisYaw = head.InverseTransformDirection((head.position - neck.position).normalized); headAxisRoll = head.InverseTransformDirection(lateral);
            footAxis = footL != null ? footL.InverseTransformDirection(lateral) : Vector3.right;
            headAxisTilt = Vector3.Cross(headAxisYaw, headAxisRoll).normalized;
            armUpper = Find(transform, "Bip01 R UpperArm"); armFore = Find(transform, "Bip01 R Forearm"); armHand = Find(transform, "Bip01 R Hand"); pelvis = Find(transform, "Bip01 Pelvis"); armLeft = Find(transform, "Bip01 L UpperArm");
            if (armUpper != null && armFore != null && armHand != null) { armUpperRest = armUpper.localRotation; armForeRest = armFore.localRotation; }
            nextBlink = Time.time + Random.Range(1.5f, 4f);
        }

        /// <summary>Runs the set-up Awake does, for the review tool, which poses the patient outside play mode.</summary>
        public void Prepare() => Awake();

        /// <summary>Rotates a bone (looked up by name, e.g. "Bip01 L Hand") by the given Euler offset on top of its resting pose.</summary>
        public void SetBoneEuler(string boneName, Vector3 euler)
        {
            var t = Find(transform, boneName); if (t == null) return;
            if (!manual.ContainsKey(t)) manual[t] = (t.localRotation, euler); else manual[t] = (manual[t].rest, euler);
        }

        // ---- expression: the face blends toward the target over about half a second (p0-characters-s03) ----
        [Header("Expression")]
        [Range(.1f, 3f), Tooltip("Seconds for the face to settle on a new expression.")] public float expressionSeconds = .6f;
        private IReadOnlyDictionary<string, float> expressionTarget = PatientExpression.Calm;
        private readonly Dictionary<string, float> expressionNow = new Dictionary<string, float>();
        private readonly Dictionary<string, int> expressionIndex = new Dictionary<string, int>();
        private readonly List<string> expressionKeys = new List<string>();
        private float droop;

        /// <summary>How far the face is into its expression right now, by shape ending (0 when not part of it). For tests and review.</summary>
        public float ExpressionWeight(string shape) => expressionNow.TryGetValue(shape, out var w) ? w : 0f;

        public float Droop => droop;

        private void UpdateExpression(float dt)
        {
            float step = dt / Mathf.Max(.05f, expressionSeconds);
            foreach (var kv in expressionTarget) if (!expressionNow.ContainsKey(kv.Key)) expressionNow[kv.Key] = 0f;
            expressionKeys.Clear(); expressionKeys.AddRange(expressionNow.Keys);                                     // a copy, because the loop removes keys; reused so no garbage per frame
            foreach (var key in expressionKeys)
            {
                float target = expressionTarget.TryGetValue(key, out var t) ? t : 0f;
                float now = Mathf.MoveTowards(expressionNow[key], target, step);
                if (now <= 0f && target <= 0f) { expressionNow.Remove(key); if (key == PatientExpression.Droop) droop = 0f; continue; }
                expressionNow[key] = now;
                if (key == PatientExpression.Droop) { droop = now; continue; }                                         // lids are set with the blink, below
                if (face == null) continue;
                if (!expressionIndex.TryGetValue(key, out var idx)) expressionIndex[key] = idx = Shape(face, key);
                SetShape(idx, now * 100f);
            }
        }

        // ---- mood: face, head, eyes, breathing depth and the guarding hand settle together ----
        [Tooltip("Review aid: type a behaviour mode (IN_PAIN, ANXIOUS, DISTRESSED, CONFUSED, DROWSY, NAUSEATED, UNCOOPERATIVE) to show it now, whatever the session says. Empty follows the session.")] public string previewMode = "";
        [Header("Guarding hand (target on the body, metres from the pelvis)")]
        public Vector3 guardTarget = new Vector3(.17f, .06f, .18f);                          // x: toward the patient's right, y: toward the head, z: up from the pelvis
        private string appliedPreview = "";
        private PatientPose poseNow = PatientPose.Calm, poseTarget = PatientPose.Calm;

        /// <summary>The mood to show: the face, head, eyes, breathing depth and guarding hand for a behaviour mode (null or unknown is calm). The session's mode is ignored while <see cref="previewMode"/> is set, unless forced.</summary>
        public void SetMood(string mode, bool force = false)
        {
            if (!force && previewMode.Length > 0) return;
            lastMode = mode ?? "";
            var shown = flinching || flinchSeconds > 0f ? "IN_PAIN" : mode;                // a flinch shows the pain mood on top of whatever the session says, until it is over
            expressionTarget = PatientExpression.For(shown);
            poseTarget = PatientPose.For(shown);
        }

        // ---- flinch: an examination that hurts (P3-ANI-06). Only the engine's finding starts it, through PatientReactions; the mood underneath comes back afterwards ----
        private bool flinching; private float flinchSeconds, flinchUntil; private string lastMode = "";
        /// <summary>True from the moment a flinch is asked for until it ends.</summary>
        public bool Flinching => flinching || flinchSeconds > 0f;
        /// <summary>The patient winces for this long: the face and guarding hand of the pain mood, then back to the session's mood.</summary>
        public void Flinch(float seconds = 1.8f) { flinchSeconds = Mathf.Max(.1f, seconds); SetMood(lastMode); }
        private void StepFlinch(float t)
        {
            if (flinchSeconds > 0f) { flinchUntil = t + flinchSeconds; flinchSeconds = 0f; flinching = true; }
            if (flinching && t >= flinchUntil) { flinching = false; SetMood(lastMode); }
        }

        /// <summary>How far into the mood's pose the patient is right now. For tests and review.</summary>
        public PatientPose Pose => poseNow;

        // The right hand comes to the lower right abdomen: the forearm bends at the elbow (two-bone solve), blended in by the pose's Guard weight.
        // The same solve brings the arm out to the side when the trainee asks for it (offer 0 to 1 moves the target from the abdomen to offerTarget).
        [Tooltip("Where the right hand goes when the arm is offered for the cuff (metres from the pelvis, same axes as the guard target).")] public Vector3 offerTarget = new Vector3(.36f, .2f, .06f);
        private float offerNow, offerGoal;
        /// <summary>Holds the right arm out to the side (true) or lets it come back (false), e.g. for the blood pressure cuff.</summary>
        public void OfferArm(bool offer) => offerGoal = offer ? 1f : 0f;
        /// <summary>How far the arm is out, 0 to 1. For tests and review.</summary>
        public float Offer => offerNow;

        [Tooltip("Where the right hand goes while the patient signs a form (metres from the pelvis, same axes): up over the chest.")] public Vector3 signTarget = new Vector3(.12f, .3f, .22f);
        private float signNow, signSeconds;
        [Tooltip("How far the trunk rises when the patient sits up for an examination (degrees at the lower spine).")] public float sitUpDegrees = 55f;
        private Transform spineBase; private Quaternion spineBaseRest; private Vector3 sitAxis; private float sitSign = 1f, sitNow, sitSeconds;
        /// <summary>The patient sits up for a few seconds (the trunk rises from the lower spine) and then lies back (P3-ANI-06).</summary>
        public void SitUp(float seconds = 12f) => sitSeconds = Mathf.Max(.1f, seconds);
        /// <summary>How far the trunk rises while the trainee holds the bed head-up (a share of the full sit-up).</summary>
        public const float HeadUpShare = .55f;
        private bool headUpHeld;
        /// <summary>The trainee raises the bed's head section (true) or lays it flat (false); the patient lies on it. Cosmetic: the engine decides nothing here.</summary>
        public void SetHeadUp(bool up) => headUpHeld = up;
        public bool HeadUp => headUpHeld;
        /// <summary>How far up the patient is aiming: fully up while sitting for an examination, else the head-up share while the bed is raised, else flat.</summary>
        public static float SitGoal(float sitSecondsLeft, bool headUp) => sitSecondsLeft > 0f ? 1f : headUp ? HeadUpShare : 0f;
        /// <summary>How far up the patient is, 0 (lying) to 1 (sitting). The bed backrest follows it.</summary>
        public float Sitting => sitNow;
        /// <summary>The patient signs for a few seconds: the right hand comes up over the chest and back (P3-ANI-06).</summary>
        public void Sign(float seconds = 3f) => signSeconds = Mathf.Max(.1f, seconds);
        public float Signing => signNow;

        private void GuardArm(float w, float offer = 0f)
        {
            if (armUpper == null || armFore == null || armHand == null || pelvis == null || armLeft == null) return;
            armUpper.localRotation = armUpperRest; armFore.localRotation = armForeRest;
            if (w < .001f) return;
            var lateral = Vector3.ProjectOnPlane(armUpper.position - armLeft.position, Vector3.up).normalized;
            var toHead = Vector3.ProjectOnPlane(head.position - pelvis.position, Vector3.up).normalized;
            var spot = Vector3.Lerp(Vector3.Lerp(guardTarget, offerTarget, offer), signTarget, signNow);
            var target = pelvis.position + lateral * spot.x + toHead * spot.y + Vector3.up * spot.z;
            TwoBoneArm.Reach(armUpper, armFore, armHand, target, lateral + Vector3.up * .3f, w);
        }

        private void SetShape(int idx, float w) { if (face != null && idx >= 0) CharacterPerfBlend.Set(face, idx, Mathf.Clamp(w, 0f, 100f)); }

        // ---- look at: the eyes turn to the target first, the head follows for the rest (limits in LookAtLimits) ----
        [Header("Look at")]
        [Tooltip("Who or what the patient looks at (the player's head when addressed). Empty: no look-at.")] public Transform lookAt;
        [Range(0f, 1f)] public float lookWeight = 1f;
        private Vector4 lookNow;                                                          // eye yaw, eye pitch, head yaw, head pitch (degrees)
        private Transform thighLeft, thighRight;

        // ---- head gesture: a nod or a head shake on top of the pose (acknowledge, consent, refuse) ----
        private HeadGestureKind gestureKind; private float gestureStart = float.NegativeInfinity; private bool gesturePending;
        /// <summary>Starts a nod or a head shake now.</summary>
        public void Gesture(HeadGestureKind kind) { gestureKind = kind; gesturePending = true; }
        /// <summary>The gesture last started (None before any). For tests and review.</summary>
        public HeadGestureKind GestureKind => gestureKind;

        private Vector4 LookTarget()
        {
            if (lookAt == null || lookWeight <= 0f || thighLeft == null || thighRight == null) return Vector4.zero;
            var along = (head.position - neck.position).normalized;                          // toward the head
            var side = (thighRight.position - thighLeft.position).normalized;
            var face = Vector3.ProjectOnPlane(neck.rotation * (headRest * headAxisTilt), along).normalized;     // the way the face points at rest (the head's own front-to-back axis)
            if (Vector3.Dot(face, Vector3.up) < 0f) face = -face;                            // lying on the back: the front is the side facing the ceiling
            var a = LookAtLimits.Angles(face, along, side, lookAt.position - head.position);
            var s = LookAtLimits.Share(a.x, a.y);
            return new Vector4(s.EyeYaw, s.EyePitch, s.HeadYaw, s.HeadPitch) * lookWeight;
        }

        private void LateUpdate() => Step(Time.time, Time.deltaTime);

        /// <summary>One frame of motion at time t (seconds) after dt seconds. LateUpdate calls it; the review tool calls it to pose the patient outside play mode.</summary>
        public void Step(float t, float dt)
        {
            if (previewMode.Length > 0 && previewMode != appliedPreview) { appliedPreview = previewMode; SetMood(previewMode, true); }
            StepFlinch(t);
            poseNow = PatientPose.Lerp(poseNow, poseTarget, 1f - Mathf.Exp(-dt / Mathf.Max(.05f, expressionSeconds * .6f)));
            float breath = autoMotion ? Mathf.Sin(t * breathsPerMinute / 60f * Mathf.PI * 2f) : 0f;
            float rise = breath * (.75f + .25f * Mathf.Sin(t * .31f + seed));                                              // depth varies a little from breath to breath
            sitSeconds = Mathf.Max(0f, sitSeconds - dt); sitNow = Mathf.MoveTowards(sitNow, SitGoal(sitSeconds, headUpHeld), dt / 1.4f);
            if (spineBase != null) spineBase.localRotation = spineBaseRest * Quaternion.AngleAxis(sitSign * sitUpDegrees * sitNow * sitNow * (3f - 2f * sitNow), sitAxis);
            spine1.localRotation = spine1Rest; spine1.localScale = spine1Scale; spine2.localRotation = spine2Rest; spine2.localScale = spine2Scale;
            if (neck.localPosition != neckWrotePos || neck.localRotation != neckWroteRot) { neckRestPos = neck.localPosition; neckRestRot = neck.localRotation; neckScale = neck.localScale; }     // someone else moved the neck (the Animator): that pose is its rest now
            neck.localPosition = neckRestPos; neck.localRotation = neckRestRot; neck.localScale = neckScale;                // back to rest first: the pin below rewrites the neck each frame and must not build on itself
            var neckPos = neck.position; var neckRot = neck.rotation;                                                       // where the neck sits at rest: the breath must not carry the head (it would sink into the pillow)
            spine1.localRotation = spine1Rest * Quaternion.AngleAxis(breathSign * breathDegrees * poseNow.Depth * .6f * rise, breathAxis1);
            spine2.localRotation = spine2Rest * Quaternion.AngleAxis(breathSign * breathDegrees * poseNow.Depth * rise, breathAxis2);

            var sc1 = spine1Scale; sc1[apAxis1] *= 1f + breathDepth * poseNow.Depth * .6f * rise; spine1.localScale = sc1;                                    // the chest wall rises and falls
            var sc2 = spine2Scale; sc2[apAxis2] *= 1f + breathDepth * poseNow.Depth * rise; spine2.localScale = sc2;
            neck.SetPositionAndRotation(neckPos, neckRot);
            var nsc = neckScale; nsc[apAxisNeck] /= sc2[apAxis2] / spine2Scale[apAxis2]; neck.localScale = nsc;                   // and the chest scale must not squash the neck and head
            neckWrotePos = neck.localPosition; neckWroteRot = neck.localRotation;

            lookNow = Vector4.Lerp(lookNow, LookTarget(), 1f - Mathf.Exp(-dt / .25f));
            if (gesturePending) { gestureStart = t; gesturePending = false; }
            var gest = HeadGesture.At(gestureKind, t - gestureStart);
            float yaw = autoMotion ? (Mathf.PerlinNoise(seed, t * .05f * Mathf.Sqrt(poseNow.Restless)) - .5f) * 2f * headDriftDegrees * poseNow.Restless : 0f, roll = autoMotion ? (Mathf.PerlinNoise(seed + 9f, t * .04f * Mathf.Sqrt(poseNow.Restless)) - .5f) * headDriftDegrees * .6f * poseNow.Restless : 0f;
            head.localRotation = headRest * Quaternion.AngleAxis(yaw + poseNow.Yaw + lookNow.z + gest.y, headAxisYaw) * Quaternion.AngleAxis(roll + poseNow.Nod + lookNow.w + gest.x, headAxisRoll) * Quaternion.AngleAxis(poseNow.Tilt, headAxisTilt) * Quaternion.Euler(headEuler);
            offerNow = Mathf.MoveTowards(offerNow, offerGoal, dt / .7f);
            float offerEase = offerNow * offerNow * (3f - 2f * offerNow);
            signSeconds = Mathf.Max(0f, signSeconds - dt); signNow = Mathf.MoveTowards(signNow, signSeconds > 0f ? 1f : 0f, dt / .5f);
            float signEase = signNow * signNow * (3f - 2f * signNow);
            GuardArm(Mathf.Max(poseNow.Guard, offerEase, signEase), offerEase);

            if (autoMotion && t >= nextBlink) { blinkStart = t; nextBlink = t + Random.Range(2.2f, 6.5f); if (Random.value < .15f) nextBlink = t + .35f; }       // now and then a quick double blink
            float b = 0f; float k = t - blinkStart; if (k >= 0f && k < .18f) b = k < .06f ? k / .06f : 1f - (k - .06f) / .12f;
            UpdateExpression(dt);
            b = Mathf.Max(b, blinkHold, droop); SetShape(blinkL, b * 100f); SetShape(blinkR, b * 100f);

            float gx = (autoMotion ? (Mathf.PerlinNoise(seed + 3f, t * .12f) - .5f) * 1.2f : 0f) + gazeOffset.x + poseNow.GazeX + lookNow.x / LookAtLimits.EyeYaw, gy = (autoMotion ? (Mathf.PerlinNoise(seed + 5f, t * .1f) - .5f) * .8f : 0f) + gazeOffset.y + poseNow.GazeY + lookNow.y / LookAtLimits.EyePitch;
            if (autoMotion && poseNow.Dart > .001f) { float ds = t * poseNow.DartSpeed; gx += (Mathf.PerlinNoise(seed + 20f, ds) - .5f) * 2f * poseNow.Dart; gy += (Mathf.PerlinNoise(seed + 30f, ds) - .5f) * 1.2f * poseNow.Dart; }       // the eyes jump about
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
