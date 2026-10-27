using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ProcSense.Core;
using ProcSense.Core.Records;

namespace ProcSense.VR.Records.Folder
{
    /// <summary>One page of the folder: part of one document (a long document runs over several pages).</summary>
    public sealed class FolderPage
    {
        public string DocumentId = "", Title = "", Text = "", ImageId = "";
        public int Part = 1, Parts = 1;
        public string Heading => Parts > 1 ? Title + "  (" + Part + " of " + Parts + ")" : Title;
    }

    /// <summary>
    /// The pages of the physical folder (p0-patient-records-emr-s02), from Core's folder (<see cref="RecordsSubsystem.Folder"/>: this admission's paperwork and the printed results filed in it), in the order the
    /// documents arrived. A printed result carries its own report; the paperwork is live: the clerking note, the observation chart, the consent form and the ward checklist are written from what the session
    /// holds now (the identifiers, the vitals and NEWS2, the explanations given, the items ticked), so the folder and the EMR can never disagree. Nothing here changes Core.
    /// </summary>
    public static class FolderPages
    {
        public const int CharsPerPage = 620;

        public static IReadOnlyList<FolderPage> Build(Session session, ProcSense.Core.Content.ContentPackage content, int charsPerPage = CharsPerPage)
        {
            var pages = new List<FolderPage>();
            var records = session?.Records;
            if (records == null) return pages;
            EmrSections sections = null;
            foreach (var doc in records.Folder)
            {
                string text = doc.Body.Length > 0 ? doc.Body : Live(doc, session, content, sections ??= records.Sections());
                var parts = Paginate(text, charsPerPage);
                for (int i = 0; i < parts.Count; i++)
                    pages.Add(new FolderPage { DocumentId = doc.Id, Title = doc.Title, Text = parts[i], ImageId = i == 0 ? doc.ImageId : "", Part = i + 1, Parts = parts.Count });
            }
            return pages;
        }

        /// <summary>The text of a paperwork document, from what the session holds now.</summary>
        public static string Live(RecordDocument doc, Session s, ProcSense.Core.Content.ContentPackage content, EmrSections sections)
        {
            var o = s.Observable;
            var sb = new StringBuilder();
            switch (doc.Type)
            {
                case "clerking":
                    if (o.Notes != null) sb.AppendLine(o.Notes.FullName).AppendLine("Date of birth: " + o.Notes.Dob).AppendLine("Record number: " + o.Notes.Mrn);
                    if (sections.Episode.Reason.Length > 0) sb.AppendLine().AppendLine("Reason for admission: " + sections.Episode.Reason);
                    sb.AppendLine().AppendLine(sections.Profile.Alerts.Count > 0 ? string.Join("\n", sections.Profile.Alerts) : "No allergies recorded.");
                    break;
                case "obs_chart":
                    foreach (var v in o.Vitals) sb.AppendLine(v.Name + ": " + v.Value + (v.At.Length > 0 ? "   at " + v.At : ""));
                    foreach (var n in o.News2) sb.AppendLine("NEWS2 " + n.Total + "   at " + n.At);
                    if (o.Vitals.Count == 0 && o.News2.Count == 0) sb.AppendLine("No observations recorded yet.");
                    break;
                case "consent":
                    sb.AppendLine("Signed.");
                    var explained = s.Consent?.Explained ?? new List<string>();
                    if (explained.Count > 0) sb.AppendLine("Explained: " + string.Join(", ", explained.Select(t => t.Replace("_", " "))));
                    break;
                case "ward_checklist":
                    sb.AppendLine("Signed.");
                    var ticked = new HashSet<string>(s.Ward?.Ticked ?? new List<string>(), StringComparer.Ordinal);
                    foreach (var item in content?.Ward.Items.OrderBy(i => i.Order) ?? Enumerable.Empty<ProcSense.Core.Workflow.Steps.WardItem>())
                        sb.AppendLine((ticked.Contains(item.Id) ? "[x] " : "[ ] ") + item.Label);
                    break;
                default:
                    sb.AppendLine(doc.Title);
                    break;
            }
            return sb.ToString().TrimEnd();
        }

        /// <summary>Splits text into pages of at most <paramref name="chars"/> characters, at a line break where it can and a space otherwise; a word longer than a page is cut.</summary>
        public static IReadOnlyList<string> Paginate(string text, int chars)
        {
            var pages = new List<string>();
            string rest = (text ?? "").Trim();
            while (rest.Length > chars)
            {
                int cut = rest.LastIndexOf('\n', chars);
                if (cut < chars / 2) cut = rest.LastIndexOf(' ', chars);
                if (cut <= 0) cut = chars;
                pages.Add(rest.Substring(0, cut).TrimEnd());
                rest = rest.Substring(cut).TrimStart();
            }
            pages.Add(rest);
            return pages;
        }
    }
}
