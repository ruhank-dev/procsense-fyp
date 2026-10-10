using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ProcSense.Core.Actions;
using ProcSense.Core.Clinical;
using ProcSense.Core.Content;
using ProcSense.Core.Pipeline;
using ProcSense.Core.State;
using ProcSense.Core.Workflow;
using ProcSense.Core.Workflow.Steps;

namespace ProcSense.Core.Tests
{
    /// <summary>Critical-result calls, interpretation and unreviewed results (p0-clinical-logic-s08, plan tests T07-1 to T07-3, T07-WT1 and T07-WT2).</summary>
    public class CriticalCallTests
    {
        private static readonly ContentPackage Content = TestCase.Package();
        private static readonly CriticalTable Table = Content.Investigations.Critical;

        private static CaseFile File_(string id) => ContentLoader.LoadCaseFile(new FileContentReader(TestCase.ContentRoot), id);

        private static SimAction Act(string verb, params (string k, string v)[] p) => new SimAction(verb, "patient", Channel.Cli, p.ToDictionary(x => x.k, x => x.v));

        private static ActionOutcome Do(Session s, string verb, params (string k, string v)[] p) => s.Apply(Act(verb, p));

        private static string Reject(ActionOutcome o) { Assert.False(o.Accepted); return o.RejectReason; }

        private static Session NewSession(string caseFile = "SC-8", CaseFile? withFile = null)
        {
            var file = withFile ?? File_(caseFile);
            var chain = Subsystems.Default(Content).Where(x => !(x is InvestigationsSubsystem) && !(x is ResultReviewSubsystem))
                .Concat(new ISubsystem[] { new InvestigationsSubsystem(Content, file), new ResultReviewSubsystem(Content, file) });
            var s = new Session(Content.Cases["VAR-S1-A"], 42, SessionMode.Practice, chain);
            foreach (var verb in new[] { "ask_identity", "read_wristband", "open_notes", "confirm_identity" }) Assert.True(Do(s, verb).Accepted, verb);
            return s;
        }

        // Orders the potassium test of SC-8 (K 6.6) and lets it finish: the lab phones.
        private static Session Ringing(string caseFile = "SC-8", string test = "u_and_e", params (string, string)[] sample)
        {
            var s = NewSession(caseFile);
            Do(s, "order_investigation", ("test", test));
            Do(s, "take_sample", new[] { ("test", test) }.Concat(sample).ToArray());
            s.Tick(100 * 60);
            return s;
        }

        private static string Id => "Ayesha Khan";
        private static string Dob => "14 March 1984";

        private static CriticalCall Call(Session s) => s.Patient.Clinical.CriticalCalls.Last();

        private static void FullCall(Session s, string test = "u_and_e", string values = "potassium=6.6")
        {
            Assert.True(Do(s, "answer_phone").Accepted);
            Assert.True(Do(s, "confirm_patient_on_call", ("name", Id), ("dob", Dob)).Accepted);
            Assert.Contains("readback:correct", Do(s, "read_back", ("values", values)).Effects);
            Assert.True(Do(s, "view_result", ("test", test)).Accepted);
        }

        // ---------------------------------------------------------------- the limits

        private static ResultRecord R(string analyte, string value, string unit, string previous = "", string quality = "") => new ResultRecord("r", analyte, value, unit, "", "high", "", quality, "case", previous);

        private static IReadOnlyList<CriticalFinding> Hits(ResultRecord r, params string[] facts) => Table.Evaluate(new[] { r }, new HashSet<string>(facts));

