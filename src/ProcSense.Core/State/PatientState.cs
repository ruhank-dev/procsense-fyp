using System;
using System.Collections.Generic;
using ProcSense.Core.Content;

namespace ProcSense.Core.State
{
    /// <summary>What the chart says about the patient: fixed for the whole case.</summary>
    public sealed record Profile(Identity Identity, Wristband? Wristband, NotesIdentifiers Notes, IReadOnlyList<Allergy> Allergies);

    /// <summary>One examination finding the trainee has found: the case's finding id, the region or check it came from, and the wording.</summary>
    public sealed record ExamFinding(string Id, string Region, string Text);

    /// <summary>
    /// What Step 5 has recorded: the ASA grade the trainee proposed (and how it compared with the case), the scores they calculated, the anaesthetist's lines so far, the agreed plan,
    /// the clot and bleeding answers and the readiness decision.
    /// </summary>
    public sealed class RiskState
    {
        public int? ProposedAsa { get; set; }
        public bool ProposedEmergency { get; set; }
        /// <summary>credit, minor or major once a grade is proposed against a case that states one; "" otherwise.</summary>
        public string AsaClass { get; set; } = "";
        public SortedDictionary<string, string> Scores { get; } = new SortedDictionary<string, string>(StringComparer.Ordinal);
        public List<string> Prompts { get; } = new List<string>();
        public List<string> AnaesthetistLines { get; } = new List<string>();
        public string Plan { get; set; } = "";
        /// <summary>plan_required, no_plan or "" while the form is not done.</summary>
        public string Vte { get; set; } = "";
        public bool BleedingRisk { get; set; }
        public string Decision { get; set; } = "";
        public bool Blocked { get; set; }
    }

    /// <summary>One NEWS2 worked out from the observations the trainee had gathered at that moment (Step is the sim step). Kept for the chart, the EMR and the debrief.</summary>
    public sealed record News2Record(long Step, int Total, bool Single3, string Tier, string Frequency, string Responder, string Note);

    /// <summary>What the trainee sees of a transfer: its state (REQUESTED, DELAYED, PREPARING, IN_TRANSIT, ARRIVED, DONE, RETURNING, BACK, CANCELLED), where to, how, with whom, and when it arrives.</summary>
    public sealed record TransportView(string State, string To, string Mode, IReadOnlyList<string> Escort, string Monitoring, bool Oxygen, string Arrives, string Message);

    /// <summary>
    /// One result the trainee can read: the analyte, the value as text, the unit, the normal range, the flag (normal, high, low, abnormal...), the lab's comment, the sample-quality artefact if any
    /// (haemolysed, drip_arm, wrong_tube_edta, mislabelled) and where it came from (case = stated by the case, generated = supplied normal, artefact = changed by how the sample was taken).
    /// </summary>
    public sealed record ResultRecord(string Id, string Analyte, string Value, string Unit, string Range, string Flag, string Comment, string Quality, string Origin, string Previous = "");

    /// <summary>
    /// The report an imaging test (ultrasound, CT, MRI, MRCP, chest X-ray, ECG) shows: the study, the finding code and its category (kept by the engine, never shown), the plain meaning, the report text, the measurements
    /// stated in the picture, the approved picture it belongs to, and where it came from (case = the finding the case states, clean = a film or strip with no abnormality).
    /// </summary>
    public sealed record ImagingReport(string Study, string Code, string Category, string Plain, string ReportId, string ImageId, string Text, IReadOnlyDictionary<string, string> Measurements, bool Synthetic, string Origin);

    /// <summary>One value the lab phones: the analyte, the number, and how it is said ("Potassium 6.6 mmol/L").</summary>
    public sealed record CriticalValue(string Analyte, double Value, string Spoken);

    /// <summary>A critical-result phone call: RINGING, ANSWERED, IDENTITY_CONFIRMED (the patient's name and date of birth confirmed with the caller), READ_BACK (every value read back exactly), DONE (acknowledged with an action and a note).</summary>
    public sealed class CriticalCall
    {
        public int Id { get; set; }
        public string Test { get; set; } = "";
        public string State { get; set; } = "";
        public string Message { get; set; } = "";
        public long RangAtStep { get; set; }
        public int ReadBackAttempts { get; set; }
        public string Action { get; set; } = "";
        public string Note { get; set; } = "";
        public List<CriticalValue> Values { get; } = new List<CriticalValue>();
    }

