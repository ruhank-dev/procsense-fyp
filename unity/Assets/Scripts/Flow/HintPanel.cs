using ProcSense.Core;
using ProcSense.VR.UI.Kit;
using ProcSense.VR.UI.Theme;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ProcSense.VR.Flow
{
    /// <summary>
    /// The lightbulb panel (P3-EXM-03): a small glass panel with a large lightbulb, the hint counter and the last hint as a card. In the examination a press first asks
    /// "Use a hint? It costs 5 points (2 of 3 left)." (cost and limit from content/scoring/grades.json through the session); in training a hint is free and comes at once.
    /// The hint itself is MenuFlow.Hint, the engine's logged request_hint (the same verb the voice phrase sends), so the panel decides nothing. It shows while a case is played,
    /// beside the flow menu in the world, and on the left wrist when the headset layer gives the wrist (<see cref="Wrist"/>). It works in menu and voice mode alike.
    /// </summary>
    public sealed class HintPanel : MonoBehaviour
    {
        public static readonly Vector2 Size = new Vector2(420, 560);
        public const float Distance = 1.5f;

        public MenuFlow Flow { get; private set; }
        /// <summary>The cost of one hint in points, shown in the confirmation (grades.json hint_penalty; 5 by owner decision D5).</summary>
        public int Cost = 5;
        public Canvas Canvas { get; private set; }
        public Button Lightbulb { get; private set; }
        public TMP_Text Counter { get; private set; }
        public TMP_Text Card { get; private set; }
        public KitDialog Dialog { get; private set; }
        /// <summary>The left wrist (set by the headset layer before Build): the panel then rides there, small, instead of standing in the world.</summary>
        public Transform Wrist;

        private Session Session => Flow != null && Flow.Host != null ? Flow.Host.Session : null;
        private bool Examination => Session != null && Session.Mode == SessionMode.Assessment;

        public void Build(MenuFlow flow, int cost = 5)
        {
            Flow = flow;
            Cost = cost;
            var t = UiTheme.Current;
            Canvas = Kit.Canvas("Hint panel", transform, Size, Distance);
            var c = Canvas.transform;
            Kit.Panel(c, "Panel", Vector2.zero, Size);
            Kit.Text(c, "Title", "Hint", new Vector2(32, 24), new Vector2(Size.x - 64, 56), TextStyle.Heading);
            Lightbulb = Kit.Button(c, null, new Vector2((Size.x - 160) / 2, 88), new Vector2(160, 160), ButtonKind.Icon, UiIcons.Hint, Press);
            Lightbulb.name = "Button Lightbulb";
            foreach (var img in Lightbulb.GetComponentsInChildren<Image>(true)) if (img.name.StartsWith("Icon")) { img.color = t.HintText; img.rectTransform.sizeDelta = new Vector2(96, 96); img.rectTransform.anchoredPosition = new Vector2(32, -32); }
            Counter = Kit.Text(c, "Counter", "", new Vector2(32, 262), new Vector2(Size.x - 64, 68), TextStyle.Body, t.TextSecondary, TextAlignmentOptions.Center);
            Card = Kit.Text(c, "Hint text", "", new Vector2(32, 344), new Vector2(Size.x - 64, 196), TextStyle.Body);
            if (Wrist != null)
            {
                transform.SetParent(Wrist, false);
                transform.localPosition = new Vector3(0f, .06f, 0f);
                transform.localRotation = Quaternion.Euler(-60f, 0f, 0f);
                transform.localScale = Vector3.one * .4f;
            }
            flow.Changed += Refresh;
            Refresh();
        }

        /// <summary>The hint cost of content/scoring/grades.json ("hint_penalty"), or 5 (owner decision D5) when it cannot be read.</summary>
        public static int CostFrom(ProcSense.Core.Content.IContentReader reader)
        {
            try
            {
                var d = (System.Collections.Generic.Dictionary<string, object>)ProcSense.Core.Content.MiniJson.Parse(reader.ReadText("scoring/grades.json"));
                return d.TryGetValue("hint_penalty", out var v) ? System.Convert.ToInt32(v) : 5;
            }
            catch (System.Exception) { return 5; }
        }

        private void OnDestroy() { if (Flow != null) Flow.Changed -= Refresh; }

        /// <summary>The lightbulb: in the examination a confirmation with the cost first, in training the hint at once.</summary>
        public void Press()
        {
            if (Session == null || Flow.Phase != MenuPhase.Playing) return;
            int left = Session.HintLimit - Session.Hints.Count;
            if (left <= 0) { Card.text = "You have used all " + Session.HintLimit + " hints."; return; }
            if (!Examination) { Ask(); return; }
            if (Dialog != null) DestroyNow(Dialog.gameObject);
            Dialog = Kit.Dialog(Canvas.transform, "Use a hint?", "It costs " + Cost + " points (" + left + " of " + Session.HintLimit + " left).", "Use a hint", "Cancel",
                                new Vector2(0, 100), false, Ask, null, Size.x);
            Dialog.Open();
        }

        private void Ask()
        {
            var o = Flow.Hint();
            if (!o.Accepted) Card.text = o.RejectReason == ExamControl.HintLimitReached ? "You have used all " + Session.HintLimit + " hints."
                                       : o.RejectReason == ExamControl.NoHintAvailable ? "There is nothing to hint at right now." : "That is not available right now.";
            Refresh();
        }

        public void Refresh()
        {
            if (Canvas == null) return;
            bool playing = Flow.Phase == MenuPhase.Playing && Session != null;
            Canvas.gameObject.SetActive(playing);
            if (!playing) return;
            int used = Session.Hints.Count;
            Counter.text = "Hints used: " + used + " of " + Session.HintLimit + "\n" + (Examination ? Cost + " points each" : "Free in training");
            var last = Session.LastHint;
            if (last != null) Card.text = last.Text;
            else if (used == 0 && string.IsNullOrEmpty(Card.text)) Card.text = Examination ? "Stuck? A hint gives a nudge, not the answer." : "Press the lightbulb for a nudge towards the next step.";
            Lightbulb.interactable = used < Session.HintLimit;
        }

        private static void DestroyNow(GameObject go) { if (Application.isPlaying) Destroy(go); else DestroyImmediate(go); }
    }
}