        [Theory]
        [InlineData("Potassium", "2.5", "mmol/L", true)]
        [InlineData("Potassium", "2.6", "mmol/L", false)]
        [InlineData("Potassium", "6.5", "mmol/L", true)]
        [InlineData("Potassium", "6.4", "mmol/L", false)]
        [InlineData("Sodium", "120", "mmol/L", true)]
        [InlineData("Sodium", "121", "mmol/L", false)]
        [InlineData("Sodium", "160", "mmol/L", true)]
        [InlineData("Sodium", "159", "mmol/L", false)]
        [InlineData("Capillary glucose", "2.5", "mmol/L", true)]
        [InlineData("Finger-prick glucose", "2.6", "mmol/L", false)]
        [InlineData("Calcium", "1.8", "mmol/L", true)]
        [InlineData("Calcium", "3.5", "mmol/L", true)]
        [InlineData("Calcium", "3.4", "mmol/L", false)]
        [InlineData("Creatinine", "354", "umol/L", true)]
        [InlineData("Creatinine", "353", "umol/L", false)]
        [InlineData("CRP", "300", "mg/L", true)]
        [InlineData("CRP", "299", "mg/L", false)]
        [InlineData("Hb", "69", "g/L", true)]
        [InlineData("Haemoglobin", "70", "g/L", false)]          // below 70: 70 itself is not critical
        [InlineData("Platelets", "29", "x10^9/L", true)]
        [InlineData("Platelets", "30", "x10^9/L", false)]
        [InlineData("Neutrophils", "0.4", "x10^9/L", true)]
        [InlineData("Neutrophils", "0.5", "x10^9/L", false)]
        [InlineData("Lactate", "4.0", "mmol/L", true)]
        [InlineData("Lactate", "3.9", "mmol/L", false)]
        public void Each_limit_catches_exactly_what_the_table_says_including_whether_the_limit_itself_counts(string analyte, string value, string unit, bool critical)
        {
            Assert.Equal(critical, Hits(R(analyte, value, unit)).Count == 1);
        }

        [Fact]
        public void A_fall_in_haemoglobin_of_20_or_more_is_critical_even_when_the_value_is_not_low()
        {
            Assert.Single(Hits(R("Hb", "110", "g/L", previous: "130")));            // a fall of 20
            Assert.Empty(Hits(R("Hb", "111", "g/L", previous: "130")));             // a fall of 19
            Assert.Empty(Hits(R("Hb", "90", "g/L")));                               // no previous value, no fall to judge
        }

        [Fact]
        public void The_INR_limit_depends_on_whether_the_patient_is_on_warfarin()
        {
            Assert.Single(Hits(R("INR", "5.0", "ratio"), "on_warfarin"));
            Assert.Empty(Hits(R("INR", "4.9", "ratio"), "on_warfarin"));
            Assert.Empty(Hits(R("INR", "2.6", "ratio"), "on_warfarin"));            // expected on warfarin
            Assert.Single(Hits(R("INR", "1.6", "ratio")));                          // unexpected: above 1.5 and not on warfarin
            Assert.Empty(Hits(R("INR", "1.5", "ratio")));
        }

        [Fact]
        public void Creatinine_is_only_critical_without_known_kidney_failure()
        {
            Assert.Single(Hits(R("Creatinine", "400", "umol/L")));
            Assert.Empty(Hits(R("Creatinine", "400", "umol/L"), "known_kidney_failure"));
        }

        [Fact]
        public void A_value_in_another_unit_a_mislabelled_result_and_text_are_never_compared()
        {
            Assert.Empty(Hits(R("Neutrophils", "0.4", "%")));                       // a percentage is not a count
            Assert.Empty(Hits(R("Potassium", "9.9", "mmol/L", quality: "mislabelled")));
            Assert.Empty(Hits(R("Potassium", "Normal", "")));
        }

        [Fact]
        public void The_content_has_every_limit_of_the_walkthrough_table()
        {
            Assert.Equal(16, Table.Rules.Count);
            Assert.Equal(6, Table.Actions.Count);
            Assert.All(Table.Rules, r => Assert.Matches("^SR-[0-9]{4}$", r.Ref));
        }

