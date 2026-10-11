using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;
using ProcSense.Core.Actions;
using ProcSense.Core.Clinical;
using ProcSense.Core.Content;
using ProcSense.Core.Workflow;

namespace ProcSense.Core.Tests
{
    /// <summary>
    /// The medicine workflow (p0-clinical-logic-s11): the trainee requests, a clinician confirms, a nurse gives. Tests T-TR2, T-TR3 and T-K8 of the Implementation Guide, plus the delays, the dose policy,
    /// the IV rule, the nurse's challenge in practice mode and the critical item in assessment mode.
    /// </summary>
    public class MedicineWorkflowTests
    {
        private static readonly ContentPackage Content = TestCase.Package();
        private static readonly TreatmentTables Tables = Content.Treatments;

        private static SimAction Act(string verb, params (string k, string v)[] p) => new SimAction(verb, "patient", Channel.Cli, p.ToDictionary(x => x.k, x => x.v));

        private static ActionOutcome Do(Session s, string verb, params (string k, string v)[] p) => s.Apply(Act(verb, p));

        /// <summary>VAR-S1-A's card with the allergies replaced (the card itself has none).</summary>
        private static CaseCard CardWithAllergy(string substance, string reaction)
        {
            var node = JsonNode.Parse(File.ReadAllText(Path.Combine(TestCase.ContentRoot, "cases", "VAR-S1-A.json")))!.AsObject();
            node["allergies"] = new JsonArray(new JsonObject { ["substance"] = substance, ["reaction"] = reaction });
            node["allergy_answer"] = substance + ", " + reaction;
            return ContentLoader.LoadCase(node.ToJsonString(), "VAR-S1-A with an allergy");
        }

        /// <summary>A session with the patient's identity confirmed. <paramref name="askAllergies"/> establishes the allergies, as the walkthrough's Step 1 does.</summary>
        private static Session Start(SessionMode mode = SessionMode.Practice, CaseCard? card = null, bool askAllergies = true, SessionOptions? options = null)
        {
            var s = new Session(card ?? Content.Cases["VAR-S1-A"], 42, mode, Subsystems.Default(Content), options);
            foreach (var verb in new[] { "ask_identity", "read_wristband", "open_notes", "confirm_identity" }) Assert.True(Do(s, verb).Accepted, verb);
            if (askAllergies) Assert.True(Do(s, "ask_allergies").Accepted);
            return s;
        }

        private static Observation.ObservedOrder Order(Session s, int index = 0) => s.Observable.Orders![index];

        private static void Wait(Session s, double seconds) => s.Tick(seconds);

        private static (string, string) Treat(string id) => ("class", id);

        // ---------------------------------------------------------------- T-K8: there is no way for the trainee to give anything

        [Fact] // T-K8
        public void The_trainee_has_no_verb_to_administer_prescribe_or_play_the_clinician_or_nurse()
        {
            var s = Start();
            foreach (var verb in new[] { "administer", "give", "give_medication", "prescribe", "npc_confirm", "npc_administer", "npc_challenge", "confirm_order" })
            {
                var o = Do(s, verb, Treat("TR-PARACETAMOL"));
                Assert.False(o.Accepted, verb);
                Assert.Equal("UNKNOWN_VERB", o.RejectReason);
            }
            Assert.Empty(s.Patient.Clinical.Orders);   // none of them did anything
        }

        [Fact]
        public void A_request_before_identity_is_confirmed_is_refused()
        {
            var s = new Session(Content.Cases["VAR-S1-A"], 42, SessionMode.Practice, Subsystems.Default(Content));

            var o = Do(s, "request_medication", Treat("TR-PARACETAMOL"));

            Assert.Equal("IDENTITY_NOT_CONFIRMED", o.RejectReason);
        }

        // ---------------------------------------------------------------- T-TR2: request, confirm, give

