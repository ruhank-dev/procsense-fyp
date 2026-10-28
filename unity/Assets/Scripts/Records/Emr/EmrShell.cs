using System.Collections.Generic;
using System.Linq;
using ProcSense.Core.Actions;
using ProcSense.Core.Records;
using ProcSense.VR.Bridge;
using ProcSense.VR.UI.Results;
using ProcSense.VR.UI.Tablet;
using ProcSense.VR.Views;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ProcSense.VR.Records.Emr
{
    /// <summary>The EMR screen's plain hospital-system look: white and light grey, dark text, clear headers.</summary>
    public static class EmrLook
    {
        public static readonly Color Background = new Color(0.93f, 0.94f, 0.95f, 1f);
        public static readonly Color Header = new Color(0.20f, 0.27f, 0.36f, 1f);
        public static readonly Color Paper = Color.white;
        public static readonly Color Text = new Color(0.10f, 0.12f, 0.16f, 1f);
        public static readonly Color Dim = new Color(0.38f, 0.42f, 0.48f, 1f);
    }

    /// <summary>
    /// The EMR workstation (p0-patient-records-emr-s03): search by name, record number or date of birth with a keypad, the quick ward list (the patient in the bay first), and the record of the chosen patient in tabs across
    /// the top (several open at once, at most six, each with a close button). A tab shows its section (<see cref="EmrText"/>) and its documents, which open with open_document and, for a picture, view_image; a search is
    /// search_patient. Core logs what the trainee looked at and decides who matches; the shell only ranks and draws. A distractor on the ward list has no record in the simulation, so choosing one shows only the name
    /// band: noticing the wrong patient is the trainee's task. Like the other panels it is not placed in a scene yet (SCREEN_EMR, UX-P0-EMR).
    /// </summary>
    public class EmrShell : MonoBehaviour
    {
        private const int ListRows = 6, DocumentRows = 5;

        public Canvas Canvas { get; private set; }
        public SearchView Search { get; private set; }
        public TabBar Tabs { get; } = new TabBar();
        public string SelectedPatient { get; private set; } = "";
        public string BannerText => _banner != null ? _banner.text : "";
        public string BodyText => _body != null ? _body.text : "";
        public string StatusText => _status != null ? _status.text : "";
        public IReadOnlyList<RecordPatient> Shown => _shown;
        public IReadOnlyList<RecordDocument> Documents => _documents;
        public IReadOnlyList<string> TabLabels => _tabButtons.Where(b => b.gameObject.activeSelf).Select(b => b.GetComponentInChildren<TMP_Text>().text).ToList();

        private readonly List<RecordPatient> _shown = new List<RecordPatient>();
        private readonly List<RecordDocument> _documents = new List<RecordDocument>();
        private readonly List<Button> _patientButtons = new List<Button>();
        private readonly List<Button> _sectionButtons = new List<Button>();
        private readonly List<Button> _tabButtons = new List<Button>();
        private readonly List<Button> _closeButtons = new List<Button>();
        private readonly List<(Button open, Button image)> _documentButtons = new List<(Button, Button)>();
        private TMP_Text _banner, _body, _status, _listTitle;
        private FormSendEmitter _send;
        private string _query = "";
        private int _docPage;
        private Button _more;
        private object _shownState;

        private static SimulationHost Host => SimulationHost.Instance;
        private static RecordsSubsystem Records => Host?.Session?.Records;

        private void Awake() => Build();

        public void Build()
        {
            if (Canvas != null) return;
            Canvas = UiKit.WorldCanvas("EMR shell", transform, new Vector2(1200, 760), 0.0006f);
            var root = Canvas.transform;
            _send = gameObject.AddComponent<FormSendEmitter>();
            UiKit.Panel(root, "Background", Vector2.zero, new Vector2(1200, 760), EmrLook.Background);
            UiKit.Panel(root, "Header", Vector2.zero, new Vector2(1200, 52), EmrLook.Header);
            UiKit.Label(root, "Title", "Electronic patient record", new Vector2(12, 0), new Vector2(300, 52), 20, TextAlignmentOptions.MidlineLeft);
            _banner = UiKit.Label(root, "Patient", "", new Vector2(324, 0), new Vector2(864, 52), 20, TextAlignmentOptions.MidlineLeft);

            Search = new SearchView(root, new Vector2(12, 64));
            Search.SearchPressed += q => Find(q, true);
            _listTitle = UiKit.Label(root, "List title", "Ward list", new Vector2(12, 340), new Vector2(300, 28), 16, TextAlignmentOptions.MidlineLeft, EmrLook.Dim);
            for (int i = 0; i < ListRows; i++)
            {
                int row = i;
                var b = UiKit.MakeButton(root, "", new Vector2(12, 372 + i * 62), new Vector2(300, 56), 14f);
                b.onClick.AddListener(() => { if (row < _shown.Count) Choose(_shown[row].Id); });
                _patientButtons.Add(b);
            }

            var sections = Records?.Tabs ?? new List<EmrTab>();
            float w = (864f - 6f * (sections.Count - 1)) / Mathf.Max(1, sections.Count);
            for (int i = 0; i < sections.Count; i++)
            {
                string id = sections[i].Id;
                var b = UiKit.MakeButton(root, sections[i].Label, new Vector2(324 + i * (w + 6), 64), new Vector2(w, 40), 13f);
                b.onClick.AddListener(() => OpenTab(id));
                _sectionButtons.Add(b);
            }
            for (int i = 0; i < TabBar.Max; i++)
            {
                int n = i;
                var tab = UiKit.MakeButton(root, "", new Vector2(324 + i * 144, 112), new Vector2(104, 40), 13f);
                tab.onClick.AddListener(() => { if (n < Tabs.Open.Count) SwitchTab(Tabs.Open[n]); });
                var close = UiKit.MakeButton(root, "x", new Vector2(324 + i * 144 + 106, 112), new Vector2(32, 40), 16f);
                close.onClick.AddListener(() => { if (n < Tabs.Open.Count) CloseTab(Tabs.Open[n]); });
                _tabButtons.Add(tab);
                _closeButtons.Add(close);
            }
            UiKit.Panel(root, "Paper", new Vector2(324, 160), new Vector2(864, 400), EmrLook.Paper);
            _body = UiKit.Label(root, "Body", "", new Vector2(336, 168), new Vector2(840, 384), 18, color: EmrLook.Text);
            for (int i = 0; i < DocumentRows; i++)
            {
                int n = i;
                var open = UiKit.MakeButton(root, "", new Vector2(324, 568 + i * 36), new Vector2(700, 32), 13f);
                open.onClick.AddListener(() => { if (n < _documents.Count) OpenDocument(_documents[n].Id, true); });
                var image = UiKit.MakeButton(root, "View image", new Vector2(1032, 568 + i * 36), new Vector2(156, 32), 13f);
                image.onClick.AddListener(() => { if (n < _documents.Count) ViewImage(_documents[n].Id, true); });
                _documentButtons.Add((open, image));
            }
            _more = UiKit.MakeButton(root, "More documents", new Vector2(1032, 528), new Vector2(156, 28), 12f);
            _more.onClick.AddListener(() => { _docPage++; RenderNow(); });
            _status = UiKit.Label(root, "Status", "", new Vector2(12, 732), new Vector2(1176, 26), 15, TextAlignmentOptions.MidlineLeft, EmrLook.Dim);
            RenderNow();
        }

        private void Update()
        {
            var state = Host != null ? Host.Observable : null;
            if (state != _shownState) RenderNow();
        }

        /// <summary>Searches the ward list (search_patient). The list then shows Core's matches, best first; an empty query goes back to the quick list.</summary>
        public ActionOutcome Find(string query, bool click = false)
        {
            if (query.Trim().Length == 0) { _query = ""; RenderNow(); return null; }
            var outcome = PanelSend.Send(_send, RecordsSubsystem.SearchVerb, new Dictionary<string, string> { ["query"] = query.Trim() }, click);
            if (outcome == null) return null;
            _query = query.Trim();
            _shown.Clear();
            if (outcome.Accepted && Records != null) _shown.AddRange(EmrSearch.Ranked(EmrSearch.Matches(outcome.Effects, Records.Patients), _query));
            _status.text = outcome.Accepted ? (_shown.Count == 0 ? "No patient found" : _shown.Count + (_shown.Count == 1 ? " patient found" : " patients found")) : HintTexts.For(outcome.RejectReason);
            RenderNow();
            return outcome;
        }

        /// <summary>Shows this patient's record. The record opens on the profile tab; a distractor has no record here, so it shows only the name band.</summary>
        public bool Choose(string patientId)
        {
            var p = Records?.Patients.FirstOrDefault(x => x.Id == patientId);
            if (p == null) return false;
            SelectedPatient = p.Id;
            Tabs.Clear();
            if (p.IsCasePatient && Records.Tabs.Count > 0) Tabs.OpenTab(Records.Tabs[0].Id);
            _status.text = "";
            RenderNow();
            return true;
        }

        public bool OpenTab(string id)
        {
            if (!CasePatientOpen || Records.Tabs.All(t => t.Id != id)) return false;
            bool opened = Tabs.OpenTab(id);
            _status.text = opened ? "" : "Six tabs are open: close one first";
            RenderNow();
            return opened;
        }

        public bool SwitchTab(string id) { bool ok = Tabs.Switch(id); _docPage = 0; RenderNow(); return ok; }

        public bool CloseTab(string id) { bool ok = Tabs.Close(id); RenderNow(); return ok; }

        public ActionOutcome OpenDocument(string id, bool click = false) =>
            After(PanelSend.Send(_send, RecordsSubsystem.OpenVerb, new Dictionary<string, string> { ["id"] = id, ["view"] = RecordsSubsystem.EmrView }, click));

        public ActionOutcome ViewImage(string id, bool click = false) =>
            After(PanelSend.Send(_send, RecordsSubsystem.ImageVerb, new Dictionary<string, string> { ["id"] = id }, click));

        private bool CasePatientOpen => Records != null && Records.Patients.Any(p => p.Id == SelectedPatient && p.IsCasePatient);

        private ActionOutcome After(ActionOutcome outcome)
        {
            if (outcome != null) _status.text = outcome.Accepted ? "" : HintTexts.For(outcome.RejectReason);
            RenderNow();
            return outcome;
        }

        public void RenderNow()
        {
            if (Canvas == null) return;
            _shownState = Host != null ? Host.Observable : null;
            var records = Records;
            if (records == null) return;

            if (_query.Length == 0) { _shown.Clear(); _shown.AddRange(EmrSearch.QuickList(records.Patients)); }
            _listTitle.text = _query.Length == 0 ? "Ward list" : "Search: " + _query;
            for (int i = 0; i < _patientButtons.Count; i++)
            {
                bool used = i < _shown.Count;
                _patientButtons[i].gameObject.SetActive(used);
                if (used) _patientButtons[i].GetComponentInChildren<TMP_Text>().text = (_shown[i].Id == SelectedPatient ? "> " : "") + _shown[i].Name + "\n" + _shown[i].Mrn + "   born " + _shown[i].Dob + (_shown[i].Location.Length > 0 ? "\n" + _shown[i].Location : "");   // where the patient is (DT-264)
            }

            var patient = records.Patients.FirstOrDefault(p => p.Id == SelectedPatient);
            _banner.text = patient == null ? "" : patient.Name + "    " + patient.Mrn + "    born " + patient.Dob + (patient.Location.Length > 0 ? "    " + patient.Location : "");   // where the patient is (DT-264)
            bool open = CasePatientOpen;
            foreach (var b in _sectionButtons) b.interactable = open;

            var labels = EmrText.Build(records).ToDictionary(t => t.Id, t => t.Label);
            for (int i = 0; i < _tabButtons.Count; i++)
            {
                bool used = i < Tabs.Open.Count;
                _tabButtons[i].gameObject.SetActive(used);
                _closeButtons[i].gameObject.SetActive(used);
                if (used) _tabButtons[i].GetComponentInChildren<TMP_Text>().text = (Tabs.Open[i] == Tabs.Active ? "> " : "") + labels[Tabs.Open[i]];
            }

            _documents.Clear();
            if (open && Tabs.Active.Length > 0)
            {
                _body.text = EmrText.Body(Tabs.Active, records.Sections());
                var all = records.Emr.Where(d => d.Tab == Tabs.Active).ToList();
                int pages = Mathf.Max(1, (all.Count + DocumentRows - 1) / DocumentRows);
                _docPage %= pages;
                _documents.AddRange(all.Skip(_docPage * DocumentRows).Take(DocumentRows));
                _more.gameObject.SetActive(pages > 1);
            }
            else
            {
                _more.gameObject.SetActive(false);
                _body.text = patient == null ? "Choose a patient from the ward list or search for one." : open ? "" : "No record for this patient is held in the simulation.";
            }
            for (int i = 0; i < _documentButtons.Count; i++)
            {
                bool used = i < _documents.Count;
                _documentButtons[i].open.gameObject.SetActive(used);
                _documentButtons[i].image.gameObject.SetActive(used && _documents[i].ImageId.Length > 0);
                if (used) _documentButtons[i].open.GetComponentInChildren<TMP_Text>().text = _documents[i].Title;
            }
        }
    }
}