        // every critical flag the case authors wrote is one the engine would also phone about
        [Fact]
        public void Every_critical_flag_in_the_53_case_files_is_caught_by_the_limits()
        {
            int authored = 0;
            foreach (var path in Directory.GetFiles(Path.Combine(TestCase.ContentRoot, "case_files"), "*.json"))
            {
                var f = File_(Path.GetFileNameWithoutExtension(path));
                var records = f.Results.Select(r => new ResultRecord(r.Id, InvestigationTables.BaseName(r.Test), r.Value, r.Unit, r.NormalRange, r.Flag, r.Comment, r.Quality ?? "", "case", r.Previous?.Value ?? ""));
                var caught = Table.Evaluate(records, new HashSet<string>(f.Facts)).Select(x => x.ResultId).ToHashSet();
                foreach (var r in f.Results.Where(x => x.Flag.StartsWith("critical")))
                {
                    authored++;
                    Assert.Contains(r.Id, caught);
                }
            }
            Assert.Equal(2, authored);
        }

        // ---------------------------------------------------------------- T07-1 and T07-2 the call

        [Fact]
        public void When_a_critical_result_arrives_the_phone_rings_silently_and_a_blocker_starts()
        {
            var s = Ringing();
            Assert.Equal("RINGING", s.Observable.Phone.State);
            Assert.Equal("", s.Observable.Phone.Message);                            // nothing is said until it is answered
            Assert.True(s.Patient.Clinical.HasFlag(ResultReviewSubsystem.CriticalFlag));
            Assert.Contains(s.ResultReviewHistory, h => h.Kind == "critical_call_rings");
        }

        [Fact]
        public void Answering_the_phone_gives_the_callers_words_with_the_patients_identifiers_and_the_values()
        {
            var s = Ringing();
            Assert.Contains("call_answered:u_and_e", Do(s, "answer_phone").Effects);
            var phone = s.Observable.Phone;
            Assert.Equal("ANSWERED", phone.State);
            Assert.Equal("This is the laboratory with a critical result for Ayesha Khan, date of birth 14/03/1984: Potassium 6.6 mmol/L.", phone.Message);
        }

        [Fact]
        public void The_call_must_be_done_in_order_answer_then_identity_then_read_back_then_act()
        {
            var s = Ringing();
            Assert.Equal(ResultReviewSubsystem.CallNotAnswered, Reject(Do(s, "confirm_patient_on_call", ("name", Id), ("dob", Dob))));
            Assert.Equal(ResultReviewSubsystem.CallNotAnswered, Reject(Do(s, "read_back", ("values", "potassium=6.6"))));
            Do(s, "answer_phone");
            Assert.Equal(ResultReviewSubsystem.CallIdentityFirst, Reject(Do(s, "read_back", ("values", "potassium=6.6"))));
            Do(s, "confirm_patient_on_call", ("name", Id), ("dob", Dob));
            Assert.Equal("IDENTITY_CONFIRMED", Call(s).State);
            Do(s, "read_back", ("values", "potassium=6.6"));
            Assert.Equal("READ_BACK", Call(s).State);
        }

        [Fact]
        public void The_patients_identity_must_be_confirmed_with_the_caller_and_a_wrong_one_is_refused()
        {
            var s = Ringing();
            Do(s, "answer_phone");
            var wrong = Do(s, "confirm_patient_on_call", ("name", "Hamza Ali"), ("dob", "2 June 2007"));
            Assert.Equal(ResultReviewSubsystem.WrongIdentity, Reject(wrong));
            Assert.Contains("identity_on_call:wrong", wrong.Effects);
            Assert.Equal("ANSWERED", Call(s).State);
            Assert.Equal(ResultReviewSubsystem.WrongIdentity, Reject(Do(s, "confirm_patient_on_call", ("name", Id))));           // a name alone is not enough
            Assert.True(Do(s, "confirm_patient_on_call", ("name", "Ayesha Khan"), ("dob", "14/03/1984")).Accepted);
        }

