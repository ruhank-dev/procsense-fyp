using System;
using System.Collections.Generic;
using ProcSense.Core.Content;

namespace ProcSense.Core.State
{
    /// <summary>What the chart says about the patient: fixed for the whole case.</summary>
    public sealed record Profile(Identity Identity, Wristband Wristband, NotesIdentifiers Notes, IReadOnlyList<Allergy> Allergies);

    /// <summary>What the trainee has found out and where the patient is.</summary>
    public sealed class ClinicalLayer
    {
        private readonly SortedSet<string> _revealed = new SortedSet<string>(StringComparer.Ordinal);

        public ClinicalLayer(string location) { Location = location; }

        public bool IdentityVerified { get; set; }
        public bool AllergiesKnown { get; set; }
        public bool WristbandRead { get; set; }
        public bool NotesOpened { get; set; }
        public string Location { get; set; }

        /// <summary>History topics the trainee has asked about, in ordinal order. Nothing starts revealed.</summary>
        public IReadOnlyCollection<string> RevealedTopics => _revealed;

        public bool IsRevealed(string topic) => _revealed.Contains(topic);

        /// <summary>Returns true the first time a topic is revealed.</summary>
        public bool Reveal(string topic) => _revealed.Add(topic);

        private readonly SortedSet<string> _flags = new SortedSet<string>(StringComparer.Ordinal);

        /// <summary>Named facts about what has happened ("introduced", "risk.wrong_blood"...). Rules set and read them.</summary>
        public IReadOnlyCollection<string> Flags => _flags;

        public bool HasFlag(string flag) => _flags.Contains(flag);

        public void SetFlag(string flag) => _flags.Add(flag);
    }

    /// <summary>Current vital signs. They start at the case baseline; physiology over time arrives in a later phase.</summary>
    public sealed class PhysiologicalLayer
    {
        public PhysiologicalLayer(Vitals vitals) { Vitals = vitals; }

        public Vitals Vitals { get; set; }
    }

    /// <summary>How the patient feels about the trainee. Plain counters for now; the values that move them come from content later.</summary>
    public sealed class BehaviouralLayer
    {
        public int Trust { get; set; }
        public int Anxiety { get; set; }
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
            new ClinicalLayer(PatientBay),
            new PhysiologicalLayer(card.BaselineVitals),
            new BehaviouralLayer());
    }
}
