using System.Collections.Generic;
using ProcSense.Core.Diagnostics;

namespace ProcSense.VR.Views
{
    /// <summary>Where one sample tube (or the patient, or the report) is for a trip, derived only from the diagnostics loop's own stage. Pure data: no Unity types, so it is tested in EditMode.</summary>
    public enum TubeSite { None, Bedside, TubeStation, Porter, Laboratory }

    public sealed class DiagnosticsLine
    {
        public int OrderId;
        public string Test = "";
        public string Stage = "";
        public TubeSite Tube = TubeSite.None;
        /// <summary>True while a porter is carrying the sample.</summary>
        public bool PorterVisible;
        /// <summary>True from RESULT_READY: the report exists; it is on the desk only once delivered.</summary>
        public bool ReportReady;
        public bool ReportOnDesk;
        /// <summary>True while the patient is away from the bay for this trip.</summary>
        public bool PatientAway;
        public string Text = "";
    }

    /// <summary>
    /// The Unity half of p0-diagnostic-transitions-s04 and s05: the lab-sample tube, the tube station, the porter and the printed report as text and flags (the 3D props read these).
    /// It changes nothing in Core. Everything is read from <see cref="DiagnosticTrip"/>; a stage the loop does not publish is shown as nothing, never guessed.
    /// </summary>
    public static class DiagnosticsSceneText
    {
        public static List<DiagnosticsLine> Build(IReadOnlyList<DiagnosticTrip> trips)
        {
            var lines = new List<DiagnosticsLine>();
            if (trips == null) return lines;
            foreach (var t in trips)
            {
                var l = new DiagnosticsLine { OrderId = t.OrderId, Test = t.Test, Stage = t.Stage };
                bool sample = t.Kind == "blood" || t.Via.Length > 0 || t.TakenStep >= 0;
                switch (t.Stage)
                {
                    case DiagnosticTrip.SampleWaiting:
                        l.Tube = TubeSite.Bedside; l.Text = "Sample taken, still at the bedside";
                        break;
                    case DiagnosticTrip.SampleSent:
                        l.Tube = t.Via == DiagnosticsLoop.Porter ? TubeSite.Porter : TubeSite.TubeStation;
                        l.PorterVisible = t.Via == DiagnosticsLoop.Porter;
                        l.Text = t.Via == DiagnosticsLoop.Porter ? "Sample with the porter" : "Sample in the tube station";
                        break;
                    case DiagnosticTrip.AtBedside:
                        l.Tube = sample ? TubeSite.Laboratory : TubeSite.Bedside; l.Text = sample ? "Sample at the laboratory" : "Bedside test in progress";
                        break;
                    case DiagnosticTrip.PatientAway:
                        l.PatientAway = true; l.Text = "Patient away for " + t.Test;
                        break;
                    case DiagnosticTrip.ResultReady:
                        l.ReportReady = true; l.PatientAway = t.PatientTravels; l.Tube = sample ? TubeSite.Laboratory : TubeSite.None;
                        l.Text = t.PatientTravels ? "Result ready; patient still away" : "Result ready; report coming";
                        break;
                    case DiagnosticTrip.PatientBack:
                        l.ReportReady = true; l.Text = "Patient back; report on its way";
                        break;
                    case DiagnosticTrip.ReportDelivered:
                        l.ReportReady = true; l.ReportOnDesk = true; l.Text = "Report delivered to the desk";
                        break;
                    case DiagnosticTrip.Rejected:
                        l.Text = "Order refused"; break;
                    default:
                        l.Text = "Requested"; break;
                }
                lines.Add(l);
            }
            return lines;
        }

        public static string Summary(IReadOnlyList<DiagnosticsLine> lines)
        {
            if (lines == null || lines.Count == 0) return "No tests in progress.";
            var parts = new List<string>();
            foreach (var l in lines) parts.Add(l.Test + ": " + l.Text);
            return string.Join("\n", parts);
        }
    }
}