        [Fact]
        public void The_values_read_back_must_match_exactly_as_numbers()
        {
            var s = Ringing();
            Do(s, "answer_phone"); Do(s, "confirm_patient_on_call", ("name", Id), ("dob", Dob));
            Assert.Contains("readback:wrong", Do(s, "read_back", ("values", "potassium=6.5")).Effects);
            Assert.Contains("readback:wrong", Do(s, "read_back", ("values", "potassium=6.6,sodium=140")).Effects);   // an extra value is not what was phoned
            Assert.Contains("readback:wrong", Do(s, "read_back", ("values", "sodium=6.6")).Effects);
            Assert.Equal(3, Call(s).ReadBackAttempts);
            Assert.Equal("IDENTITY_CONFIRMED", Call(s).State);
            Assert.Equal(ResultReviewSubsystem.MissingValues, Reject(Do(s, "read_back")));
            Assert.Contains("readback:correct", Do(s, "read_back", ("values", "Potassium = 6.60")).Effects);        // 6.60 is 6.6
            Assert.Contains(s.ResultReviewHistory, h => h.Kind == "critical_readback_wrong" && h.ScoreItems.Contains("minor.readback_wrong"));
        }

        [Fact]
        public void The_whole_call_then_an_action_and_a_note_acknowledges_the_result_and_lifts_the_blocker()
        {
            var s = Ringing();
            Do(s, "answer_phone"); Do(s, "confirm_patient_on_call", ("name", Id), ("dob", Dob)); Do(s, "read_back", ("values", "potassium=6.6"));
            Assert.Equal(InvestigationsSubsystem.ViewFirst, Reject(Do(s, "acknowledge_result", ("test", "u_and_e"), ("action", "repeat_sample"), ("note", "Repeat K, ECG."))));   // open it first
            Do(s, "view_result", ("test", "u_and_e"));
            Assert.Equal(ResultReviewSubsystem.MissingAction, Reject(Do(s, "acknowledge_result", ("test", "u_and_e"))));
            Assert.Equal(ResultReviewSubsystem.UnknownAction, Reject(Do(s, "acknowledge_result", ("test", "u_and_e"), ("action", "ignore"), ("note", "x"))));
            Assert.Equal(ResultReviewSubsystem.MissingNote, Reject(Do(s, "acknowledge_result", ("test", "u_and_e"), ("action", "repeat_sample"), ("note", "  "))));
            Assert.True(s.Patient.Clinical.HasFlag(ResultReviewSubsystem.CriticalFlag));

            var done = Do(s, "acknowledge_result", ("test", "u_and_e"), ("action", "repeat_sample"), ("note", "Repeat K, ECG."));
            Assert.Contains("critical_acknowledged:repeat_sample", done.Effects);
            Assert.Contains("acknowledged:u_and_e", done.Effects);
            Assert.Equal("DONE", Call(s).State);
            Assert.Equal(("repeat_sample", "Repeat K, ECG."), (Call(s).Action, Call(s).Note));
            Assert.False(s.Patient.Clinical.HasFlag(ResultReviewSubsystem.CriticalFlag));
            Assert.Equal("", s.Observable.Phone.State);
            Assert.Contains(s.ResultReviewHistory, h => h.Kind == "critical_call_done" && h.ScoreItems.Contains("credit.critical_acknowledged"));
        }

        [Fact]
        public void A_critical_result_cannot_be_acknowledged_until_the_call_is_complete()
        {
            var s = Ringing();
            Do(s, "view_result", ("test", "u_and_e"));                               // the result can be opened without answering the phone
            Assert.Equal(ResultReviewSubsystem.CallNotAnswered, Reject(Do(s, "acknowledge_result", ("test", "u_and_e"), ("action", "repeat_sample"), ("note", "x"))));
            Do(s, "answer_phone");
            Assert.Equal(ResultReviewSubsystem.CallIdentityFirst, Reject(Do(s, "acknowledge_result", ("test", "u_and_e"), ("action", "repeat_sample"), ("note", "x"))));
            Do(s, "confirm_patient_on_call", ("name", Id), ("dob", Dob));
            Assert.Equal(ResultReviewSubsystem.CallReadBackFirst, Reject(Do(s, "acknowledge_result", ("test", "u_and_e"), ("action", "repeat_sample"), ("note", "x"))));
            Assert.Equal("VIEWED", s.Patient.Clinical.Investigations.Single().State);
        }

