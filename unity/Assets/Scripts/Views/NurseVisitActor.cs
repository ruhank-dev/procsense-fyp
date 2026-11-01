using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace ProcSense.VR.Views
{
    /// <summary>Replaceable, labelled character prototype. No clinical input, colliders on props or authored-asset mutation.</summary>
    public sealed class NurseVisitActor : MonoBehaviour
    {
        public NavMeshAgent Agent { get; private set; }
        public Transform Head { get; private set; }
        public Transform RightHand { get; private set; }
        private Transform _torso, _leftShoulder, _rightShoulder, _leftHand, _leftUpper, _leftLower, _rightUpper, _rightLower;
        private Transform _leftLeg, _rightLeg, _clipboard, _band, _label;
        private TextMesh _caption;
        private readonly List<Material> _materials = new List<Material>();
        private float _gait, _lean;
        private bool _poseReady;

        public void Build(GameObject clipboard, GameObject band)
        {
            var uniform = MakeMaterial(new Color(.32f, .39f, .67f));
            var skin = MakeMaterial(new Color(.72f, .55f, .43f));
            var dark = MakeMaterial(new Color(.06f, .08f, .13f));
            _torso = Node("Torso pivot", transform, new Vector3(0, .88f, 0));
            Part("Nurse tunic", PrimitiveType.Cube, _torso, new Vector3(0, .27f, 0), new Vector3(.34f, .55f, .27f), uniform);
            Part("Nurse hips", PrimitiveType.Cube, transform, new Vector3(0, .84f, 0), new Vector3(.31f, .18f, .25f), uniform);
            Head = Part("Nurse head", PrimitiveType.Sphere, _torso, new Vector3(0, .73f, 0), Vector3.one * .25f, skin);
            Part("Nose", PrimitiveType.Cube, Head, new Vector3(0, 0, .49f), new Vector3(.16f, .19f, .16f), skin);
            for (int side = -1; side <= 1; side += 2)
                Part("Eye", PrimitiveType.Sphere, Head, new Vector3(side * .18f, .13f, .43f), Vector3.one * .09f, dark);
            _leftShoulder = Node("Left shoulder", _torso, new Vector3(-.18f, .46f, 0));
            _rightShoulder = Node("Right shoulder", _torso, new Vector3(.18f, .46f, 0));
            _leftHand = Part("Left hand", PrimitiveType.Sphere, transform, Vector3.zero, Vector3.one * .085f, skin);
            RightHand = Part("Right hand", PrimitiveType.Sphere, transform, Vector3.zero, Vector3.one * .085f, skin);
            _leftUpper = Part("Left upper arm", PrimitiveType.Cylinder, transform, Vector3.zero, Vector3.one, uniform);
            _leftLower = Part("Left forearm", PrimitiveType.Cylinder, transform, Vector3.zero, Vector3.one, skin);
            _rightUpper = Part("Right upper arm", PrimitiveType.Cylinder, transform, Vector3.zero, Vector3.one, uniform);
            _rightLower = Part("Right forearm", PrimitiveType.Cylinder, transform, Vector3.zero, Vector3.one, skin);
            _leftLeg = Leg("Left", -.10f, uniform, dark);
            _rightLeg = Leg("Right", .10f, uniform, dark);
            _clipboard = ModelOnly(clipboard, "Held records", transform);
            _band = ModelOnly(band, "Replacement band", RightHand);
            if (_band != null) { _band.localPosition = Vector3.zero; _band.localRotation = Quaternion.identity; _band.localScale = new Vector3(.5f, 1, 1); }

            _label = Node("Prototype label", transform, new Vector3(0, 1.96f, 0));
            Part("Label back", PrimitiveType.Cube, _label, new Vector3(0, 0, .012f), new Vector3(.78f, .24f, .012f), dark);
            _caption = new GameObject("Caption", typeof(TextMesh)).GetComponent<TextMesh>();
            _caption.transform.SetParent(_label, false); _caption.anchor = TextAnchor.MiddleCenter;
            _caption.fontSize = 42; _caption.characterSize = .018f; _caption.color = Color.white;

            var collider = gameObject.AddComponent<CapsuleCollider>();
            collider.center = Vector3.up * .88f; collider.radius = .18f; collider.height = 1.72f;
            var body = gameObject.AddComponent<Rigidbody>(); body.isKinematic = true; body.useGravity = false;
            Agent = gameObject.AddComponent<NavMeshAgent>();
            Agent.radius = .20f; Agent.height = 1.75f; Agent.speed = 1.1f; Agent.acceleration = 3;
            Agent.stoppingDistance = .06f; Agent.angularSpeed = 180; Agent.avoidancePriority = 55;
            Agent.updatePosition = false; Agent.updateRotation = false;
        }

        public void Pose(string step, float seconds, float progress, Transform wrist, bool walking, bool bandPlaced, string status)
        {
            _gait += walking ? Time.deltaTime * 7 : 0;
            _leftLeg.localRotation = Quaternion.Euler(walking ? Mathf.Sin(_gait) * 18 : 0, 0, 0);
            _rightLeg.localRotation = Quaternion.Euler(walking ? -Mathf.Sin(_gait) * 18 : 0, 0, 0);
            float lean = step == "REPLACING" ? 55 * Mathf.SmoothStep(0, 1, Mathf.Clamp01(progress / .5f)) : step == "AT_BEDSIDE" ? 8 : 0;
            _lean = Mathf.MoveTowards(_lean, lean, Time.deltaTime * 85);
            _torso.localRotation = Quaternion.Euler(_lean, 0, 0);
            _torso.localPosition = new Vector3(0, .88f, .09f * Mathf.Clamp01(_lean / 55));
            var board = transform.TransformPoint(new Vector3(-.05f, 1.12f, .28f));
            if (_clipboard != null) { _clipboard.position = board; _clipboard.rotation = transform.rotation * Quaternion.Euler(-55, 0, 0); }
            Vector3 left = transform.TransformPoint(new Vector3(-.20f, 1.09f, .25f));
            Vector3 right = transform.TransformPoint(new Vector3(.25f, .91f, .08f));
            if (step == "AT_BEDSIDE") right = transform.TransformPoint(new Vector3(.16f, 1.10f, .28f));
            if (step == "REPLACING" && wrist != null)
            {
                float reach = Mathf.SmoothStep(0, 1, Mathf.Clamp01(progress / .5f));
                right = Vector3.Lerp(right, wrist.position + Vector3.up * .015f, reach);
                left = Vector3.Lerp(left, wrist.position - transform.right * .045f + Vector3.up * .03f, reach);
            }
            if (_poseReady)
            {
                float blend = 1 - Mathf.Exp(-Time.deltaTime * 12);
                left = Vector3.Lerp(_leftHand.position, left, blend); right = Vector3.Lerp(RightHand.position, right, blend);
            }
            _poseReady = true;
            Arm(_leftShoulder.position, left, -1, _leftUpper, _leftLower, _leftHand);
            Arm(_rightShoulder.position, right, 1, _rightUpper, _rightLower, RightHand);
            if (step == "AT_BEDSIDE") Head.rotation = Quaternion.LookRotation(board - Head.position, transform.up);
            else if (step == "REPLACING" && wrist != null) Head.rotation = Quaternion.LookRotation(wrist.position - Head.position, transform.up);
            else Head.localRotation = Quaternion.Euler(step == "DONE" ? (seconds < .7f ? Mathf.Sin(seconds / .7f * Mathf.PI) * 10 : 0) : 0, 0, 0);
            if (_band != null) _band.gameObject.SetActive(step == "REPLACING" && !bandPlaced);
            _caption.text = "PROTOTYPE NURSE\n" + status;
        }

        private void Arm(Vector3 shoulder, Vector3 target, int side, Transform upper, Transform lower, Transform hand)
        {
            var direction = target - shoulder; float distance = Mathf.Clamp(direction.magnitude, .04f, .739f);
            var axis = direction.normalized;
            var bend = Vector3.ProjectOnPlane(-transform.forward + transform.right * side * .5f, axis).normalized;
            var end = shoulder + axis * distance;
            var elbow = (shoulder + end) * .5f + bend * Mathf.Sqrt(Mathf.Max(0, .37f * .37f - distance * distance * .25f));
            Segment(upper, shoulder, elbow); Segment(lower, elbow, end); hand.position = end;
            hand.rotation = transform.rotation;
        }
        private static void Segment(Transform part, Vector3 start, Vector3 end)
        { part.position = (start + end) * .5f; part.up = (end - start).normalized; part.localScale = new Vector3(.065f, Vector3.Distance(start, end) * .5f, .065f); }
        private Transform Leg(string name, float x, Material uniform, Material shoe)
        {
            var pivot = Node(name + " leg pivot", transform, new Vector3(x, .81f, 0));
            Part(name + " leg", PrimitiveType.Capsule, pivot, new Vector3(0, -.34f, 0), new Vector3(.13f, .34f, .14f), uniform);
            Part(name + " shoe", PrimitiveType.Cube, pivot, new Vector3(0, -.75f, .06f), new Vector3(.15f, .10f, .25f), shoe);
            return pivot;
        }
        public static Transform ModelOnly(GameObject prefab, string name, Transform parent)
        {
            if (prefab == null || prefab.transform.childCount == 0) return null;
            var go = Instantiate(prefab.transform.GetChild(0).gameObject, parent, false); go.name = name;
            foreach (var c in go.GetComponentsInChildren<Collider>(true)) c.enabled = false;
            foreach (var b in go.GetComponentsInChildren<MonoBehaviour>(true)) b.enabled = false;
            return go.transform;
        }
        private static Transform Node(string name, Transform parent, Vector3 at)
        { var go = new GameObject(name).transform; go.SetParent(parent, false); go.localPosition = at; return go; }
        private static Transform Part(string name, PrimitiveType shape, Transform parent, Vector3 at, Vector3 scale, Material material)
        {
            var go = GameObject.CreatePrimitive(shape); go.name = name; go.transform.SetParent(parent, false);
            go.transform.localPosition = at; go.transform.localScale = scale;
            var c = go.GetComponent<Collider>(); c.enabled = false; Destroy(c);
            go.GetComponent<Renderer>().sharedMaterial = material; return go.transform;
        }
        private Material MakeMaterial(Color color)
        { var m = new Material(Shader.Find("Universal Render Pipeline/Lit")) { color = color }; _materials.Add(m); return m; }
        private void LateUpdate()
        {
            if (_label != null && Camera.main != null)
            { var forward = _label.position - Camera.main.transform.position; forward.y = 0; if (forward.sqrMagnitude > .001f) _label.rotation = Quaternion.LookRotation(forward, Vector3.up); }
        }
        private void OnDestroy() { foreach (var m in _materials) if (m != null) Destroy(m); }
    }
}
