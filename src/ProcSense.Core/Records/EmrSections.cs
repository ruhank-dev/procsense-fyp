using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using ProcSense.Core.Content;
using ProcSense.Core.State;

namespace ProcSense.Core.Records
{
    public sealed record EmrProfile(string Name, string Dob, string Mrn, string Sex, long Age, IReadOnlyList<Allergy> Allergies, IReadOnlyList<string> Alerts);
    public sealed record EmrNews2Point(string At, int Total);
    public sealed record EmrEpisode(string Reason, IReadOnlyList<string> Documents, IReadOnlyList<EmrNews2Point> News2Trend);
    public sealed record EmrLabRow(string Test, string Analyte, string Value, string Unit, string Flag, string Comment, string Previous, string At, string Range = "", string PreviousWhen = "");
    public sealed record EmrTrendPoint(string At, string Value);
    public sealed record EmrLabs(IReadOnlyList<EmrLabRow> Rows, IReadOnlyList<string> PreviousDocuments, IReadOnlyDictionary<string, IReadOnlyList<EmrTrendPoint>> Trends);
    public sealed record EmrImage(string DocumentId, string Title, string Report, string ImageId, string Notice);
    public sealed record EmrPrescription(string Treatment, string Label, string State);
    public sealed record EmrMedications(IReadOnlyList<EmrPrescription> Prescriptions, string RegularNote);

    /// <summary>
    /// What each section of the EMR shows (p0-patient-records-emr-s04), worked out on request from the record and the session, so it can never be stale or disagree with the folder.
    /// Profile (identity, allergies, alerts), current episode (reason, this admission's documents, the NEWS2 totals the trainee recorded), labs (results with flags, comments and the previous value,
    /// earlier results from the record, a trend for any analyte with two values or more), imaging (each report with its approved image, or a report-only notice), reports (every document),
    /// medical history, medications (the treatments a clinician has confirmed) and previous admissions and tests (the hospital record). Nothing here is a hidden fact the case wants found by asking:
    /// the history and medicines the patient tells are not copied in; only what the record itself holds.
    /// </summary>
    public sealed class EmrSections
    {
        public const string ReportOnly = "Report only: no image is available for this study.";

        public EmrProfile Profile { get; private set; } = null!;
        public EmrEpisode Episode { get; private set; } = null!;
        public EmrLabs Labs { get; private set; } = null!;
        public IReadOnlyList<EmrImage> Imaging { get; private set; } = null!;
        public IReadOnlyList<string> Reports { get; private set; } = null!;
        public IReadOnlyList<string> History { get; private set; } = null!;
        public EmrMedications Medications { get; private set; } = null!;
        public IReadOnlyList<string> Previous { get; private set; } = null!;

        internal static EmrSections Build(Session s, CaseFile? file, IReadOnlyList<RecordDocument> docs)
        {
            var c = s.Patient.Clinical;
            var emr = docs.Where(d => d.InEmr).ToList();
            var id = file?.Identity;
            var allergies = file?.Allergies ?? (IReadOnlyList<Allergy>)new List<Allergy>();
            var alerts = allergies.Select(a => "Allergy: " + a.Substance + (a.Reaction.Length > 0 ? " (" + a.Reaction + ")" : "")).ToList();

            var rows = new List<EmrLabRow>();
            var trends = new SortedDictionary<string, List<EmrTrendPoint>>(StringComparer.Ordinal);
            foreach (var o in c.Investigations.Where(o => o.Kind != "imaging" && (o.State == "RESULT_AVAILABLE" || o.State == "VIEWED" || o.State == "ACKNOWLEDGED")))
            {
                string at = Clock(s, o.ReadyStep);
                foreach (var r in o.Results)
                {
                    rows.Add(new EmrLabRow(o.Label, r.Analyte, r.Value, r.Unit, r.Flag, r.Comment, r.Previous, at, r.Range, r.PreviousWhen));
                    if (r.Value.Length == 0) continue;
                    if (!trends.TryGetValue(r.Analyte, out var points)) trends[r.Analyte] = points = new List<EmrTrendPoint>();
                    if (points.Count == 0 && r.Previous.Length > 0) points.Add(new EmrTrendPoint("previous", r.Previous));
                    points.Add(new EmrTrendPoint(at, r.Value));
                }
            }

            return new EmrSections
            {
                Profile = new EmrProfile(id == null ? "" : (id.Title + " " + id.FullName).Trim(), id?.Dob ?? "", RecordsSubsystem.Printed(id?.Mrn ?? ""), id?.Sex ?? "", id?.Age ?? 0, allergies, alerts),
                Episode = new EmrEpisode(file?.Presentation.Summary ?? "", emr.Where(d => d.Episode == "current" && d.Tab == "current_episode").Select(d => d.Id).ToList(),
                    c.News2.Select(n => new EmrNews2Point(Clock(s, n.Step), n.Total)).ToList()),
                Labs = new EmrLabs(rows, emr.Where(d => d.Type == "lab_result").Select(d => d.Id).ToList(),
                    trends.Where(kv => kv.Value.Count >= 2).ToDictionary(kv => kv.Key, kv => (IReadOnlyList<EmrTrendPoint>)kv.Value, StringComparer.Ordinal)),
                Imaging = emr.Where(d => d.Tab == "imaging").Select(d => new EmrImage(d.Id, d.Title, d.Body, d.ImageId, d.ImageId.Length == 0 ? ReportOnly : "")).ToList(),
                Reports = emr.Select(d => d.Id).ToList(),
                History = emr.Where(d => d.Tab == "history" || d.Tab == "reports" && d.Episode == "record").Select(d => d.Id).ToList(),
                Medications = new EmrMedications(c.Orders.Where(o => o.State == "CONFIRMED" || o.State == "ADMINISTERED").Select(o => new EmrPrescription(o.Treatment, o.Label, o.State)).ToList(),
                    "Regular medicines are in the record's summary letter (Reports) and are confirmed by asking the patient."),
                Previous = emr.Where(d => d.Episode == "record").Select(d => d.Id).ToList(),
            };
        }

        private static string Clock(Session s, long step) => Observation.ObservableBuilder.Hms(s.Clock.Start + TimeSpan.FromSeconds(step * Time.SimClock.Step)).Substring(0, 5);
    }
}