        [Fact]
        public void Without_a_phone_call_there_is_nothing_to_answer_confirm_or_read_back()
        {
            var s = NewSession();
            Assert.Equal(ResultReviewSubsystem.NoCall, Reject(Do(s, "answer_phone")));
            Assert.Equal(ResultReviewSubsystem.NoCall, Reject(Do(s, "confirm_patient_on_call", ("name", Id), ("dob", Dob))));
            Assert.Equal(ResultReviewSubsystem.NoCall, Reject(Do(s, "read_back", ("values", "potassium=6.6"))));
        }

        // ---------------------------------------------------------------- readiness blockers

        private static (string, string)[] Transfer => new[] { ("mode", "trolley"), ("escort", "porter,nurse") };

        [Fact]
        public void An_unacknowledged_critical_result_blocks_the_transfer_to_theatre_until_it_is_dealt_with()
        {
            var s = Ringing();
            var blocked = Do(s, "transfer_to_theatre", Transfer);
            Assert.Equal("BLOCKED:B-CRITICAL", Reject(blocked));
            Assert.Equal("BLOCKED:B-CRITICAL", s.LastRejection);
            FullCall(s);
            Assert.Equal("BLOCKED:B-CRITICAL", Reject(Do(s, "transfer_to_theatre", Transfer)));             // read back and opened, but not yet acknowledged
            Do(s, "acknowledge_result", ("test", "u_and_e"), ("action", "escalate_senior"), ("note", "Told the registrar."));
            Assert.True(Do(s, "transfer_to_theatre", Transfer).Accepted);
        }

        [Fact]
        public void No_read_back_means_the_blocker_stays_and_the_trainee_cannot_progress()
        {
            var s = Ringing();
            Do(s, "answer_phone"); Do(s, "confirm_patient_on_call", ("name", Id), ("dob", Dob));
            Do(s, "view_result", ("test", "u_and_e"));
            Assert.Equal(ResultReviewSubsystem.CallReadBackFirst, Reject(Do(s, "acknowledge_result", ("test", "u_and_e"), ("action", "repeat_sample"), ("note", "x"))));
            Assert.Equal("BLOCKED:B-CRITICAL", Reject(Do(s, "transfer_to_theatre", Transfer)));
        }

        [Fact]
        public void Deciding_to_proceed_with_an_unacknowledged_critical_result_is_a_critical_item_and_other_decisions_are_not()
        {
            var s = Ringing();
            var proceed = Do(s, "decide_ready", ("option", "proceed"));
            Assert.Contains("critical:proceed_with_unacknowledged_result", proceed.Effects);
            Assert.Contains("score:CRITICAL.ignored_critical_result", proceed.Effects);
            Assert.Contains(s.ResultReviewHistory, h => h.Kind == "proceeded_with_unacknowledged_critical");
            var other = NewSessionRinging();
            Assert.DoesNotContain(Do(other, "decide_ready", ("option", "review")).Effects, e => e.StartsWith("critical:"));
        }

        private static Session NewSessionRinging() => Ringing();

        [Fact]
        public void A_critical_result_a_sampling_mistake_made_still_phones_and_the_trainee_should_repeat_it()
        {
            var s = Ringing("VAR-S1-J", "u_and_e", ("tube", "edta_purple"));       // the potassium becomes 8.9 in the wrong tube
            Assert.Equal("RINGING", s.Observable.Phone.State);
            Do(s, "answer_phone");
            Assert.Contains("Potassium 8.9 mmol/L", s.Observable.Phone.Message);
        }

        // ---------------------------------------------------------------- results never opened

        private static Session Plain() => NewSession("VAR-S1-A");

