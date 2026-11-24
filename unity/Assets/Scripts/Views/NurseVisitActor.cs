using UnityEngine;
using UnityEngine.AI;

namespace ProcSense.VR.Views
{
    /// <summary>The visiting nurse: a Rocketbox Humanoid that walks (Walk_Loop) or stands (Idle_Loop), holds the chart, leans in and reaches the real wrist. No clinical input; the controller says what to do.</summary>
    public sealed class NurseVisitActor : MonoBehaviour
    {
        public NavMeshAgent Agent { get; private set; }
        public Transform Head { get; private set; }
        public Transform RightHand { get; private set; }
        private Transform _spine, _spine1, _spine2, _leftUpper, _leftFore, _leftHand, _rightUpper, _rightFore, _clipboard, _band;
        private Animator _animator;
        private Transform _body, _leftThigh, _leftCalf, _leftFoot, _rightThigh, _rightCalf, _rightFoot;
        /// <summary>How far she steps in over the bed (metres) when fully leaning: the bay's navigation stops at the bed frame, further out than the Rocketbox arm reaches.</summary>
        private const float LeanIn = .45f, Crouch = .35f;
        private RuntimeAnimatorController _idle, _walk;
        private string _step = ""; private float _seconds, _progress, _lean, _rightWeight;
        private bool _walking, _bandPlaced, _poseReady;
        private Transform _wrist;
        private Vector3 _leftPos, _rightPos;
        private Quaternion _look = Quaternion.identity;

        private static Transform Find(Transform root, string n) { foreach (var t in root.GetComponentsInChildren<Transform>(true)) if (t.name == n) return t; return null; }

        public void Build(GameObject clipboard, GameObject band, GameObject model, RuntimeAnimatorController idle, RuntimeAnimatorController walk)
        {
            var body = Instantiate(model, transform, false); body.name = "Nurse"; _body = body.transform;
            foreach (var c in body.GetComponentsInChildren<Collider>(true)) c.enabled = false;
            foreach (var b in body.GetComponentsInChildren<MonoBehaviour>(true)) b.enabled = false;
            _idle = idle; _walk = walk;
            ProcSense.VR.Audio.MovingSound.Attach(gameObject, "foley_steps_vinyl", .4f);
            _animator = body.GetComponent<Animator>() ?? body.AddComponent<Animator>();
            _animator.applyRootMotion = false; _animator.cullingMode = AnimatorCullingMode.AlwaysAnimate; _animator.runtimeAnimatorController = idle;
            _spine = Find(body.transform, "Bip01 Spine"); _spine1 = Find(body.transform, "Bip01 Spine1"); _spine2 = Find(body.transform, "Bip01 Spine2"); Head = Find(body.transform, "Bip01 Head");
            _leftUpper = Find(body.transform, "Bip01 L UpperArm"); _leftFore = Find(body.transform, "Bip01 L Forearm"); _leftHand = Find(body.transform, "Bip01 L Hand");
            _leftThigh = Find(body.transform, "Bip01 L Thigh"); _leftCalf = Find(body.transform, "Bip01 L Calf"); _leftFoot = Find(body.transform, "Bip01 L Foot");
            _rightThigh = Find(body.transform, "Bip01 R Thigh"); _rightCalf = Find(body.transform, "Bip01 R Calf"); _rightFoot = Find(body.transform, "Bip01 R Foot");
            _rightUpper = Find(body.transform, "Bip01 R UpperArm"); _rightFore = Find(body.transform, "Bip01 R Forearm"); RightHand = Find(body.transform, "Bip01 R Hand");
            var presenter = gameObject.AddComponent<CharacterPresenter>(); presenter.turnsHead = false; presenter.Setup("nurse", Head);      // this actor already turns her head in LateUpdate
            _clipboard = ModelOnly(clipboard, "Held records", transform);
            _band = ModelOnly(band, "Replacement band", RightHand);
            if (_band != null) { _band.localPosition = Vector3.zero; _band.localRotation = Quaternion.identity; _band.localScale = new Vector3(.5f, 1, 1); }

            var collider = gameObject.AddComponent<CapsuleCollider>();
            collider.center = Vector3.up * .88f; collider.radius = .18f; collider.height = 1.72f;
            var rb = gameObject.AddComponent<Rigidbody>(); rb.isKinematic = true; rb.useGravity = false;
            Agent = gameObject.AddComponent<NavMeshAgent>();
            Agent.radius = .20f; Agent.height = 1.75f; Agent.speed = 1.1f; Agent.acceleration = 3;
            Agent.stoppingDistance = .06f; Agent.angularSpeed = 180; Agent.avoidancePriority = 55;
            Agent.updatePosition = false; Agent.updateRotation = false;
        }