        [Fact] // T-TR2
        public void A_request_is_confirmed_by_the_clinician_then_given_by_the_nurse_and_the_effects_are_published()
        {
            var s = Start();
            var given = new List<TreatmentGiven>();
            s.Events.Subscribe<TreatmentGiven>(given.Add);

            var request = Do(s, "request_medication", Treat("TR-PARACETAMOL"), ("indication", "pain"));
            Assert.Contains("waiting_for:clinician", request.Effects);
            Assert.Equal("REQUESTED", Order(s).State);
            Assert.Equal("", Order(s).Dose);   // nothing is said about a dose until the clinician confirms

            Wait(s, 59);
            Assert.Equal("REQUESTED", Order(s).State);
            Wait(s, 1);
            Assert.Equal("CONFIRMED", Order(s).State);
            Assert.Equal("I confirm the order for Paracetamol.", Order(s).Lines.Single());

            Wait(s, 60);
            Assert.Equal("ADMINISTERED", Order(s).State);
            Assert.Equal("Paracetamol has been given.", Order(s).Lines.Last());
            Assert.Equal(new[] { "EFF-ANALGESIA" }, Assert.Single(given).Effects);
            Assert.Contains("treatment.TR-PARACETAMOL.given", s.Patient.Clinical.Flags);
            Assert.Empty(s.Patient.Clinical.Orders[0].Items);   // identity, allergies, reason, no restriction, confirmed: every check passed
        }

        [Fact]
        public void One_long_tick_and_many_short_ones_give_the_same_orders_and_the_same_hash()
        {
            string Run(Action<Session> advance)
            {
                var s = Start();
                Do(s, "request_medication", Treat("TR-PARACETAMOL"), ("indication", "pain"));
                advance(s);
                return s.Observable.Hash() + "|" + string.Join(",", s.MedicineHistory.Select(e => e.Kind + "@" + e.Step));
            }

            string longTick = Run(s => s.Tick(180));
            string shortTicks = Run(s => { for (int i = 0; i < 360; i++) s.Tick(0.5); });
            string skipped = Run(s => s.TimeSkip(3));

            Assert.Equal(longTick, shortTicks);
            Assert.Equal(longTick, skipped);
        }

        [Fact]
        public void The_history_has_the_request_the_confirmation_and_the_giving_at_the_steps_they_were_due()
        {
            var s = Start();
            long start = s.Clock.Steps;
            Do(s, "request_medication", Treat("TR-PARACETAMOL"), ("indication", "pain"));
            Wait(s, 180);

            Assert.Equal(new[] { "requested", "confirmed", "administered" }, s.MedicineHistory.Select(e => e.Kind));
            Assert.Equal(new[] { start, start + 120, start + 240 }, s.MedicineHistory.Select(e => e.Step));
        }

        [Fact]
        public void The_same_treatment_cannot_be_requested_twice_while_one_is_waiting_but_can_be_again_once_given()
        {
            var s = Start();
            Do(s, "request_medication", Treat("TR-PARACETAMOL"));

            Assert.Equal("ORDER_PENDING", Do(s, "request_medication", Treat("TR-PARACETAMOL")).RejectReason);
            Wait(s, 150);
            Assert.True(Do(s, "request_medication", Treat("TR-PARACETAMOL")).Accepted);
            Assert.Equal(2, s.Observable.Orders!.Count);
        }

        [Fact]
        public void A_request_names_a_known_treatment_of_the_right_kind()
        {
            var s = Start();

            Assert.Equal("MISSING_TREATMENT", Do(s, "request_medication").RejectReason);
            Assert.Equal("UNKNOWN_TREATMENT", Do(s, "request_medication", Treat("TR-MAGIC")).RejectReason);
            Assert.Equal("WRONG_KIND", Do(s, "request_medication", Treat("TR-IV-FLUIDS")).RejectReason);
            Assert.Equal("WRONG_KIND", Do(s, "request_fluids", ("type", "TR-PARACETAMOL")).RejectReason);
            Assert.Empty(s.Patient.Clinical.Orders);
        }

        [Fact]
        public void A_request_with_no_reason_is_given_but_leaves_a_major_item()
        {
            var s = Start();
            Do(s, "request_medication", Treat("TR-PARACETAMOL"));
            Wait(s, 150);

            Assert.Equal("ADMINISTERED", Order(s).State);
            var given = s.MedicineHistory.Single(e => e.Kind == "administered");
            Assert.Equal(new[] { "major.treatment_indication" }, given.ScoreItems);
        }

        // ---------------------------------------------------------------- dose policy: strictly by dose_policy

