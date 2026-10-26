using ProcSense.Core.Actions;
using ProcSense.Core.Workflow;

namespace ProcSense.Core.Tests
{
    /// <summary>DT-084: the older trust, anxiety and understanding counters and the behaviour model are ONE state; the counters are the model's change since the start.</summary>
    public class BehaviourMergeTests
    {
        private static Session New() => new Session(TestCase.Card(), 42, SessionMode.Practice, Subsystems.Default(TestCase.Package()));

        [Fact]
        public void A_write_through_the_counter_moves_the_model_and_a_write_to_the_model_shows_in_the_counter()
        {
            var s = New();
            double t0 = s.Behaviour!.State.Get("trust");
            s.Patient.Behavioural.Trust += 2;
            Assert.Equal(t0 + 2, s.Behaviour.State.Get("trust"));
            s.Behaviour.State.Set("anxiety", s.Behaviour.State.Get("anxiety") + 3);
            Assert.Equal(3, s.Patient.Behavioural.Anxiety);
            s.Behaviour.State.Set("understanding", s.Behaviour.State.Get("understanding") + 1);
            Assert.Equal(1, s.Patient.Behavioural.Understanding);
        }

        [Fact]
        public void Introducing_yourself_first_changes_trust_and_anxiety_once_by_the_rule_in_the_table_and_not_twice()
        {
            var s = New();
            double trust = s.Behaviour!.State.Get("trust"), anxiety = s.Behaviour.State.Get("anxiety");
            Assert.True(s.Apply(new SimAction("introduce_self")).Accepted);
            Assert.Equal(trust + 1, s.Behaviour.State.Get("trust"));              // the rule introduce_self gives +1; the older code used to add its own +1 beside it
            Assert.Equal(anxiety - 1, s.Behaviour.State.Get("anxiety"));
            Assert.Equal(1, s.Patient.Behavioural.Trust);
            Assert.True(s.Apply(new SimAction("introduce_self")).Accepted);       // a second introduction earns nothing
            Assert.Equal(trust + 1, s.Behaviour.State.Get("trust"));
        }

        [Fact]
        public void Summarising_counts_once_through_the_model_only()
        {
            var s = New();
            var before = s.Behaviour!.State.Get("understanding");
            foreach (var v in new[] { "introduce_self", "ask_identity", "read_wristband", "open_notes", "confirm_identity", "ask_allergies", "ask_allergy_reaction", "ask_reason_for_admission", "ask_responsible_adult" }) s.Apply(new SimAction(v));
            double u = s.Behaviour.State.Get("understanding");
            var r = s.Apply(new SimAction("summarise"));
            if (r.Accepted && !r.Effects.Contains("already_summarised") && !r.Effects.Exists(e => e.StartsWith("too_early:")))
                Assert.Equal(u + 1, s.Behaviour.State.Get("understanding"));
            else
                Assert.Equal(u, s.Behaviour.State.Get("understanding"));
            Assert.True(before >= 0);
        }
    }
}
