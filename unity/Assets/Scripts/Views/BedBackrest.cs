using UnityEngine;

namespace ProcSense.VR.Views
{
    /// <summary>
    /// P3-ANI-08: the bed's backrest. A cushion hinged at the patient's hips rises with the patient when they sit up for an examination and folds away when they lie back. It only follows
    /// <see cref="PatientMotion.Sitting"/> (which the engine's accepted examination started), so it is cosmetic and decides nothing.
    /// </summary>
    public sealed class BedBackrest : MonoBehaviour
    {
        [SerializeField, Tooltip("The cushion, a child of this hinge. Hidden while the patient lies flat.")] private Transform panel;
        private PatientMotion patient; private float nextLook;
        public Transform Panel { get => panel; set => panel = value; }

        /// <summary>Hinge angle for how far up the patient is (0 to 1) and how far the patient's trunk turns (degrees). Public for tests.</summary>
        public static float Angle(float sitting, float degrees) => Mathf.Clamp01(sitting) * degrees;

        private void LateUpdate()
        {
            if (panel == null) return;
            if (patient == null || !patient.isActiveAndEnabled)
            {
                if (Time.time < nextLook) return;
                nextLook = Time.time + 1f; patient = null;
                foreach (var p in FindObjectsByType<PatientMotion>(FindObjectsInactive.Exclude)) if (p.isActiveAndEnabled) { patient = p; break; }
                if (patient == null) return;
            }
            float angle = Angle(patient.Sitting, patient.sitUpDegrees);
            panel.gameObject.SetActive(angle > .5f);
            transform.localRotation = Quaternion.Euler(-angle, 0, 0);
        }
    }
}
