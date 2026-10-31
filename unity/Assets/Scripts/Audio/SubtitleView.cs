using ProcSense.VR.Views;
using TMPro;
using UnityEngine;

namespace ProcSense.VR.Audio
{
    /// <summary>
    /// The always-on subtitle (p0-voice-audio-s03): the speaker's name and the line, in view but below eye level so it never covers the patient's face, kept on screen at least <see cref="MinSeconds"/> however short
    /// the line. It follows the camera it is given (the headset), or stays where it is placed when there is none. It works when the audio does not: a line with no audio file is still shown.
    /// </summary>
    public sealed class SubtitleView : MonoBehaviour
    {
        public const float MinSeconds = 2f;
        public Transform Follow;
        public Vector3 Offset = new Vector3(0f, -0.32f, 0.9f);          // below eye level, 0.9 m ahead

        public string Text => _text != null ? _text.text : "";
        public bool Showing => _left > 0f;
        public Canvas Canvas { get; private set; }

        private TMP_Text _text;
        private float _left;

        private void Awake() => Build();

        public void Build()
        {
            if (Canvas != null) return;
            Canvas = UiKit.WorldCanvas("Subtitles", transform, new Vector2(900, 120), 0.0009f);
            _text = UiKit.Label(Canvas.transform, "Line", "", new Vector2(16, 8), new Vector2(868, 104), 26, TextAlignmentOptions.Center);
            Canvas.gameObject.SetActive(false);
        }

        /// <summary>Shows a line for this long (never under <see cref="MinSeconds"/>).</summary>
        public void Show(string speaker, string line, float seconds)
        {
            Build();
            _text.text = Label(speaker) + ": " + line;
            _left = Mathf.Max(MinSeconds, seconds);
            Canvas.gameObject.SetActive(true);
        }

        public void Tick(float seconds)
        {
            if (_left <= 0f) return;
            _left -= seconds;
            if (_left <= 0f) { _left = 0f; Canvas.gameObject.SetActive(false); _text.text = ""; }
        }

        private void LateUpdate()
        {
            Tick(Time.deltaTime);
            if (Follow == null) return;
            transform.position = Follow.TransformPoint(Offset);
            transform.rotation = Quaternion.LookRotation(transform.position - Follow.position, Vector3.up);
        }

        public static string Label(string speaker) => speaker.Length == 0 ? "" : char.ToUpperInvariant(speaker[0]) + speaker.Substring(1);
    }
}
