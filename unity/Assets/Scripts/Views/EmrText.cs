using System.Collections.Generic;
using System.Linq;
using System.Text;
using ProcSense.Core.Records;

namespace ProcSense.VR.Views
{
    /// <summary>One tab of the EMR as the trainee reads it: its label (with the number of new documents), and its text.</summary>
    public sealed class EmrTabText
    {
        public string Id, Label, Body;
        public int NewDocuments;
    }

    /// <summary>
    /// Puts the EMR sections that Core works out (<see cref="EmrSections"/>, plan p0-patient-records-emr-s04) and the new-document badges (<see cref="Badges"/>, s05) into plain text, one block per tab. Nothing is invented here: every line is a field of a section,
    /// and a tab with nothing says so. A tab's label carries "(n)" while n documents on it are unopened; opening a document anywhere clears it in Core, so the next call shows no badge. Layout, fonts and the 3D screen are Sannan's work (UX-P0-EMR).
    /// </summary>
    public static class EmrText
    {
        public static IReadOnlyList<EmrTabText> Build(RecordsSubsystem records)
        {
            var sec = records.Sections();
            var badges = records.Badges;
            var tabs = new List<EmrTabText>();
            foreach (var tab in records.Tabs)
            {
                int n = badges.Count(tab.Id);
                tabs.Add(new EmrTabText { Id = tab.Id, NewDocuments = n, Label = n > 0 ? tab.Label + " (" + n + ")" : tab.Label, Body = Body(tab.Id, sec) });
            }
            return tabs;
        }

        public static string Body(string tab, EmrSections s)
        {
            var sb = new StringBuilder();
            switch (tab)
            {
                case "profile":
                    sb.AppendLine(s.Profile.Name).AppendLine("Date of birth: " + s.Profile.Dob + "   Sex: " + s.Profile.Sex + "   Age: " + s.Profile.Age).AppendLine("Record number: " + s.Profile.Mrn);
                    foreach (var a in s.Profile.Alerts) sb.AppendLine(a);
                    if (s.Profile.Alerts.Count == 0) sb.AppendLine("No alerts on the record.");
                    break;
                case "current_episode":
                    sb.AppendLine(s.Episode.Reason.Length > 0 ? s.Episode.Reason : "No reason recorded.");
                    foreach (var d in s.Episode.Documents) sb.AppendLine("Document: " + d);
                    foreach (var p in s.Episode.News2Trend) sb.AppendLine("NEWS2 " + p.Total + " at " + p.At);
                    break;
                case "labs":
                    foreach (var r in s.Labs.Rows)
                        sb.AppendLine(r.Test + " - " + r.Analyte + ": " + r.Value + " " + r.Unit + (r.Flag.Length > 0 ? " [" + r.Flag + "]" : "") + (r.Previous.Length > 0 ? " (previously " + r.Previous + ")" : "") + (r.Comment.Length > 0 ? " - " + r.Comment : "") + "  at " + r.At);
                    foreach (var kv in s.Labs.Trends) sb.AppendLine("Trend " + kv.Key + ": " + string.Join(" > ", kv.Value.Select(p => p.Value)));
                    foreach (var d in s.Labs.PreviousDocuments) sb.AppendLine("Earlier result: " + d);
                    if (s.Labs.Rows.Count == 0 && s.Labs.PreviousDocuments.Count == 0) sb.AppendLine("No results yet.");
                    break;
                case "imaging":
                    foreach (var i in s.Imaging) sb.AppendLine(i.Title + "\n" + i.Report + (i.Notice.Length > 0 ? "\n" + i.Notice : "\n[image " + i.ImageId + "]"));
                    if (s.Imaging.Count == 0) sb.AppendLine("No imaging yet.");
                    break;
                case "reports":
                    foreach (var r in s.Reports) sb.AppendLine(r);
                    if (s.Reports.Count == 0) sb.AppendLine("No reports.");
                    break;
                case "history":
                    foreach (var h in s.History) sb.AppendLine(h);
                    foreach (var p in s.Previous) sb.AppendLine("Previous: " + p);
                    if (s.History.Count == 0 && s.Previous.Count == 0) sb.AppendLine("Nothing recorded.");
                    break;
                case "medications":
                    foreach (var m in s.Medications.Prescriptions) sb.AppendLine(m.Label + " - " + m.State);
                    if (s.Medications.RegularNote.Length > 0) sb.AppendLine(s.Medications.RegularNote);
                    if (s.Medications.Prescriptions.Count == 0 && s.Medications.RegularNote.Length == 0) sb.AppendLine("No medicines confirmed.");
                    break;
                default:
                    sb.AppendLine("");
                    break;
            }
            return sb.ToString().TrimEnd();
        }
    }
}
