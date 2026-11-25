using System.Linq;
using ProcSense.Core;
using ProcSense.VR.Bridge;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace ProcSense.VR.ActionEmitters
{
    /// <summary>
    /// P3-INT-07 / ANI-06: the IV cannula kit is used by the trainee's own hands. Pick the tray up and let go of it at the patient's forearm: that sends <c>request_iv_access</c>, the same order the tablet button sends, and Core decides
    /// (identity, indication, allergy checks). A refusal is said in words above the arm. The tray always goes back to where it came from; when the order is accepted a green cannula hub and a clear dressing show on the arm and the patient winces.
    /// The nurse is the trainee's help, not a replacement: she only carries out an order the trainee has placed and confirmed.
    /// </summary>
    [RequireComponent(typeof(XRGrabInteractable))]
    public sealed class IvKitUse : ActionEmitter
    {
        public const float Reach = .24f;
        private XRGrabInteractable grab; private Pose home; private bool homeSet;
        private readonly EquipmentNotice notice = new EquipmentNotice();
        private GameObject onArm; private object sessionShown;

        /// <summary>True when something at <paramref name="at"/> is close enough to the forearm at <paramref name="arm"/> to count as "at the arm". Public for tests.</summary>
        public static bool AtArm(Vector3 at, Vector3 arm) => (at - arm).sqrMagnitude <= Reach * Reach;

        private void OnEnable()
        {
            grab = GetComponent<XRGrabInteractable>();
            Configure("request_iv_access", "patient", "", "", "Place the IV cannula");
            grab.selectExited.AddListener(Released);
            if (!homeSet) { home = new Pose(transform.position, transform.rotation); homeSet = true; }
        }

        private void OnDisable() { grab.selectExited.RemoveListener(Released); UnregisterFallback(); notice.Hide(); guide?.Hide(); }
        private void OnDestroy() { notice.Dispose(); guide?.Dispose(); if (onArm != null) Destroy(onArm); }

        private static Transform Forearm() =>
            FindObjectsByType<ProcSense.VR.Views.PatientMotion>(FindObjectsInactive.Exclude).Where(m => m.isActiveAndEnabled)
                .Select(m => m.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "Bip01 R Forearm")).FirstOrDefault(t => t != null);

        private void Released(UnityEngine.XR.Interaction.Toolkit.SelectExitEventArgs args)
        {
            var arm = Forearm();
            if (arm != null && AtArm(transform.position, arm.position)) Use(arm);
            Return();
        }

        /// <summary>The kit has been let go at the arm: send the order, show the result. Public for tests.</summary>
        public void Use(Transform arm)
        {
            var answer = Fire();
            if (answer == null) return;
            if (!answer.Accepted) { notice.Show(EquipmentRefusalText.For(answer.RejectReason, ModeGate.PracticeAids, false), arm.position + Vector3.up * .25f); return; }
            ShowOnArm(arm);
        }

        private void ShowOnArm(Transform arm)
        {
            if (onArm != null) Destroy(onArm);
            onArm = new GameObject("Cannula in place"); onArm.transform.SetParent(arm, false);
            onArm.transform.position = arm.position + (arm.parent != null ? (arm.position - arm.parent.position).normalized * .07f : Vector3.zero) + Vector3.up * .052f;   // on top of the forearm, above the bone and the sleeve
            Piece(PrimitiveType.Capsule, new Vector3(.018f, .03f, .018f), new Vector3(0, .02f, 0), new Color(.1f, .75f, .3f));       // the coloured hub
            Piece(PrimitiveType.Cube, new Vector3(.09f, .003f, .06f), new Vector3(0, .004f, 0), new Color(.92f, .97f, 1f));       // the clear dressing
            foreach (var r in onArm.GetComponentsInChildren<Renderer>()) { r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; r.receiveShadows = false; }
            sessionShown = SimulationHostSession();
            var motion = arm.GetComponentInParent<ProcSense.VR.Views.PatientMotion>(); if (motion != null) motion.Flinch(1.2f);
        }

        private void Piece(PrimitiveType type, Vector3 scale, Vector3 at, Color colour)
        {
            var g = GameObject.CreatePrimitive(type); Destroy(g.GetComponent<Collider>()); g.transform.SetParent(onArm.transform, false); g.transform.localScale = scale; g.transform.localPosition = at;
            var m = new Material(Shader.Find("Universal Render Pipeline/Lit")) { color = colour }; g.GetComponent<Renderer>().sharedMaterial = m;
        }

        private static object SimulationHostSession() { var h = ProcSense.VR.Bridge.SimulationHost.Instance; return h != null ? h.Session : null; }

        private void Return()
        {
            var body = GetComponent<Rigidbody>(); if (body != null) { body.linearVelocity = Vector3.zero; body.angularVelocity = Vector3.zero; body.position = home.position; body.rotation = home.rotation; }
            transform.SetPositionAndRotation(home.position, home.rotation);
        }

        /// <summary>The bubble on the forearm is this big and turns green inside it. The order is still accepted out to <see cref="Reach"/>; green means certainly close enough.</summary>
        public const float GuideRadius = .12f;
        [SerializeField] private Material placementHighlight;
        private SensorPlacementGuide guide; private Transform guideArm; private float nextArmLook;
        public void ConfigurePlacementHighlight(Material material) => placementHighlight = material;

        private void Update()
        {
            if (onArm != null && !ReferenceEquals(sessionShown, SimulationHostSession())) { Destroy(onArm); onArm = null; }   // a new session: the arm is bare again
            if (placementHighlight == null) return;
            if (guideArm == null && Time.time >= nextArmLook) { nextArmLook = Time.time + 1f; guideArm = Forearm(); if (guideArm != null) guide = new SensorPlacementGuide(guideArm, GuideRadius, null, placementHighlight, 0f); }
            if (guide == null) return;
            var host = SimulationHost.Instance;
            guide.Refresh(grab.isSelected, (transform.position - guideArm.position).sqrMagnitude <= GuideRadius * GuideRadius, false, host != null && host.Session != null && !host.Paused && !host.Ended);   // blue while approaching, green inside, amber when nothing can take the order
        }
    }
}
