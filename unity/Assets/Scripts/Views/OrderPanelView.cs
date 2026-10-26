using System.Collections.Generic;
using System.Linq;
using ProcSense.VR.Voice;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ProcSense.VR.Views
{
    /// <summary>
    /// The in-VR panel of a spoken medical order (ADR-0028, 27 Oct 2026): which system read the sentence (Gemma Cloud, or the offline grammar and why), what the simulator understood ("Confirm: order CRP?") with Yes and No, a question when the sentence
    /// fitted two items (choose one, or say it again), the closed list of a missing detail (the oxygen device, how the patient is moved), and what happened to the order. It shows what <see cref="SpokenOrderFlow"/> holds and presses its buttons; it applies
    /// nothing itself, and a Yes is a button press by the trainee or the trainee's own spoken yes: nothing is sent to the engine before one of them. Every button here is also reachable by speech through the flow, and the tablet is untouched.
    /// </summary>
    public sealed class OrderPanelView : MonoBehaviour
    {
        public SpokenOrderFlow Flow;
        public Canvas Canvas { get; private set; }
        public Button YesButton { get; private set; }
        public Button NoButton { get; private set; }
        public Button[] OptionButtons { get; private set; }
        public string BadgeText => _badge != null ? _badge.text : "";
        public string MessageText => _message != null ? _message.text : "";

        private TMP_Text _title, _badge, _message;
        private GameObject _root;

        public void Build()
        {
            if (Canvas != null) return;
            Canvas = UiKit.WorldCanvas("Spoken order", transform, new Vector2(560, 440), 0.0006f);
            _root = Canvas.gameObject;
            var root = Canvas.transform;
            _title = UiKit.Label(root, "Title", "Spoken order", new Vector2(16, 10), new Vector2(240, 30), 20, TextAlignmentOptions.TopLeft, UiKit.DimTextColor);
            _badge = UiKit.Label(root, "Interpreter", "", new Vector2(250, 10), new Vector2(298, 30), 18, TextAlignmentOptions.TopRight, UiKit.WarnColor);
            _message = UiKit.Label(root, "Message", "", new Vector2(16, 48), new Vector2(528, 130), 28);
            YesButton = UiKit.MakeButton(root, "Yes", new Vector2(16, 190), new Vector2(256, 70), 30f);
            NoButton = UiKit.MakeButton(root, "No", new Vector2(288, 190), new Vector2(256, 70), 30f);
            OptionButtons = new Button[4];
            for (int i = 0; i < OptionButtons.Length; i++)
            {
                OptionButtons[i] = UiKit.MakeButton(root, "Option " + (i + 1), new Vector2(16 + (i % 2) * 272, 270 + (i / 2) * 76), new Vector2(256, 66), 24f);
                OptionButtons[i].gameObject.SetActive(false);
            }
            YesButton.onClick.AddListener(() => { if (Flow != null) Flow.StartCoroutine(Flow.Confirm()); });
            NoButton.onClick.AddListener(() => { if (Flow != null) Flow.StartCoroutine(Flow.Reject()); });
            for (int i = 0; i < OptionButtons.Length; i++)
            {
                int index = i;
                OptionButtons[i].onClick.AddListener(() => PickOption(index));
            }
            YesButton.gameObject.SetActive(false);
            NoButton.gameObject.SetActive(false);
            _root.SetActive(false);
        }

        private readonly List<System.Action> _picks = new List<System.Action>();

        private void PickOption(int index) { if (index < _picks.Count) _picks[index](); }

        private void OnEnable()
        {
            Build();
            if (Flow != null) { Flow.Changed += Refresh; Refresh(); }
        }

        private void OnDisable() { if (Flow != null) Flow.Changed -= Refresh; }

        /// <summary>Redraws from the flow. Public so a test can call it without a frame.</summary>
        public void Refresh()
        {
            if (Canvas == null) Build();
            if (Flow == null) return;
            bool show = Flow.Phase != OrderPhase.Idle || !string.IsNullOrEmpty(Flow.InterpreterText);
            if (_root.activeSelf != show) _root.SetActive(show);
            _badge.text = Flow.InterpreterText;
            _message.text = Flow.Message;
            _picks.Clear();
            var phase = Flow.Phase;
            YesButton.gameObject.SetActive(phase == OrderPhase.Proposed);
            NoButton.gameObject.SetActive(phase == OrderPhase.Proposed || phase == OrderPhase.NeedsDetail);
            var choices = new List<(string label, System.Action pick)>();
            if (phase == OrderPhase.NeedsDetail && Flow.Current != null && Flow.Current.Needs.Count > 0)
            {
                var need = Flow.Current.Needs[0];
                foreach (var o in need.Options) { var opt = o; choices.Add((opt.Value, () => Flow.StartCoroutine(Flow.Choose(need.Param, opt.Key)))); }
            }
            else if (phase == OrderPhase.Clarifying && Flow.Current != null)
            {
                foreach (var label in Flow.Current.Options) { var l = label; choices.Add((l, () => Flow.StartCoroutine(Flow.PickOption(l, ok => { })))); }
            }
            for (int i = 0; i < OptionButtons.Length; i++)
            {
                bool on = i < choices.Count;
                OptionButtons[i].gameObject.SetActive(on);
                if (!on) continue;
                var text = OptionButtons[i].GetComponentInChildren<TMP_Text>();
                if (text != null) text.text = choices[i].label;
                _picks.Add(choices[i].pick);
            }
        }
    }
}
