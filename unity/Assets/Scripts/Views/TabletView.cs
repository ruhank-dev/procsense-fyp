using System;
using System.Collections.Generic;
using System.Linq;
using ProcSense.Core.Observation;
using ProcSense.VR.ActionEmitters;
using ProcSense.VR.Bridge;
using ProcSense.VR.Fallback;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ProcSense.VR.Views
{
    /// <summary>
    /// The hand-held tablet: the Talk menu, the sensor and reading buttons, the patient's latest answer as a subtitle, the clerking note
    /// and the hint for the last refused action. Every button sends one action through an emitter; which buttons are enabled depends only
    /// on the ObservableState. Every VR gesture also has a button here (the tablet fallback).
    /// </summary>
    public class TabletView : StateView
    {
        private sealed class Item
        {
            public string Label, Verb, Key = "", Value = "";
            public Func<ObservableState, bool> Enabled;
            public Button Button;
        }

        private static readonly (string label, string topic)[] HistoryTopics =
        {
            ("Main problem (pain)", "complaint"), ("Red flags", "red_flags"), ("Other illnesses", "past_history"), ("Medicines", "medicines"),
            ("Herbal remedies", "herbal"), ("Previous anaesthetic", "previous_anaesthetic"), ("Family anaesthetic", "family_anaesthetic"),
            ("Smoking", "smoking"), ("Alcohol", "alcohol"), ("Last food", "last_food"), ("Last drink", "last_drink"), ("Pregnancy (privately)", "pregnancy"),
        };

        private static readonly string[] TabNames = { "Identity", "History", "Observe", "More" };

        private readonly List<Item>[] _tabs = { new List<Item>(), new List<Item>(), new List<Item>(), new List<Item>() };
        private readonly List<GameObject> _tabRoots = new List<GameObject>();
        private readonly HashSet<string> _builtInKeys = new HashSet<string>();
        private TMP_Text _clock, _hint, _subtitle, _note;
        private int _factCount;
        private int _fallbackBuilt;
        private string _hintFromHost = "";

        public Canvas Canvas { get; private set; }

        public string ClockText => _clock.text;
        public string HintText => _hint.text;
        public string SubtitleText => _subtitle.text;
        public string NoteText => _note.text;

        /// <summary>Presses the tablet button for an action (what a trigger pull on the button does). Used by tests and the headset-free rig.</summary>
        public bool Press(string verb, string key = "", string value = "")
        {
            var item = _tabs.SelectMany(t => t).FirstOrDefault(i => i.Verb == verb && i.Key == key && i.Value == value);
            if (item == null) return false;
            var emitter = item.Button.GetComponent<UIButtonEmitter>();
            emitter.DebounceSeconds = 0f;
            emitter.Fire();
            return true;
        }

        /// <summary>The buttons by label, for tests and the fallback check.</summary>
        public IEnumerable<(string label, Button button)> Buttons => _tabs.SelectMany(t => t).Select(i => (i.Label, i.Button));

        private void Awake() => Build();

        protected override void OnEnable()
        {
            base.OnEnable();
            if (SimulationHost.Instance != null) SimulationHost.Instance.SessionPaused += OnPaused;
        }

        protected override void OnDisable()
        {
            if (SimulationHost.Instance != null) SimulationHost.Instance.SessionPaused -= OnPaused;
            base.OnDisable();
        }

        private void OnPaused(string reason) => _hintFromHost = HintTexts.For("SESSION_PAUSED");

        public void Build()
        {
            if (Canvas != null) return;
            Canvas = UiKit.WorldCanvas("Tablet", transform, new Vector2(900, 600), 0.0006f);
            var root = Canvas.transform;

            _clock = UiKit.Label(root, "Clock", "", new Vector2(14, 8), new Vector2(240, 36), 26, TextAlignmentOptions.TopLeft, UiKit.DimTextColor);
            _hint = UiKit.Label(root, "Hint", "", new Vector2(260, 8), new Vector2(630, 36), 24, TextAlignmentOptions.TopRight, UiKit.WarnColor);
            UiKit.Panel(root, "SubtitleBackground", new Vector2(10, 50), new Vector2(880, 80), new Color(0.12f, 0.15f, 0.22f, 1f));
            _subtitle = UiKit.Label(root, "Subtitle", "", new Vector2(22, 54), new Vector2(856, 72), 26, TextAlignmentOptions.MidlineLeft);
            UiKit.Panel(root, "NoteBackground", new Vector2(610, 190), new Vector2(280, 400), new Color(0.12f, 0.15f, 0.22f, 1f));
            UiKit.Label(root, "NoteTitle", "Clerking note", new Vector2(620, 194), new Vector2(260, 30), 22, TextAlignmentOptions.TopLeft, UiKit.DimTextColor);
            _note = UiKit.Label(root, "Note", "", new Vector2(620, 226), new Vector2(262, 360), 18);

            DefineItems();
            for (int t = 0; t < TabNames.Length; t++)
            {
                var tabRoot = new GameObject("Tab - " + TabNames[t], typeof(RectTransform));
                tabRoot.transform.SetParent(root, false);
                var rect = (RectTransform)tabRoot.transform;
                rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
                rect.sizeDelta = Vector2.zero;
                _tabRoots.Add(tabRoot);
                for (int i = 0; i < _tabs[t].Count; i++) MakeItemButton(tabRoot.transform, _tabs[t][i], i);
                int tab = t;
                var tabButton = UiKit.MakeButton(root, TabNames[t], new Vector2(10 + t * 150, 138), new Vector2(142, 46), 22);
                tabButton.onClick.AddListener(() => ShowTab(tab));
            }
            ShowTab(0);
        }

        private void DefineItems()
        {
            Func<ObservableState, bool> always = s => true;
            var t0 = _tabs[0];
            t0.Add(new Item { Label = "Introduce yourself", Verb = "introduce_self", Enabled = always });
            t0.Add(new Item { Label = "Ask name + date of birth", Verb = "ask_identity", Enabled = always });
            t0.Add(new Item { Label = "Read name to patient", Verb = "read_name_to_patient", Enabled = always });
            t0.Add(new Item { Label = "Read wristband", Verb = "read_wristband", Enabled = always });
            t0.Add(new Item { Label = "Open notes", Verb = "open_notes", Enabled = always });
            t0.Add(new Item { Label = "Confirm identity", Verb = "confirm_identity", Enabled = s => s.DisclosedFacts.Any(f => f.Topic == "identity") && s.Wristband != null && s.Notes != null });
            t0.Add(new Item { Label = "Report mismatch", Verb = "report_mismatch", Enabled = s => s.Wristband != null || s.Notes != null });
            t0.Add(new Item { Label = "Ask nurse for a wristband", Verb = "request_wristband", Enabled = always });
            t0.Add(new Item { Label = "Ask about allergies", Verb = "ask_allergies", Enabled = always });
            t0.Add(new Item { Label = "Ask what happened", Verb = "ask_allergy_reaction", Enabled = always });
            t0.Add(new Item { Label = "Ask why she is here", Verb = "ask_reason_for_admission", Enabled = always });
            t0.Add(new Item { Label = "Ask about home support", Verb = "ask_responsible_adult", Enabled = always });

            foreach (var (label, topic) in HistoryTopics)
                _tabs[1].Add(new Item { Label = label, Verb = "ask", Key = "topic", Value = topic, Enabled = always });
            _tabs[1].Add(new Item { Label = "Summarise", Verb = "summarise", Enabled = always });

            var t2 = _tabs[2];
            t2.Add(new Item { Label = "Attach SpO2 probe", Verb = "attach_sensor", Key = "sensor", Value = "spo2", Enabled = always });
            t2.Add(new Item { Label = "Attach BP cuff", Verb = "attach_sensor", Key = "sensor", Value = "nibp_cuff", Enabled = always });
            t2.Add(new Item { Label = "Measure BP", Verb = "measure_nibp", Enabled = always });
            t2.Add(new Item { Label = "Take temperature", Verb = "measure_temperature", Enabled = always });
            t2.Add(new Item { Label = "Count breathing rate", Verb = "count_rr", Enabled = always });
            t2.Add(new Item { Label = "Check alertness", Verb = "assess_acvpu", Enabled = always });
            t2.Add(new Item { Label = "Ask pain score", Verb = "ask_pain_score", Enabled = always });
            t2.Add(new Item { Label = "Remove SpO2 probe", Verb = "detach_sensor", Key = "sensor", Value = "spo2", Enabled = always });
            t2.Add(new Item { Label = "Remove BP cuff", Verb = "detach_sensor", Key = "sensor", Value = "nibp_cuff", Enabled = always });

            foreach (var item in _tabs.SelectMany(t => t)) _builtInKeys.Add(KeyOf(item.Verb, item.Key, item.Value));
        }

        private static string KeyOf(string verb, string key, string value) => verb + "|" + key + "=" + value;

        private void MakeItemButton(Transform tabRoot, Item item, int index)
        {
            int col = index % 2, row = index / 2;
            item.Button = UiKit.MakeButton(tabRoot, item.Label, new Vector2(10 + col * 296, 192 + row * 58), new Vector2(288, 50), 22);
            var emitter = item.Button.gameObject.AddComponent<UIButtonEmitter>();
            emitter.Configure(item.Verb, "patient", item.Key, item.Value);
        }

        public void ShowTab(int tab)
        {
            for (int i = 0; i < _tabRoots.Count; i++) _tabRoots[i].SetActive(i == tab);
        }

        protected override void Render(ObservableState s)
        {
            SetText(_clock, s.ClockTime);
            SetText(_hint, !string.IsNullOrEmpty(_hintFromHost) ? _hintFromHost : HintTexts.For(s.LastRejection));

            if (s.DisclosedFacts.Count != _factCount)
            {
                _factCount = s.DisclosedFacts.Count;
                SetText(_subtitle, s.DisclosedFacts.Count == 0 ? "" : s.DisclosedFacts[s.DisclosedFacts.Count - 1].Answer);
            }
            SetText(_note, string.Join("\n", s.DisclosedFacts.AsEnumerable().Reverse().Take(9).Select(f => "- " + f.Answer)));

            foreach (var tab in _tabs)
                foreach (var item in tab)
                    if (item.Button != null) item.Button.interactable = item.Enabled(s);

            AddMissingFallbacks();
        }

        // Any emitter in the world registers its action; if the tablet has no matching button yet, it gets one on the "More" tab.
        private void AddMissingFallbacks()
        {
            var entries = TabletFallbackRegistry.All;
            for (; _fallbackBuilt < entries.Count; _fallbackBuilt++)
            {
                var action = entries[_fallbackBuilt].Action;
                var first = action.Params.FirstOrDefault();
                if (_builtInKeys.Contains(KeyOf(action.Verb, first.Key ?? "", first.Value ?? ""))) continue;
                var item = new Item { Label = entries[_fallbackBuilt].Label, Verb = action.Verb, Key = first.Key ?? "", Value = first.Value ?? "", Enabled = s => true };
                _tabs[3].Add(item);
                MakeItemButton(_tabRoots[3].transform, item, _tabs[3].Count - 1);
            }
        }
    }
}
