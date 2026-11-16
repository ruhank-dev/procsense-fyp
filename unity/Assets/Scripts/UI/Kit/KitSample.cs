using ProcSense.VR.UI.Theme;
using UnityEngine;

namespace ProcSense.VR.UI.Kit
{
    /// <summary>
    /// Every kit component on one panel (P3-UIX-04 "kit sample"): drop it on an object, or open Assets/Scenes/UiKitSample.unity, or let UiKitSampleSetup capture it in the patient bay.
    /// Also the fixture of the kit tests, which check every target and every label against the readability rules.
    /// </summary>
    public sealed class KitSample : MonoBehaviour
    {
        public const float Distance = 1.4f;
        public static readonly Vector2 Size = new Vector2(1600, 1000);

        public Canvas Canvas { get; private set; }

        private void Awake() { if (Canvas == null) Canvas = Build(transform); }

        public static Canvas Build(Transform parent)
        {
            var t = UiTheme.Current;
            var canvas = Kit.Canvas("Kit sample", parent, Size, Distance);
            var root = canvas.transform;
            Kit.Panel(root, "Panel", Vector2.zero, Size);
            float pad = 48, col = 470, x2 = pad + col + 40, x3 = x2 + col + 40;
            Kit.Header(root, "Component kit", "Every component of the ProcSense interface, built from the theme tokens.", new Vector2(pad, pad), Size.x - 2 * pad, UiIcons.Settings);

            // column 1: buttons and inputs
            float y = 210;
            Kit.Button(root, "Start training", new Vector2(pad, y), new Vector2(col, Kit.ButtonHeight), ButtonKind.Primary, UiIcons.Training); y += 88;
            Kit.Button(root, "Settings", new Vector2(pad, y), new Vector2(col, Kit.ButtonHeight), ButtonKind.Secondary, UiIcons.Settings); y += 88;
            Kit.Button(root, "Submit patient to theatre", new Vector2(pad, y), new Vector2(col, Kit.ButtonHeight), ButtonKind.Danger, UiIcons.Submit); y += 88;
            Kit.Button(root, null, new Vector2(pad, y), new Vector2(72, 72), ButtonKind.Icon, UiIcons.Hint);
            Kit.Button(root, null, new Vector2(pad + 88, y), new Vector2(72, 72), ButtonKind.Icon, UiIcons.Mic);
            Kit.Button(root, null, new Vector2(pad + 176, y), new Vector2(72, 72), ButtonKind.Icon, UiIcons.Pause);
            Kit.Tooltip(root, "Ask for a hint", new Vector2(pad + 264, y + 8), 206); y += 100;
            Kit.Toggle(root, "Subtitles", new Vector2(pad, y), col, true); y += 72;
            Kit.Toggle(root, "Reduce motion", new Vector2(pad, y), col, false); y += 72;
            Kit.Slider(root, "Voice volume", new Vector2(pad, y), col, .7f); y += 80;
            Kit.Segmented(root, new[] { "Menu", "Voice" }, new Vector2(pad, y), new Vector2(col, 72), 0);

            // column 2: list rows and a card
            y = 210;
            Kit.ListRow(root, "Confirm the patient's identity", new Vector2(x2, y), col, RowState.Done); y += 76;
            Kit.ListRow(root, "Check the consent form", new Vector2(x2, y), col, RowState.Open); y += 76;
            Kit.ListRow(root, "Group and save sample", new Vector2(x2, y), col, RowState.NotNeeded); y += 76;
            Kit.ListRow(root, "Mark the operation site", new Vector2(x2, y), col, RowState.Missed); y += 92;
            Kit.Card(root, "Hint", "Confirm the patient's identity before anything else.", new Vector2(x2, y), new Vector2(col, 220), UiIcons.Hint, t.HintText); y += 240;
            // feedback (P3-UIX-07): a drawn tick and the listening ring
            var tick = KitFeedback.Tick(root, new Vector2(x2, y), 64);
            tick.fillAmount = 1f;   // drawn, in the still sample
            KitFeedback.Listening(root, new Vector2(x2 + 96, y), 64).Set(KitListening.Mode.Listening);
            Kit.Text(root, "Listening label", "Listening...", new Vector2(x2 + 176, y), new Vector2(col - 176, 64), TextStyle.Body, t.TextSecondary).verticalAlignment = TMPro.VerticalAlignmentOptions.Middle;

            // column 3: progress, badges, toast and a dialog
            y = 210;
            Kit.Ring(root, new Vector2(x3, y), 160, .78f, t.DoneText, 14);
            Kit.Text(root, "Score", "78", new Vector2(x3, y), new Vector2(160, 160), TextStyle.Title, null, TMPro.TextAlignmentOptions.Center).verticalAlignment = TMPro.VerticalAlignmentOptions.Middle;
            Kit.Badge(root, "PASS", new Vector2(x3 + 190, y + 20), t.Done);
            Kit.Badge(root, "2 HINTS", new Vector2(x3 + 190, y + 76), t.Hint);
            Kit.Badge(root, "CRITICAL", new Vector2(x3 + 190, y + 132), t.Danger); y += 200;
            var toast = Kit.Toast(root, "Blood sample sent to the laboratory", new Vector2(x3, y), col, UiIcons.Done, t.DoneText);
            toast.Group.alpha = 1f;   // shown still in the sample
            y += 110;
            var dialog = Kit.Dialog(root, "Return to the main menu?", "This examination will be submitted as it stands.", "Return", "Keep going", new Vector2(x3, y), true, null, null, col);
            dialog.gameObject.SetActive(true);
            dialog.GetComponent<CanvasGroup>().alpha = 1f;

            // the 3D elements of the kit (P3-AST-07) on a row just under the panel, in metres
            var row = new GameObject("Kit 3D elements").transform;
            row.SetParent(canvas.transform.parent, false);
            row.localPosition = canvas.transform.localPosition + Vector3.down * (Size.y * .5f * canvas.transform.lossyScale.y + .09f);
            row.localRotation = canvas.transform.localRotation;
            float x = -.18f;
            foreach (Icon3D kind in System.Enum.GetValues(typeof(Icon3D)))
            {
                var e = Kit3D.Make(kind, row);
                e.transform.localPosition = new Vector3(x + Kit3D.SizeOf(kind) * .5f, 0f, 0f);
                e.transform.localRotation = kind == Icon3D.Lightbulb || kind == Icon3D.WristButton ? Quaternion.Euler(kind == Icon3D.WristButton ? -70f : 0f, 0f, 0f) : Quaternion.Euler(0f, 180f, 0f);
                x += Kit3D.SizeOf(kind) + .05f;
            }
            return canvas;
        }
    }
}