        /// <summary>Says what the nurse is doing; the bones follow in LateUpdate (after the Animator).</summary>
        public void Pose(string step, float seconds, float progress, Transform wrist, bool walking, bool bandPlaced)
        {
            if (bandPlaced && !_bandPlaced) ProcSense.VR.Audio.SoundPlayer.Instance?.Play("foley_wristband_snap");   // the new band is fastened
            _step = step; _seconds = seconds; _progress = progress; _wrist = wrist; _walking = walking; _bandPlaced = bandPlaced;
            var want = walking ? _walk : _idle;
            if (_animator != null && _animator.runtimeAnimatorController != want) _animator.runtimeAnimatorController = want;
            if (_band != null) _band.gameObject.SetActive(step == "REPLACING" && !bandPlaced);
        }

        private void LateUpdate()
        {
            if (_leftHand == null || RightHand == null || Head == null || _leftFoot == null || _rightFoot == null || _leftCalf == null || _rightCalf == null) return;
            float dt = Time.deltaTime;
            bool reaching = _step == "REPLACING" && _wrist != null;
            float lean = _step == "REPLACING" ? 55 * Mathf.SmoothStep(0, 1, Mathf.Clamp01(_progress / .5f)) : _step == "AT_BEDSIDE" ? 8 : 0;
            _lean = Mathf.MoveTowards(_lean, lean, dt * 85);
            float f = Mathf.Clamp01(_lean / 55f);
            _body.position = transform.position;                                          // the animation pose, in place
            var shift = transform.forward * (LeanIn * f) - Vector3.up * (Crouch * f); var feet = (l: _leftFoot.position, r: _rightFoot.position, lr: _leftFoot.rotation, rr: _rightFoot.rotation);
            _body.position = transform.position + shift;                     // she steps in over the bed and bends her knees, feet planted
            TwoBoneArm.Reach(_leftThigh, _leftCalf, _leftFoot, feet.l, transform.forward, 1); TwoBoneArm.Reach(_rightThigh, _rightCalf, _rightFoot, feet.r, transform.forward, 1);
            _leftFoot.rotation = feet.lr; _rightFoot.rotation = feet.rr;
            var forward = Quaternion.AngleAxis(_lean / 3f, transform.right);
            if (_spine != null) _spine.rotation = forward * _spine.rotation;
            if (_spine1 != null) _spine1.rotation = forward * _spine1.rotation;
            if (_spine2 != null) _spine2.rotation = forward * _spine2.rotation;

            var board = transform.TransformPoint(new Vector3(-.05f, 1.12f, .28f)) + shift;
            if (_clipboard != null) { _clipboard.position = board; _clipboard.rotation = transform.rotation * Quaternion.Euler(-55, 0, 0); }
            Vector3 left = transform.TransformPoint(new Vector3(-.20f, 1.09f, .25f)) + shift, right = transform.TransformPoint(new Vector3(.16f, 1.10f, .28f)) + shift;
            if (reaching)
            {
                float reach = Mathf.SmoothStep(0, 1, Mathf.Clamp01(_progress / .5f));
                right = Vector3.Lerp(right, _wrist.position + Vector3.up * .015f, reach);
                left = Vector3.Lerp(left, _wrist.position - transform.right * .045f + Vector3.up * .03f, reach);
            }
            _rightWeight = Mathf.MoveTowards(_rightWeight, _step == "AT_BEDSIDE" || reaching ? 1 : 0, dt * 3);
            if (_poseReady)
            {
                float blend = 1 - Mathf.Exp(-dt * 12);
                left = Vector3.Lerp(_leftPos, left, blend); right = Vector3.Lerp(_rightPos, right, blend);
            }
            _poseReady = true; _leftPos = left; _rightPos = right;
            TwoBoneArm.Reach(_leftUpper, _leftFore, _leftHand, left, -transform.forward - transform.right * .5f + Vector3.down * .3f, 1);
            TwoBoneArm.Reach(_rightUpper, _rightFore, RightHand, right, -transform.forward + transform.right * .5f + Vector3.down * .3f, _rightWeight);
            if (_rightWeight <= 0) _rightPos = RightHand.position;

            // the head looks at the chart or the wrist; on DONE it gives a small nod
            Quaternion want = Quaternion.identity;
            if (_step == "AT_BEDSIDE" || reaching) want = Quaternion.RotateTowards(Quaternion.identity, Quaternion.FromToRotation(transform.forward, (reaching ? _wrist.position : board) - Head.position), 55);
            else if (_step == "DONE" && _seconds < .7f) want = Quaternion.AngleAxis(Mathf.Sin(_seconds / .7f * Mathf.PI) * 10, transform.right);
            _look = Quaternion.Slerp(_look, want, 1 - Mathf.Exp(-dt * 8));
            Head.rotation = _look * Head.rotation;
        }

        public static Transform ModelOnly(GameObject prefab, string name, Transform parent)
        {
            if (prefab == null || prefab.transform.childCount == 0) return null;
            var go = Instantiate(prefab.transform.GetChild(0).gameObject, parent, false); go.name = name;
            foreach (var c in go.GetComponentsInChildren<Collider>(true)) c.enabled = false;
            foreach (var b in go.GetComponentsInChildren<MonoBehaviour>(true)) b.enabled = false;
            return go.transform;
        }
    }
}
