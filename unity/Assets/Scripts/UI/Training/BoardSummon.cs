using ProcSense.VR.UI.Kit;
using ProcSense.VR.UI.Theme;
using UnityEngine;
using UnityEngine.UI;

namespace ProcSense.VR.UI.Training
{
    /// <summary>
    /// Summoning the training board (P3-TRN-04): a small board icon at the edge of the view that follows the head gently, and a button on the left wrist when the headset layer gives
    /// one. Pressing either brings the board world-locked 0.8 m in front of the eyes, level and facing the trainee; pressing again puts it back where it stands in the bay. Nothing
    /// stays in the centre of the view. Training only: the icon shows only while the board does (never in the examination).
    /// The plan put the icon at 35 degrees right and 20 up; the head-locked limits of P3-UIX-08 (20 to the side, 15 up) win, so it sits at 18 right and 12 up.
    /// </summary>
    public sealed class BoardSummon : MonoBehaviour
    {
        public const float BoardDistance = .8f, IconDistance = 1f, IconRight = 18f, IconUp = 12f;

        public TrainingBoardView Board { get; private set; }
        /// <summary>The eyes (the main camera unless set).</summary>
        public Transform Head;
        public Canvas Icon { get; private set; }
        public Button IconButton { get; private set; }
        public Button WristButton { get; private set; }
        public bool Summoned { get; private set; }

        private Vector3 _homePosition;
        private Quaternion _homeRotation;

        /// <summary>The icon's place in head space: 1 m away, 18 degrees right and 12 up.</summary>
        public static Vector3 IconOffset
        {
            get
            {
                float yaw = IconRight * Mathf.Deg2Rad, pitch = IconUp * Mathf.Deg2Rad;
                return new Vector3(Mathf.Sin(yaw) * Mathf.Cos(pitch), Mathf.Sin(pitch), Mathf.Cos(yaw) * Mathf.Cos(pitch)) * IconDistance;
            }
        }

        public void Init(TrainingBoardView board, Transform wrist = null)
        {
            Board = board;
            _homePosition = board.transform.position;
            _homeRotation = board.transform.rotation;
            var iconRoot = new GameObject("Board summon icon").transform;
            iconRoot.SetParent(transform, false);
            Icon = Kit.Kit.Canvas("Board summon", iconRoot, new Vector2(96, 96), IconDistance);
            IconButton = Kit.Kit.Button(Icon.transform, null, Vector2.zero, new Vector2(96, 96), ButtonKind.Icon, UiIcons.Board, Toggle);
            if (wrist != null)
            {
                var w = Kit.Kit.Canvas("Board wrist button", wrist, new Vector2(96, 96), .35f);
                w.transform.localPosition = new Vector3(0f, .04f, -.02f);
                w.transform.localRotation = Quaternion.Euler(-60f, 0f, 0f);
                WristButton = Kit.Kit.Button(w.transform, null, Vector2.zero, new Vector2(96, 96), ButtonKind.Icon, UiIcons.Board, Toggle);
            }
        }

        /// <summary>The icon or the wrist button: brings the board in front, or sends it back.</summary>
        public void Toggle()
        {
            if (Board == null) return;
            var head = HeadNow();
            if (!Summoned && head != null)
            {
                var forward = Vector3.ProjectOnPlane(head.forward, Vector3.up);
                if (forward.sqrMagnitude < 1e-4f) forward = Vector3.forward;
                forward.Normalize();
                Board.transform.SetPositionAndRotation(head.position + forward * BoardDistance, Quaternion.LookRotation(forward, Vector3.up));
                Summoned = true;
            }
            else
            {
                Board.transform.SetPositionAndRotation(_homePosition, _homeRotation);
                Summoned = false;
            }
            if (Board.Collapsed) Board.ToggleCollapsed();   // a summoned board is open
        }

        private Transform HeadNow() => Head != null ? Head : (Camera.main != null ? Camera.main.transform : null);

        /// <summary>Shows the icon only while the board is shown (training with a session) and keeps it at the edge of the view (LateUpdate calls it; tests call it directly).</summary>
        public void Follow(float dt)
        {
            if (Icon == null || Board == null) return;
            bool on = Board.Canvas != null && Board.Canvas.gameObject.activeSelf;
            if (Icon.gameObject.activeSelf != on) Icon.gameObject.SetActive(on);
            if (WristButton != null && WristButton.gameObject.activeSelf != on) WristButton.gameObject.SetActive(on);
            var head = HeadNow();
            if (!on || head == null) return;
            var root = Icon.transform.parent;
            var target = head.TransformPoint(IconOffset);
            float k = ProcSense.VR.Settings.GameSettings.Current.reduceMotion ? 1f : 1f - Mathf.Exp(-6f * dt);
            root.position = Vector3.Lerp(root.position, target, k);
            root.rotation = Quaternion.LookRotation(root.position - head.position, Vector3.up);
        }

        private void LateUpdate() => Follow(Time.unscaledDeltaTime);
    }
}
