using ProcSense.VR.UI.Theme;
using UnityEngine;
using UnityEngine.UI;

namespace ProcSense.VR.UI.Kit
{
    /// <summary>
    /// The wrist menu (P3-CTX-07, and the wrist entry of P3-TRN-04, P3-SET-02 and P3-MNU-08): four glass icon buttons on the back of the non-dominant controller (the left one, or the right
    /// for a left-handed player): Tablet brings the tablet in front of the trainee as a full list of actions and sends it back on the next press; Board brings the training board (training only);
    /// Settings opens the settings panel; Pause pauses a running case. Each button only calls what the other menus already call; the wrist adds no action of its own.
    /// It finds the hand on the XR rig by name ("Left Controller" / "Right Controller") once the rig exists; on the desktop without a rig there is no wrist menu.
    /// </summary>
    public sealed class WristMenu : MonoBehaviour
    {
        public const float TabletDistance = .55f, TabletBelowEyes = .25f;
        public static readonly Vector2 Size = new Vector2(352, 96);

        /// <summary>The controller it rides on (found on the rig unless set).</summary>
        public Transform Hand;
        /// <summary>The eyes (the main camera unless set).</summary>
        public Transform Head;
        public Canvas Canvas { get; private set; }
        public Button TabletButton { get; private set; }
        public Button BoardButton { get; private set; }
        public Button SettingsButton { get; private set; }
        public Button PauseButton { get; private set; }
        public bool TabletSummoned { get; private set; }

        private Transform _tablet;
        private Vector3 _tabletHome;
        private Quaternion _tabletHomeRotation;
        private Transform _tabletParent;

        private void Update()
        {
            if (Canvas == null)
            {
                if (Hand == null) Hand = FindHand();
                if (Hand != null) Build();
                return;
            }
            Refresh();
        }

        /// <summary>The non-dominant controller on the XR rig, or null.</summary>
        public static Transform FindHand()
        {
            string name = ProcSense.VR.Settings.GameSettings.Current.leftHanded ? "Right Controller" : "Left Controller";
            foreach (var t in FindObjectsByType<Transform>(FindObjectsSortMode.None)) if (t.name == name && t.GetComponentInParent<Unity.XR.CoreUtils.XROrigin>() != null) return t;
            return null;
        }

        public void Build()
        {
            if (Canvas != null || Hand == null) return;
            var root = new GameObject("Wrist menu").transform;
            root.SetParent(Hand, false);
            root.localPosition = new Vector3(0f, .035f, -.07f);     // on the back of the wrist, facing up and towards the eyes when the wrist is turned
            root.localRotation = Quaternion.Euler(-60f, 0f, 0f);
            Canvas = Kit.Canvas("Wrist menu", root, Size, .35f);
            var c = Canvas.transform;
            Kit.Panel(c, "Panel", Vector2.zero, Size, GlassPanel.Style.Dark, null, 24);
            float x = 12;
            TabletButton = Kit.Button(c, null, new Vector2(x, 12), new Vector2(72, 72), ButtonKind.Icon, UiIcons.Records, ToggleTablet); x += 84;
            BoardButton = Kit.Button(c, null, new Vector2(x, 12), new Vector2(72, 72), ButtonKind.Icon, UiIcons.Board, ToggleBoard); x += 84;
            SettingsButton = Kit.Button(c, null, new Vector2(x, 12), new Vector2(72, 72), ButtonKind.Icon, UiIcons.Settings, ToggleSettings); x += 84;
            PauseButton = Kit.Button(c, null, new Vector2(x, 12), new Vector2(72, 72), ButtonKind.Icon, UiIcons.Pause, () => { var p = ProcSense.VR.Flow.PauseMenu.Running(); if (p != null) p.Toggle(); });
            TabletButton.name = "Button Tablet"; BoardButton.name = "Button Board"; SettingsButton.name = "Button Settings"; PauseButton.name = "Button Pause";
            Refresh();
        }

        /// <summary>Shows the board button only in training with the board up, and the pause button only while a case runs.</summary>
        public void Refresh()
        {
            var summon = FindAnyObjectByType<ProcSense.VR.UI.Training.BoardSummon>();
            bool board = summon != null && summon.Board != null && summon.Board.Canvas != null && summon.Board.Canvas.gameObject.activeSelf;
            if (BoardButton.interactable != board) BoardButton.interactable = board;
            bool pause = ProcSense.VR.Flow.PauseMenu.Running() != null;
            if (PauseButton.interactable != pause) PauseButton.interactable = pause;
            bool tablet = FindTablet() != null;
            if (TabletButton.interactable != tablet) TabletButton.interactable = tablet;
        }

        private Transform HeadNow() => Head != null ? Head : (Camera.main != null ? Camera.main.transform : null);

        private Transform FindTablet()
        {
            if (_tablet != null) return _tablet;
            var view = FindAnyObjectByType<ProcSense.VR.Views.TabletView>();
            return _tablet = view != null ? view.transform : null;
        }

        /// <summary>Brings the tablet (the full list of actions, P3-CTX-07) in front of the trainee, a little below the eyes and tilted up, or sends it back where it was.</summary>
        public void ToggleTablet()
        {
            var tablet = FindTablet();
            var head = HeadNow();
            if (tablet == null) return;
            if (!TabletSummoned && head != null)
            {
                _tabletParent = tablet.parent;
                _tabletHome = tablet.localPosition;
                _tabletHomeRotation = tablet.localRotation;
                var forward = Vector3.ProjectOnPlane(head.forward, Vector3.up);
                if (forward.sqrMagnitude < 1e-4f) forward = Vector3.forward;
                forward.Normalize();
                var at = head.position + forward * TabletDistance + Vector3.down * TabletBelowEyes;
                tablet.SetParent(null, true);
                tablet.SetPositionAndRotation(at, Quaternion.LookRotation(at - head.position, Vector3.up));
                TabletSummoned = true;
            }
            else
            {
                tablet.SetParent(_tabletParent, false);
                tablet.localPosition = _tabletHome;
                tablet.localRotation = _tabletHomeRotation;
                TabletSummoned = false;
            }
        }

        /// <summary>Opens the settings panel straight ahead at its reading distance, or closes it.</summary>
        public void ToggleSettings()
        {
            var head = HeadNow();
            var anchor = new GameObject("settings anchor").transform;
            try
            {
                if (head != null)
                {
                    var forward = Vector3.ProjectOnPlane(head.forward, Vector3.up);
                    if (forward.sqrMagnitude < 1e-4f) forward = Vector3.forward;
                    forward.Normalize();
                    anchor.SetPositionAndRotation(head.position + forward * ProcSense.VR.UI.SettingsUi.SettingsPanel.Distance, Quaternion.LookRotation(forward, Vector3.up) * Quaternion.Euler(0f, -20f, 0f));
                }
                ProcSense.VR.UI.SettingsUi.SettingsPanel.Toggle(anchor, 0f);   // the panel turns 20 degrees itself; the anchor cancels it so it faces the trainee
            }
            finally { if (Application.isPlaying) Destroy(anchor.gameObject); else DestroyImmediate(anchor.gameObject); }
        }

        public void ToggleBoard()
        {
            var summon = FindAnyObjectByType<ProcSense.VR.UI.Training.BoardSummon>();
            if (summon != null) summon.Toggle();
        }
    }
}
