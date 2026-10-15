using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using ProcSense.Core.Actions;
using ProcSense.Core.Content;
using ProcSense.Core.Diagnostics;
using ProcSense.Core.Pipeline;
using ProcSense.Core.State;
using ProcSense.Core.Workflow;
using ProcSense.Core.Workflow.Steps;

namespace ProcSense.Core.Records
{
    /// <summary>One document of the patient's record. There is one copy: the folder and the EMR are two views of it (so they can never disagree).</summary>
    public sealed class RecordDocument
    {
        public string Id { get; internal set; } = "";
        public string Type { get; internal set; } = "";

        /// <summary>"current" (this admission's paperwork) or "record" (the hospital record, the EMR only).</summary>
        public string Episode { get; internal set; } = "";
        public string Title { get; internal set; } = "";
        public string Body { get; internal set; } = "";

        /// <summary>When it was written, as the record states it ("2 months before today"); "" for a document of this session (see CreatedStep).</summary>
        public string DateOffset { get; internal set; } = "";

        /// <summary>The picture that goes with it (an imaging report), or "".</summary>
        public string ImageId { get; internal set; } = "";
        public bool Synthetic { get; internal set; }

        /// <summary>The sim step it appeared at (0 for what the session starts with).</summary>
        public long CreatedStep { get; internal set; }

        /// <summary>The investigation it holds the result of (0 for other documents).</summary>
        public int OrderId { get; internal set; }

        /// <summary>In the physical folder (current paperwork, and a printed result once the trainee has filed it).</summary>
        public bool InFolder { get; internal set; }

        /// <summary>A printed result that has been delivered (by the porter, with the patient, or from the bedside test) and lies on the desk until the trainee files it (p0-diagnostic-transitions-s05).</summary>
        public bool OnDesk { get; internal set; }

        /// <summary>Who brought the printout: "patient" (back from a scan), "laboratory" (the porter with a sample's report) or "bedside" ("" while not delivered).</summary>
        public string DeliveredWith { get; internal set; } = "";

        /// <summary>On the EMR computer (every document, a result as soon as it is ready).</summary>
        public bool InEmr { get; internal set; }

        public int TimesOpened { get; internal set; }
    }

    /// <summary>A patient on the EMR's ward list: the case patient or a distractor.</summary>
    public sealed record RecordPatient(string Id, string Name, string Dob, string Mrn, bool IsCasePatient);

    /// <summary>
    /// The patient's record (p0-patient-records-emr-s01). The folder holds this admission's paperwork (clerking note, observation chart, consent and ward checklist once signed, printed results once
    /// filed: a delivered printout lies on the desk until the trainee files it, p0-diagnostic-transitions-s05); the EMR holds everything, including the hospital record from earlier (content/records/records.json). A result becomes a document the moment it is ready (in the EMR) and goes into the
    /// folder when its report is delivered (p0-diagnostic-transitions-s01). The trainee's looking is logged as actions, so it is evidence for the score and the debrief ("did the trainee look at the previous
    /// HbA1c?"): open_document, view_image and search_patient (the ward list holds distractor patients, so finding the right record is a real task). Nothing here changes the patient.
    /// </summary>
    public sealed class RecordsSubsystem : ISubsystem
    {
        public const string OpenVerb = "open_document";
        public const string ImageVerb = "view_image";
        public const string SearchVerb = "search_patient";
        public const string FileVerb = "file_document";

        public const string MissingDocument = "MISSING_DOCUMENT";
        public const string UnknownDocument = "UNKNOWN_DOCUMENT";
        public const string NotInFolder = "NOT_IN_FOLDER";
        public const string UnknownView = "UNKNOWN_VIEW";
        public const string NoImage = "NO_IMAGE";
        public const string MissingQuery = "MISSING_QUERY";
        public const string NotDelivered = "NOT_DELIVERED";
        public const string AlreadyFiled = "ALREADY_FILED";
        public const string NotPrintout = "NOT_A_PRINTOUT";

        public const string FolderView = "folder";
        public const string EmrView = "emr";

        private readonly ContentPackage _content;
        private readonly string? _caseId;
        private readonly List<RecordDocument> _docs = new List<RecordDocument>();
        private readonly HashSet<string> _created = new HashSet<string>(StringComparer.Ordinal);
        private readonly List<RecordPatient> _patients = new List<RecordPatient>();
        private readonly List<GraphEvent> _history = new List<GraphEvent>();
        private Session _s = null!;

        /// <param name="caseId">Test hook: show the record of this case instead of the session's case.</param>
        public RecordsSubsystem(ContentPackage content, string? caseId = null) { _content = content; _caseId = caseId; }

        public int Order => PipelineOrder.Events + 6;

        private RecordTables Tables => _content.Records;
        private ClinicalLayer Clinical => _s.Patient.Clinical;

