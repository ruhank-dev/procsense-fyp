using System.Collections.Generic;
using System.Linq;
using ProcSense.Core;
using ProcSense.Core.Actions;
using ProcSense.Core.Observation;
using ProcSense.VR.Bridge;
using ProcSense.VR.UI.Results;
using ProcSense.VR.UI.Tablet;
using ProcSense.VR.Views;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ProcSense.VR.UI.Checklist
{
    /// <summary>
    /// The ward checklist panel (p0-vr-interaction-ui-s08): each content item with a verify button (tick_ward_item) that shows the evidence the session holds for it, for example the pregnancy test and its time,
    /// the fasting status, the last temperature or the wristband read; ticked items are marked; Sign sends sign_ward_checklist. Core decides whether an item can be ticked: the evidence is shown, never judged here.
    /// </summary>
    public class WardChecklistPanel : MonoBehaviour
    {
        public const string TickVerb = "tick_ward_item", SignVerb = "sign_ward_checklist";
        private const int RowCount = 14;

        public Canvas Canvas { get; private set; }
        public IReadOnlyList<string> Lines => _buttons.Where(b => b.gameObject.activeSelf).Select(b => b.GetComponentInChildren<TMP_Text>().text).ToList();
        public string HintText => _hint != null ? _hint.text : "";

        private readonly List<Button> _buttons = new List<Button>();
        private readonly List<string> _ids = new List<string>();
        private TMP_Text _hint;
        private FormSendEmitter _send;
        private object _shown;

        private static SimulationHost Host => SimulationHost.Instance;

        private void Awake() => Build();

        public void Build()
        {
            if (Canvas != null) return;
            Canvas = UiKit.WorldCanvas("Ward checklist", transform, new Vector2(900, 1000), 0.001f);
            var root = Canvas.transform;
            _send = gameObject.AddComponent<FormSendEmitter>();
            for (int i = 0; i < RowCount; i++)
            {
                int n = i;
                var b = UiKit.MakeButton(root, "", new Vector2(12, 12 + i * 60), new Vector2(876, 56), 15f);
                b.GetComponentInChildren<TMP_Text>().alignment = TextAlignmentOptions.MidlineLeft;
                b.onClick.AddListener(() => { if (n < _ids.Count) Verify(_ids[n], true); });
                _buttons.Add(b);
            }
            UiKit.MakeButton(root, LayoutText.Label(SignVerb), new Vector2(12, 860), new Vector2(400, 60), 20f).onClick.AddListener(() => Sign(true));
            _hint = UiKit.Label(root, "Hint", "", new Vector2(12, 930), new Vector2(876, 60), 18, TextAlignmentOptions.MidlineLeft, UiKit.WarnColor);
            RenderNow();
        }

        private void Update()
        {
            if (Host != null && Host.Observable != _shown) RenderNow();
        }

        public ActionOutcome Verify(string item, bool click = false) => After(PanelSend.Send(_send, TickVerb, new Dictionary<string, string> { ["item"] = item }, click));

        public ActionOutcome Sign(bool click = false) => After(PanelSend.Send(_send, SignVerb, new Dictionary<string, string>(), click));

        private ActionOutcome After(ActionOutcome outcome)
        {
            if (outcome != null) _hint.text = outcome.Accepted ? "" : ModeGate.Practice(HintTexts.For(outcome.RejectReason));
            RenderNow();
            return outcome;
        }

        /// <summary>What the session shows for a ward item now ("" when it holds nothing that bears on it).</summary>
        public static string Evidence(string item, Session s, ObservableState o)
        {
            switch (item)
            {
                case "fasting":
                    return o.Fasting == null || o.Fasting.Status.Length == 0 ? "" : "fasting: " + o.Fasting.Status.Replace("_", " ") + (o.Fasting.SafeFrom.Length > 0 ? ", safe from " + o.Fasting.SafeFrom : "");
                case "pregnancy":
                    var test = o.Investigations.LastOrDefault(i => i.Test == "pregnancy" && i.Results.Count > 0);
                    return test == null ? "" : "pregnancy test: " + test.Results[0].Value + (test.Ready.Length > 0 ? " " + test.Ready : "");
                case "temperature":
                    var t = o.Vitals.LastOrDefault(v => v.Name.ToLowerInvariant().StartsWith("temp"));
                    return t == null ? "" : "temperature: " + t.Value + (t.At.Length > 0 ? " at " + t.At : "");
                case "consent":
                    return s?.Consent != null && s.Consent.Signed ? "consent form signed" : "";
                case "bands":
                    return o.Wristband == null ? "" : "wristband read" + (o.AllergyBand ? ", allergy band on" : "");
                default:
                    return "";
            }
        }

        public void RenderNow()
        {
            if (Canvas == null) return;
            var s = Host?.Session;
            var o = Host?.Observable;
            _shown = o;
            _ids.Clear();
            var items = Host?.Content?.Ward.Items.OrderBy(i => i.Order).ToList() ?? new List<ProcSense.Core.Workflow.Steps.WardItem>();
            var ticked = new HashSet<string>(s?.Ward?.Ticked ?? new List<string>());
            for (int i = 0; i < _buttons.Count; i++)
            {
                bool used = i < items.Count;
                _buttons[i].gameObject.SetActive(used);
                if (!used) continue;
                _ids.Add(items[i].Id);
                string evidence = o == null ? "" : Evidence(items[i].Id, s, o);
                var colours = _buttons[i].colors;
                colours.normalColor = ticked.Contains(items[i].Id) ? new Color(0.12f, 0.50f, 0.24f) : UiKit.ButtonColor;   // ticked in green, as on the WHO board
                colours.highlightedColor = colours.normalColor * 1.2f;
                _buttons[i].colors = colours;
                _buttons[i].GetComponentInChildren<TMP_Text>().text = (ticked.Contains(items[i].Id) ? "[x] " : "[ ] ") + items[i].Label + (items[i].Required ? "" : "  (only if it applies)") + (evidence.Length > 0 ? "\n      " + evidence : "");
            }
        }
    }
}
