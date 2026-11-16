using System.Collections.Generic;
using System.Linq;
using ProcSense.VR.Bridge;
using ProcSense.VR.UI.Kit;
using ProcSense.VR.UI.Theme;
using UnityEngine;

namespace ProcSense.VR.UI.Training
{
    /// <summary>
    /// P3-TRN-05: when the engine completes a goal in training, the trainee sees it once: a tick drawn on the training board when it is open, or a toast with the step's name in front of them
    /// when it is not (the chime is the sound player's, from the same event). Driven only by the feedback stream's GoalCompleted, and remembered per session and goal, so a goal completed once
    /// gives one tick however often the stream is polled. Nothing in the examination.
    /// </summary>
    public sealed class CompletionFeedback : MonoBehaviour
    {
        public const float ToastDistance = .9f, ToastBelowEyes = .25f;

        public int Ticks { get; private set; }
        public int Toasts { get; private set; }
        public string LastText { get; private set; } = "";
        public System.Func<BoardSummon> Board = () => FindAnyObjectByType<BoardSummon>();
        public System.Func<Transform> Head = () => Camera.main != null ? Camera.main.transform : null;

        private readonly HashSet<(object, string)> _seen = new HashSet<(object, string)>();
        private SimFeedback _stream;
        private Canvas _toastCanvas;
        private KitToast _toast;

        public void Attach(SimFeedback stream)
        {
            if (_stream != null) _stream.Raised -= OnFeedback;
            _stream = stream;
            if (_stream != null) _stream.Raised += OnFeedback;
        }

        private void OnEnable() { if (_stream == null) Attach(SimFeedback.Default); }
        private void OnDestroy() { if (_stream != null) _stream.Raised -= OnFeedback; if (_toastCanvas != null) Destroy(_toastCanvas.gameObject); }

        public void OnFeedback(SimFeedbackEvent e)
        {
            if (e.Kind != FeedbackKind.GoalCompleted || !ModeGate.PracticeAids) return;
            var session = SimulationHost.Instance != null ? (object)SimulationHost.Instance.Session : this;
            if (!_seen.Add((session, e.Id))) return;                                  // one per engine completion, never more
            var summon = Board?.Invoke();
            var board = summon != null ? summon.Board : null;
            string label = board != null ? board.Model.AllSteps.FirstOrDefault(s => s.Id == e.Id)?.Label : null;
            LastText = "Done: " + (string.IsNullOrEmpty(label) ? e.Id.Replace('_', ' ') : label);
            if (summon != null && summon.Summoned && board.Canvas != null)
            {
                var tick = KitFeedback.Tick(board.Canvas.transform, new Vector2(24, 24), 64, UiTheme.Current.DoneText);
                KitFeedback.Draw(tick, () => { if (tick != null) Destroy(tick.gameObject, .6f); });
                Ticks++;
            }
            else { ShowToast(LastText); Toasts++; }
        }

        private void ShowToast(string text)
        {
            var head = Head?.Invoke();
            if (_toastCanvas == null)
            {
                _toastCanvas = ProcSense.VR.UI.Kit.Kit.Canvas("Completion toast", null, new Vector2(560, 100), ToastDistance);
                _toast = ProcSense.VR.UI.Kit.Kit.Toast(_toastCanvas.transform, text, Vector2.zero, 560, UiIcons.Done, UiTheme.Current.DoneText);
            }
            if (head != null)
            {
                var fwd = Vector3.ProjectOnPlane(head.forward, Vector3.up);
                if (fwd.sqrMagnitude < 1e-4f) fwd = Vector3.forward;
                fwd.Normalize();
                var at = head.position + fwd * ToastDistance + Vector3.down * ToastBelowEyes;
                _toastCanvas.transform.SetPositionAndRotation(at, Quaternion.LookRotation(fwd, Vector3.up));
            }
            _toast.Show(text);
        }
    }
}