        [Fact]
        public void A_result_the_case_needed_and_the_trainee_never_opened_is_a_major_item_and_blocks_the_transfer()
        {
            var s = Plain();
            Do(s, "order_investigation", ("test", "lft")); Do(s, "take_sample", ("test", "lft"));
            s.Tick(50 * 60);
            Assert.True(s.Patient.Clinical.HasFlag(ResultReviewSubsystem.UnreviewedFlag));
            Assert.Equal("BLOCKED:B-RESULTS", Reject(Do(s, "transfer_to_theatre", Transfer)));
            var ready = Do(s, "decide_ready", ("option", "proceed"));
            Assert.Contains("major:unreviewed_result:lft", ready.Effects);
            Assert.Contains("score:major.result_not_reviewed", ready.Effects);
            Do(s, "view_result", ("test", "lft"));
            Assert.False(s.Patient.Clinical.HasFlag(ResultReviewSubsystem.UnreviewedFlag));
            Assert.True(Do(s, "transfer_to_theatre", Transfer).Accepted);
        }

        [Fact]
        public void A_result_still_processing_is_listed_as_pending_when_the_trainee_decides_to_proceed()
        {
            var s = Plain();
            Do(s, "order_investigation", ("test", "lft")); Do(s, "take_sample", ("test", "lft"));
            var ready = Do(s, "decide_ready", ("option", "proceed"));
            Assert.Contains("pending:lft", ready.Effects);
            Assert.DoesNotContain(Do(s, "decide_ready", ("option", "postpone")).Effects, e => e.StartsWith("pending:"));
        }

        // ---------------------------------------------------------------- interpretation (L7-02, T07-WT1)

        private static void Get(Session s, string test)
        {
            Do(s, "order_investigation", ("test", test)); Do(s, "take_sample", ("test", test));
            s.Tick(100 * 60);
            Do(s, "view_result", ("test", test));
        }

        [Fact]
        public void The_haemolysed_potassium_is_correctly_called_an_artefact_and_the_repeat_normal()
        {
            var s = NewSession("VAR-S1-J");
            Get(s, "u_and_e");
            Assert.Empty(s.Patient.Clinical.CriticalCalls);                          // 6.4 is below the 6.5 limit
            Assert.Contains("interpretation:correct", Do(s, "interpret_result", ("test", "u_and_e"), ("interpretation", "artefact")).Effects);
            Get(s, "u_and_e");                                                       // the repeat: 4.6
            Assert.Contains("interpretation:correct", Do(s, "interpret_result", ("test", "u_and_e"), ("interpretation", "normal")).Effects);
            Assert.False(s.Patient.Clinical.HasFlag("interpretation.artefact_taken_at_face_value"));
        }

        [Fact]
        public void Taking_an_artefact_at_face_value_is_wrong_and_kept_for_the_debrief()
        {
            var s = NewSession("VAR-S1-J");
            Get(s, "u_and_e");
            var o = Do(s, "interpret_result", ("test", "u_and_e"), ("interpretation", "abnormal"));
            Assert.Contains("interpretation:wrong", o.Effects);
            Assert.True(s.Patient.Clinical.HasFlag("interpretation.artefact_taken_at_face_value"));
            Assert.Contains(s.ResultReviewHistory, h => h.Kind == "artefact_taken_at_face_value" && h.ScoreItems.Contains("debrief.over_treatment_risk"));
            Assert.Contains(s.ResultReviewHistory, h => h.Kind == "interpretation_wrong" && h.ScoreItems.Contains("major.interpretation_wrong"));
        }

        [Fact]
        public void An_abnormal_liver_pattern_called_normal_is_a_major_item()
        {
            var s = NewSession("VAR-S1-PB");                                       // jaundice: raised bilirubin
            Get(s, "lft");
            Assert.Contains("interpretation:wrong", Do(s, "interpret_result", ("test", "lft"), ("interpretation", "normal")).Effects);
            Assert.Contains("interpretation:correct", Do(s, "interpret_result", ("test", "lft"), ("interpretation", "abnormal")).Effects);     // the last answer stands
            Assert.Equal("abnormal", s.Patient.Clinical.Investigations.Single().Interpretation);
        }

