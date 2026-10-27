using System;
using System.Collections.Generic;
using System.Linq;
using ProcSense.Core.Records;

namespace ProcSense.VR.Records.Emr
{
    /// <summary>
    /// The order the EMR shows patients in (p0-patient-records-emr-s03). Core decides who matches a search (search_patient, every patient whose name, number or date of birth contains the query); this only ranks them on
    /// the screen: an exact record number first, then a name word that is the query (the surname above another name), then a name word that starts with it, then the date of birth, then any other match. Ties go by name.
    /// The quick list puts the patient in the bay first, because typing in VR is slow.
    /// </summary>
    public static class EmrSearch
    {
        public static int Rank(RecordPatient p, string query)
        {
            string q = query.Trim();
            if (q.Length == 0) return 0;
            if (Same(p.Mrn, q) || Same(p.Mrn.Replace("MR ", ""), q)) return 50;
            var words = p.Name.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (words.Length > 0 && Same(words[words.Length - 1], q)) return 40;
            if (words.Any(w => Same(w, q))) return 30;
            if (words.Any(w => w.StartsWith(q, StringComparison.OrdinalIgnoreCase))) return 20;
            if (p.Dob.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0) return 10;
            return 1;
        }

        /// <summary>The patients Core matched, best first.</summary>
        public static IReadOnlyList<RecordPatient> Ranked(IEnumerable<RecordPatient> matched, string query) =>
            matched.OrderByDescending(p => Rank(p, query)).ThenBy(p => p.Name, StringComparer.Ordinal).ThenBy(p => p.Id, StringComparer.Ordinal).ToList();

        /// <summary>The ward list before any search: the patient in the bay, then the others by name.</summary>
        public static IReadOnlyList<RecordPatient> QuickList(IEnumerable<RecordPatient> patients) =>
            patients.OrderByDescending(p => p.IsCasePatient).ThenBy(p => p.Name, StringComparer.Ordinal).ThenBy(p => p.Id, StringComparer.Ordinal).ToList();

        /// <summary>The patients a search_patient answer names (its "match:" effects), in the ward list's order.</summary>
        public static IReadOnlyList<RecordPatient> Matches(IEnumerable<string> effects, IReadOnlyList<RecordPatient> patients)
        {
            var ids = new HashSet<string>(effects.Where(e => e.StartsWith("match:")).Select(e => e.Substring(6)), StringComparer.Ordinal);
            return patients.Where(p => ids.Contains(p.Id)).ToList();
        }

        private static bool Same(string a, string b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
    }
}