        /// <summary>Every document, in the order it appeared.</summary>
        public IReadOnlyList<RecordDocument> Documents => _docs;

        /// <summary>The physical folder: this admission's paperwork and the printed results filed in it.</summary>
        public IReadOnlyList<RecordDocument> Folder => _docs.Where(d => d.Episode == "current" && d.InFolder).ToList();

        /// <summary>Printed results delivered but not filed yet: they lie on the desk (nothing is lost if the trainee never files them; the EMR already has them).</summary>
        public IReadOnlyList<RecordDocument> Desk => _docs.Where(d => d.OnDesk).ToList();

        /// <summary>The EMR: the whole record, this admission and before.</summary>
        public IReadOnlyList<RecordDocument> Emr => _docs.Where(d => d.InEmr).ToList();

        /// <summary>The EMR's ward list (the case patient and the distractors), by name.</summary>
        public IReadOnlyList<RecordPatient> Patients => _patients;

        /// <summary>Documents opened, pictures viewed and searches, with sim steps (no score items yet; the scorer reads them as evidence later).</summary>
        public IReadOnlyList<GraphEvent> History => _history;

        public void Init(Session session)
        {
            _s = session;
            session.RecordsStep = this;
            string caseId = _caseId ?? session.Case.Id;
            foreach (var c in Tables.Current.Where(c => c.Created == "start")) AddCurrent(c);
            if (Tables.Records.TryGetValue(caseId, out var entries))
                foreach (var e in entries)
                    _docs.Add(new RecordDocument { Id = e.Id, Type = e.Type, Episode = Episode(e.Type), Title = e.Title, Body = e.Body, DateOffset = e.DateOffset, Synthetic = e.Synthetic, InEmr = true });
            var patients = new List<RecordPatient>();
            if (_content.CaseFiles.TryGetValue(caseId, out var file))
                patients.Add(new RecordPatient(caseId, file.Identity.Title + " " + file.Identity.FullName, file.Identity.Dob, file.Identity.Mrn, true));
            patients.AddRange(Tables.Distractors.Select(d => new RecordPatient(d.Id, d.Name, d.Dob, d.Mrn, false)));
            _patients.AddRange(patients.OrderBy(p => p.Name, StringComparer.Ordinal).ThenBy(p => p.Id, StringComparer.Ordinal));
            session.Verbs.Register(OpenVerb);
            session.Verbs.Register(ImageVerb);
            session.Verbs.Register(SearchVerb);
            session.Verbs.Register(FileVerb);
            session.Events.Subscribe<ResultReady>(OnResult);
            session.Events.Subscribe<ReportDelivered>(OnDelivered);
        }

        private string Episode(string type) => Tables.Type(type)?.Episode ?? "record";

        private void AddCurrent(CurrentDocument c)
        {
            if (!_created.Add(c.Id)) return;
            _docs.Add(new RecordDocument { Id = c.Id, Type = c.Type, Episode = "current", Title = c.Title, CreatedStep = _s.Clock.Steps, InFolder = true, InEmr = true });
        }

        // ---------------------------------------------------------------- what creates documents

        private void OnResult(ResultReady e)
        {
            var order = Clinical.Investigations.LastOrDefault(o => o.Test == e.Test && o.Instance == e.Instance);
            if (order == null || _docs.Any(d => d.OrderId == order.Id)) return;     // a test with several results publishes one ResultReady per result: one document
            string type = Tables.ResultTypes.TryGetValue(order.Kind, out var t) ? t : "lab_report";
            var report = order.Kind == "imaging" ? order.Report : null;
            var doc = new RecordDocument
            {
                Id = "result-" + order.Id.ToString(CultureInfo.InvariantCulture),
                Type = type,
                Episode = "current",
                Title = order.Label + (order.Instance > 1 ? " (repeat " + order.Instance.ToString(CultureInfo.InvariantCulture) + ")" : ""),
                Body = report != null ? report.Text : order.Results.Count > 0 ? string.Join("\n", order.Results.Select(Line)) : order.Note,   // no stated result: the reason instead
                ImageId = report?.ImageId ?? "",
                Synthetic = report?.Synthetic ?? false,
                CreatedStep = _s.Clock.Steps,
                OrderId = order.Id,
                InEmr = true,
                OnDesk = _s.Diagnostics == null,            // without the diagnostics loop nothing delivers the printout, so it is on the desk at once
            };
            _docs.Add(doc);
            _history.Add(new GraphEvent("document_added:" + type, doc.Id, doc.CreatedStep, Array.Empty<string>()));
        }

        private static string Line(ResultRecord r)
        {
            string flag = r.Flag.Length > 0 && r.Flag != "normal" ? " (" + r.Flag + ")" : "";
            return (r.Analyte + " " + r.Value + " " + r.Unit).Trim() + flag;
        }

