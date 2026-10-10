using System.Linq;
using ProcSense.Core.Observation;
using ProcSense.VR.ActionEmitters;
using TMPro;
using UnityEngine;

namespace ProcSense.VR.Views
{
    /// <summary>The notes folder's identifiers. Shown only after the folder has been opened.</summary>
    public class NotesPanelView : StateView
    {
        private TMP_Text _body;

        public string BodyText => _body.text;

        private void Awake()
        {
            var canvas = UiKit.WorldCanvas("Notes", transform, new Vector2(420, 190), 0.0007f);
            UiKit.Label(canvas.transform, "Title", "NOTES", new Vector2(14, 8), new Vector2(392, 30), 22, TextAlignmentOptions.TopLeft, UiKit.DimTextColor);
            _body = UiKit.Label(canvas.transform, "Body", "", new Vector2(14, 40), new Vector2(392, 140), 28);
        }

        protected override void Render(ObservableState s) =>
            SetText(_body, s.Notes == null ? "Grab the folder to open it" : s.Notes.FullName + "\n" + s.Notes.Dob + "\n" + s.Notes.Mrn);
    }
}
