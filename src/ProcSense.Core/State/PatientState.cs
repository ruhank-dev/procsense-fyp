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
        public string Location { get; set; }

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
