using ProcSense.Core;
using ProcSense.VR.Bridge;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ProcSense.VR.Views
{
    /// <summary>
    /// The main menu (plan p0-evaluation-performance-s04): the mode chooser (<see cref="ModeChooserView"/>), a Start button that loads the Boot scene with the chosen mode, and a Quit button. The mode is
    /// kept in <see cref="ModeChoice"/>, which the session host reads when it starts, and is locked while a session runs. A practice-only line (<see cref="PracticeOnly"/>) shows how the menu behaves
    /// with the practice aids: it is visible only in practice mode, so assessment mode shows no hint here either. Nothing here starts a clinical action.
    /// </summary>
    public sealed class MainMenuController : MonoBehaviour
    {
        public const string BootScene = "Boot";
        public ModeChooserView Chooser { get; private set; }
        public Canvas Canvas { get; private set; }
        public Button StartButton { get; private set; }
        public Button QuitButton { get; private set; }
        public GameObject PracticeHint { get; private set; }
        public string Loading { get; private set; } = "";
        /// <summary>Replaced by tests so no scene is loaded.</summary>
        public System.Action<string> LoadScene = name => SceneManager.LoadScene(name);

        private TMP_Text _line;

        private void Awake() => Build();

        public void Build()
        {
            if (Canvas != null) return;
            var chooserGo = new GameObject("Mode chooser");
            chooserGo.transform.SetParent(transform, false);
            chooserGo.transform.localPosition = new Vector3(0f, 1.6f, 0f);
            Chooser = chooserGo.AddComponent<ModeChooserView>();
            Chooser.Build();

            Canvas = UiKit.WorldCanvas("Main menu", transform, new Vector2(640, 250), 0.0006f);
            Canvas.transform.localPosition = new Vector3(0f, 1.2f, 0f);
            UiKit.Label(Canvas.transform, "Title", "ProcSense", new Vector2(24, 12), new Vector2(592, 48), 36);
            StartButton = UiKit.MakeButton(Canvas.transform, "Start", new Vector2(24, 70), new Vector2(280, 72), 30f);
            QuitButton = UiKit.MakeButton(Canvas.transform, "Quit", new Vector2(336, 70), new Vector2(280, 72), 30f);
            _line = UiKit.Label(Canvas.transform, "Line", "", new Vector2(24, 160), new Vector2(592, 80), 22, TextAlignmentOptions.TopLeft, UiKit.WarnColor);
            StartButton.onClick.AddListener(() => Start_());
            QuitButton.onClick.AddListener(Quit);

            PracticeHint = new GameObject("Practice hint");
            PracticeHint.transform.SetParent(Canvas.transform, false);
            UiKit.Label(PracticeHint.transform, "Text", "Practice: hints and prompts are on.", new Vector2(24, 215), new Vector2(592, 30), 20, TextAlignmentOptions.TopLeft, UiKit.DimTextColor);
            PracticeHint.AddComponent<PracticeOnly>();
        }

        /// <summary>The mode the next session starts in; practice until the person chooses otherwise.</summary>
        public SessionMode Mode => ModeChoice.Chosen ?? SessionMode.Practice;

        public bool Start_()
        {
            if (ModeChoice.Locked) { _line.text = "A session is running; the mode cannot be changed."; return false; }
            if (!ModeChoice.Chosen.HasValue) ModeChoice.Choose(SessionMode.Practice);
            Loading = BootScene;
            _line.text = "Starting " + (Mode == SessionMode.Assessment ? "an assessment" : "practice") + "...";
            LoadScene(BootScene);
            return true;
        }

        public void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
