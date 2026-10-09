using System;
using System.Collections.Generic;

// Lets C# 9 records compile on netstandard2.1 (Unity 6 supports this).
namespace System.Runtime.CompilerServices
{
    internal static class IsExternalInit { }
}

namespace ProcSense.Core.Content
{
    public sealed record Identity(string Title, string FullName, long Age, string Occupation, string Dob, string Mrn);
    public sealed record Wristband(string NameText, string DobText, string MrnText);
    public sealed record NotesIdentifiers(string FullName, string Dob, string Mrn);
    public sealed record Allergy(string Substance, string Reaction);
    public sealed record ResponsibleAdult(string Who, bool TakesHome, bool StaysOvernight, string Text, string Wording);
    public sealed record Timing(TimeSpan TimeNow, TimeSpan SurgeryTime);
    public sealed record HistoryFact(string Topic, string Answer, string Wording, bool Volunteered, string? ClockTime);

    public sealed record Vitals(
        double HeartRate, string HeartRhythm, double SystolicBp, double DiastolicBp, double RespiratoryRate,
        double Spo2, string Oxygen, double TemperatureC, string Acvpu, long PainScore);

    /// <summary>One patient case exactly as authored in content/cases. Refs keep the source-register IDs for the debrief.</summary>
    public sealed record CaseCard(
        string Id, string WalkthroughLabel, string Scenario, Identity Identity, string SpokenIdentityAnswer,
        Wristband Wristband, NotesIdentifiers Notes, IReadOnlyList<Allergy> Allergies, string AllergyAnswer,
        string ReasonAnswer, ResponsibleAdult ResponsibleAdult, Timing Timing, IReadOnlyList<HistoryFact> History,
        Vitals BaselineVitals, IReadOnlyDictionary<string, string> Refs);

    public sealed record NibpTiming(double CycleSecondsMin, double CycleSecondsMax, string Ref);

    /// <summary>Everything loaded from content/, immutable.</summary>
    public sealed class ContentPackage
    {
        public ContentPackage(IReadOnlyDictionary<string, CaseCard> cases, NibpTiming nibp)
        {
            Cases = cases;
            Nibp = nibp;
        }

        public IReadOnlyDictionary<string, CaseCard> Cases { get; }
        public NibpTiming Nibp { get; }
    }
}
