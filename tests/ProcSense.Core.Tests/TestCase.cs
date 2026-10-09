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
