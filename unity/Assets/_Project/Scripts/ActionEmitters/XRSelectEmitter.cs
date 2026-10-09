using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace ProcSense.VR.ActionEmitters
{
    /// <summary>A world object (wristband, notes, probe) that sends one action when the trainee selects it with a controller or hand.</summary>
    [UnityEngine.RequireComponent(typeof(XRSimpleInteractable))]
    public class XRSelectEmitter : ActionEmitter
    {
        private XRSimpleInteractable _interactable;

        private void OnEnable()
        {
            _interactable = GetComponent<XRSimpleInteractable>();
            _interactable.selectEntered.AddListener(OnSelect);
            RegisterFallback();
        }

        private void OnDisable() => _interactable.selectEntered.RemoveListener(OnSelect);

        private void OnSelect(UnityEngine.XR.Interaction.Toolkit.SelectEnterEventArgs _) => Fire();
    }
}