    /// <summary>
    /// One test the trainee ordered (the Nth time for that test): its state (REQUESTED, PROCESSING, RESULT_AVAILABLE, VIEWED, ACKNOWLEDGED), what the indication rule said at the time (APPROPRIATE or NOT_INDICATED),
    /// how the sample was taken (tube, arm, label) for the result generator, the case results it will hold, and when it is ready.
    /// </summary>
    public sealed class InvestigationOrder
    {
        public int Id { get; set; }
        public string Test { get; set; } = "";
        public string Label { get; set; } = "";
        public int Instance { get; set; }
        public string State { get; set; } = "";
        public string Kind { get; set; } = "";
        public string Location { get; set; } = "";
        public long OrderedStep { get; set; }
        public long ReadyStep { get; set; }
        public string Indication { get; set; } = "";
        public string Reason { get; set; } = "";
        public string Urgency { get; set; } = "";
        public bool Delayed { get; set; }
        public SortedDictionary<string, string> Sample { get; } = new SortedDictionary<string, string>(StringComparer.Ordinal);
        public List<string> ResultIds { get; set; } = new List<string>();

        /// <summary>The results once the test is available (empty while processing, when rejected, or when the case states none).</summary>
        public List<ResultRecord> Results { get; } = new List<ResultRecord>();

        /// <summary>False when the sample was rejected or belonged to someone else: a repeat then returns the first set of results, not the repeat set.</summary>
        public bool Valid { get; set; } = true;

        /// <summary>Why there is nothing to read: the lab rejected the sample, or the case states no result for this test.</summary>
        public string Note { get; set; } = "";

        /// <summary>The report for an imaging test, once it is available (null for other tests, or when nothing is shown).</summary>
        public ImagingReport? Report { get; set; }

        /// <summary>What the trainee said the result means (normal, abnormal or artefact; for imaging a category), or "".</summary>
        public string Interpretation { get; set; } = "";
    }

    /// <summary>What the trainee has found out and where the patient is.</summary>
    public sealed class ClinicalLayer
    {
        private readonly SortedSet<string> _revealed = new SortedSet<string>(StringComparer.Ordinal);

        public ClinicalLayer(string location) { Location = location; }

        public bool IdentityVerified { get; set; }
        public bool AllergiesKnown { get; set; }
        public bool WristbandRead { get; set; }
        public bool NotesOpened { get; set; }

        /// <summary>The band on the patient's wrist right now (null = none). The nurse can replace or apply it.</summary>
        public Wristband? CurrentWristband { get; set; }
        public bool AllergyBand { get; set; }

        /// <summary>Last nurse step for the wristband ("" until a nurse was involved, then "DONE").</summary>
        public string NurseState { get; set; } = "";

        /// <summary>Where the nurse is during her wristband task: "" until she is called, "corridor" (outside) when called, then the patient's place.</summary>
        public string NurseLocation { get; set; } = "";
        public string Location { get; set; }

        /// <summary>Where the trainee is. It is the patient's place until a transfer: a trainee who does not accompany stays behind, and actions aimed at the patient are then refused.</summary>
        public string TraineeLocation { get; set; } = PatientFactory.PatientBay;

        /// <summary>The transfer in progress or the last one, or null when there has been none.</summary>
        public TransportView? Transport { get; set; }

        private readonly List<string> _revealOrder = new List<string>();

        /// <summary>Topics the trainee has asked about, in ordinal order. Nothing starts revealed.</summary>
        public IReadOnlyCollection<string> RevealedTopics => _revealed;

        /// <summary>The same topics in the order they were revealed (the clerking note reads in this order).</summary>
        public IReadOnlyList<string> RevealOrder => _revealOrder;

        /// <summary>Exact clock times the patient gave for last food and last drink (null until asked).</summary>
        public TimeSpan? LastFoodAt { get; set; }
        public TimeSpan? LastDrinkAt { get; set; }

        public bool IsRevealed(string topic) => _revealed.Contains(topic);

        /// <summary>Returns true the first time a topic is revealed.</summary>
        public bool Reveal(string topic)
        {
            if (!_revealed.Add(topic)) return false;
            _revealOrder.Add(topic);
            return true;
        }

        private readonly List<ExamFinding> _findings = new List<ExamFinding>();
        private readonly SortedDictionary<string, string> _airway = new SortedDictionary<string, string>(StringComparer.Ordinal);

        /// <summary>Examination findings the trainee has found, oldest first (the findings list on the tablet).</summary>
        public IReadOnlyList<ExamFinding> Findings => _findings;

        /// <summary>Airway checks the trainee has made (mouth_opening, mallampati, neck, thyromental) with what they showed; kept for the anaesthetic assessment and Sign In.</summary>
        public IReadOnlyDictionary<string, string> Airway => _airway;

