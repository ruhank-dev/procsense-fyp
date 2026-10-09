using System;
using System.Collections.Generic;
using System.Globalization;
using ProcSense.Core.Content;

namespace ProcSense.Core.Workflow.Steps
{
    /// <summary>
    /// Compares the three identifier sources (what the patient says, the wristband, the notes) with the patient's real identity.
    /// What the patient says is read by <see cref="SpokenIdentity"/>, so it can be phrased in many ways.
    /// </summary>
    public static class IdentityCheck
    {
        /// <summary>Every difference found, as plain sentences. Empty means nothing disagrees with the patient's identity (see <see cref="Unclear"/> for answers that are incomplete).</summary>
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

            var said = SpokenIdentity.Compare(c.SpokenIdentityAnswer, id);
            if (said.Name == Fit.Mismatch) found.Add("the name the patient says ('" + c.SpokenIdentityAnswer + "') does not match " + id.FullName);
            if (said.Date == Fit.Mismatch) found.Add("the date of birth the patient says ('" + c.SpokenIdentityAnswer + "') does not match " + id.Dob);
            return found;
        }

        /// <summary>What the patient said is not wrong but cannot be relied on: part of it is missing or could be read two ways. The answer text needs fixing, not the trainee.</summary>
        public static List<string> Unclear(CaseCard c)
        {
            var said = SpokenIdentity.Compare(c.SpokenIdentityAnswer, c.Identity);
            var unclear = new List<string>();
            if (said.Name == Fit.Missing || said.Name == Fit.Ambiguous) unclear.Add("the patient's spoken answer does not give her full name ('" + c.SpokenIdentityAnswer + "')");
            if (said.Date == Fit.Missing) unclear.Add("the patient's spoken answer does not give a complete date of birth ('" + c.SpokenIdentityAnswer + "')");
            if (said.Date == Fit.Ambiguous) unclear.Add("the patient's spoken date of birth can be read two ways ('" + c.SpokenIdentityAnswer + "')");
            return unclear;
        }

        private static void Compare(List<string> found, string what, string a, string b)
        {
            if (!string.Equals(a, b, StringComparison.OrdinalIgnoreCase)) found.Add(what + " '" + a + "' does not match '" + b + "'");
        }
    }
}
