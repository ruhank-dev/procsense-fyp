using System.Globalization;
using System.Linq;
using ProcSense.Core.Actions;
using ProcSense.Core.Content;
using ProcSense.Core.Pipeline;
using ProcSense.Core.State;

namespace ProcSense.Core.Workflow.Steps
{
    /// <summary>
    /// Walkthrough Step 2: history questions. The patient volunteers only the main complaint; everything else stays hidden until asked.
    /// Both verbs need a confirmed identity (rule L2-01, enforced by the identity gate because they are registered with requiresIdentity).
    /// </summary>
    public sealed class HistorySubsystem : ISubsystem
    {
        public const string UnknownTopic = "UNKNOWN_TOPIC";
        public const string TopicNotInCase = "TOPIC_NOT_IN_CASE";
        public const string MissingTopic = "MISSING_TOPIC";
        public const string Summarised = "summarised";

        private readonly ContentPackage _content;
        private Session _s = null!;

        public HistorySubsystem(ContentPackage content) { _content = content; }

        public int Order => PipelineOrder.Clinical;

        private ClinicalLayer Clinical => _s.Patient.Clinical;

        public void Init(Session session)
        {
            _s = session;
            session.Verbs.Register("ask", requiresIdentity: true);
            session.Verbs.Register("summarise", requiresIdentity: true);
        }

        public void OnAction(SimAction a, ActionOutcome o)
        {
            if (a.Verb == "ask") Ask(a, o);
            else if (a.Verb == "summarise") Summarise(o);
        }

        public void OnTick() { }

        private void Ask(SimAction a, ActionOutcome o)
        {
            if (!a.Params.TryGetValue("topic", out var topic)) { o.Reject(MissingTopic); return; }
            if (!_content.HistoryTopics.Ids.Contains(topic)) { o.Reject(UnknownTopic); return; }
            var fact = _s.Patient.History.FirstOrDefault(h => h.Topic == topic);
            if (fact == null) { o.Reject(TopicNotInCase); return; }

            Volunteer(o);
            if (!Clinical.Reveal(topic)) { o.Effects.Add("already_known:" + topic); return; }
            if (fact.ClockTime != null) RecordTime(topic, fact.ClockTime);
            o.Effects.Add("answered:" + topic);
            if (_content.HistoryTopics.Sensitive.Contains(topic)) o.Effects.Add("sensitive:" + topic);
        }

        // The patient says the main complaint without being asked, as soon as the history conversation starts.
        private void Volunteer(ActionOutcome o)
        {
            foreach (var f in _s.Patient.History)
                if (f.Volunteered && Clinical.Reveal(f.Topic)) o.Effects.Add("volunteered:" + f.Topic);
        }

        // Last food and last drink are stored as exact clock times; the fasting rule is evaluated later (clinical-logic).
        private void RecordTime(string topic, string clock)
        {
            var t = new System.TimeSpan(int.Parse(clock.Substring(0, 2), CultureInfo.InvariantCulture), int.Parse(clock.Substring(3, 2), CultureInfo.InvariantCulture), 0);
            if (topic == "last_food") Clinical.LastFoodAt = t;
            else if (topic == "last_drink") Clinical.LastDrinkAt = t;
        }

        // L2-05: summarising once, after enough topics have been asked, builds trust and understanding.
        private void Summarise(ActionOutcome o)
        {
            Volunteer(o);
            var m = _content.Summarise;
            if (Clinical.HasFlag(Summarised)) { o.Effects.Add("already_summarised"); return; }
            if (Clinical.RevealedTopics.Count < m.MinTopics) { o.Effects.Add("too_early:" + Clinical.RevealedTopics.Count.ToString(CultureInfo.InvariantCulture) + "_of_" + m.MinTopics.ToString(CultureInfo.InvariantCulture)); return; }
            Clinical.SetFlag(Summarised);
            _s.Patient.Behavioural.Trust += (int)m.Trust;
            _s.Patient.Behavioural.Understanding += (int)m.Understanding;
            o.Effects.Add("trust+" + m.Trust.ToString(CultureInfo.InvariantCulture));
            o.Effects.Add("understanding+" + m.Understanding.ToString(CultureInfo.InvariantCulture));
        }
    }
}
