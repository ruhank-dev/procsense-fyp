using System.Collections.Generic;
using System.Linq;
using ProcSense.Core.Difficulty;
using ProcSense.Core.Scoring;
using ProcSense.VR.Bridge;
using ProcSense.VR.Views;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ProcSense.VR.UI.Debrief
{
    /// <summary>
    /// The debrief screen (p0-vr-interaction-ui-s09), meant to be read in about two minutes: how the case ended, the items with the most serious first (minor ones collapsed until asked for), a bar per
    /// behaviour (credits and penalties), the timeline with the lateness of anything done after its window, the tests the case did not need with the waiting they cost, and in practice the case to play next.
    /// Tapping an item or a timeline marker opens the evidence drawer: the log entries behind it, the rule and the source. Everything comes from Core's debrief facts (<see cref="DebriefFacts"/>); the screen
    /// adds nothing. The AI debrief tab exists but stays hidden until P1 (template only, offline). Not placed in a scene yet (UX-P0-VRUI).
    /// </summary>
    public class DebriefView : MonoBehaviour
    {
        private const int ItemRows = 9;

        public Canvas Canvas { get; private set; }
        public DebriefFacts Facts { get; private set; }
        public bool MinorShown { get; private set; }
        public IReadOnlyList<DebriefLine> ShownLines => _shown;
        public string HeadlineText => _headline != null ? _headline.text : "";
        public string CategoryText => string.Join("\n", _bars.Select(b => b.label.text));
        public string UnneededText => _unneeded != null ? _unneeded.text : "";
        public string NextCaseText => _next != null ? _next.text : "";
        public bool AiTabVisible => _aiTab != null && _aiTab.gameObject.activeSelf;
        public EvidenceDrawer Drawer { get; private set; }
        public TimelineView Timeline { get; private set; }

        private readonly List<DebriefLine> _shown = new List<DebriefLine>();
        private readonly List<Button> _itemButtons = new List<Button>();
        private readonly List<(TMP_Text label, Image credit, Image penalty)> _bars = new List<(TMP_Text, Image, Image)>();
        private TMP_Text _headline, _unneeded, _next, _more;
        private Button _aiTab;
        private Transform _root;

        private static SimulationHost Host => SimulationHost.Instance;

        private void Awake() => Build();

        public void Build()
        {
            if (Canvas != null) return;
            Canvas = UiKit.WorldCanvas("Debrief", transform, new Vector2(1400, 900), 0.001f);
            _root = Canvas.transform;
            _headline = UiKit.Label(_root, "Headline", "", new Vector2(16, 10), new Vector2(1000, 50), 28);
            UiKit.MakeButton(_root, "Template", new Vector2(1060, 12), new Vector2(150, 44), 16f).interactable = false;      // the debrief shown is the template one
            _aiTab = UiKit.MakeButton(_root, "AI", new Vector2(1220, 12), new Vector2(150, 44), 16f);
            _aiTab.gameObject.SetActive(false);                                                                               // P1: an AI rephrasing of the same facts

            UiKit.Label(_root, "Items title", "What happened, most serious first", new Vector2(16, 70), new Vector2(680, 30), 18, color: UiKit.DimTextColor);
            for (int i = 0; i < ItemRows; i++)
            {
                int n = i;
                var b = UiKit.MakeButton(_root, "", new Vector2(16, 104 + i * 54), new Vector2(680, 48), 14f);
                b.GetComponentInChildren<TMP_Text>().alignment = TextAlignmentOptions.MidlineLeft;
                b.onClick.AddListener(() => { if (n < _shown.Count) Open(_shown[n]); });
                _itemButtons.Add(b);
            }
            var more = UiKit.MakeButton(_root, "", new Vector2(16, 594), new Vector2(680, 40), 14f);
            more.onClick.AddListener(() => { MinorShown = !MinorShown; Render(); });
            _more = more.GetComponentInChildren<TMP_Text>();

            UiKit.Label(_root, "Bars title", "By behaviour (credits / penalties)", new Vector2(716, 70), new Vector2(668, 30), 18, color: UiKit.DimTextColor);
            Timeline = new TimelineView(_root, new Vector2(16, 646), new Vector2(1368, 90), this);
            _unneeded = UiKit.Label(_root, "Unneeded", "", new Vector2(16, 744), new Vector2(680, 140), 15);
            _next = UiKit.Label(_root, "Next case", "", new Vector2(716, 744), new Vector2(668, 140), 15, color: UiKit.WarnColor);
            Drawer = new EvidenceDrawer(_root, new Vector2(716, 360), new Vector2(668, 274));       // under the bars (six at most)
            Refresh();
        }

        /// <summary>Builds the debrief from the running session now (Core's facts) and draws it.</summary>
        public void Refresh()
        {
            var host = Host;
            if (host?.Session == null || host.Content == null) return;
            Facts = DebriefFacts.Build(host.Session, host.Content);
            Render();
        }

        public void ShowMinor(bool show) { MinorShown = show; Render(); }

        /// <summary>Opens the evidence drawer for an item.</summary>
        public void Open(DebriefLine line) => Drawer.Show(Host.Session, line);

        /// <summary>Opens the drawer at a timeline marker: the item it marks, or the plain log entry for an action.</summary>
        public void OpenSeq(int seq)
        {
            var line = Facts?.Lines.FirstOrDefault(l => l.Evidence.Contains(seq));
            if (line != null) { Open(line); return; }
            var e = DebriefText.Entry(Host.Session, seq);
            if (e != null) Drawer.ShowEntry(Host.Session, e);
        }

        private void Render()
        {
            if (Facts == null) return;
            var s = Host.Session;
            _headline.text = DebriefText.Headline(Facts);

            var ordered = DebriefText.Ordered(Facts);
            int collapsed = ordered.Count(DebriefText.Collapsed);
            _shown.Clear();
            _shown.AddRange(ordered.Where(l => MinorShown || !DebriefText.Collapsed(l)));
            for (int i = 0; i < _itemButtons.Count; i++)
            {
                bool used = i < _shown.Count;
                _itemButtons[i].gameObject.SetActive(used);
                if (!used) continue;
                var l = _shown[i];
                _itemButtons[i].GetComponentInChildren<TMP_Text>().text = l.Severity.ToUpperInvariant() + "  " + l.Label + (l.Points != 0 ? "  (" + (l.Points > 0 ? "+" : "") + l.Points + ")" : "");
                var c = _itemButtons[i].colors;
                c.normalColor = l.Severity == "critical" ? new Color(0.62f, 0.10f, 0.10f) : l.Severity == "major" ? new Color(0.55f, 0.30f, 0.08f) : l.Points > 0 ? new Color(0.12f, 0.42f, 0.22f) : UiKit.ButtonColor;
                c.highlightedColor = c.normalColor * 1.2f;
                _itemButtons[i].colors = c;
            }
            _more.text = collapsed == 0 ? "" : MinorShown ? "Hide the " + collapsed + " minor items" : "Show " + collapsed + " minor items";
            _more.transform.parent.gameObject.SetActive(collapsed > 0);

            Bars();
            Timeline.Draw(s, Facts);
            var unneeded = DebriefText.Unneeded(s, Host.Content, Facts);
            _unneeded.text = unneeded.Count == 0 ? "No test was ordered that the case did not need." :
                "Tests the case did not need:\n" + string.Join("\n", unneeded.Select(u => u.Label + " at " + u.At + ": the patient waited up to " + u.Minutes + " min for it"));
            _next.text = ModeGate.Practice(NextCase());
        }

        private void Bars()
        {
            var cats = Facts.Categories.Where(c => c.Items > 0).Take(6).ToList();
            while (_bars.Count < cats.Count)
            {
                int i = _bars.Count;
                var label = UiKit.Label(_root, "Bar label", "", new Vector2(716, 104 + i * 40), new Vector2(300, 36), 14, TextAlignmentOptions.MidlineLeft);
                var credit = UiKit.Panel(_root, "Credit", new Vector2(1020, 112 + i * 40), new Vector2(1, 20), new Color(0.2f, 0.7f, 0.35f)).GetComponent<Image>();
                var penalty = UiKit.Panel(_root, "Penalty", new Vector2(1020, 112 + i * 40), new Vector2(1, 20), new Color(0.85f, 0.25f, 0.2f)).GetComponent<Image>();
                _bars.Add((label, credit, penalty));
            }
            int max = Mathf.Max(1, cats.Select(c => c.Credits + c.Penalties).DefaultIfEmpty(1).Max());
            for (int i = 0; i < _bars.Count; i++)
            {
                bool used = i < cats.Count;
                _bars[i].label.gameObject.SetActive(used);
                _bars[i].credit.gameObject.SetActive(used);
                _bars[i].penalty.gameObject.SetActive(used);
                if (!used) continue;
                var c = cats[i];
                _bars[i].label.text = c.Label + "  +" + c.Credits + " / -" + c.Penalties;
                float w = 360f;
                var cr = (RectTransform)_bars[i].credit.transform;
                cr.sizeDelta = new Vector2(Mathf.Max(1f, w * c.Credits / max), 20);
                var pr = (RectTransform)_bars[i].penalty.transform;
                pr.anchoredPosition = new Vector2(1020 + cr.sizeDelta.x, pr.anchoredPosition.y);
                pr.sizeDelta = new Vector2(Mathf.Max(1f, w * c.Penalties / max), 20);
            }
        }

        // The engine's next-case choice (the same as the CLI's next-case) from this session alone: a practice aid, never shown in assessment.
        private string NextCase()
        {
            var host = Host;
            var by = Facts.Categories.Where(c => c.Items > 0).ToDictionary(c => c.Behaviour, c => (c.Credits, c.Penalties));
            var history = new List<SessionRecord> { new SessionRecord(Facts.Case, by, null) };
            var files = host.Content.CaseFiles;
            var ratings = Ratings.From(history, host.Content.Difficulty, id => files.TryGetValue(id, out var f) ? f.Tier : "P0");
            var pick = Selector.Next(ratings, new List<string> { Facts.Case }, files, host.Session.Mode, id => host.Content.Scoring.Find(id)?.Label ?? id);
            return pick.CaseId.Length == 0 ? "" : pick.Explanation;
        }
    }
}
