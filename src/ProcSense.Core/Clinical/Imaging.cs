using System;
using System.Collections.Generic;
using System.Linq;
using ProcSense.Core.Content;
using ProcSense.Core.State;

namespace ProcSense.Core.Clinical
{
    public sealed record ImagingFinding(string Code, string Plain, string Category, string Ref);

    /// <summary>One approved report text for a finding: the picture it goes with and the numbers stated in it.</summary>
    public sealed record ImagingReportText(string Id, string Code, string ImageId, string Text, IReadOnlyDictionary<string, string> Measurements, bool Synthetic);

    /// <summary>
    /// What each imaging test can show: the categories a trainee interprets, each finding code's category, the approved report texts, and the finding each case states for each study
    /// (content/imaging/imaging.json, built from the verified finding tables and the approved reports). Only approved reports are in it, so an unapproved picture is never shown.
    /// </summary>
    public sealed class ImagingTables
    {
        public static readonly ImagingTables Empty = new ImagingTables(new List<string>(), new Dictionary<string, IReadOnlyList<string>>(), new Dictionary<string, string>(), new List<ImagingFinding>(), new List<ImagingReportText>(),
            new Dictionary<string, IReadOnlyDictionary<string, string>>());

        private readonly Dictionary<string, List<ImagingReportText>> _byCode;

        public ImagingTables(IReadOnlyList<string> categories, IReadOnlyDictionary<string, IReadOnlyList<string>> tests, IReadOnlyDictionary<string, string> clean, IReadOnlyList<ImagingFinding> findings,
            IReadOnlyList<ImagingReportText> reports, IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> caseStudies)
        {
            Categories = categories; Tests = tests; Clean = clean; Findings = findings; Reports = reports; CaseStudies = caseStudies;
            _byCode = reports.GroupBy(r => r.Code).ToDictionary(g => g.Key, g => g.OrderBy(r => r.Id, StringComparer.Ordinal).ToList());
        }

        /// <summary>normal, positive, complicated, other_diagnosis, non_diagnostic ('not visualised' is equivocal, never negative).</summary>
        public IReadOnlyList<string> Categories { get; }
        /// <summary>Investigation test id to the studies that belong to it (the ultrasound test covers gallbladder, appendix, pelvis and renal studies).</summary>
        public IReadOnlyDictionary<string, IReadOnlyList<string>> Tests { get; }
        public IReadOnlyDictionary<string, string> Clean { get; }
        public IReadOnlyList<ImagingFinding> Findings { get; }
        public IReadOnlyList<ImagingReportText> Reports { get; }
        public IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> CaseStudies { get; }

        public ImagingFinding? FindingOf(string code) => Findings.FirstOrDefault(f => f.Code == code);

        public bool IsImagingTest(string test) => Tests.ContainsKey(test);

        /// <summary>
        /// The report the test shows for this case: the finding the case states for the study (one of the approved report variants for it, chosen by a fixed rule from the case id and study, so the same case
        /// always shows the same one); or, for a chest X-ray or an ECG the case states nothing for, a film or strip with no abnormality; otherwise null.
        /// </summary>
        public ImagingReport? Resolve(string caseId, string test)
        {
            if (!Tests.TryGetValue(test, out var studies)) return null;
            string study = "", code = "", origin = "case";
            if (CaseStudies.TryGetValue(caseId, out var mine))
                foreach (var s in studies)
                    if (mine.TryGetValue(s, out var c)) { study = s; code = c; break; }
            if (code.Length == 0)
            {
                if (!Clean.TryGetValue(test, out var clean)) return null;
                study = studies[0]; code = clean; origin = "clean";
            }
            var finding = FindingOf(code);
            if (finding == null || !_byCode.TryGetValue(code, out var texts) || texts.Count == 0) return null;
            var report = texts[(int)(Fnv(caseId + "|" + study) % (uint)texts.Count)];
            return new ImagingReport(study, code, finding.Category, finding.Plain, report.Id, report.ImageId, report.Text, report.Measurements, report.Synthetic, origin);
        }

        // FNV-1a, so the choice does not depend on the runtime's string hashing.
        private static uint Fnv(string text)
        {
            uint h = 2166136261;
            foreach (char ch in text) { h ^= ch; h *= 16777619; }
            return h;
        }

