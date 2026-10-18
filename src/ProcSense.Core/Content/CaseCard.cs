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
        Wristband? Wristband, NotesIdentifiers Notes, IReadOnlyList<Allergy> Allergies, string AllergyAnswer,
        string ReasonAnswer, ResponsibleAdult ResponsibleAdult, Timing Timing, IReadOnlyList<HistoryFact> History,
        Vitals BaselineVitals, IReadOnlyDictionary<string, string> Refs);

    public sealed record NibpTiming(double CycleSecondsMin, double CycleSecondsMax, string Ref);

    /// <summary>How a trainee behaviour moves the patient's trust and anxiety counters.</summary>
    public sealed record BehaviourEffect(long Trust, long Anxiety, string Ref);

    /// <summary>What examining does to the patient: without permission (trust and the patient's words), the painful region first (pain, trust, words), and a positive Murphy's sign (words).</summary>
    public sealed record ExamEffects(long NoPermissionTrust, string NoPermissionReaction, long PainfulPain, long PainfulTrust, string PainfulReaction, string MurphyReaction);

    /// <summary>Summarising after MinTopics topics have been asked raises trust and understanding.</summary>
    public sealed record SummariseEffect(long MinTopics, long Trust, long Understanding, string Ref);

    /// <summary>The history topics a trainee can ask about, and which of them are sensitive.</summary>
    public sealed record HistoryTopics(IReadOnlyList<string> Ids, IReadOnlyList<string> Sensitive, string Ref);

    /// <summary>Everything loaded from content/, immutable.</summary>
    public sealed class ContentPackage
    {
        public ContentPackage(IReadOnlyDictionary<string, CaseCard> cases, NibpTiming nibp, BehaviourEffect introduceSelf, SummariseEffect summarise, HistoryTopics historyTopics, Workflow.CaseGraph workflow, IReadOnlyDictionary<string, CaseFile>? caseFiles = null, ProcSense.Core.Workflow.FlowMaps? flowMaps = null, ExamEffects? exam = null, ProcSense.Core.Clinical.Scores.ScoreTables? scores = null, ProcSense.Core.Clinical.FastingRules? fasting = null, ProcSense.Core.Clinical.RiskTables? risk = null, ProcSense.Core.Clinical.News2Table? news2 = null, ProcSense.Core.Location.LocationTables? locations = null, ProcSense.Core.Clinical.InvestigationTables? investigations = null, ProcSense.Core.Clinical.ImagingTables? imaging = null, ProcSense.Core.Clinical.ProblemTables? problems = null, ProcSense.Core.Clinical.TreatmentTables? treatments = null, ProcSense.Core.Clinical.SepsisTables? sepsis = null, ProcSense.Core.Clinical.ClashTables? clashes = null, ProcSense.Core.Clinical.DecisionTables? decision = null, ProcSense.Core.Physiology.DeteriorationTables? deterioration = null, ProcSense.Core.Workflow.Steps.WardTables? ward = null, ProcSense.Core.Workflow.OrTables? orRequirements = null, ProcSense.Core.Workflow.Steps.HandoverTables? handover = null, ProcSense.Core.Workflow.Steps.SafetyChecklistTables? checklist = null, ProcSense.Core.Workflow.EndStateTables? endStates = null, ProcSense.Core.Physiology.PhysioTables? physio = null, ProcSense.Core.Behaviour.BehaviourTables? behaviour = null, ProcSense.Core.Workflow.Steps.ConsentTables? consent = null, ProcSense.Core.Events.EventTables? events = null, ProcSense.Core.Scoring.ScoreTables? scoring = null, ProcSense.Core.Difficulty.DifficultyTables? difficulty = null, ProcSense.Core.AIContext.AiTables? ai = null, ProcSense.Core.Records.RecordTables? records = null, ProcSense.Core.Diagnostics.DiagnosticsTables? diagnostics = null, ProcSense.Core.Workflow.Steps.HandoverTables? recoveryHandover = null, ProcSense.Core.Workflow.Steps.IntraOpTables? intraOp = null, ProcSense.Core.Physiology.PulseTraceTables? pulseTraces = null)
        {
            Cases = cases;
            Nibp = nibp;
            IntroduceSelf = introduceSelf;
            Summarise = summarise;
            HistoryTopics = historyTopics;
            Workflow = workflow;
            CaseFiles = caseFiles ?? new Dictionary<string, CaseFile>();
            FlowMaps = flowMaps ?? ProcSense.Core.Workflow.FlowMaps.Empty;
            Exam = exam ?? new ExamEffects(0, "", 0, 0, "", "");
            Scores = scores ?? ProcSense.Core.Clinical.Scores.ScoreTables.Empty;
            Fasting = fasting ?? ProcSense.Core.Clinical.FastingRules.Empty;
            Risk = risk ?? ProcSense.Core.Clinical.RiskTables.Empty;
            News2 = news2 ?? ProcSense.Core.Clinical.News2Table.Empty;
            Locations = locations ?? ProcSense.Core.Location.LocationTables.Empty;
            Investigations = investigations ?? ProcSense.Core.Clinical.InvestigationTables.Empty;
            Imaging = imaging ?? ProcSense.Core.Clinical.ImagingTables.Empty;
            Problems = problems ?? ProcSense.Core.Clinical.ProblemTables.Empty;
            Treatments = treatments ?? ProcSense.Core.Clinical.TreatmentTables.Empty;
            Sepsis = sepsis ?? ProcSense.Core.Clinical.SepsisTables.Empty;
            Clashes = clashes ?? ProcSense.Core.Clinical.ClashTables.Empty;
            Decision = decision ?? ProcSense.Core.Clinical.DecisionTables.Empty;
            Deterioration = deterioration ?? ProcSense.Core.Physiology.DeteriorationTables.Empty;
            Ward = ward ?? ProcSense.Core.Workflow.Steps.WardTables.Empty;
            Or = orRequirements ?? ProcSense.Core.Workflow.OrTables.Empty;
            Handover = handover ?? ProcSense.Core.Workflow.Steps.HandoverTables.Empty;
            Checklist = checklist ?? ProcSense.Core.Workflow.Steps.SafetyChecklistTables.Empty;
            EndStates = endStates ?? ProcSense.Core.Workflow.EndStateTables.Empty;
            Physio = physio ?? ProcSense.Core.Physiology.PhysioTables.Empty;
            Behaviour = behaviour ?? ProcSense.Core.Behaviour.BehaviourTables.Empty;
            Consent = consent ?? ProcSense.Core.Workflow.Steps.ConsentTables.Empty;
            Events = events ?? ProcSense.Core.Events.EventTables.Empty;
            Scoring = scoring ?? ProcSense.Core.Scoring.ScoreTables.Empty;
            Difficulty = difficulty ?? ProcSense.Core.Difficulty.DifficultyTables.Empty;
            Ai = ai ?? ProcSense.Core.AIContext.AiTables.Empty;
            Records = records ?? ProcSense.Core.Records.RecordTables.Empty;
            Diagnostics = diagnostics ?? ProcSense.Core.Diagnostics.DiagnosticsTables.Empty;
            RecoveryHandover = recoveryHandover ?? ProcSense.Core.Workflow.Steps.HandoverTables.Empty;
            IntraOp = intraOp ?? ProcSense.Core.Workflow.Steps.IntraOpTables.Empty;
            PulseTraces = pulseTraces ?? ProcSense.Core.Physiology.PulseTraceTables.Empty;
        }

        public IReadOnlyDictionary<string, CaseCard> Cases { get; }
        public NibpTiming Nibp { get; }
        public BehaviourEffect IntroduceSelf { get; }
        public SummariseEffect Summarise { get; }
        public HistoryTopics HistoryTopics { get; }
        /// <summary>Goals, decision points and readiness blockers (content/workflow/workflow.json).</summary>
        public Workflow.CaseGraph Workflow { get; }
        /// <summary>The scenario master maps: 20 stages for S1 and for S2 (content/flow/flow_maps.json).</summary>
        public ProcSense.Core.Workflow.FlowMaps FlowMaps { get; }
        /// <summary>What examining does to the patient (content/behaviour.json).</summary>
        public ExamEffects Exam { get; }
        /// <summary>The score tables the clinical calculators read (content/scores/scores.json).</summary>
        public ProcSense.Core.Clinical.Scores.ScoreTables Scores { get; }
        /// <summary>The fasting hours by kind of intake (content/fasting/fasting.json).</summary>
        public ProcSense.Core.Clinical.FastingRules Fasting { get; }
        /// <summary>Step 5 content: the risk tables and the anaesthetist's approved lines (content/risk, content/npc).</summary>
        public ProcSense.Core.Clinical.RiskTables Risk { get; }
        /// <summary>The NEWS2 chart and response table (content/news2/news2.json).</summary>
        public ProcSense.Core.Clinical.News2Table News2 { get; }
        /// <summary>The places, transfer timing and escort rule (content/locations/locations.json).</summary>
        public ProcSense.Core.Location.LocationTables Locations { get; }
        /// <summary>The tests, tubes, turnaround defaults and indication kinds (content/investigations).</summary>
        public ProcSense.Core.Clinical.InvestigationTables Investigations { get; }
        /// <summary>What each imaging test can show: finding categories, approved reports and each case's studies (content/imaging/imaging.json).</summary>
        public ProcSense.Core.Clinical.ImagingTables Imaging { get; }
        /// <summary>The six problem types, the 7-step ladder and the priorities (content/problems/problems.json).</summary>
        public ProcSense.Core.Clinical.ProblemTables Problems { get; }
        /// <summary>NICE NG253 sepsis tiers, their timers and the cases with an infection (content/sepsis/sepsis.json).</summary>
        public ProcSense.Core.Clinical.SepsisTables Sepsis { get; }
        /// <summary>The treatment clash table, how a case's factors are read and the perioperative medicine plans (content/clashes/clashes.json).</summary>
        public ProcSense.Core.Clinical.ClashTables Clashes { get; }
        /// <summary>The five outcomes and the ordered checks of the master decision (content/decision/decision.json).</summary>
        public ProcSense.Core.Clinical.DecisionTables Decision { get; }
        /// <summary>The deterioration clock and the timelines of cases that have one (content/physiology/deterioration.json).</summary>
        public ProcSense.Core.Physiology.DeteriorationTables Deterioration { get; }
        /// <summary>The ward / ED checklist of Step 10 (content/ward/ward_checklist.json).</summary>
        public ProcSense.Core.Workflow.Steps.WardTables Ward { get; }
        /// <summary>The theatre stations of Step 11, who checks them and the cases with a fault (content/or/or_requirements.json).</summary>
        public ProcSense.Core.Workflow.OrTables Or { get; }
        /// <summary>The SBAR items, which each scenario must include and how an omission is scored (content/handover/handover_required.json).</summary>
        public ProcSense.Core.Workflow.Steps.HandoverTables Handover { get; }
        /// <summary>The WHO safety checklist items and the rule that works out each expected answer (content/checklist/safety_checklist.json).</summary>
        public ProcSense.Core.Workflow.Steps.SafetyChecklistTables Checklist { get; }
        /// <summary>The five end states, what counts as an avoidable delay and the facts that surface later (content/endstate/end_states.json).</summary>
        public ProcSense.Core.Workflow.EndStateTables EndStates { get; }
        /// <summary>The LitePhysio model: variables, couplings, flags and noise (content/physio).</summary>
        public ProcSense.Core.Physiology.PhysioTables Physio { get; }
        /// <summary>The patient behaviour model: attributes, transitions, modes, capacity and the disclosure gate (content/behaviour).</summary>
        public ProcSense.Core.Behaviour.BehaviourTables Behaviour { get; }
        /// <summary>What the consent conversation must cover, the patient's questions and the consent rules (content/consent/consent.json).</summary>
        public ProcSense.Core.Workflow.Steps.ConsentTables Consent { get; }
        /// <summary>The approved events the Event Director can fire, with its settings (content/events/events.json).</summary>
        public ProcSense.Core.Events.EventTables Events { get; }
        /// <summary>The rubric, the 22 assessed behaviours with their evidence, and the debrief sentences (content/scoring, content/assessment, content/debrief).</summary>
        public ProcSense.Core.Scoring.ScoreTables Scoring { get; }
        /// <summary>The ratings settings, the 8 difficulty dimensions and the standard order (content/difficulty/dimensions.json).</summary>
        public ProcSense.Core.Difficulty.DifficultyTables Difficulty { get; }
        /// <summary>The intents each AI-voiced character may use and the lexicon the grounding validator checks against (content/ai).</summary>
        public ProcSense.Core.AIContext.AiTables Ai { get; }

        /// <summary>The patient record: document types, current documents, each case's hospital record and the distractor patients (content/records/records.json).</summary>
        public ProcSense.Core.Records.RecordTables Records { get; }

        /// <summary>How samples go to the laboratory and the nurse's prompt about tubes left at the bedside (content/diagnostics/diagnostics.json).</summary>
        public ProcSense.Core.Diagnostics.DiagnosticsTables Diagnostics { get; }

        /// <summary>The handover to the recovery nurse after Sign Out (empty when the package has no extension content).</summary>
        public ProcSense.Core.Workflow.Steps.HandoverTables RecoveryHandover { get; }

        /// <summary>The intra-operative training events of the ProcSense educational extension (empty when the package has none).</summary>
        public ProcSense.Core.Workflow.Steps.IntraOpTables IntraOp { get; }

        /// <summary>Recorded Pulse runs a session may replay instead of LitePhysio (content/physio/pulse_traces); none when the package has no such folder.</summary>
        public ProcSense.Core.Physiology.PulseTraceTables PulseTraces { get; }
        /// <summary>The treatment library: each treatment with its required checks, prerequisites, restrictions and effect ids (content/treatments.json).</summary>
        public ProcSense.Core.Clinical.TreatmentTables Treatments { get; }
        /// <summary>The playable case files that were asked for (content/case_files), by id; empty when none were loaded.</summary>
        public IReadOnlyDictionary<string, CaseFile> CaseFiles { get; }
    }
}
