using System.Collections.Generic;
using System.Linq;
using ProcSense.Core.Actions;
using ProcSense.Core.Content;
using ProcSense.Core.Twin;
using ProcSense.Core.Workflow;

namespace ProcSense.Core.Tests
{
    /// <summary>The patient twin (p2-ai-advanced-simulation-s02, E48): what a patient carries into the next encounter, that the carried state replays, and that a session without a twin is unchanged.</summary>
    public class PatientTwinTests
    {
        private static readonly ContentPackage Content = TestCase.Package();

        private static SimAction Act(string verb, params (string k, string v)[] p) => new SimAction(verb, "patient", Channel.Cli, p.ToDictionary(x => x.k, x => x.v));

        private static ActionOutcome Do(Session s, string verb, params (string k, string v)[] p) => s.Apply(Act(verb, p));

        private static Session New(CaseCard card, ulong seed = 42) => new Session(card, seed, SessionMode.Practice, Subsystems.Default(Content));

        // an encounter in which the patient is identified, asks about the allergies, is given paracetamol and an hour passes
        private static Session FirstEncounter()
        {
            var s = New(Content.Cases["VAR-S1-A"]);
            foreach (var verb in new[] { "ask_identity", "read_wristband", "open_notes", "confirm_identity", "ask_allergies" }) Assert.True(Do(s, verb).Accepted, verb);
            Assert.True(Do(s, "request_medication", ("class", "TR-PARACETAMOL"), ("indication", "pain")).Accepted);
            s.Tick(130);
            Assert.Equal("ADMINISTERED", s.Patient.Clinical.Orders[0].State);
            s.Tick(3600);
            return s;
        }

        [Fact]
        public void The_snapshot_is_what_the_session_ended_with()
        {
            var s = FirstEncounter();
            var t = PatientTwin.Capture(s);
            Assert.Equal("VAR-S1-A", t.CaseId);
            Assert.Equal(s.Clock.ClockTime.ToString(@"hh\:mm\:ss"), t.ClockTime);
            Assert.Equal(s.Patient.Physiological.Vitals, t.Vitals);
            Assert.Equal(new[] { "TR-PARACETAMOL" }, t.Treatments);
            Assert.Contains("treatment.TR-PARACETAMOL.given", t.Flags);
        }

        [Fact]
        public void The_snapshot_reads_back_to_the_same_snapshot_and_the_same_hash()
        {
            var t = PatientTwin.Capture(FirstEncounter());
            var back = TwinSnapshot.FromJson(t.ToJson());
            Assert.Equal(t.ToJson(), back.ToJson());
            Assert.Equal(t.Hash(), back.Hash());
            Assert.Equal(64, t.Hash().Length);
        }

        [Fact]
        public void A_snapshot_of_another_case_an_unknown_field_and_a_newer_version_are_refused()
        {
            var t = PatientTwin.Capture(FirstEncounter());
            var other = Content.Cases.Values.First(c => c.Id != "VAR-S1-A");
            Assert.Throws<ContentException>(() => PatientTwin.FollowUp(other, t));
            Assert.Throws<ContentException>(() => TwinSnapshot.FromJson(t.ToJson().Replace("\"flags\":", "\"surprise\":1,\"flags\":")));
            Assert.Throws<ContentException>(() => TwinSnapshot.FromJson(t.ToJson().Replace("\"version\":1", "\"version\":2")));
        }

        [Fact]
        public void The_next_encounter_starts_from_the_carried_clock_vitals_flags_and_behaviour()
        {
            var first = FirstEncounter();
            var t = PatientTwin.Capture(first);
            var second = New(PatientTwin.FollowUp(Content.Cases["VAR-S1-A"], t), seed: 7);
            PatientTwin.Apply(second, t);

            Assert.Equal(first.Clock.ClockTime, second.Clock.ClockTime);
            Assert.Equal(first.Patient.Physiological.Vitals, second.Patient.Physiological.Vitals);
            Assert.Equal(first.Patient.Behavioural.Trust, second.Patient.Behavioural.Trust);
            Assert.True(second.Patient.Clinical.HasFlag(PatientTwin.CarriedFlag));
            Assert.True(second.Patient.Clinical.HasFlag(PatientTwin.PriorTreatmentPrefix + "TR-PARACETAMOL"));
            Assert.Equal(first.Behaviour!.State.Values.OrderBy(k => k.Key).ToList(), second.Behaviour!.State.Values.OrderBy(k => k.Key).ToList());
            // what is not carried: the encounter's own workflow progress
            Assert.False(second.Patient.Clinical.IdentityVerified);
            Assert.Empty(second.Patient.Clinical.Orders);
        }

        [Fact]
        public void The_carried_state_replays_to_the_same_snapshot()
        {
            var t = PatientTwin.Capture(FirstEncounter());
            var second = New(PatientTwin.FollowUp(Content.Cases["VAR-S1-A"], t), seed: 7);
            PatientTwin.Apply(second, t);
            Assert.Equal(t.ToJson(), PatientTwin.Capture(second).ToJson());                    // captured again before anything happens, it is the same twin
        }

        [Fact]
        public void The_same_snapshot_and_the_same_actions_give_the_same_log_and_a_fresh_patient_gives_another()
        {
            var t = PatientTwin.Capture(FirstEncounter());
            (string log, string state) Run(CaseCard card, bool twin)
            {
                var s = New(card, seed: 11);
                if (twin) PatientTwin.Apply(s, t);
                s.Tick(120);
                Do(s, "ask_identity");
                s.Tick(300);
                return (s.Log.Hash(), PatientTwin.Capture(s).Hash());
            }
            var followUp = PatientTwin.FollowUp(Content.Cases["VAR-S1-A"], t);
            Assert.Equal(Run(followUp, true), Run(followUp, true));
            Assert.NotEqual(Run(followUp, true).state, Run(Content.Cases["VAR-S1-A"], false).state);       // the carried patient is not the fresh one
        }

        [Fact]
        public void A_twin_cannot_be_applied_after_the_first_action_and_a_session_without_one_is_unchanged()
        {
            var t = PatientTwin.Capture(FirstEncounter());
            var s = New(PatientTwin.FollowUp(Content.Cases["VAR-S1-A"], t));
            Do(s, "ask_identity");
            Assert.Throws<ContentException>(() => PatientTwin.Apply(s, t));
            var plain = New(Content.Cases["VAR-S1-A"]);
            Assert.False(plain.Patient.Clinical.HasFlag(PatientTwin.CarriedFlag));
            Assert.Equal(Content.Cases["VAR-S1-A"].BaselineVitals, plain.Patient.Physiological.Vitals);
        }
    }
}