        [Fact]
        public void An_educational_example_shows_its_label_the_text_and_the_source_only_after_the_clinician_confirms()
        {
            var s = Start();
            Do(s, "request_medication", Treat("TR-PARACETAMOL"), ("indication", "pain"));
            Wait(s, 60);
            var def = Tables.ById["TR-PARACETAMOL"];

            string dose = Order(s).Dose;

            Assert.StartsWith(Tables.DoseLabels["EDUCATIONAL_EXAMPLE"], dose, StringComparison.Ordinal);
            Assert.Contains(def.Dose!.Text, dose);
            Assert.Contains("(" + def.Dose.Source + ")", dose);
        }

        [Fact]
        public void A_patient_specific_treatment_shows_that_the_clinician_decides_and_never_a_number()
        {
            var s = Start();
            Do(s, "request_medication", Treat("TR-NSAID"), ("indication", "pain"));
            Wait(s, 60);

            string dose = Order(s).Dose;

            Assert.Equal(Tables.DoseLabels["PATIENT_SPECIFIC"] + ": the clinician decides", dose);
            Assert.DoesNotContain(dose, c => char.IsDigit(c));
        }

        [Fact]
        public void No_confirmed_line_of_a_patient_specific_or_institutional_treatment_contains_a_digit()
        {
            foreach (var def in Tables.Treatments.Where(t => t.Kind == "medicine" && t.DosePolicy != "EDUCATIONAL_EXAMPLE"))
            {
                var s = Start();
                s.Patient.Clinical.SetFlag("iv_access.in_place");   // the IV rule has its own tests
                Assert.True(Do(s, "request_medication", Treat(def.Id), ("indication", "x")).Accepted, def.Id);
                Wait(s, 60);
                var order = Order(s);
                Assert.DoesNotContain(order.Dose, c => char.IsDigit(c));
                Assert.DoesNotContain(string.Join(" ", order.Lines), c => char.IsDigit(c));
            }
        }

        // ---------------------------------------------------------------- IV access, fluids, oxygen

        [Fact]
        public void A_fluid_request_without_IV_access_is_refused_and_works_once_the_IV_is_in()
        {
            var s = Start();

            Assert.Equal("IV_ACCESS_REQUIRED", Do(s, "request_fluids", ("type", "TR-IV-FLUIDS")).RejectReason);
            Assert.Empty(s.Patient.Clinical.Orders);

            Assert.True(Do(s, "request_iv_access").Accepted);
            Wait(s, 150);
            Assert.Equal("ADMINISTERED", Order(s).State);
            Assert.Contains("iv_access.in_place", s.Patient.Clinical.Flags);
            Assert.Equal("The IV access is in.", Order(s).Lines.Last());

            Assert.True(Do(s, "request_fluids", ("type", "TR-IV-FLUIDS"), ("indication", "dehydration")).Accepted);
            Assert.Equal("ALREADY_IN_PLACE", Do(s, "request_iv_access").RejectReason);
        }

        [Fact]
        public void An_IV_medicine_is_refused_without_IV_access_and_a_non_IV_medicine_is_not()
        {
            var s = Start();

            Assert.Equal("IV_ACCESS_REQUIRED", Do(s, "request_medication", Treat("TR-OPIOID")).RejectReason);
            Assert.True(Do(s, "request_medication", Treat("TR-PARACETAMOL")).Accepted);
        }

        [Fact]
        public void Oxygen_is_requested_with_one_of_the_known_devices()
        {
            var s = Start();

            Assert.Equal("MISSING_DEVICE", Do(s, "request_oxygen").RejectReason);
            Assert.Equal("UNKNOWN_DEVICE", Do(s, "request_oxygen", ("device", "jet_ventilator")).RejectReason);
            Assert.True(Do(s, "request_oxygen", ("device", "non_rebreather_mask"), ("indication", "low oxygen")).Accepted);
            Wait(s, 150);
            Assert.Equal("non_rebreather_mask", Order(s).Device);
            Assert.Equal("ADMINISTERED", Order(s).State);
            Assert.Equal(new[] { "nasal_cannula", "simple_mask", "non_rebreather_mask" }, Tables.OxygenDevices);
        }

