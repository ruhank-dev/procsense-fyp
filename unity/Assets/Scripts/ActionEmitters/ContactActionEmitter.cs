using System.Linq;
using ProcSense.VR.Bridge;
using TMPro;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace ProcSense.VR.ActionEmitters
{
    /// <summary>A held instrument activates at its fitted contact point. Core alone decides its clinical result.</summary>
    [RequireComponent(typeof(XRGrabInteractable))]
    public sealed class ContactActionEmitter : ActionEmitter
    {
        [SerializeField] private Transform contactPoint;
        [SerializeField] private Transform contactTarget;
        [SerializeField, Min(.001f)] private float contactRadius = .045f;
        [SerializeField] private TMP_Text instruction;
        private XRGrabInteractable _grab;
        public Transform ContactPoint => contactPoint;
        public Transform ContactTarget => contactTarget;
        public float ContactRadius => contactRadius;
        public string Hint { get; private set; } = "HOLD AT EAR";

        public void ConfigureContact(Transform point, Transform destination, TMP_Text label)
        {
            contactPoint = point; contactTarget = destination; instruction = label;
        }

        private void OnEnable()
        {
            _grab = GetComponent<XRGrabInteractable>();
            _grab.activated.AddListener(Activate);
            RegisterFallback();
        }
        private void OnDisable()
        {
            _grab.activated.RemoveListener(Activate);
            UnregisterFallback();
        }

        public bool PatientAvailable
        {
            get
            {
                var host = SimulationHost.Instance;
                if (host == null || host.Session == null || host.Ended || host.Paused) return false;
                var state = host.Observable; string trip = state?.Transport?.State ?? "";
                return state?.Location == "patient_bay" && (trip.Length == 0 || trip == "REQUESTED" ||
                    trip == "PREPARING" || trip == "DELAYED" || trip == "BACK" || trip == "CANCELLED");
            }
        }

        private bool Held => _grab.interactorsSelecting.Any(i => !(i is XRSocketInteractor) && !(i is XRPokeInteractor));
        private bool Near => contactPoint != null && contactTarget != null && contactTarget.gameObject.activeInHierarchy &&
            Vector3.Distance(contactPoint.position, contactTarget.position) <= contactRadius;

        private void Update()
        {
            if (!PatientAvailable) Hint = "UNAVAILABLE";
            else if (!Held) Hint = "HOLD AT EAR";
            else Hint = Near ? "ACTIVATE" : "TIP TO EAR";
            if (instruction != null && instruction.text != Hint) instruction.text = Hint;
        }

        private void Activate(ActivateEventArgs args)
        {
            // Activation must come from the current hand, never a socket, stale interactor or pickup event.
            if (!isActiveAndEnabled || !PatientAvailable || !Held || !Near || args == null ||
                !_grab.interactorsSelecting.Any(i => ReferenceEquals(i, args.interactorObject))) return;
            Fire();
        }
    }
}
