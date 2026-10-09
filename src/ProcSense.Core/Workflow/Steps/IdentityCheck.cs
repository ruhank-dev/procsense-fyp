using System;
using System.Collections.Generic;
using System.Globalization;
using ProcSense.Core.Content;

namespace ProcSense.Core.Workflow.Steps
{
    /// <summary>
    /// Compares the three identifier sources (what the patient says, the wristband, the notes) with the patient's real identity.
    /// shortcut: the spoken answer is matched by name + day word + month + year found in the text, so spoken answers must be authored
    /// with the date spelled out ("fourteenth of March 1984"); upgrade when a case needs another phrasing.
    /// </summary>
    public static class IdentityCheck
    {
        private static readonly string[] DayWords =
        {
            "first", "second", "third", "fourth", "fifth", "sixth", "seventh", "eighth", "ninth", "tenth", "eleventh", "twelfth",
            "thirteenth", "fourteenth", "fifteenth", "sixteenth", "seventeenth", "eighteenth", "nineteenth", "twentieth",
            "twenty-first", "twenty-second", "twenty-third", "twenty-fourth", "twenty-fifth", "twenty-sixth", "twenty-seventh",
            "twenty-eighth", "twenty-ninth", "thirtieth", "thirty-first",
        };

        /// <summary>Every difference found, as plain sentences. Empty means all three sources agree with the patient's identity.</summary>
        public static List<string> Mismatches(CaseCard c, Wristband? band)
        {
            var found = new List<string>();
            var id = c.Identity;
            DateTime.TryParseExact(id.Dob, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var born);

            if (band != null) // no band on the wrist: nothing to compare (the missing band itself is handled by the caller)
            {
                Compare(found, "wristband name", band.NameText, id.FullName);
                if (!DateTime.TryParseExact(band.DobText, "dd/MM/yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var bandDob) || bandDob != born)
                    found.Add("wristband date of birth '" + band.DobText + "' does not match " + id.Dob);
                string bandMrn = band.MrnText.StartsWith("MR ", StringComparison.OrdinalIgnoreCase) ? band.MrnText.Substring(3) : band.MrnText;
                if (bandMrn != id.Mrn) found.Add("wristband MRN '" + band.MrnText + "' does not match " + id.Mrn);
            }

            Compare(found, "notes name", c.Notes.FullName, id.FullName);
            if (c.Notes.Dob != id.Dob) found.Add("notes date of birth '" + c.Notes.Dob + "' does not match " + id.Dob);
            if (c.Notes.Mrn != id.Mrn) found.Add("notes MRN '" + c.Notes.Mrn + "' does not match " + id.Mrn);

            string spoken = c.SpokenIdentityAnswer;
            string month = born.ToString("MMMM", CultureInfo.InvariantCulture);
            if (!Has(spoken, id.FullName) || !Has(spoken, DayWords[born.Day - 1]) || !Has(spoken, month) || !Has(spoken, born.Year.ToString(CultureInfo.InvariantCulture)))
                found.Add("what the patient says ('" + spoken + "') does not match " + id.FullName + ", " + id.Dob);
            return found;
        }

        private static void Compare(List<string> found, string what, string a, string b)
        {
            if (!string.Equals(a, b, StringComparison.OrdinalIgnoreCase)) found.Add(what + " '" + a + "' does not match '" + b + "'");
        }

        private static bool Has(string text, string part) => text.IndexOf(part, StringComparison.OrdinalIgnoreCase) >= 0;
    }
}
