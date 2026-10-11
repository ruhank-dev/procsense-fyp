using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace ProcSense.VR.ActionEmitters
{
    /// <summary>A world object (wristband, notes, probe) that sends one action when the trainee selects it with a controller or hand.</summary>
    [DisallowMultipleComponent]
    public class XRSelectEmitter : ActionEmitter
    {
        public enum SelectionRoute { Any, Grab, Socket, Poke }

        [SerializeField] private SelectionRoute route;
        [SerializeField] private XRSocketInteractor requiredSocket;
        private XRBaseInteractable _interactable;

        public SelectionRoute Route { get => route; set => route = value; }
        public XRSocketInteractor RequiredSocket { get => requiredSocket; set => requiredSocket = value; }

        private void OnEnable()
        {
            // Preserve existing simple proxies while also supporting real grabbable props.
            // Never add a second interactable to an existing XRGrabInteractable.
            _interactable = GetComponent<XRBaseInteractable>();
            if (_interactable == null) _interactable = gameObject.AddComponent<XRSimpleInteractable>();
            _interactable.selectEntered.AddListener(OnSelect);
            RegisterFallback();
        }

        private void OnDisable()
        {
            if (_interactable != null) _interactable.selectEntered.RemoveListener(OnSelect);
            UnregisterFallback();
        }

        private void OnSelect(SelectEnterEventArgs args)
        {
            var source = args.interactorObject;
            bool allowed = route switch
            {
                SelectionRoute.Any => true,
                SelectionRoute.Grab => _interactable is XRGrabInteractable && source != null &&
                    !(source is XRSocketInteractor) && !(source is XRPokeInteractor),
                // Explicit destination prevents a sensor dropped into an unrelated socket
                // from silently issuing the clinical attachment request.
                SelectionRoute.Socket => requiredSocket != null && ReferenceEquals(source, requiredSocket),
                SelectionRoute.Poke => source is XRPokeInteractor,
                _ => false
            };
            if (allowed) Fire();
        }
    }
}
