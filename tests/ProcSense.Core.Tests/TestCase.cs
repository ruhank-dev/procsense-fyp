using System;
using System.IO;
using ProcSense.Core.Content;

namespace ProcSense.Core.Tests
{
    /// <summary>Finds the repo's real content/ folder and loads the VAR-S1-A case for tests.</summary>
    internal static class TestCase
    {
        public static readonly string Repo = FindRepoRoot();
        public static readonly string ContentRoot = Path.Combine(Repo, "content");

        /// <summary>The walkthrough's own case files (17 P0, 16 P1, 20 P2). A new walkthrough case would change this on purpose.</summary>
        public const int BookCases = 53;

        /// <summary>The combination cases of DT-220, listed by content/combination_cases.json (written by tools/content/build_case_files.py): the number of case files is BookCases plus this, never a literal.</summary>
        public static readonly System.Collections.Generic.IReadOnlySet<string> CombinationIds = LoadCombinationIds();

        public static int AllCases => BookCases + CombinationIds.Count;

        private static System.Collections.Generic.IReadOnlySet<string> LoadCombinationIds()
        {
            using var doc = System.Text.Json.JsonDocument.Parse(File.ReadAllText(Path.Combine(Repo, "content", "combination_cases.json")));
            var ids = new System.Collections.Generic.HashSet<string>(System.StringComparer.Ordinal);
            foreach (var c in doc.RootElement.GetProperty("cases").EnumerateArray()) ids.Add(c.GetProperty("id").GetString()!);
            return ids;
        }

        /// <summary>VAR-S1-A plus the three test-only wristband fixtures from tests/fixtures.</summary>
        public static ContentPackage Package() => ContentLoader.Load(
            new CompositeContentReader(new FileContentReader(ContentRoot), new FileContentReader(Path.Combine(Repo, "tests", "fixtures"))),
            "VAR-S1-A", "TEST-ID-MISMATCH", "TEST-ID-MISSING-BAND", "TEST-ID-MISSING-BAND-ALLERGY");

        public static CaseCard Card() => Package().Cases["VAR-S1-A"];

        private static string FindRepoRoot()
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null && !File.Exists(Path.Combine(dir.FullName, "ProcSense.sln"))) dir = dir.Parent;
            return dir?.FullName ?? throw new InvalidOperationException("ProcSense.sln not found above the test folder");
        }
    }
}
