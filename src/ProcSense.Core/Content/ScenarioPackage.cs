using System;
using System.Collections.Generic;
using System.IO;

namespace ProcSense.Core.Content
{
    /// <summary>
    /// A generated practice scenario as the backend delivers it (server/schemas/scenario_package.json, p1-live-scenario-generation-s10): the case file inside it is played like any other, through the same loader and
    /// the same review-status gate. The package reads as a content folder that holds one case file, put in front of the real content, so everything else (tables, rules, workflow) is the real content. A package
    /// whose status is not GENERATED_PRACTICE_ONLY, or whose case says otherwise, is refused: nothing in the package can raise its own status.
    /// </summary>
    public sealed class ScenarioPackageReader : IContentReader
    {
        private readonly string _caseJson;

        public string CaseId { get; }
        public string? Spine { get; }
        /// <summary>SHA-256 of the package text (line endings normalised): the value a host gives the session as its package hash.</summary>
        public string PackageHash { get; }

        public ScenarioPackageReader(string packageText)
        {
            PackageHash = Logging.JsonText.Sha256Hex(packageText.Replace("\r\n", "\n"));
            var root = new JsonObj(MiniJson.Parse(packageText), "package");
            if (root.Int("version") != 1) throw new ContentException("package.version: only version 1 is supported");
            if (root.Str("status") != ContentGate.GeneratedPracticeOnly) throw new ContentException("package.status: a delivered package is " + ContentGate.GeneratedPracticeOnly);
            var caseNode = root.Any("case") ?? throw new ContentException("package.case: missing");
            var c = new JsonObj(caseNode, "package.case");
            CaseId = c.Str("id");
            if (c.Str("status") != ContentGate.GeneratedPracticeOnly) throw new ContentException("package.case.status: the case in a package is " + ContentGate.GeneratedPracticeOnly);
            if (c.Str("created_by") != "composer") throw new ContentException("package.case.created_by: a package holds a composed case");
            Spine = c.Obj("composition").Str("spine");
            _caseJson = MiniJson.Write(caseNode);
        }

        public string ReadText(string relativePath)
        {
            if (relativePath == "case_files/" + CaseId + ".json") return _caseJson;
            throw new FileNotFoundException("not in the package: " + relativePath);
        }
    }
}
