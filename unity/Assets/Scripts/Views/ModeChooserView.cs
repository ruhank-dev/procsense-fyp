using ProcSense.Core;
using ProcSense.VR.Bridge;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ProcSense.VR.Views
{
    /// <summary>
    /// The place where the mode of the next session is chosen: two buttons, Practice and Assessment, and one line saying what each means (plan p0-evaluation-performance-s04). It only calls <see cref="ModeChoice.Choose"/>; once a session has started the choice
    /// is locked and the buttons are disabled with a line saying so. It is NOT placed in any scene yet: putting it into the main menu (which does not exist as a scene) is Sannan's scene work, listed in docs/testing/SANNAN_EXTERNAL_TESTING.md (UX-P0-ASSESS).
    /// </summary>
    public class ModeChooserView : MonoBehaviour
    {
        public Canvas Canvas { get; private set; }
        public Button PracticeButton { get; private set; }
        public Button AssessmentButton { get; private set; }
        public string StatusText => _status != null ? _status.text : "";
        private TMP_Text _status;

        private void Awake() => Build();
        private void OnEnable() { ModeChoice.Changed += OnChanged; Refresh(); }
        private void OnDisable() => ModeChoice.Changed -= OnChanged;
        private void Update() => Refresh();
        private void OnChanged(SessionMode _) => Refresh();

        public void Build()
        {
            if (Canvas != null) return;
            Canvas = UiKit.WorldCanvas("ModeChooser", transform, new Vector2(640, 300), 0.0006f);
            UiKit.Label(Canvas.transform, "Title", "Choose the mode", new Vector2(24, 16), new Vector2(592, 40), 32);
            PracticeButton = UiKit.MakeButton(Canvas.transform, "Practice", new Vector2(24, 76), new Vector2(280, 72), 30f);
            AssessmentButton = UiKit.MakeButton(Canvas.transform, "Assessment", new Vector2(336, 76), new Vector2(280, 72), 30f);
            UiKit.Label(Canvas.transform, "Help", "Practice: hints and prompts are shown.   Assessment: none are, and only content approved for assessment is played.", new Vector2(24, 164), new Vector2(592, 70), 22, TextAlignmentOptions.TopLeft, UiKit.DimTextColor);
            _status = UiKit.Label(Canvas.transform, "Status", "", new Vector2(24, 244), new Vector2(592, 40), 24, TextAlignmentOptions.TopLeft, UiKit.WarnColor);
            PracticeButton.onClick.AddListener(() => ModeChoice.Choose(SessionMode.Practice));
            AssessmentButton.onClick.AddListener(() => ModeChoice.Choose(SessionMode.Assessment));
        }

        public void Refresh()
        {
            if (PracticeButton == null) return;
            bool open = !ModeChoice.Locked;
            PracticeButton.interactable = open;
            AssessmentButton.interactable = open;
            _status.text = !open ? "The mode cannot be changed while a session is running." : ModeChoice.Chosen.HasValue ? "Next session: " + (ModeChoice.Chosen.Value == SessionMode.Assessment ? "Assessment" : "Practice") : "";
        }
    }
}
