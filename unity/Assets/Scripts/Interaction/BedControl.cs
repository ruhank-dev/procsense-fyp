using TMPro;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using ProcSense.VR.Views;

namespace ProcSense.VR.Interaction
{
    /// <summary>
    /// The bed's head-up control. Selecting it raises the head section (the patient lies back on it) or lays it flat. It is a visual and physical cue only (equipment spec row 3: no numeric data), so it
    /// sends no action: <see cref="PatientMotion"/> holds the state and <see cref="BedBackrest"/> follows it.
    /// </summary>
    [RequireComponent(typeof(XRSimpleInteractable))]
    public sealed class BedControl : MonoBehaviour
    {
        public const string Flat = "Bed\nSelect to raise the head";
        public const string Raised = "Bed: head up\nSelect to lay flat";
        [SerializeField] private TMP_Text label;

        public void Configure(TMP_Text text) { label = text; Show(Flat); }

        private void OnEnable() { GetComponent<XRSimpleInteractable>().selectEntered.AddListener(OnSelect); }
        private void OnDisable() { GetComponent<XRSimpleInteractable>().selectEntered.RemoveListener(OnSelect); }

        private void OnSelect(UnityEngine.XR.Interaction.Toolkit.SelectEnterEventArgs _) => Press(FindPatient());

        /// <summary>One press. Public for tests, which pass their own patient.</summary>
        public void Press(PatientMotion patient)
        {
            if (patient == null) { Show("No patient on the bed"); return; }
            patient.SetHeadUp(!patient.HeadUp);
            Show(patient.HeadUp ? Raised : Flat);
        }

        private static PatientMotion FindPatient()
        {
            foreach (var p in FindObjectsByType<PatientMotion>(FindObjectsInactive.Exclude)) if (p.isActiveAndEnabled) return p;
            return null;
        }

        private void Update()
        {
            var cam = Camera.main;
            if (label != null && cam != null) { var d = label.transform.position - cam.transform.position; d.y = 0; if (d.sqrMagnitude > .01f) label.transform.rotation = Quaternion.LookRotation(d); }   // the words face whoever walks up
        }

        private void Show(string text) { if (label != null) label.text = text; }
    }
}