        private void OnDelivered(ReportDelivered e)
        {
            var doc = _docs.FirstOrDefault(d => d.OrderId == e.OrderId);
            if (doc == null || doc.OnDesk || doc.InFolder) return;
            doc.OnDesk = true;
            doc.DeliveredWith = e.With;
            _history.Add(new GraphEvent("document_delivered:" + doc.Type, doc.Id, _s.Clock.Steps, Array.Empty<string>()));
        }

        // Runs every step, so it allocates nothing until a document is actually created (the engine's no-allocation-per-tick rule, PerformanceTests).
        private void Flags()
        {
            var current = Tables.Current;
            for (int i = 0; i < current.Count; i++)
            {
                var c = current[i];
                if (c.Created == "start" || _created.Contains(c.Id) || !Clinical.HasFlag(c.Created)) continue;
                AddCurrent(c);
                _history.Add(new GraphEvent("document_added:" + c.Type, c.Id, _s.Clock.Steps, Array.Empty<string>()));
            }
        }

        public void OnTick() => Flags();

        // ---------------------------------------------------------------- what the trainee does

        public void OnAction(SimAction a, ActionOutcome o)
        {
            switch (a.Verb)
            {
                case OpenVerb: Open(a, o); break;
                case ImageVerb: Image(a, o); break;
                case SearchVerb: Search(a, o); break;
                case FileVerb: File(a, o); break;
            }
            if (o.Accepted) Flags();
        }

        private RecordDocument? Find(SimAction a, ActionOutcome o)
        {
            if (!a.Params.TryGetValue("id", out var id) || id.Length == 0) { o.Reject(MissingDocument); return null; }
            var doc = _docs.FirstOrDefault(d => d.Id == id);
            if (doc == null) o.Reject(UnknownDocument);
            return doc;
        }

        private void Open(SimAction a, ActionOutcome o)
        {
            string view = a.Params.TryGetValue("view", out var v) && v.Length > 0 ? v : EmrView;
            if (view != FolderView && view != EmrView) { o.Reject(UnknownView); return; }
            var doc = Find(a, o);
            if (doc == null) return;
            if (view == FolderView && !(doc.Episode == "current" && (doc.InFolder || doc.OnDesk))) { o.Reject(NotInFolder); return; }   // a printout on the desk can be read before it is filed
            if (view == EmrView && !doc.InEmr) { o.Reject(UnknownDocument); return; }
            doc.TimesOpened++;
            o.Effects.Add("opened:" + doc.Id);
            o.Effects.Add("document_type:" + doc.Type);
            _history.Add(new GraphEvent("document_opened:" + doc.Type, doc.Id, _s.Clock.Steps, Array.Empty<string>()));
        }

        // The trainee puts a delivered printout into the patient's folder. The EMR had it from the moment the result was ready; this is the same document, now also in the folder.
        private void File(SimAction a, ActionOutcome o)
        {
            var doc = Find(a, o);
            if (doc == null) return;
            if (doc.OrderId == 0) { o.Reject(NotPrintout); return; }
            if (doc.InFolder) { o.Reject(AlreadyFiled); return; }
            if (!doc.OnDesk) { o.Reject(NotDelivered); return; }
            doc.OnDesk = false;
            doc.InFolder = true;
            o.Effects.Add("filed:" + doc.Id);
            _history.Add(new GraphEvent("document_filed:" + doc.Type, doc.Id, _s.Clock.Steps, Array.Empty<string>()));
        }

        private void Image(SimAction a, ActionOutcome o)
        {
            var doc = Find(a, o);
            if (doc == null) return;
            if (doc.ImageId.Length == 0) { o.Reject(NoImage); return; }
            o.Effects.Add("image:" + doc.ImageId);
            _history.Add(new GraphEvent("image_viewed", doc.Id, _s.Clock.Steps, Array.Empty<string>()));
        }

        // Name, number or date of birth, any part of it, any case: every patient on the ward list that matches (several can, which is the point of the near-miss names).
        private void Search(SimAction a, ActionOutcome o)
        {
            if (!a.Params.TryGetValue("query", out var q) || q.Trim().Length == 0) { o.Reject(MissingQuery); return; }
            string query = q.Trim();
            var found = _patients.Where(p => Has(p.Name, query) || Has(p.Mrn, query) || Has(p.Mrn.Replace("MR ", ""), query) || Has(p.Dob, query)).ToList();
            foreach (var p in found) o.Effects.Add("match:" + p.Id);
            o.Effects.Add("matches:" + found.Count.ToString(CultureInfo.InvariantCulture));
            _history.Add(new GraphEvent("patient_search", query, _s.Clock.Steps, Array.Empty<string>()));
        }

        private static bool Has(string text, string query) => text.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0;
    }
}
