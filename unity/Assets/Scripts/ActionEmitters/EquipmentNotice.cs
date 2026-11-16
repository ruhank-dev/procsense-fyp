using ProcSense.VR.Bridge;
using ProcSense.VR.Views;
using TMPro;
using UnityEngine;

namespace ProcSense.VR.ActionEmitters
{
    /// <summary>
    /// Plain words for why patient equipment did not attach or come off. Session states (no scenario, paused, finished) are said in every mode; a Core refusal is said with its hint
    /// in training and with one neutral sentence in the examination, so the examination never tells the trainee what to do next. Interface wording only; Core decides every result.
    /// </summary>
    public static class EquipmentRefusalText
    {
        public const string NoSessionCode = "NO_SESSION";
        public const string PausedCode = "SESSION_PAUSED";
        public const string FinishedCode = "SESSION_SUBMITTED";
        public const string WrongTargetCode = "WRONG_TARGET";

        public const string NoSession = "Start a scenario before attaching equipment.";
        public const string Paused = "The scenario is paused: equipment cannot be attached now.";
        public const string Finished = "The scenario has finished: equipment cannot be attached.";
        public const string ExamNotAttached = "Not attached: that was not allowed now.";
        public const string ExamNotRemoved = "Not removed: that was not allowed now.";
        public const string WrongTarget = "Not attached: this does not go there. Place it on its highlighted spot.";
        public const string ExamWrongTarget = "Not attached here.";

        /// <summary>The words for a refusal code. <paramref name="practiceAids"/> is true in training; <paramref name="removing"/> is true for a refused removal.</summary>
        public static string For(string reason, bool practiceAids, bool removing = false)
        {
            switch (reason)
            {
                case null: case "": return "";
                case NoSessionCode: return NoSession;
                case PausedCode: return Paused;
                case FinishedCode: return Finished;
                case WrongTargetCode: return practiceAids ? WrongTarget : ExamWrongTarget;
            }
            if (!practiceAids) return removing ? ExamNotRemoved : ExamNotAttached;
            string words = reason == "PATIENT_ELSEWHERE" ? "the patient is not here" : HintTexts.For(reason);
            return (removing ? "Not removed: " : "Not attached: ") + words + ".";
        }
    }

    /// <summary>
    /// A small world-space label above the patient target that says why equipment was not attached (the amber target alone does not say why). Built once with the UI kit on first use,
    /// text written only when it changes, hidden after a few seconds; no per-frame allocation, no fullscreen layer, no raycast target.
    /// </summary>
    public sealed class EquipmentNotice
    {
        public const float ShowSeconds = 4f;
        private const float MetresPerPixel = .0005f;
        private static readonly Vector2 Size = new Vector2(560, 110);
        private GameObject _root;
        private TMP_Text _label;
        private float _until;

        public bool Visible => _root != null && _root.activeSelf;
        public string Text => Visible ? _label.text : "";

        public void Show(string text, Vector3 near)
        {
            if (string.IsNullOrEmpty(text)) return;
            if (_root == null)
            {
                var canvas = UiKit.WorldCanvas("Equipment notice", null, Size, MetresPerPixel);
                _root = canvas.gameObject;
                _label = UiKit.Label(_root.transform, "Reason", text, new Vector2(16, 10), Size - new Vector2(32, 20), 34, TextAlignmentOptions.Center, UiKit.WarnColor);
            }
            bool changed = !Visible || _label.text != text;
            if (_label.text != text) _label.text = text;
            if (changed)
            {
                var pos = near + Vector3.up * .14f;
                var cam = Camera.main;
                var facing = cam != null ? pos - cam.transform.position : Vector3.forward;
                facing.y = 0;
                _root.transform.SetPositionAndRotation(pos, Quaternion.LookRotation(facing.sqrMagnitude > 1e-4f ? facing : Vector3.forward, Vector3.up));
                _root.SetActive(true);
            }
            _until = Time.unscaledTime + ShowSeconds;
        }

        public void Tick()
        {
            if (Visible && Time.unscaledTime > _until) _root.SetActive(false);
        }

        public void Hide() { if (_root != null) _root.SetActive(false); }

        public void Dispose() { if (_root != null) Object.Destroy(_root); }
    }
}
