using TMPro;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace ProcSense.VR.Interaction
{
    /// <summary>
    /// P3-INT-08: the handover point at the theatre entrance. Selecting it asks to hand the patient over (the game flow arms the submission); selecting it again within <see cref="ConfirmSeconds"/> sends it.
    /// It only calls the same flow the tablet button uses, so the engine still decides everything.
    /// </summary>
    [RequireComponent(typeof(XRSimpleInteractable))]
    public sealed class TheatreHandoverPoint : MonoBehaviour
    {
        public const float ConfirmSeconds = 8f;
        public const string Ask = "Hand the patient over to theatre?\nSelect again to confirm";
        public const string Idle = "Theatre handover\nSelect to hand the patient over";
        [SerializeField] private TMP_Text label;
        private float armedAt = float.NegativeInfinity;

        public void Configure(TMP_Text text) { label = text; Show(Idle); }
        public bool Armed => Time.time - armedAt <= ConfirmSeconds;

        private void OnEnable() { GetComponent<XRSimpleInteractable>().selectEntered.AddListener(OnSelect); Show(Idle); }
        private void OnDisable() { GetComponent<XRSimpleInteractable>().selectEntered.RemoveListener(OnSelect); }

        private void OnSelect(UnityEngine.XR.Interaction.Toolkit.SelectEnterEventArgs _) => Press(ContextPanel.FlowSource?.Invoke());

        /// <summary>One press. Public for tests, which pass their own flow.</summary>
        public void Press(ProcSense.VR.Flow.MenuFlow flow)
        {
            if (flow == null || flow.Phase != ProcSense.VR.Flow.MenuPhase.Playing) { Show("No patient to hand over"); return; }
            if (!Armed) { armedAt = Time.time; if (!flow.SubmitArmed) flow.Submit(); Show(Ask); return; }
            armedAt = float.NegativeInfinity;
            var outcome = flow.Submit();
            Show(outcome != null && outcome.Accepted ? "Patient handed over" : (flow.Message ?? Idle));
        }

        private void Update()
        {
            if (armedAt > float.NegativeInfinity && !Armed) { armedAt = float.NegativeInfinity; Show(Idle); }
            var cam = Camera.main;
            if (label != null && cam != null) { var d = label.transform.position - cam.transform.position; d.y = 0; if (d.sqrMagnitude > .01f) label.transform.rotation = Quaternion.LookRotation(d); }   // the words face whoever walks up
        }
        private void Show(string text) { if (label != null) label.text = text; }
    }
}
