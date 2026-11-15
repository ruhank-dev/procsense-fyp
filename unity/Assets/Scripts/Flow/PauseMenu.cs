using System;
using ProcSense.Core;
using ProcSense.Core.Actions;
using ProcSense.VR.Bridge;
using ProcSense.VR.UI.Kit;
using ProcSense.VR.UI.SettingsUi;
using ProcSense.VR.UI.Theme;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ProcSense.VR.Flow
{
    /// <summary>
    /// The pause menu (P3-MNU-08, with the confirmations of P3-MNU-09 and the abandon path of P3-EXM-06; the pause itself is P3-SES-08).
    /// The controller's menu button (Escape on the desktop) or the headset's system menu and taking the headset off pause a running case: the engine gets its own logged
    /// <c>pause</c> action, so its clock stops and every other action is refused with SESSION_PAUSED until <c>resume</c>; only the player resumes.
    /// The panel offers Resume, Settings, Restart scenario (practice), Return to main menu and Quit. Each way out says first what happens to the attempt and needs a confirmation:
    /// leaving submits the attempt as abandoned (saved and uploaded; the examination ends as not completed and its results are shown), restart begins the same case and seed again.
    /// Everything goes through MenuFlow and the engine verbs; the menu itself changes nothing.
    /// </summary>
    public sealed class PauseMenu : MonoBehaviour
    {
        public const float Distance = 1.2f;
        public static readonly Vector2 Size = new Vector2(640, 700);

        public MenuFlow Flow;
        /// <summary>What Quit does after its confirmation (tests replace it).</summary>
        public Action QuitApp = Application.Quit;
        /// <summary>Where the panel opens; the main camera unless set.</summary>
        public Transform Head;

        public Canvas Canvas { get; private set; }
        public KitDialog Dialog { get; private set; }
        public string LastRefusal { get; private set; } = "";

        private SimulationHost Host => Flow != null ? Flow.Host : null;
        private Session Session => Host != null ? Host.Session : null;
        public bool IsPaused => Session != null && Session.Paused;
        private bool Examination => Session != null && Session.Mode == SessionMode.Assessment;

        /// <summary>A case is running: briefing or play, not yet submitted.</summary>
        public bool CanPause => Flow != null && Session != null && !Session.Submitted && (Flow.Phase == MenuPhase.Playing || Flow.Phase == MenuPhase.Briefing);

        private InputAction _button;

        private void OnEnable()
        {
            _button = new InputAction("Pause menu", InputActionType.Button);
            _button.AddBinding("<XRController>{LeftHand}/menuButton");
            _button.AddBinding("<XRController>{LeftHand}/menu");
            _button.AddBinding("<Keyboard>/escape");
            _button.performed += _ => Toggle();
            _button.Enable();
        }

        private void OnDisable() { if (_button != null) { _button.Disable(); _button.Dispose(); _button = null; } }

        // the headset's system menu and taking the headset off pause the case; not in the Editor, where focus changes with every click
        private void OnApplicationFocus(bool focus) { if (!focus && !Application.isEditor) Pause(); }
        private void OnApplicationPause(bool paused) { if (paused && !Application.isEditor) Pause(); }

        public void Toggle() { if (IsPaused) Resume(); else Pause(); }

        public bool Pause()
        {
            if (!CanPause || IsPaused) return false;
            if (!Send(SessionControl.Pause)) return false;
            Show();
            return true;
        }

        public bool Resume()
        {
            if (!IsPaused) { Hide(); return false; }
            if (!Send(SessionControl.Resume)) return false;
            Hide();
            return true;
        }

        private bool Send(string verb)
        {
            var o = Host.Apply(new SimAction(verb, "", Channel.System));
            LastRefusal = o.Accepted ? "" : o.RejectReason;
            return o.Accepted;
        }

        // ------------------------------------------------------------------ ways out (P3-MNU-09, P3-EXM-06)

        /// <summary>What the confirmation of each way out says.</summary>
        public string Consequence(string way)
        {
            switch (way)
            {
                case "restart": return "This attempt is saved and uploaded as not finished, then the same case starts again from the beginning.";
                case "return": return Examination ? "The examination ends now as not completed, and your results are shown." : "This attempt is saved and uploaded as not finished, and you go back to the main menu.";
                default: return Examination ? "The examination ends now as not completed, then ProcSense closes." : "This attempt is saved and uploaded as not finished, then ProcSense closes.";
            }
        }

        /// <summary>Practice only: the attempt ends as abandoned, then the same case and seed start again.</summary>
        public bool Restart()
        {
            if (Examination || !Abandon()) return false;
            return Flow.Retry();
        }

        /// <summary>The attempt ends as abandoned; the examination shows its results, practice goes back to the main menu.</summary>
        public bool ReturnToMenu()
        {
            if (!Abandon()) return false;
            if (!Examination) Flow.BackToMenu();
            return true;
        }

        public void Quit()
        {
            Abandon();
            QuitApp?.Invoke();
        }

        /// <summary>Resumes the engine (a paused session refuses every action) and submits the attempt as abandoned: logged, saved and uploaded.</summary>
        private bool Abandon()
        {
            if (Session == null || Session.Submitted) { Hide(); return Session != null; }
            if (IsPaused && !Send(SessionControl.Resume)) return false;
            Hide();
            var o = Flow.Submit(abandoned: true);
            LastRefusal = o.Accepted ? "" : o.RejectReason;
            return o.Accepted;
        }

        // ------------------------------------------------------------------ panel

        private void Show()
        {
            Hide();
            var head = Head != null ? Head : (Camera.main != null ? Camera.main.transform : null);
            var root = new GameObject("Pause menu panel").transform;
            root.SetParent(transform, false);
            if (head != null)
            {
                var forward = Vector3.ProjectOnPlane(head.forward, Vector3.up);
                if (forward.sqrMagnitude < 1e-4f) forward = Vector3.forward;
                forward.Normalize();
                root.SetPositionAndRotation(head.position + forward * Distance, Quaternion.LookRotation(forward, Vector3.up));
            }
            Canvas = Kit.Canvas("Pause menu", root, Size, Distance);
            var c = Canvas.transform;
            const float pad = 40, w = 560, h = Kit.ButtonHeight, gap = 88;
            Kit.Panel(c, "Panel", Vector2.zero, Size);
            Kit.Header(c, "Paused", "The clock is stopped. Nothing happens until you resume.", new Vector2(pad, pad), w, UiIcons.Pause);
            float y = 200;
            Kit.Button(c, "Resume", new Vector2(pad, y), new Vector2(w, h), ButtonKind.Primary, UiIcons.Play, () => Resume()); y += gap;
            Kit.Button(c, "Settings", new Vector2(pad, y), new Vector2(w, h), ButtonKind.Secondary, UiIcons.Settings, () => SettingsPanel.Toggle(root, 1.1f)); y += gap;
            if (!Examination) { Kit.Button(c, "Restart scenario", new Vector2(pad, y), new Vector2(w, h), ButtonKind.Secondary, UiIcons.Retry, () => Confirm("Restart this case?", "restart", "Restart", () => Restart())); y += gap; }
            Kit.Button(c, "Return to main menu", new Vector2(pad, y), new Vector2(w, h), ButtonKind.Secondary, UiIcons.Back, () => Confirm("Return to the main menu?", "return", "Return", () => ReturnToMenu())); y += gap;
            Kit.Button(c, "Quit ProcSense", new Vector2(pad, y), new Vector2(w, h), ButtonKind.Danger, UiIcons.Quit, () => Confirm("Quit ProcSense?", "quit", "Quit", Quit));
        }

        private void Confirm(string title, string way, string yes, Action then)
        {
            if (Dialog != null) DestroyNow(Dialog.gameObject);
            Dialog = Kit.Dialog(Canvas.transform, title, Consequence(way), yes, "Cancel", new Vector2(0, (Size.y - 360) / 2), true, then, null, Size.x);
            Dialog.Open();
        }

        private void Hide()
        {
            Dialog = null;
            if (SettingsPanel.Open != null) SettingsPanel.Open.Close();
            if (Canvas != null) DestroyNow(Canvas.transform.parent.gameObject);
            Canvas = null;
        }

        /// <summary>True while the pause panel is showing.</summary>
        public bool Showing => Canvas != null;

        private static void DestroyNow(GameObject go) { if (Application.isPlaying) Destroy(go); else DestroyImmediate(go); }
    }
}