        [Fact]
        public void The_nurse_waits_while_the_patient_is_in_another_place()
        {
            var s = Start();
            Do(s, "request_medication", Treat("TR-PARACETAMOL"), ("indication", "pain"));
            Wait(s, 60);
            s.Patient.Clinical.Location = "ct";

            Wait(s, 300);
            Assert.Equal("CONFIRMED", Order(s).State);

            s.Patient.Clinical.Location = ProcSense.Core.State.PatientFactory.PatientBay;
            Wait(s, 1);
            Assert.Equal("ADMINISTERED", Order(s).State);
        }

        // ---------------------------------------------------------------- the checks the nurse judges

        [Fact]
        public void With_allergies_not_established_practice_has_the_nurse_ask_and_not_give()
        {
            var s = Start(SessionMode.Practice, askAllergies: false);
            var given = new List<TreatmentGiven>();
            s.Events.Subscribe<TreatmentGiven>(given.Add);
            Do(s, "request_medication", Treat("TR-PARACETAMOL"), ("indication", "pain"));

            Wait(s, 150);

            Assert.Equal("CHALLENGED", Order(s).State);
            Assert.Equal("Before I give this, can you confirm the patient's allergy status?", Order(s).Lines.Last());
            Assert.Empty(given);
            Assert.Equal(new[] { "challenged.treatment_allergy_known" }, s.MedicineHistory.Single(e => e.Kind == "challenged").ScoreItems);
        }

        [Fact]
        public void With_allergies_not_established_assessment_gives_it_and_the_item_is_critical()
        {
            var s = Start(SessionMode.Assessment, askAllergies: false);
            Do(s, "request_medication", Treat("TR-PARACETAMOL"), ("indication", "pain"));

            Wait(s, 150);

            Assert.Equal("ADMINISTERED", Order(s).State);
            Assert.Equal(new[] { "critical.treatment_allergy_known" }, s.MedicineHistory.Single(e => e.Kind == "administered").ScoreItems);
        }

        [Fact]
        public void A_missing_timing_check_is_an_item_and_checking_it_first_removes_the_item()
        {
            var s = Start();
            Do(s, "request_medication", Treat("TR-ABX-S2"), ("indication", "appendicitis"));
            Wait(s, 150);
            Assert.Equal(new[] { "major.treatment_timing" }, s.MedicineHistory.Single(e => e.Kind == "administered").ScoreItems);

            var t = Start();
            Assert.True(Do(t, "check_treatment_timing", ("treatment", "TR-ABX-S2")).Accepted);
            Do(t, "request_medication", Treat("TR-ABX-S2"), ("indication", "appendicitis"));
            Wait(t, 150);
            Assert.Empty(t.MedicineHistory.Single(e => e.Kind == "administered").ScoreItems);
        }

        [Fact]
        public void A_restriction_that_applies_is_challenged_in_practice_and_critical_in_assessment()
        {
            var practice = Start(SessionMode.Practice);
            practice.Patient.Clinical.SetFlag("condition.kidney_injury");
            Do(practice, "request_medication", Treat("TR-NSAID"), ("indication", "pain"));
            Wait(practice, 150);
            Assert.Equal("CHALLENGED", Order(practice).State);
            Assert.Equal("Before I give this, I am worried about kidney injury. Please ask the clinician to review the order.", Order(practice).Lines.Last());

            var assessment = Start(SessionMode.Assessment);
            assessment.Patient.Clinical.SetFlag("condition.kidney_injury");
            Do(assessment, "request_medication", Treat("TR-NSAID"), ("indication", "pain"));
            Wait(assessment, 150);
            Assert.Equal("ADMINISTERED", Order(assessment).State);
            Assert.Equal(new[] { "critical.treatment_restriction.kidney_injury" }, assessment.MedicineHistory.Single(e => e.Kind == "administered").ScoreItems);
        }

        [Fact]
        public void A_nurse_challenge_stops_a_time_skip_so_the_trainee_sees_it()
        {
            var s = Start(SessionMode.Practice, askAllergies: false);
            Do(s, "request_medication", Treat("TR-PARACETAMOL"), ("indication", "pain"));

            var skip = s.TimeSkip(30);

            Assert.Contains("stopped:nurse_challenge", skip.Effects);
            Assert.Equal("CHALLENGED", Order(s).State);
        }

        // ---------------------------------------------------------------- T-TR3: the allergen

        private static CaseCard Anaphylaxis() => CardWithAllergy("Penicillin", "Throat swelling and collapse in 2019 (anaphylaxis)");

