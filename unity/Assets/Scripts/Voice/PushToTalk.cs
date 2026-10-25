using System;
using System.Collections;
using System.Linq;
using ProcSense.VR.Auth;
using ProcSense.VR.Views;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ProcSense.VR.Voice
{
    /// <summary>The microphone behind a seam, so push-to-talk is tested without hardware.</summary>
    public interface IMicrophone
    {
        bool Begin(out int sampleRate);
        float[] End();
    }

    public sealed class UnityMicrophone : IMicrophone
    {
        private AudioClip _clip;
        private string _device;

        public bool Begin(out int sampleRate)
        {
            sampleRate = WavEncoder.SampleRate;
            if (Microphone.devices.Length == 0) return false;
            _device = Microphone.devices[0];
            _clip = Microphone.Start(_device, false, WavEncoder.MaxSeconds, WavEncoder.SampleRate);
            return _clip != null;
        }

        public float[] End()
        {
            if (_clip == null) return new float[0];
            int heard = Math.Min(Microphone.GetPosition(_device), _clip.samples);
            Microphone.End(_device);
            var samples = new float[Math.Max(0, heard) * _clip.channels];
            if (samples.Length > 0) _clip.GetData(samples, 0);
            _clip = null;
            return samples;
        }
    }

    /// <summary>
    /// Push to talk (plan p1-voice-input-s01, hook 6): <see cref="Press"/> starts recording, <see cref="Release"/> ends it, cuts the clip at 15 s, sends it to the recogniser and raises <see cref="Transcribed"/> with the words.
    /// Silence and a recogniser that is not sure give a plain message (and nothing is asked or credited); an unreachable service gives "use the tablet". The clip is sent to the service's own recogniser (faster-whisper), never
    /// to a cloud model, and it is not stored. Binding the button of the controller to <see cref="Press"/> and <see cref="Release"/> is the headset layer's job; <see cref="PushToTalkView"/> offers a world-space button.
    /// </summary>
    public sealed class PushToTalk : MonoBehaviour
    {
        public IMicrophone Mic = new UnityMicrophone();
        public Func<AuthService> Auth = () => AuthService.Current;
        public event Action<string> Transcribed;
        public event Action<string> StatusChanged;
        /// <summary>Raised when recording starts and stops, so the speaker can be muted while the trainee talks (no echo into the microphone).</summary>
        public event Action<bool> RecordingChanged;
        /// <summary>Set by the headset layer to <see cref="VoiceHealthState.Offline"/>: voice never blocks play, so offline means the tablet menu at once.</summary>
        public Func<bool> Offline = () => false;
        /// <summary>True while a character is speaking: a clip recorded meanwhile waits until the character has finished (T-VC4).</summary>
        public Func<bool> CharacterSpeaking = () => false;
        public bool Recording { get; private set; }
        public string Status { get; private set; } = "";
        public float RecordedSeconds { get; private set; }
        private int _rate;
        private float _startedAt;

        private void Set(string status) { Status = status; StatusChanged?.Invoke(status); }

        public bool Press()
        {
            if (Recording) return false;
            if (Offline()) { Set(VoiceHealthState.OfflineBanner); return false; }
            if (!Mic.Begin(out _rate)) { Set("No microphone found: use the tablet."); return false; }
            Recording = true;
            _startedAt = Time.realtimeSinceStartup;
            Set("Listening...");
            RecordingChanged?.Invoke(true);
            return true;
        }

        /// <summary>After "Say that again, please": clears the message and listens again, so the trainee does not have to find the button first.</summary>
        public bool SayAgain()
        {
            if (Recording) return false;
            Set("");
            return Press();
        }

        /// <summary>Ends the recording; the answer is raised through <see cref="Transcribed"/> when it arrives.</summary>
        public void Release()
        {
            if (!Recording) return;
            Recording = false;
            RecordingChanged?.Invoke(false);
            var samples = Mic.End();
            RecordedSeconds = _rate > 0 ? samples.Length / (float)_rate : 0f;
            if (!WavEncoder.HasSpeech(samples, _rate)) { Set("I did not hear anything. Hold the button and speak."); return; }
            StartCoroutine(Send(WavEncoder.Encode(samples, _rate)));
        }

        public IEnumerator Send(byte[] wav)
        {
            if (CharacterSpeaking()) { Set("Waiting for them to finish..."); while (CharacterSpeaking()) yield return null; }
            if (Offline()) { Set(VoiceHealthState.OfflineBanner); yield break; }
            Set("Recognising...");
            AsrResult result = null;
            yield return AsrClient.Transcribe(Auth(), wav, r => result = r);
            if (result.Unavailable) { Set("Speech recognition is not available: use the tablet."); yield break; }
            if (!result.Usable) { Set("Say that again, please."); yield break; }
            Set("Heard: " + result.Text);
            Transcribed?.Invoke(result.Text);
        }
    }

    /// <summary>A world-space button that starts and stops <see cref="PushToTalk"/> (press once to talk, press again to send), with the status line under it.</summary>
    public sealed class PushToTalkView : MonoBehaviour
    {
        public PushToTalk Talk;
        public SpokenActionRouter Router;
        public VoiceHealth Health;
        public UnityClipPlayer Speaker;
        public Button SayAgainButton { get; private set; }
        public Canvas Canvas { get; private set; }
        public Button[] SuggestionButtons { get; private set; }
        public Button TalkButton { get; private set; }
        private TMP_Text _status, _caption;

        public void Build()
        {
            if (Canvas != null) return;
            Canvas = UiKit.WorldCanvas("Push to talk", transform, new Vector2(420, 280), 0.0006f);
            TalkButton = UiKit.MakeButton(Canvas.transform, "Talk", new Vector2(20, 14), new Vector2(380, 70), 28f);
            _caption = TalkButton.GetComponentInChildren<TMP_Text>();
            _status = UiKit.Label(Canvas.transform, "Status", "", new Vector2(20, 90), new Vector2(380, 60), 17, TextAlignmentOptions.TopLeft, UiKit.DimTextColor);
            TalkButton.onClick.AddListener(Toggle);
            SayAgainButton = UiKit.MakeButton(Canvas.transform, "Say again", new Vector2(20, 280), new Vector2(380, 50), 20f);
            SayAgainButton.onClick.AddListener(() => { if (Talk != null) Talk.SayAgain(); });
            SayAgainButton.gameObject.SetActive(false);
            SuggestionButtons = new[] { UiKit.MakeButton(Canvas.transform, "Suggestion 1", new Vector2(20, 160), new Vector2(380, 50), 20f), UiKit.MakeButton(Canvas.transform, "Suggestion 2", new Vector2(20, 220), new Vector2(380, 50), 20f) };
            for (int i = 0; i < SuggestionButtons.Length; i++)
            {
                int index = i;
                SuggestionButtons[i].onClick.AddListener(() => { if (Router != null && index < Router.Suggestions.Count) Router.Confirm(Router.Suggestions[index]); });
                SuggestionButtons[i].gameObject.SetActive(false);
            }
        }

        private void Update()
        {
            if (SuggestionButtons == null) return;
            if (SayAgainButton != null && Talk != null)
            {
                bool again = !Talk.Recording && (Talk.Status.StartsWith("Say that again") || Talk.Status.StartsWith("I did not hear"));
                if (SayAgainButton.gameObject.activeSelf != again) SayAgainButton.gameObject.SetActive(again);
            }
            if (_status != null && Talk != null)                                               // what the trainee needs to read: the recogniser, then what was understood, then which model answered
            {
                string text = string.Join("\n", new[] { Talk.Status, Router != null ? Router.Status : "", Router != null && Router.Talk != null ? Router.Talk.Status : "" }.Where(x => !string.IsNullOrEmpty(x)).Distinct());
                if (_status.text != text) _status.text = text;
            }
            int n = Router != null ? Router.Suggestions.Count : 0;
            var tablet = Router != null ? Router.Tablet : null;
            for (int i = 0; i < SuggestionButtons.Length; i++)
            {
                bool on = i < n;
                if (SuggestionButtons[i].gameObject.activeSelf != on) SuggestionButtons[i].gameObject.SetActive(on);
                if (on) { var label = SuggestionButtons[i].GetComponentInChildren<TMP_Text>(); string want = "Yes: " + (tablet != null ? tablet.LabelOf(Router.Suggestions[i]) : Router.Suggestions[i]); if (label != null && label.text != want) label.text = want; }
            }
        }

        private void OnEnable()
        {
            Build();
            if (Talk == null) return;
            Talk.StatusChanged += OnStatus;
            Talk.RecordingChanged += OnRecording;
            if (Health != null) Talk.Offline = () => Health.State.Offline;
            if (Speaker != null) Talk.CharacterSpeaking = () => Speaker.IsPlaying;
        }

        private void OnDisable()
        {
            if (Talk == null) return;
            Talk.StatusChanged -= OnStatus;
            Talk.RecordingChanged -= OnRecording;
        }

        private void OnRecording(bool on) { if (Speaker != null) Speaker.SetMuted(on); }
        private void OnStatus(string s) { if (_status != null) _status.text = s; if (_caption != null) _caption.text = Talk != null && Talk.Recording ? "Send" : "Talk"; }

        public void Toggle()
        {
            if (Talk == null) return;
            if (Talk.Recording) Talk.Release(); else Talk.Press();
            OnStatus(Talk.Status);
        }
    }
}
