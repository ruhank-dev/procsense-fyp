using ProcSense.VR.Bridge;
using TMPro;
using UnityEngine;

namespace ProcSense.VR.Views
{
    /// <summary>
    /// The specimen container's label (p1-theatre-extension-s04), drawn at its LABEL_Specimen anchor: the patient's name, date of birth and record number from the running session, never baked into the model, so
    /// reading the label aloud at Sign Out (read_specimen_label, which Core checks against the case) reads the live patient. Empty with no session.
    /// </summary>
    public sealed class SpecimenLabelView : MonoBehaviour
    {
        public string Text => _text != null ? _text.text : "";
        private TMP_Text _text;
        private object _session;

        private void Awake() => Build();

        public void Build()
        {
            if (_text != null) return;
            var canvas = UiKit.WorldCanvas("Specimen label", transform, new Vector2(240, 140), 0.0001f);
            canvas.GetComponentInChildren<UnityEngine.UI.Image>().color = Color.white;
            _text = UiKit.Label(canvas.transform, "Label", "", new Vector2(10, 8), new Vector2(220, 124), 18, TextAlignmentOptions.TopLeft, Color.black);
            Refresh();
        }

        private void Update()
        {
            var s = SimulationHost.Instance != null ? SimulationHost.Instance.Session : null;
            if (!ReferenceEquals(s, _session)) Refresh();
        }

        public void Refresh() => Show(SimulationHost.Instance != null ? SimulationHost.Instance.Session : null);

        /// <summary>Writes this session's patient on the label (tests and previews pass the session; the host's is used otherwise).</summary>
        public void Show(ProcSense.Core.Session s)
        {
            Build();
            _session = s;
            var id = s?.Case.Identity;
            _text.text = id == null ? "" : id.FullName + "\nDOB " + id.Dob + "\n" + id.Mrn + "\nSpecimen";
        }
    }
}
