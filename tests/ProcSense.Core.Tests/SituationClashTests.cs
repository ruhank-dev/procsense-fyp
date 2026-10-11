using System.Collections.Generic;
using System.IO;
using System.Linq;
using ProcSense.Core.Actions;
using ProcSense.Core.Content;
using ProcSense.Core.Pipeline;
using ProcSense.Core.Workflow;
using ProcSense.Core.Workflow.Steps;

namespace ProcSense.Core.Tests
{
    /// <summary>The five clashes that wait for a situation (nil by mouth, high potassium with suxamethonium, a full stomach, low oxygen in a CO2 retainer, a confused patient needing consent in an emergency) can be raised (DT-072).</summary>
    public class SituationClashTests
    {
        private static readonly string[] Ids = Directory.GetFiles(Path.Combine(TestCase.ContentRoot, "case_files"), "*.json").Select(Path.GetFileNameWithoutExtension).OrderBy(x => x, System.StringComparer.Ordinal).Select(x => x!).ToArray();
        private static readonly ContentPackage All = ContentLoader.Load(new CompositeContentReader(new FileContentReader(TestCase.ContentRoot), new FileContentReader(Path.Combine(TestCase.Repo, "tests", "fixtures"))), Ids, Ids);

        private static SimAction Act(string verb, params (string k, string v)[] p) => new SimAction(verb, "patient", Channel.Cli, p.ToDictionary(x => x.k, x => x.v));

        private static Session Start(string id, CaseFile? file = null)
        {
            var chain = Subsystems.Default(All).Where(x => !(x is ClashSubsystem)).Concat(new ISubsystem[] { new ClashSubsystem(All, file ?? All.CaseFiles[id]) });
            var s = new Session(All.Cases[id], 42, SessionMode.Practice, chain);
            foreach (var v in new[] { "ask_identity", "read_wristband", "open_notes", "confirm_identity" }) Assert.True(s.Apply(Act(v)).Accepted, v);
            return s;
        }

        private static bool Raised(Session s, string clash) => s.Clashes!.History.Any(h => h.Kind == "clash:" + clash);

        private static string last = "";

        private static int _n;

        // a different test each time, so an earlier order is never still pending
        private static void Request(Session s) { var o = s.Apply(Act("order_investigation", ("test", new[] { "crp", "fbc", "lft", "clotting", "lipase", "ferritin" }[_n++ % 6]))); last = o.Accepted + ":" + o.RejectReason + ":" + string.Join(",", o.Effects); }       // any accepted request is checked against the table

        [Fact]
        public void Insulin_and_nil_by_mouth_once_the_trainee_knows_what_the_patient_last_ate_and_drank()
        {
            var s = Start("SC-6");
            Request(s);
            Assert.False(Raised(s, "insulin_nbm"));                                  // nothing is known about eating yet
            s.Apply(Act("ask", ("topic", "last_food"))); s.Apply(Act("ask", ("topic", "last_drink")));
            Request(s);
            Assert.True(Raised(s, "insulin_nbm"), last);
        }

        [Fact]
        public void A_full_stomach_in_an_emergency()
        {
            var s = Start("VAR-S2-F");
            s.Apply(Act("ask", ("topic", "last_food"))); s.Apply(Act("ask", ("topic", "last_drink")));
            Request(s);
            Assert.True(Raised(s, "full_stomach_emergency"), last);
        }

        [Fact]
        public void A_high_potassium_the_trainee_has_opened_with_a_rapid_sequence_induction_planned()
        {
            var s = Start("VAR-S1-J");
            s.Apply(Act("order_investigation", ("test", "u_and_e"))); s.Apply(Act("take_sample", ("test", "u_and_e"), ("tube", "serum_gold")));
            s.Tick(3600);
            Request(s);
            Assert.False(Raised(s, "high_potassium_suxamethonium"));                 // not opened yet, and no rapid sequence induction planned
            s.Apply(Act("view_result", ("test", "u_and_e")));
            s.Patient.Clinical.Risk.Plan = "rsi";
            Request(s);
            Assert.True(Raised(s, "high_potassium_suxamethonium"));
        }

        [Fact]
        public void Low_oxygen_in_a_patient_who_retains_carbon_dioxide()
        {
            var file = All.CaseFiles["VAR-S1-A"];
            var copd = file with { History = file.History.Select(h => h.Topic == "past_history" ? h with { Answer = "COPD, long-term lung disease." } : h).ToList() };
            var s = Start("VAR-S1-A", copd);
            Request(s);
            Assert.False(Raised(s, "low_oxygen_copd"));
            s.Patient.Physiological.Vitals = s.Patient.Physiological.Vitals with { Spo2 = 90 };
            Request(s);
            Assert.True(Raised(s, "low_oxygen_copd"));
        }

        [Fact]
        public void A_confused_emergency_patient_who_has_not_consented()
        {
            var s = Start("STK-S2-B");
            Request(s);
            Assert.False(Raised(s, "confused_consent_emergency"));
            s.Patient.Physiological.Vitals = s.Patient.Physiological.Vitals with { Acvpu = "C" };
            Request(s);
            Assert.True(Raised(s, "confused_consent_emergency"));
        }

        [Fact]
        public void Once_consent_is_given_by_the_emergency_route_the_clash_is_not_raised_again()
        {
            var s = Start("STK-S2-B");
            s.Patient.Clinical.SetFlag("consent.emergency_route");
            s.Patient.Physiological.Vitals = s.Patient.Physiological.Vitals with { Acvpu = "C" };
            Request(s);
            Assert.False(Raised(s, "confused_consent_emergency"));
        }
    }
}
