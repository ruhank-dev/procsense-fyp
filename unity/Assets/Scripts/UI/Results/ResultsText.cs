using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using ProcSense.Core.Observation;
using ProcSense.Core.Records;

namespace ProcSense.VR.UI.Results
{
    /// <summary>One line of the results table: every field is the EMR's lab row (<see cref="EmrLabRow"/>), plus the flag as a mark and the trend from the previous value.</summary>
    public sealed class ResultLine
    {
        public string Test = "", Analyte = "", Value = "", Unit = "", Range = "", Flag = "", Mark = "", Comment = "", Previous = "", PreviousWhen = "", Trend = "", At = "";
    }

    /// <summary>
    /// The results viewer's words (p0-vr-interaction-ui-s05), worked out from the same Core data as the EMR (<see cref="EmrSections"/>), so the viewer and the record can never disagree. Nothing is converted or invented:
    /// values, units, ranges and lab comments are shown as content wrote them; the flag is Core's, only shortened (high is H, low is L). The trend compares the value with the previous one as numbers.
    /// </summary>
    public static class ResultsText
    {
        public const string Table = "table", Image = "image", Strip = "strip";
        public const string Up = "↑", Down = "↓", Same = "→";

        /// <summary>How a test is shown: imaging as a picture with its report, the ECG as a strip, everything else as a table.</summary>
        public static string ModeOf(string test, string kind) => kind == "imaging" ? Image : test == "ecg" ? Strip : Table;

        public static string Mark(string flag)
        {
            switch (flag)
            {
                case "high": return "H";
                case "low": return "L";
                case "critical_high": return "HH";
                case "critical_low": return "LL";
                case "abnormal": return "A";
                case "positive": return "POS";
                default: return "";
            }
        }

        public static string TrendOf(string value, string previous)
        {
            if (!double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var now) ||
                !double.TryParse(previous, NumberStyles.Float, CultureInfo.InvariantCulture, out var before)) return "";
            return now > before ? Up : now < before ? Down : Same;
        }

        /// <summary>The rows of one test (by its label, as the EMR names it), oldest first; a repeat appears below the first sample.</summary>
        public static IReadOnlyList<ResultLine> Lines(EmrSections sections, string testLabel) =>
            sections.Labs.Rows.Where(r => r.Test == testLabel).Select(r => new ResultLine
            {
                Test = r.Test, Analyte = r.Analyte, Value = r.Value, Unit = r.Unit, Range = r.Range, Flag = r.Flag, Mark = Mark(r.Flag), Comment = r.Comment,
                Previous = r.Previous, PreviousWhen = r.PreviousWhen, Trend = TrendOf(r.Value, r.Previous), At = r.At,
            }).ToList();

        public static string Line(ResultLine l)
        {
            string s = l.Analyte + ": " + l.Value + (l.Unit.Length > 0 ? " " + l.Unit : "") + (l.Mark.Length > 0 ? "  " + l.Mark : "") + (l.Range.Length > 0 ? "   (" + l.Range + ")" : "");
            if (l.Previous.Length > 0) s += "   previously " + l.Previous + (l.PreviousWhen.Length > 0 ? " (" + l.PreviousWhen + ")" : "") + (l.Trend.Length > 0 ? " " + l.Trend : "");
            if (l.Comment.Length > 0) s += "\n    " + l.Comment;
            return s + "   at " + l.At;
        }

        /// <summary>The text of the viewer for one investigation: its table, its image and report, or its strip. Empty until the result is available.</summary>
        public static string Body(EmrSections sections, ObservedInvestigation investigation, string mode)
        {
            if (mode == Image)
            {
                if (investigation.Report == null) return "";
                return investigation.Report.Text + (investigation.Report.ImageId.Length > 0 ? "\n[image " + investigation.Report.ImageId + "]" : "\n" + EmrSections.ReportOnly) +
                       string.Concat(investigation.Report.Measurements.Select(m => "\n" + m));
            }
            var lines = Lines(sections, investigation.Label);
            if (mode == Strip) return string.Join("\n", lines.Select(l => "[ECG strip]  " + l.Value + (l.Comment.Length > 0 ? "\n    " + l.Comment : "") + "   at " + l.At));
            return string.Join("\n", lines.Select(Line));
        }
    }
}
