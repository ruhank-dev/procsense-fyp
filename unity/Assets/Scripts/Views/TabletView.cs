using System;
using System.Collections.Generic;
using System.Linq;
using ProcSense.Core.Actions;
using ProcSense.Core.Observation;
using ProcSense.VR.ActionEmitters;
using ProcSense.VR.Bridge;
using ProcSense.VR.Content;
using ProcSense.VR.Fallback;
using ProcSense.VR.UI.Tablet;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ProcSense.VR.Views
{
    /// <summary>
    /// The hand-held tablet (p0-vr-interaction-ui-s03): the tabs of content/ui/tablet.json in walkthrough order (Admission, Ask, Known, Assess, Orders, Results,
    /// Treat, Consent, Checklist, Handover, Debrief), then More (a button for every VR gesture without one, the tablet fallback), Session and runtime Diagnostics. A button sends one
    /// action through an emitter; a form collects its fields and sends one action. Every label and choice comes from content; which buttons are enabled
    /// depends only on the ObservableState. A refused action shows a short hint and changes nothing on screen. Practice mode adds the hint chip and the list
    /// of outstanding goals (Debrief tab); assessment shows neither.
    /// </summary>
    public class TabletView : StateView
    {
        public const string MoreTab = "more", SessionTabId = "session", DiagnosticsTabId = "diagnostics";
        private const int PerPage = 12, FormRows = 6;
        private static readonly Vector2 Size = new Vector2(900, 600);

        private sealed class Item
        {
            public string Label;
            public TabletEntry Entry;            // a layout button or form; null for a fallback
            public SimAction FallbackAction;     // a gesture's action offered on the More tab
            public Button Button;
            public int Tab;
        }

        private sealed class Row
        {
            public TabletField Field;
            public TabletOption Item;             // one item of a flags, booleans or multi field
            public GameObject Root;
            public TMP_Text Value;
            public TMP_InputField Input;
        }

        private static TabletLayout _layout;
        private readonly List<Item> _items = new List<Item>();
        private readonly List<GameObject> _tabRoots = new List<GameObject>();
        private readonly List<string> _tabIds = new List<string>();
        private readonly Dictionary<int, int> _page = new Dictionary<int, int>();
        private readonly List<Row> _rows = new List<Row>();
        private readonly Dictionary<string, string> _values = new Dictionary<string, string>(StringComparer.Ordinal);
        private TMP_Text _clock, _hint, _chip, _subtitle, _note, _sideTitle, _side, _pageLabel, _formTitle, _formPageLabel;
        private GameObject _sessionRoot, _diagnosticsRoot, _noteRoot, _sideRoot, _formRoot, _paging;
        private FormSendEmitter _send;
        private TabletEntry _form;
        private int _formPage, _tab, _factCount;
        private int _fallbackRevision = -1;

        public Canvas Canvas { get; private set; }

        /// <summary>The Session tab (account, uploads, latest results). It is not an action tab and holds no simulation buttons.</summary>
        public SessionTabView SessionTab { get; private set; }
        public DiagnosticsTabView DiagnosticsTab { get; private set; }

        public string ClockText => _clock.text;
        public string HintText => _hint.text;
        public string ChipText => _chip.text;
        public string SubtitleText => _subtitle.text;
        public string NoteText => _note.text;
        public string SideText => _side.text;
        public string FormTitle => _form != null ? _formTitle.text : "";

        /// <summary>The tab ids in order (the layout's, then More and Session).</summary>
        public IReadOnlyList<string> TabIds => _tabIds;
        public int CurrentTab => _tab;
        public int SessionTabIndex => _tabIds.IndexOf(SessionTabId);

        public static TabletLayout Layout => _layout ??= TabletLayout.Parse(UnityContentReader.ForThisPlatform().ReadText(TabletLayout.File));

        /// <summary>The id of a tablet button in the spoken-action list (content/ai/spoken_actions.json): the verb, or verb:value for one parameter.</summary>
        private static string IdOf(TabletEntry e) => e.Params.Count == 0 ? e.Verb : e.Verb + ":" + string.Join(":", e.Params.Values);

        private IEnumerable<Item> LayoutButtons => _items.Where(i => i.Entry != null && !i.Entry.IsForm);

        /// <summary>The ids of the tablet buttons enabled now (forms and gestures are not here). The service keeps only those that may be started by speech.</summary>
        public IReadOnlyList<string> EnabledActionIds()
        {
            var state = SimulationHost.Instance != null ? SimulationHost.Instance.Observable : null;
            if (state == null) return new List<string>();
            return LayoutButtons.Where(i => Enabled(i.Entry, state)).Select(i => IdOf(i.Entry)).Distinct().ToList();
        }

        public string LabelOf(string id) => LayoutButtons.Where(i => IdOf(i.Entry) == id).Select(i => i.Label).FirstOrDefault() ?? id;

        /// <summary>Starts the tablet button with this id exactly as pressing it does (the same emitter, the same engine path). False when there is no such enabled button.</summary>
        public bool PressId(string id)
        {
            var state = SimulationHost.Instance != null ? SimulationHost.Instance.Observable : null;
            var item = LayoutButtons.FirstOrDefault(i => IdOf(i.Entry) == id && state != null && Enabled(i.Entry, state));
            return item != null && Fire(item);
        }

        /// <summary>Deterministic test/desktop dispatch of an action with at most one parameter, including inactive tabs and refused actions.</summary>
        public bool Press(string verb, string key = "", string value = "") =>
            PressAction(verb, string.IsNullOrEmpty(key) ? new Dictionary<string, string>() : new Dictionary<string, string> { [key] = value });

        /// <summary>
        /// Makes this action from the tablet as a trainee would: its button, or the form that makes it (opened, filled with these values, then sent), or the
        /// More tab's fallback. False when the tablet has no way to make it. Physical clicks go through the normal UI gating and debounce.
        /// </summary>
        public bool PressAction(string verb, IReadOnlyDictionary<string, string> parameters)
        {
            SyncFallbacks();
            var button = LayoutButtons.FirstOrDefault(i => i.Entry.Verb == verb && i.Entry.Params.Count == parameters.Count && i.Entry.Params.All(kv => parameters.TryGetValue(kv.Key, out var v) && v == kv.Value));
            if (button != null) return Fire(button);
            foreach (var item in _items.Where(i => i.Entry != null && i.Entry.IsForm))
            {
                var values = TabletLayout.Fill(item.Entry, verb, parameters, StateOptions);
                if (values == null) continue;
                ShowTab(item.Tab);
                OpenForm(item.Entry);
                foreach (var row in _rows) Set(row, values);
                return Send();
            }
            var fallback = _items.FirstOrDefault(i => i.FallbackAction != null && i.FallbackAction.Verb == verb && i.FallbackAction.Params.Count == parameters.Count &&
                                                    i.FallbackAction.Params.All(kv => parameters.TryGetValue(kv.Key, out var v) && v == kv.Value));
            if (fallback == null) return false;
            TabletFallbackRegistry.Fire(fallback.FallbackAction);
            return true;
        }

        /// <summary>The buttons by label (layout buttons, form openers and fallbacks), for tests and the fallback check.</summary>
        public IEnumerable<(string label, Button button)> Buttons => _items.Select(i => (i.Label, i.Button));

        private void Awake() => Build();

        protected override void Update()
        {
            base.Update();
            if (SimulationHost.Instance != null && SimulationHost.Instance.Paused)
                SetText(_hint, HintTexts.For("SESSION_PAUSED"));
            SyncFallbacks(); // Registry changes also matter while Core is paused/not ticking.
        }

        public void Build()
        {
            if (Canvas != null) return;
            Canvas = UiKit.WorldCanvas("Tablet", transform, Size, 0.0006f);
            var root = Canvas.transform;

            _clock = UiKit.Label(root, "Clock", "", new Vector2(14, 8), new Vector2(150, 32), 24, TextAlignmentOptions.TopLeft, UiKit.DimTextColor);
            _chip = UiKit.Label(root, "HintChip", "", new Vector2(170, 8), new Vector2(720, 32), 20, TextAlignmentOptions.TopLeft, new Color(0.55f, 0.85f, 0.78f, 1f));
            _hint = UiKit.Label(root, "Hint", "", new Vector2(10, 558), new Vector2(590, 34), 21, TextAlignmentOptions.MidlineLeft, UiKit.WarnColor);   // under the buttons; a form covers it
            UiKit.Panel(root, "SubtitleBackground", new Vector2(10, 44), new Vector2(880, 64), new Color(0.12f, 0.15f, 0.22f, 1f));
            _subtitle = UiKit.Label(root, "Subtitle", "", new Vector2(22, 46), new Vector2(856, 60), 24, TextAlignmentOptions.MidlineLeft);

            _noteRoot = Group(root, "Clerking note");
            UiKit.Panel(_noteRoot.transform, "NoteBackground", new Vector2(610, 194), new Vector2(280, 396), new Color(0.12f, 0.15f, 0.22f, 1f));
            UiKit.Label(_noteRoot.transform, "NoteTitle", "Clerking note", new Vector2(620, 198), new Vector2(260, 28), 20, TextAlignmentOptions.TopLeft, UiKit.DimTextColor);
            _note = UiKit.Label(_noteRoot.transform, "Note", "", new Vector2(620, 228), new Vector2(262, 356), 17);
            _sideRoot = Group(root, "Side list");
            UiKit.Panel(_sideRoot.transform, "SideBackground", new Vector2(610, 194), new Vector2(280, 396), new Color(0.12f, 0.15f, 0.22f, 1f));
            _sideTitle = UiKit.Label(_sideRoot.transform, "SideTitle", "", new Vector2(620, 198), new Vector2(260, 28), 20, TextAlignmentOptions.TopLeft, UiKit.DimTextColor);
            _side = UiKit.Label(_sideRoot.transform, "Side", "", new Vector2(620, 228), new Vector2(262, 356), 16);

            var tabs = Layout.Tabs.Select(t => (Id: t.Id, Label: t.Label)).Concat(new[] { (Id: MoreTab, Label: "More"), (Id: SessionTabId, Label: "Session"), (Id: DiagnosticsTabId, Label: "Diagnostics") }).ToList();
            for (int t = 0; t < tabs.Count; t++)
            {
                _tabIds.Add(tabs[t].Id);
                int perRow = (tabs.Count + 1) / 2, row = t / perRow, col = t % perRow;
                float width = 880f / perRow;
                int tab = t;
                UiKit.MakeButton(root, tabs[t].Label, new Vector2(10 + col * width, 114 + row * 40), new Vector2(width - 4, 36), 17).onClick.AddListener(() => ShowTab(tab));
                if (tabs[t].Id == SessionTabId || tabs[t].Id == DiagnosticsTabId) continue;
                var tabRoot = Group(root, "Tab - " + tabs[t].Label);
                _tabRoots.Add(tabRoot);
                if (t < Layout.Tabs.Count)
                    foreach (var entry in Layout.Tabs[t].Entries)
                    {
                        var item = new Item { Label = entry.Label, Entry = entry, Tab = t };
                        _items.Add(item);
                        MakeItemButton(tabRoot.transform, item, Layout.Tabs[t].Entries.IndexOf(entry));
                    }
            }

            _paging = Group(root, "Paging");
            UiKit.MakeButton(_paging.transform, "< Previous", new Vector2(10, 512), new Vector2(140, 40), 18).onClick.AddListener(() => Turn(-1));
            _pageLabel = UiKit.Label(_paging.transform, "Page", "", new Vector2(160, 512), new Vector2(280, 40), 18, TextAlignmentOptions.Center, UiKit.DimTextColor);
            UiKit.MakeButton(_paging.transform, "Next >", new Vector2(450, 512), new Vector2(140, 40), 18).onClick.AddListener(() => Turn(1));

            BuildFormPanel(root);

            _sessionRoot = Group(root, "Tab - Session");
            SessionTab = _sessionRoot.AddComponent<SessionTabView>();
            SessionTab.Build(_sessionRoot.transform);
            _diagnosticsRoot = Group(root, "Tab - Diagnostics");
            DiagnosticsTab = _diagnosticsRoot.AddComponent<DiagnosticsTabView>();
            DiagnosticsTab.Build(_diagnosticsRoot.transform);
            var waitStatus = GetComponent<DiagnosticWaitOverlay>() ?? gameObject.AddComponent<DiagnosticWaitOverlay>();
            waitStatus.Build();
            ShowTab(0);
        }

        private static GameObject Group(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
            rect.sizeDelta = Vector2.zero;
            return go;
        }

        private void MakeItemButton(Transform tabRoot, Item item, int index)
        {
            item.Button = UiKit.MakeButton(tabRoot, item.Label + (item.Entry != null && item.Entry.IsForm ? " ..." : ""), Slot(index), new Vector2(288, 46), 18);
            item.Button.name = "Button - " + item.Label;
            if (item.FallbackAction != null)
            {
                item.Button.onClick.AddListener(() => TabletFallbackRegistry.Fire(item.FallbackAction));
                return;
            }
            if (item.Entry.IsForm)
            {
                item.Button.onClick.AddListener(() => OpenForm(item.Entry));
                return;
            }
            item.Button.gameObject.AddComponent<UIButtonEmitter>().ConfigureParams(item.Entry.Verb, item.Entry.Params);
        }

        private static Vector2 Slot(int index)
        {
            int onPage = index % PerPage;
            return new Vector2(10 + onPage % 2 * 296, 194 + onPage / 2 * 52);
        }

        private bool Fire(Item item)
        {
            var emitter = item.Button.GetComponent<UIButtonEmitter>();
            float debounce = emitter.DebounceSeconds;
            try { emitter.DebounceSeconds = 0f; emitter.Fire(); }
            finally { emitter.DebounceSeconds = debounce; }
            return true;
        }

        // ---------------------------------------------------------------- tabs and pages

        public void ShowTab(int tab)
        {
            CloseForm();
            _tab = Mathf.Clamp(tab, 0, _tabIds.Count - 1);
            bool session = _tabIds[_tab] == SessionTabId;
            for (int i = 0; i < _tabRoots.Count; i++) _tabRoots[i].SetActive(i == _tab);
            if (_sessionRoot != null) _sessionRoot.SetActive(session);
            bool diagnostics = _tabIds[_tab] == DiagnosticsTabId;
            if (_diagnosticsRoot != null) _diagnosticsRoot.SetActive(diagnostics);
            ShowPage();
            ShowSide();
        }

        public void ShowTab(string id)
        {
            int index = _tabIds.IndexOf(id);
            if (index < 0) throw new ArgumentException("the tablet has no tab '" + id + "'");
            ShowTab(index);
        }

        private void Turn(int delta)
        {
            int pages = Pages(_tab);
            _page[_tab] = Mathf.Clamp((_page.TryGetValue(_tab, out var p) ? p : 0) + delta, 0, pages - 1);
            ShowPage();
        }

        private int Pages(int tab) => Mathf.Max(1, (_items.Count(i => i.Tab == tab) + PerPage - 1) / PerPage);

        private void ShowPage()
        {
            int page = _page.TryGetValue(_tab, out var p) ? p : 0, pages = Pages(_tab), index = 0;
            foreach (var item in _items.Where(i => i.Tab == _tab))
            {
                item.Button.gameObject.SetActive(index / PerPage == page);
                ((RectTransform)item.Button.transform).anchoredPosition = new Vector2(Slot(index).x, -Slot(index).y);
                index++;
            }
            bool session = _tabIds[_tab] == SessionTabId;
            _paging.SetActive(!session && _form == null && pages > 1);
            SetText(_pageLabel, "Page " + (page + 1) + " of " + pages);
        }

        // ---------------------------------------------------------------- side list (Known, Results, Orders, Goals)

        private string ListOf(int tab) => tab < Layout.Tabs.Count ? Layout.Tabs[tab].List : "";

        private void ShowSide()
        {
            string list = ListOf(_tab);
            bool session = _tabIds[_tab] == SessionTabId;
            bool diagnostics = _tabIds[_tab] == DiagnosticsTabId;
            _sideRoot.SetActive(!session && !diagnostics && list.Length > 0);
            _noteRoot.SetActive(!session && !diagnostics && list.Length == 0);
            if (Last != null) RenderSide(Last);
        }

        private void RenderSide(ObservableState s)
        {
            switch (ListOf(_tab))
            {
                case "known":
                    SetText(_sideTitle, "What you know");
                    SetText(_side, string.Join("\n", s.DisclosedFacts.Select(f => "- " + f.Answer)
                        .Concat(s.Findings.Select(f => "- " + f.Text)).Concat(s.Airway.Select(a => "- " + a.Text))
                        .Concat(s.Vitals.Select(v => "- " + v.Name + " " + v.Value))));
                    break;
                case "investigations":
                    SetText(_sideTitle, "Investigations");
                    SetText(_side, string.Join("\n", s.Investigations.Select(i => "- " + i.Label + ": " + i.State + (i.Ready.Length > 0 ? " (" + i.Ready + ")" : ""))));
                    break;
                case "orders":
                    SetText(_sideTitle, "Orders");
                    SetText(_side, string.Join("\n", (s.Orders ?? Array.Empty<ObservedOrder>()).Select(o => "- " + o.Label + ": " + o.State)));
                    break;
                case "goals":
                    SetText(_sideTitle, "Still to do");
                    SetText(_side, ModeGate.PracticeAids ? string.Join("\n", s.Goals.Where(g => g.State != "done").Select(g => "- " + g.Label)) : "");
                    break;
            }
        }

        // ---------------------------------------------------------------- forms

        private void BuildFormPanel(Transform root)
        {
            _formRoot = Group(root, "Form");
            UiKit.Panel(_formRoot.transform, "FormBackground", new Vector2(10, 194), new Vector2(590, 396), new Color(0.11f, 0.14f, 0.21f, 1f));
            _formTitle = UiKit.Label(_formRoot.transform, "FormTitle", "", new Vector2(20, 198), new Vector2(570, 32), 21);
            var bar = 548f;
            UiKit.MakeButton(_formRoot.transform, "Cancel", new Vector2(14, bar), new Vector2(120, 38), 18).onClick.AddListener(CloseForm);
            UiKit.MakeButton(_formRoot.transform, "<", new Vector2(142, bar), new Vector2(56, 38), 18).onClick.AddListener(() => TurnForm(-1));
            _formPageLabel = UiKit.Label(_formRoot.transform, "FormPage", "", new Vector2(202, bar), new Vector2(150, 38), 16, TextAlignmentOptions.Center, UiKit.DimTextColor);
            UiKit.MakeButton(_formRoot.transform, ">", new Vector2(356, bar), new Vector2(56, 38), 18).onClick.AddListener(() => TurnForm(1));
            var send = UiKit.MakeButton(_formRoot.transform, "Send", new Vector2(456, bar), new Vector2(136, 38), 20);
            send.name = "Button - Send";
            _send = send.gameObject.AddComponent<FormSendEmitter>();
            send.onClick.AddListener(() => { Sync(); _send.Fire(); CloseForm(); });   // a click keeps the normal debounce
            _formRoot.SetActive(false);
        }

        private void OpenForm(TabletEntry entry)
        {
            CloseForm();
            _form = entry;
            _formPage = 0;
            _values.Clear();
            SetText(_formTitle, entry.Label);
            foreach (var f in entry.Fields)
                switch (f.Kind)
                {
                    case "flags":
                    case "booleans":
                    case "multi":
                        foreach (var item in f.Kind == "multi" ? f.Options : f.Items) AddRow(f, item);
                        break;
                    default:
                        AddRow(f, null);
                        break;
                }
            foreach (var r in _rows) Default(r);
            _formRoot.SetActive(true);
            foreach (var t in _tabRoots) t.SetActive(false);
            _paging.SetActive(false);
            ShowFormPage();
            Sync();
        }

        private void CloseForm()
        {
            if (_formRoot == null) return;
            foreach (var r in _rows) Destroy(r.Root);
            _rows.Clear();
            _form = null;
            _formRoot.SetActive(false);
            if (_tabRoots.Count > 0)
            {
                for (int i = 0; i < _tabRoots.Count; i++) _tabRoots[i].SetActive(i == _tab);
                ShowPage();
            }
        }

        private static string RowKey(Row r) => r.Item == null ? r.Field.Key : r.Field.Key + "|" + r.Item.Value;

        private void AddRow(TabletField field, TabletOption item)
        {
            var row = new Row { Field = field, Item = item, Root = Group(_formRoot.transform, "Field - " + (item?.Label ?? field.Label)) };
            var t = row.Root.transform;
            UiKit.Label(t, "Label", item?.Label ?? field.Label, new Vector2(20, 0), new Vector2(item != null ? 380 : 170, 44), 17, TextAlignmentOptions.MidlineLeft);
            if (field.Kind == "text")
            {
                row.Input = UiKit.Input(t, new Vector2(196, 2), new Vector2(392, 40));
                row.Input.onValueChanged.AddListener(v => { _values[RowKey(row)] = v; Sync(); });
            }
            else if (item != null)
            {
                var toggle = UiKit.MakeButton(t, "", new Vector2(410, 2), new Vector2(178, 40), 18);
                row.Value = toggle.GetComponentInChildren<TMP_Text>();
                toggle.onClick.AddListener(() => { _values[RowKey(row)] = Next(row); Show(row); Sync(); });
            }
            else
            {
                UiKit.MakeButton(t, "<", new Vector2(196, 2), new Vector2(44, 40), 20).onClick.AddListener(() => { Step(row, -1); });
                row.Value = UiKit.Label(t, "Value", "", new Vector2(244, 0), new Vector2(296, 44), 17, TextAlignmentOptions.Center);
                UiKit.MakeButton(t, ">", new Vector2(544, 2), new Vector2(44, 40), 20).onClick.AddListener(() => { Step(row, 1); });
            }
            _rows.Add(row);
        }

        private IReadOnlyList<TabletOption> OptionsOf(TabletField f) => f.Kind == "state" ? StateOptions(f).ToList() : f.Optional ? new[] { new TabletOption { Value = "", Label = "(not set)" } }.Concat(f.Options).ToList() : f.Options;

        /// <summary>The choices of a state field from what the session shows now (for example the investigations, by name).</summary>
        private IEnumerable<TabletOption> StateOptions(TabletField f)
        {
            var s = SimulationHost.Instance != null ? SimulationHost.Instance.Observable : null;
            if (s == null) return Enumerable.Empty<TabletOption>();
            switch (f.Source)
            {
                case "investigations":
                    return s.Investigations.Select(i => i.Test).Distinct().Select(t => new TabletOption { Value = t, Label = s.Investigations.First(i => i.Test == t).Label });
                default:
                    return Enumerable.Empty<TabletOption>();
            }
        }

        private void Default(Row r)
        {
            string key = RowKey(r);
            switch (r.Field.Kind)
            {
                case "flags": _values[key] = "no"; break;
                case "booleans": _values[key] = "false"; break;
                case "multi": _values[key] = "no"; break;
                case "text": _values[key] = ""; break;
                default: _values[key] = OptionsOf(r.Field).Select(o => o.Value).FirstOrDefault() ?? ""; break;
            }
            Show(r);
        }

        private string Next(Row r)
        {
            string v = _values[RowKey(r)];
            return r.Field.Kind == "booleans" ? (v == "true" ? "false" : "true") : (v == "yes" ? "no" : "yes");
        }

        private void Step(Row r, int delta)
        {
            var options = OptionsOf(r.Field);
            if (options.Count == 0) return;
            int i = Mathf.Max(0, options.ToList().FindIndex(o => o.Value == _values[RowKey(r)]));
            _values[RowKey(r)] = options[(i + delta + options.Count) % options.Count].Value;
            Show(r);
            Sync();
        }

        private void Show(Row r)
        {
            string v = _values[RowKey(r)];
            if (r.Input != null) { if (r.Input.text != v) r.Input.SetTextWithoutNotify(v); return; }
            if (r.Item != null) { SetText(r.Value, v == "yes" || v == "true" ? "Yes" : "No"); return; }
            var options = OptionsOf(r.Field);
            SetText(r.Value, options.Where(o => o.Value == v).Select(o => o.Label).FirstOrDefault() ?? (options.Count == 0 ? "(none yet)" : ""));
        }

        /// <summary>Puts the values of an action into the rows (programmatic filling: tests and the spoken route).</summary>
        private void Set(Row r, IReadOnlyDictionary<string, string> values)
        {
            string key = RowKey(r);
            switch (r.Field.Kind)
            {
                case "booleans":
                    if (values.TryGetValue(r.Item.Value, out var b)) _values[key] = b;
                    break;
                case "flags":
                    if (values.TryGetValue(r.Field.Key, out var flags))
                        _values[key] = flags.Split(',').Select(p => p.Split(':')).Where(p => p[0] == r.Item.Value).Select(p => p[1]).FirstOrDefault() ?? "no";
                    break;
                case "multi":
                    _values[key] = values.TryGetValue(r.Field.Key, out var many) && many.Split(',').Contains(r.Item.Value) ? "yes" : "no";
                    break;
                default:
                    _values[key] = values.TryGetValue(r.Field.Key, out var v) ? v : "";
                    break;
            }
            Show(r);
            Sync();
        }

        /// <summary>The parameters of the form's action now (fixed ones plus the fields); an optional field left unset and an empty multi are not sent.</summary>
        public IReadOnlyDictionary<string, string> FormParams()
        {
            var p = new SortedDictionary<string, string>(StringComparer.Ordinal);
            if (_form == null) return p;
            foreach (var kv in _form.Params) p[kv.Key] = kv.Value;
            foreach (var f in _form.Fields)
            {
                var rows = _rows.Where(r => r.Field == f).ToList();
                switch (f.Kind)
                {
                    case "booleans":
                        foreach (var r in rows) p[r.Item.Value] = _values[RowKey(r)];
                        break;
                    case "flags":
                        p[f.Key] = string.Join(",", rows.Select(r => r.Item.Value + ":" + _values[RowKey(r)]));
                        break;
                    case "multi":
                        var chosen = rows.Where(r => _values[RowKey(r)] == "yes").Select(r => r.Item.Value).ToList();
                        if (chosen.Count > 0) p[f.Key] = string.Join(",", chosen);
                        break;
                    default:
                        var v = _values[RowKey(rows[0])];
                        if (!(f.Optional && v.Length == 0)) p[f.Key] = v;
                        break;
                }
            }
            return p;
        }

        private void Sync()
        {
            if (_form != null) _send.ConfigureParams(_form.Verb, FormParams());
        }

        /// <summary>Sends the open form (the same emitter a click uses) and closes it.</summary>
        public bool Send()
        {
            if (_form == null) return false;
            Sync();
            float debounce = _send.DebounceSeconds;
            try { _send.DebounceSeconds = 0f; _send.Fire(); }
            finally { _send.DebounceSeconds = debounce; }
            CloseForm();
            return true;
        }

        private void TurnForm(int delta)
        {
            int pages = Mathf.Max(1, (_rows.Count + FormRows - 1) / FormRows);
            _formPage = Mathf.Clamp(_formPage + delta, 0, pages - 1);
            ShowFormPage();
        }

        private void ShowFormPage()
        {
            int pages = Mathf.Max(1, (_rows.Count + FormRows - 1) / FormRows);
            for (int i = 0; i < _rows.Count; i++)
            {
                _rows[i].Root.SetActive(i / FormRows == _formPage);
                ((RectTransform)_rows[i].Root.transform).anchoredPosition = new Vector2(0, -(236 + i % FormRows * 50));
            }
            SetText(_formPageLabel, pages > 1 ? (_formPage + 1) + " of " + pages : "");
        }

        // ---------------------------------------------------------------- state

        private static bool Enabled(TabletEntry e, ObservableState s)
        {
            switch (e.Enabled)
            {
                case "identity_ready": return s.DisclosedFacts.Any(f => f.Topic == "identity") && s.Wristband != null && s.Notes != null;
                case "has_record": return s.Wristband != null || s.Notes != null;
                default: return true;
            }
        }

        protected override void Render(ObservableState s)
        {
            SetText(_clock, s.ClockTime);
            SetText(_hint, SimulationHost.Instance != null && SimulationHost.Instance.Paused
                ? HintTexts.For("SESSION_PAUSED") : ModeGate.Practice(HintTexts.For(s.LastRejection)));
            SetText(_chip, ModeGate.PracticeAids ? s.Hint ?? "" : "");

            if (s.DisclosedFacts.Count != _factCount)
            {
                _factCount = s.DisclosedFacts.Count;
                SetText(_subtitle, s.DisclosedFacts.Count == 0 ? "" : s.DisclosedFacts[s.DisclosedFacts.Count - 1].Answer);
            }
            SetText(_note, string.Join("\n", s.DisclosedFacts.AsEnumerable().Reverse().Take(9).Select(f => "- " + f.Answer)));
            RenderSide(s);
            foreach (var item in LayoutButtons) item.Button.interactable = Enabled(item.Entry, s);
            foreach (var r in _rows.Where(r => r.Field.Kind == "state")) Show(r);
            SyncFallbacks();
        }

        // Any emitter in the world registers its action; if the tablet has no matching button yet, it gets one on the More tab.
        private void SyncFallbacks()
        {
            int more = _tabIds.IndexOf(MoreTab);
            if (more < 0 || _fallbackRevision == TabletFallbackRegistry.Revision) return;
            _fallbackRevision = TabletFallbackRegistry.Revision;
            var desired = new List<TabletFallbackRegistry.Entry>();
            foreach (var entry in TabletFallbackRegistry.All)
                if (!LayoutButtons.Any(i => TabletFallbackRegistry.SameAction(ActionOf(i.Entry), entry.Action)) &&
                    !desired.Any(e => TabletFallbackRegistry.SameAction(e.Action, entry.Action))) desired.Add(entry);

            // Remove obsolete dynamic buttons only; keep the canvas, tabs and surviving buttons intact.
            foreach (var item in _items.Where(i => i.FallbackAction != null).ToArray())
            {
                if (desired.Any(e => TabletFallbackRegistry.SameAction(e.Action, item.FallbackAction))) continue;
                _items.Remove(item);
                item.Button.onClick.RemoveAllListeners();
                item.Button.gameObject.SetActive(false);
                Destroy(item.Button.gameObject);
            }
            foreach (var entry in desired)
            {
                var item = _items.FirstOrDefault(i => i.FallbackAction != null && TabletFallbackRegistry.SameAction(i.FallbackAction, entry.Action));
                if (item == null)
                {
                    item = new Item { Label = entry.Label, FallbackAction = entry.Action, Tab = more };
                    _items.Add(item);
                    MakeItemButton(_tabRoots[more].transform, item, _items.Count(i => i.Tab == more) - 1);
                }
                item.Label = entry.Label;
                item.Button.name = "Button - " + entry.Label;
                SetText(item.Button.GetComponentInChildren<TMP_Text>(), entry.Label);
            }
            if (_tab == more) ShowPage();
            else foreach (var item in _items.Where(i => i.Tab == more)) item.Button.gameObject.SetActive(false);
        }

        private static SimAction ActionOf(TabletEntry e) => new SimAction(e.Verb, "patient", Channel.Vr, e.Params.Count == 0 ? null : new Dictionary<string, string>(e.Params));
    }
}