        /// <summary>How many region findings (not airway checks) and airway checks this case has to find. Set once by the examination subsystem; rules read them to know when examining is complete.</summary>
        public int ExamTotal { get; set; }
        public int AirwayTotal { get; set; }

        /// <summary>Returns true the first time a finding is found.</summary>
        public bool AddFinding(ExamFinding f)
        {
            foreach (var x in _findings) if (x.Id == f.Id) return false;
            _findings.Add(f);
            return true;
        }

        public void SetAirway(string item, string text) => _airway[item] = text;

        /// <summary>The fasting status once both the last food and the last drink are known (null until then), and the clock time it will be safe ("" when it already is).</summary>
        public ProcSense.Core.Clinical.FastingResult? Fasting { get; set; }
        public string FastingSafeFrom { get; set; } = "";

        private readonly SortedSet<string> _flags = new SortedSet<string>(StringComparer.Ordinal);

        /// <summary>Named facts about what has happened ("introduced", "risk.wrong_blood"...). Rules set and read them.</summary>
        public IReadOnlyCollection<string> Flags => _flags;

        public bool HasFlag(string flag) => _flags.Contains(flag);

        public void SetFlag(string flag) => _flags.Add(flag);

        public void ClearFlag(string flag) => _flags.Remove(flag);

        /// <summary>Critical-result phone calls, oldest first.</summary>
        public List<CriticalCall> CriticalCalls { get; } = new List<CriticalCall>();

        /// <summary>Every test ordered, oldest first.</summary>
        public List<InvestigationOrder> Investigations { get; } = new List<InvestigationOrder>();

        /// <summary>The NEWS2 scores so far, oldest first (clinical.news2[]).</summary>
        public List<News2Record> News2 { get; } = new List<News2Record>();

        /// <summary>Step 5: the risk assessment, anaesthetic review and readiness decision.</summary>
        public RiskState Risk { get; } = new RiskState();
    }

    /// <summary>Current vital signs. They start at the case baseline; physiology over time arrives in a later phase.</summary>
    /// <summary>A one-off reading the trainee took (blood pressure, temperature...), with the sim step it was taken at.</summary>
    public sealed record Measurement(string Value, long AtStep);

    public sealed class PhysiologicalLayer
    {
        private readonly SortedSet<string> _attached = new SortedSet<string>(StringComparer.Ordinal);
        private readonly SortedDictionary<string, Measurement> _measured = new SortedDictionary<string, Measurement>(StringComparer.Ordinal);

        public PhysiologicalLayer(Vitals vitals) { Vitals = vitals; }

        public Vitals Vitals { get; set; }

        /// <summary>Sensors currently on the patient. A live sensor (the SpO2 probe) shows values only while attached.</summary>
        public IReadOnlyCollection<string> AttachedSensors => _attached;

        public bool IsAttached(string sensor) => _attached.Contains(sensor);

        public bool Attach(string sensor) => _attached.Add(sensor);

        public bool Detach(string sensor) => _attached.Remove(sensor);

        /// <summary>One-off readings by name; a new reading replaces the old one.</summary>
        public IReadOnlyDictionary<string, Measurement> Measured => _measured;

        public void Record(string name, string value, long atStep) => _measured[name] = new Measurement(value, atStep);
    }

    /// <summary>How the patient feels about the trainee. Plain counters for now; the values that move them come from content later.</summary>
    public sealed class BehaviouralLayer
    {
        public int Trust { get; set; }
        public int Anxiety { get; set; }
        public int Understanding { get; set; }
    }

    /// <summary>The four layers of the patient model. Built from a case card by PatientFactory; all values come from content.</summary>
    public sealed class PatientState
    {
        public PatientState(Profile profile, IReadOnlyList<HistoryFact> history, ClinicalLayer clinical, PhysiologicalLayer physiological, BehaviouralLayer behavioural)
        {
            Profile = profile;
            History = history;
            Clinical = clinical;
            Physiological = physiological;
            Behavioural = behavioural;
        }

        public Profile Profile { get; }
        public IReadOnlyList<HistoryFact> History { get; }
        public ClinicalLayer Clinical { get; }
        public PhysiologicalLayer Physiological { get; }
        public BehaviouralLayer Behavioural { get; }
    }

    public static class PatientFactory
    {
        public const string PatientBay = "patient_bay";

        public static PatientState Create(CaseCard card) => new PatientState(
            new Profile(card.Identity, card.Wristband, card.Notes, card.Allergies),
            card.History,
            new ClinicalLayer(PatientBay) { CurrentWristband = card.Wristband, AllergyBand = card.Wristband != null && card.Allergies.Count > 0 },
            new PhysiologicalLayer(card.BaselineVitals),
            new BehaviouralLayer());
    }
}
