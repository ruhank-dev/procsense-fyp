using System.Linq;
using ProcSense.Core.Observation;
using ProcSense.VR.ActionEmitters;
using TMPro;
using UnityEngine;

namespace ProcSense.VR.Views
{
    /// <summary>The patient's wristband as a readable card next to her wrist. Shows the band only after the trainee has read it.</summary>
    public class IdentityCardView : StateView
    {
        private TMP_Text _body;

        public string BodyText => _body.text;

        private void Awake()
        {
            var canvas = UiKit.WorldCanvas("Wristband card", transform, new Vector2(420, 190), 0.0007f);
            UiKit.Label(canvas.transform, "Title", "WRISTBAND", new Vector2(14, 8), new Vector2(392, 30), 22, TextAlignmentOptions.TopLeft, UiKit.DimTextColor);
            _body = UiKit.Label(canvas.transform, "Body", "", new Vector2(14, 40), new Vector2(392, 140), 28);
        }

        protected override void Render(ObservableState s)
        {
            string text = s.Wristband == null
                ? "Point at her wrist to read the band"
                : s.Wristband.NameText + "\n" + s.Wristband.DobText + "\n" + s.Wristband.MrnText;
            if (s.AllergyBand) text += "\nAllergy band on";
            if (s.Nurse == "DONE") text += "\nChecked by the nurse";
            SetText(_body, text);
        }
    }
}