        [Fact] // T-TR3 (beginner)
        public void An_antibiotic_for_a_patient_allergic_to_penicillin_is_challenged_by_the_nurse_in_practice_mode()
        {
            var s = Start(SessionMode.Practice, Anaphylaxis());
            var given = new List<TreatmentGiven>();
            var events = new List<WorkflowEvent>();
            s.Events.Subscribe<TreatmentGiven>(given.Add);
            s.Events.Subscribe<WorkflowEvent>(events.Add);
            Do(s, "check_treatment_timing", ("treatment", "TR-ABX-S2"));
            Do(s, "request_medication", Treat("TR-ABX-S2"), ("indication", "appendicitis"));

            Wait(s, 150);

            Assert.Equal("CHALLENGED", Order(s).State);
            Assert.Equal("Before I give this, I have to stop: the chart records an allergy to Penicillin: Throat swelling and collapse in 2019 (anaphylaxis). Please ask the clinician to review the order.", Order(s).Lines.Last());
            Assert.Empty(given);
            Assert.Empty(events);
            Assert.DoesNotContain("allergy.reaction_eligible", s.Patient.Clinical.Flags);
        }

        [Fact] // T-TR3 (advanced level in practice mode, case-library s06: the consequence plays out)
        public void An_advanced_trainee_in_practice_mode_gets_the_consequence_not_the_nurses_challenge()
        {
            var s = Start(SessionMode.Practice, Anaphylaxis(), options: new SessionOptions(TrainingLevel.Advanced));
            Do(s, "check_treatment_timing", ("treatment", "TR-ABX-S2"));
            Do(s, "request_medication", Treat("TR-ABX-S2"), ("indication", "appendicitis"));
            Wait(s, 150);
            Assert.Equal("ADMINISTERED", Order(s).State);
            Assert.Contains("allergy.reaction_eligible", s.Patient.Clinical.Flags);
        }

        [Fact] // T-TR3 (advanced)
        public void The_same_request_in_assessment_mode_is_given_as_a_critical_item_and_the_reaction_becomes_eligible()
        {
            var s = Start(SessionMode.Assessment, Anaphylaxis());
            var given = new List<TreatmentGiven>();
            var events = new List<WorkflowEvent>();
            s.Events.Subscribe<TreatmentGiven>(given.Add);
            s.Events.Subscribe<WorkflowEvent>(events.Add);
            Do(s, "check_treatment_timing", ("treatment", "TR-ABX-S2"));
            Do(s, "request_medication", Treat("TR-ABX-S2"), ("indication", "appendicitis"));

            Wait(s, 150);

            Assert.Equal("ADMINISTERED", Order(s).State);
            Assert.Single(given);
            Assert.Equal("E-HYPERSENSITIVITY", Assert.Single(events).Name);
            Assert.Contains("allergy.reaction_eligible", s.Patient.Clinical.Flags);
            var item = s.MedicineHistory.Single(e => e.Kind == "administered").ScoreItems;
            Assert.Equal(new[] { "critical.treatment_allergy_conflict.antibiotic" }, item);
            Assert.Contains(s.MedicineHistory, e => e.Kind == "allergen_given");
        }

        [Fact]
        public void An_allergy_to_antibiotics_is_judged_on_the_chart_even_when_the_trainee_never_asked()
        {
            var s = Start(SessionMode.Assessment, Anaphylaxis(), askAllergies: false);
            Do(s, "request_medication", Treat("TR-ABX-S2"), ("indication", "appendicitis"));

            Wait(s, 150);

            var items = s.MedicineHistory.Single(e => e.Kind == "administered").ScoreItems;
            Assert.Contains("critical.treatment_allergy_conflict.antibiotic", items);
            Assert.Contains("critical.treatment_allergy_known", items);
        }

        [Fact]
        public void A_mild_old_rash_is_not_a_conflict_but_the_clinician_notes_it()
        {
            var s = Start(SessionMode.Practice, CardWithAllergy("Penicillin", "Mild rash as a child (delayed)"));
            Do(s, "check_treatment_timing", ("treatment", "TR-ABX-S2"));
            Do(s, "request_medication", Treat("TR-ABX-S2"), ("indication", "appendicitis"));

            Wait(s, 150);

            Assert.Equal("ADMINISTERED", Order(s).State);
            Assert.Contains("I have noted the allergy on record: Penicillin: Mild rash as a child (delayed). I will take that into account.", Order(s).Lines);
            Assert.Empty(s.MedicineHistory.Single(e => e.Kind == "administered").ScoreItems);
        }

