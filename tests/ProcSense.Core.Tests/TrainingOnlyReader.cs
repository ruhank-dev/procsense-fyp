using ProcSense.Core.Content;

namespace ProcSense.Core.Tests
{
    /// <summary>The real content with one case file lowered to APPROVED_FOR_TRAINING in memory only (every playable case is now approved for assessment, P3-SCN-07), to test what a training-only case does in the examination.</summary>
    internal sealed class TrainingOnlyReader : IContentReader
    {
        private readonly IContentReader _inner;
        private readonly string _caseId;
        public TrainingOnlyReader(IContentReader inner, string caseId) { _inner = inner; _caseId = caseId; }
        public string ReadText(string path) => path == "case_files/" + _caseId + ".json" ? _inner.ReadText(path).Replace("\"status\": \"APPROVED_FOR_ASSESSMENT\"", "\"status\": \"APPROVED_FOR_TRAINING\"") : _inner.ReadText(path);
    }
}