        [Fact]
        public void A_normal_result_called_normal_is_credited()
        {
            var s = NewSession("VAR-S1-A");
            Get(s, "lft");
            Assert.Contains("interpretation:correct", Do(s, "interpret_result", ("test", "lft"), ("interpretation", "normal")).Effects);
        }

        [Fact]
        public void Interpretation_needs_an_opened_result_something_to_read_and_one_of_the_three_answers()
        {
            var s = NewSession("VAR-S2-E");
            Assert.Equal(ResultReviewSubsystem.MissingTest, Reject(Do(s, "interpret_result", ("interpretation", "normal"))));
            Assert.Equal(ResultReviewSubsystem.UnknownTest, Reject(Do(s, "interpret_result", ("test", "mri_head"), ("interpretation", "normal"))));
            Assert.Equal(ResultReviewSubsystem.NotOrdered, Reject(Do(s, "interpret_result", ("test", "crp"), ("interpretation", "normal"))));
            Do(s, "order_investigation", ("test", "u_and_e")); Do(s, "take_sample", ("test", "u_and_e"));
            Assert.Equal(ResultReviewSubsystem.NotViewed, Reject(Do(s, "interpret_result", ("test", "u_and_e"), ("interpretation", "normal"))));
            s.Tick(100 * 60); Do(s, "view_result", ("test", "u_and_e"));
            Assert.Equal(ResultReviewSubsystem.NothingToInterpret, Reject(Do(s, "interpret_result", ("test", "u_and_e"), ("interpretation", "normal"))));    // the case states no result
            var t = NewSession("VAR-S1-A");
            Get(t, "lft");
            Assert.Equal(ResultReviewSubsystem.UnknownInterpretation, Reject(Do(t, "interpret_result", ("test", "lft"), ("interpretation", "maybe"))));
        }

        [Fact]
        public void The_patient_on_warfarin_with_an_expected_INR_does_not_get_a_call_but_would_without_the_fact()
        {
            var s = NewSession("SC-13");
            Get(s, "clotting");
            Assert.Empty(s.Patient.Clinical.CriticalCalls);                          // INR 2.6 on warfarin
            var noFact = NewSession(withFile: File_("SC-13") with { FactsOrNull = new List<string>() });
            Do(noFact, "order_investigation", ("test", "clotting")); Do(noFact, "take_sample", ("test", "clotting")); noFact.Tick(100 * 60);
            Assert.Single(noFact.Patient.Clinical.CriticalCalls);
            Assert.Equal(new[] { "on_warfarin" }, File_("SC-13").Facts);
        }

        // ---------------------------------------------------------------- broken content is refused

        private static string Edit(Action<System.Text.Json.Nodes.JsonNode> change)
        {
            var node = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(Path.Combine(TestCase.ContentRoot, "investigations", "critical.json")))!;
            change(node);
            return node.ToJsonString();
        }

        [Fact]
        public void Broken_critical_content_is_refused_with_its_place()
        {
            Assert.Contains("duplicate rule", Assert.Throws<ContentException>(() => CriticalTable.Parse(Edit(n => n["rules"]![1]!["id"] = "k.low"))).Message);
            Assert.Contains("has no result names", Assert.Throws<ContentException>(() => CriticalTable.Parse(Edit(n => n["rules"]![0]!["analyte"] = "magnesium"))).Message);
            Assert.Contains("needs at least one action", Assert.Throws<ContentException>(() => CriticalTable.Parse(Edit(n => n["actions"] = new System.Text.Json.Nodes.JsonArray()))).Message);
            Assert.Contains("is not one of", Assert.Throws<ContentException>(() => CriticalTable.Parse(Edit(n => n["rules"]![0]!["condition"] = "sometimes"))).Message);
            Assert.Contains("unknown field", Assert.Throws<ContentException>(() => CriticalTable.Parse(Edit(n => n["colour"] = 1))).Message);
        }
    }
}