        [Fact]
        public void A_medicine_outside_the_allergy_group_is_not_a_conflict()
        {
            var s = Start(SessionMode.Assessment, Anaphylaxis());
            Do(s, "request_medication", Treat("TR-PARACETAMOL"), ("indication", "pain"));

            Wait(s, 150);

            Assert.Equal("ADMINISTERED", Order(s).State);
            Assert.Empty(s.MedicineHistory.Single(e => e.Kind == "administered").ScoreItems);
            Assert.DoesNotContain("allergy.reaction_eligible", s.Patient.Clinical.Flags);
        }

        // ---------------------------------------------------------------- documentation

        [Fact]
        public void Documentation_needs_a_treatment_that_was_given_and_an_undocumented_one_is_a_minor_item_at_readiness_once()
        {
            var s = Start();
            Assert.Equal("NOT_GIVEN", Do(s, "document_treatment", ("treatment", "TR-PARACETAMOL")).RejectReason);
            Do(s, "request_medication", Treat("TR-PARACETAMOL"), ("indication", "pain"));
            Wait(s, 150);
            Assert.Equal("NOT_GIVEN", Do(s, "document_treatment", ("treatment", "TR-NSAID")).RejectReason);

            Do(s, "decide_ready", ("option", "proceed"));
            Do(s, "decide_ready", ("option", "proceed"));

            var missing = s.MedicineHistory.Where(e => e.Kind == "not_documented").ToList();
            Assert.Equal(new[] { "minor.treatment_documentation" }, Assert.Single(missing).ScoreItems);

            var t = Start();
            Do(t, "request_medication", Treat("TR-PARACETAMOL"), ("indication", "pain"));
            Wait(t, 150);
            Assert.True(Do(t, "document_treatment", ("treatment", "TR-PARACETAMOL")).Accepted);
            Do(t, "decide_ready", ("option", "proceed"));
            Assert.DoesNotContain(t.MedicineHistory, e => e.Kind == "not_documented");
        }

        // ---------------------------------------------------------------- the observable and replay

        [Fact]
        public void A_session_with_no_requests_has_no_orders_in_its_observable_so_existing_hashes_do_not_change()
        {
            var s = Start();

            Assert.DoesNotContain("\"orders\"", s.Observable.ToJson());
            Do(s, "request_medication", Treat("TR-PARACETAMOL"));
            Assert.Contains("\"orders\"", s.Observable.ToJson());
        }

        [Fact]
        public void The_same_seed_and_actions_give_the_same_log_and_state_hash()
        {
            (string, string) Run()
            {
                var s = Start(SessionMode.Assessment, Anaphylaxis());
                Do(s, "request_medication", Treat("TR-ABX-S2"), ("indication", "appendicitis"));
                Do(s, "request_oxygen", ("device", "simple_mask"));
                Wait(s, 200);
                return (s.Log.Hash(), s.Observable.Hash());
            }

            Assert.Equal(Run(), Run());
        }

        // The five real case files that record an allergy: a severe immediate reaction is challenged, a mild old rash is not
        [Theory]
        [InlineData("SC-1", "CHALLENGED")]
        [InlineData("SC-15", "CHALLENGED")]
        [InlineData("STK-S2-B", "CHALLENGED")]
        [InlineData("VAR-S1-I", "CHALLENGED")]
        [InlineData("VAR-S2-K", "ADMINISTERED")]
        public void The_allergies_the_real_case_files_record_are_judged_correctly(string caseId, string expected)
        {
            var file = ContentLoader.LoadCaseFile(new FileContentReader(TestCase.ContentRoot), caseId);
            var allergy = file.Allergies.Single();
            var s = Start(SessionMode.Practice, CardWithAllergy(allergy.Substance, allergy.Reaction));
            Do(s, "check_treatment_timing", ("treatment", "TR-ABX-S2"));
            Do(s, "request_medication", Treat("TR-ABX-S2"), ("indication", "appendicitis"));
            Wait(s, 150);
            Assert.Equal(expected, Order(s).State);
        }
    }
}
