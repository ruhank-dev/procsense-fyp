using ProcSense.VR.UI.Kit;
using ProcSense.VR.UI.Theme;
using TMPro;
using UnityEngine;

namespace ProcSense.VR.Voice
{
    /// <summary>
    /// The listening and processing indicator of voice mode (P3-VOX-09), built with the kit's listening ring (P3-UIX-07):
    /// a ring near the view (following the head gently, 17 degrees below the line of sight at 1 m, inside the head-locked limits of P3-UIX-08) and, when a controller is given,
    /// a small ring on the controller. Listening: pulsing ring. Recognising: turning arc. The words heard are echoed for 3 s; a failure phrase is shown for 3 s; when the answer
    /// takes longer than 8 s the indicator stops waiting and shows the time-out phrase (P3-VOX-07), so it never spins for ever. It only shows: push-to-talk does the work.
    /// </summary>
    public sealed class VoiceIndicator : MonoBehaviour
    {
        public const float EchoSeconds = 3f, TimeoutSeconds = 8f;
        public static readonly Vector3 HeadOffset = new Vector3(0f, -.3f, 1f);

        public PushToTalk Talk { get; private set; }
        public KitListening Near { get; private set; }
        public KitListening OnController { get; private set; }
        public TMP_Text Echo { get; private set; }
        public KitListening.Mode State => Near != null ? Near.State : KitListening.Mode.Idle;
        /// <summary>Where the near indicator follows; the main camera unless set (tests set it).</summary>
        public Transform Head;

        private Canvas _near;
        private float _echoLeft, _waiting;

        public void Init(PushToTalk talk, Transform controller = null)
        {
            Talk = talk;
            var root = new GameObject("Voice indicator").transform;
            root.SetParent(transform, false);
            _near = Kit.Canvas("Voice indicator", root, new Vector2(560, 120), HeadOffset.magnitude);
            Near = KitFeedback.Listening(_near.transform, new Vector2(12, 12), 96);
            Echo = Kit.Text(_near.transform, "Echo", "", new Vector2(124, 0), new Vector2(424, 120), TextStyle.Body, UiTheme.Current.TextPrimary);
            Echo.verticalAlignment = VerticalAlignmentOptions.Middle;
            if (controller != null)
            {
                var c = Kit.Canvas("Controller voice ring", controller, new Vector2(72, 72), .4f);
                c.transform.localPosition = new Vector3(0f, .03f, .02f);
                OnController = KitFeedback.Listening(c.transform, Vector2.zero, 72);
            }
            if (talk != null)
            {
                talk.RecordingChanged += OnRecording;
                talk.StatusChanged += OnStatus;
                talk.FeedbackGiven += OnFeedback;
                talk.Transcribed += OnHeard;
            }
            Show("");
        }

        private void OnDestroy()
        {
            if (Talk == null) return;
            Talk.RecordingChanged -= OnRecording;
            Talk.StatusChanged -= OnStatus;
            Talk.FeedbackGiven -= OnFeedback;
            Talk.Transcribed -= OnHeard;
        }

        public void OnRecording(bool on)
        {
            if (on) { Set(KitListening.Mode.Listening); Show("Listening..."); }
            else { Set(KitListening.Mode.Processing); _waiting = 0f; Show(""); }
        }

        public void OnStatus(string status)
        {
            if (string.IsNullOrEmpty(status) || State == KitListening.Mode.Listening) return;
            if (status == "Recognising...") { Set(KitListening.Mode.Processing); return; }
            if (status.StartsWith("Heard: ")) { OnHeard(status.Substring(7)); return; }
            if (State == KitListening.Mode.Processing && status != "Waiting for them to finish...") Done(status);   // silence, offline, no microphone
        }

        public void OnHeard(string text) => Done("“" + text + "”");

        public void OnFeedback(VoiceFeedbackLine line) { if (line != null) Done(line.Subtitle); }

        /// <summary>Advances the echo and the time-out (Update calls it; tests call it directly).</summary>
        public void Step(float dt)
        {
            if (State == KitListening.Mode.Processing)
            {
                _waiting += dt;
                if (_waiting >= TimeoutSeconds)
                {
                    var host = ProcSense.VR.Bridge.SimulationHost.Instance;
                    bool practice = host == null || host.Session == null || host.Session.PracticeAids;
                    Done(VoiceFeedback.Loaded.Resolve(VoiceFeedback.Timeout, practice).Subtitle);
                }
            }
            if (_echoLeft > 0f)
            {
                _echoLeft -= dt;
                if (_echoLeft <= 0f && State == KitListening.Mode.Idle) Show("");
            }
        }

        private void Done(string text)
        {
            Set(KitListening.Mode.Idle);
            Show(text);
            _echoLeft = EchoSeconds;
        }

        private void Set(KitListening.Mode mode)
        {
            Near.Set(mode);
            if (OnController != null) OnController.Set(mode);
        }

        private void Show(string text)
        {
            Echo.text = text;
            _near.enabled = State != KitListening.Mode.Idle || text.Length > 0;
        }

        /// <summary>True when the near indicator is showing anything.</summary>
        public bool Visible => _near != null && _near.enabled;

        private void Update() => Step(Time.unscaledDeltaTime);

        private void LateUpdate()
        {
            var head = Head != null ? Head : (Camera.main != null ? Camera.main.transform : null);
            if (head == null || _near == null) return;
            var target = head.TransformPoint(HeadOffset);
            var root = _near.transform.parent;
            float follow = GameSettingsReduceMotion() ? 1f : 1f - Mathf.Exp(-8f * Time.unscaledDeltaTime);   // lazy follow, instant with reduce motion
            root.position = Vector3.Lerp(root.position, target, follow);
            root.rotation = Quaternion.LookRotation(root.position - head.position, Vector3.up);
        }

        private static bool GameSettingsReduceMotion() => ProcSense.VR.Settings.GameSettings.Current.reduceMotion;
    }
}
