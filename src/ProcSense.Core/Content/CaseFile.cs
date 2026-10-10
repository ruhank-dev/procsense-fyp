using System;
using System.Collections.Generic;

namespace ProcSense.Core.Content
{
    // The playable case file (content/case_files/<id>.json, schema content/schema/case_file.schema.json). It is the full case: history with
    // when each fact is disclosed, observations over time, examination, results, scores, ranked problems, decisions, end state, paths and the debrief.
    // The Steps 1 to 3 card (CaseCard) stays as it is; a case file is read next to it, never instead of it.

    public sealed record CaseFileIdentity(string Title, string FullName, string Sex, long Age, string Occupation, string Dob, string Mrn);
    public sealed record CaseFilePresentation(string Setting, TimeSpan TimeNow, TimeSpan? SurgeryTime, string Summary);
    public sealed record CaseFileHistory(string Id, string Topic, string Answer, bool Volunteered, IReadOnlyList<string> DiscloseOn, string Origin, string? Variant);

    public sealed record CaseFileObservation(
        TimeSpan Time, string Condition, double HeartRate, string HeartRhythm, double SystolicBp, double DiastolicBp, double RespiratoryRate, double Spo2, string Oxygen,
        double TemperatureC, string Acvpu, long PainScore, long News2, string Note, string Origin, string? Variant);

    public sealed record CaseFileExamination(string Id, string System, string Finding, bool Abnormal, IReadOnlyList<string> DiscloseOn, string Origin, bool Painful = false, IReadOnlyList<string>? Flags = null);
    public sealed record PreviousResult(string Value, string When);

    public sealed record CaseFileResult(
        string Id, string Test, string Value, string Unit, string NormalRange, string Flag, string Comment, string? Quality, PreviousResult? Previous, long MinutesToResult,
        string Origin, string? SourceRow, string? Variant);

    public sealed record CaseFileScore(string Name, string Value, string Note, string Origin);
    public sealed record CaseFileProblem(string Id, string Type, string Label, long Rank, string Why, IReadOnlyList<string> ExpectedActions);
    public sealed record CaseFileOption(string Id, string Text, bool Correct, string Consequence);
    public sealed record CaseFileDecision(string Id, string Prompt, string? Rule, IReadOnlyList<CaseFileOption> Options, string? Variant);
    public sealed record IncorrectPath(string Mistake, string Shows, string Risk, string Recovery);
    public sealed record CaseFilePaths(IReadOnlyList<string> Correct, IReadOnlyList<IncorrectPath> Incorrect, IReadOnlyList<string> Missed, IReadOnlyList<string> Recovery);

    public sealed record CaseFileDebrief(
        IReadOnlyList<string> Recognised, IReadOnlyList<string> Missed, string Priority, string WhyPriority, string Related, string Independent, string TriggerEscalation, string IfIgnored, string Remember);

    public sealed record CaseFileApproval(string By, string Date, string Note);
    public sealed record CaseFileSources(string Story, string Authored, IReadOnlyList<string> Measurements);

    /// <summary>One playable case exactly as authored in content/case_files, immutable. Every item says whether the book states it or it was authored.</summary>
    public sealed record CaseFile(
        string Id, string WalkthroughLabel, string Scenario, string Tier, string Status, CaseFileApproval? Approval, string ReferenceDate, string? BaseCard,
        CaseFileIdentity Identity, string SpokenIdentityAnswer, Wristband? Wristband, IReadOnlyList<Allergy> Allergies, string AllergyAnswer, string ReasonAnswer,
        CaseFilePresentation Presentation, IReadOnlyList<CaseFileHistory> History, IReadOnlyList<CaseFileObservation> Observations, IReadOnlyList<CaseFileExamination> Examination,
        IReadOnlyList<CaseFileResult> Results, IReadOnlyList<CaseFileScore> Scores, IReadOnlyList<CaseFileProblem> Problems, IReadOnlyList<CaseFileDecision> Decisions,
        string ExpectedEndState, IReadOnlyDictionary<string, string> VariantEndStates, CaseFilePaths Paths, CaseFileDebrief Debrief, IReadOnlyList<string> Verification, CaseFileSources Sources);
}