        public static ImagingTables Parse(string json, string source = "imaging")
        {
            var root = new JsonObj(MiniJson.Parse(json), source);
            var categories = new List<string>();
            foreach (var c in root.Arr("categories")) categories.Add(c as string ?? throw new ContentException(root.Sub("categories") + ": expected text"));
            if (categories.Count < 2) throw new ContentException(root.Sub("categories") + ": needs at least two categories");
            var tests = new SortedDictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);
            var rawTests = root.Any("tests") as Dictionary<string, object?> ?? throw new ContentException(root.Sub("tests") + ": expected an object");
            foreach (var kv in rawTests)
            {
                var studies = new List<string>();
                foreach (var s in kv.Value as List<object?> ?? throw new ContentException(root.Sub("tests") + "." + kv.Key + ": expected a list")) studies.Add(s as string ?? throw new ContentException(root.Sub("tests") + "." + kv.Key + ": expected text"));
                if (studies.Count == 0) throw new ContentException(root.Sub("tests") + "." + kv.Key + ": needs at least one study");
                tests[kv.Key] = studies;
            }
            var rawClean = root.StringMap("clean_fallback");
            var clean = new SortedDictionary<string, string>(rawClean, StringComparer.Ordinal);

            var findings = new List<ImagingFinding>();
            var rawFindings = root.Arr("findings");
            for (int i = 0; i < rawFindings.Count; i++)
            {
                var o = new JsonObj(rawFindings[i], JsonObj.Index(root.Sub("findings"), i));
                string code = o.Str("code"), category = o.Str("category");
                if (findings.Any(f => f.Code == code)) throw new ContentException(o.Sub("code") + ": duplicate finding '" + code + "'");
                if (!categories.Contains(category)) throw new ContentException(o.Sub("category") + ": '" + category + "' is not one of " + string.Join(", ", categories));
                findings.Add(new ImagingFinding(code, o.Str("plain"), category, o.Str("ref")));
                o.Finish();
            }
            var reports = new List<ImagingReportText>();
            var rawReports = root.Arr("reports");
            for (int i = 0; i < rawReports.Count; i++)
            {
                var o = new JsonObj(rawReports[i], JsonObj.Index(root.Sub("reports"), i));
                string code = o.Str("code");
                if (findings.All(f => f.Code != code)) throw new ContentException(o.Sub("code") + ": the report is for '" + code + "', which is not a finding");
                var m = new SortedDictionary<string, string>(o.StringMap("measurements"), StringComparer.Ordinal);
                reports.Add(new ImagingReportText(o.Str("id"), code, o.Str("image_id"), o.Str("text"), m, o.Bool("synthetic")));
                o.Finish();
            }
            var caseStudies = new SortedDictionary<string, IReadOnlyDictionary<string, string>>(StringComparer.Ordinal);
            var rawCases = root.Any("case_studies") as Dictionary<string, object?> ?? throw new ContentException(root.Sub("case_studies") + ": expected an object");
            foreach (var kv in rawCases)
            {
                var o = new JsonObj(kv.Value, root.Sub("case_studies") + "." + kv.Key);
                var map = new SortedDictionary<string, string>(StringComparer.Ordinal);
                foreach (var study in tests.Values.SelectMany(x => x))
                    if (o.Has(study)) map[study] = o.Str(study);
                o.Finish();
                foreach (var code in map.Values)
                    if (findings.All(f => f.Code != code) || reports.All(r => r.Code != code)) throw new ContentException(root.Sub("case_studies") + "." + kv.Key + ": '" + code + "' has no finding or no approved report");
                caseStudies[kv.Key] = map;
            }
            foreach (var kv in clean)
                if (reports.All(r => r.Code != kv.Value)) throw new ContentException(root.Sub("clean_fallback") + "." + kv.Key + ": '" + kv.Value + "' has no approved report");
            foreach (var r in root.Arr("refs")) if (!(r is string)) throw new ContentException(root.Sub("refs") + ": expected register ids");
            root.Finish();
            return new ImagingTables(categories, tests, clean, findings, reports, caseStudies);
        }
    }
}
