using System.Collections.Generic;
using ProcSense.Core.Actions;
using ProcSense.Core.Workflow;
using ProcSense.Core.Workflow.Steps;

namespace ProcSense.Core.Tests
{
    /// <summary>Holding a home medicine (DT-208): only a medicine the patient has told about, once.</summary>
    public class HomeMedicineTests
    {
        private static Session Verified()
        {
            var s = new Session(TestCase.Card(), 42, SessionMode.Practice, Subsystems.Default(TestCase.Package()));
            foreach (var v in new[] { "ask_identity", "read_wristband", "open_notes", "confirm_identity" }) s.Apply(new SimAction(v));
            return s;
        }

        private static ActionOutcome Hold(Session s, string? drug) =>
            s.Apply(new SimAction("hold_medicine", "patient", Channel.Cli, drug == null ? new Dictionary<string, string>() : new Dictionary<string, string> { ["drug"] = drug }));

        private static void AskMedicines(Session s) =>
            s.Apply(new SimAction("ask", "patient", Channel.Cli, new Dictionary<string, string> { ["topic"] = "medicines" }));

        [Fact]
        public void Needs_a_confirmed_identity()
        {
            var s = new Session(TestCase.Card(), 42, SessionMode.Practice, Subsystems.Default(TestCase.Package()));
            Assert.Equal("IDENTITY_NOT_CONFIRMED", Hold(s, "paracetamol").RejectReason);
        }

        [Fact]
        public void A_medicine_not_yet_asked_about_cannot_be_held()
        {
            var s = Verified();
            Assert.Equal(HomeMedicineSubsystem.MedicinesNotAsked, Hold(s, "paracetamol").RejectReason);
        }

        [Fact]
        public void A_missing_drug_is_refused()
        {
            var s = Verified(); AskMedicines(s);
            Assert.Equal(HomeMedicineSubsystem.MissingDrug, Hold(s, null).RejectReason);
        }

        [Fact]
        public void A_drug_the_patient_did_not_name_is_refused()
        {
            var s = Verified(); AskMedicines(s);                       // "None except paracetamol sometimes."
            Assert.Equal(HomeMedicineSubsystem.NotOnList, Hold(s, "warfarin").RejectReason);
            Assert.Equal(HomeMedicineSubsystem.NotOnList, Hold(s, "para").RejectReason);     // a whole word, not a fragment
        }

        [Fact]
        public void The_named_drug_is_held_once_and_recorded()
        {
            var s = Verified(); AskMedicines(s);
            var first = Hold(s, " Paracetamol ");
            Assert.True(first.Accepted);
            Assert.Contains("medicine_held:paracetamol", first.Effects);
            Assert.True(s.Patient.Clinical.HasFlag("held_medicine:paracetamol"));
            Assert.Equal(HomeMedicineSubsystem.AlreadyHeld, Hold(s, "paracetamol").RejectReason);
        }
    }
}
