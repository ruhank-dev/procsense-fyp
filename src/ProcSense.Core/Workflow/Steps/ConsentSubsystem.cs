using System;
using System.Collections.Generic;
using System.Linq;
using ProcSense.Core.Actions;
using ProcSense.Core.Content;
using ProcSense.Core.Pipeline;

namespace ProcSense.Core.Workflow.Steps
{
    public sealed record ConsentElement(string Topic, string Text, string Ref);

    public sealed record ConsentAnswer(string Id, string Text, string Kind);

    /// <summary>A question the patient asks, with the answers the trainee can choose from (the book's approved one and two bad ones).</summary>
    public sealed record ConsentQuestion(string Id, string Scenario, string Text, IReadOnlyList<ConsentAnswer> Answers, string Ref);

    /// <summary>The consent content (content/consent/consent.json, built from walkthrough Part 10 with the verified risk figures).</summary>
    public sealed class ConsentTables
    {
        public static readonly ConsentTables Empty = new ConsentTables(new List<string>(), new Dictionary<string, IReadOnlyList<ConsentElement>>(), new List<ConsentQuestion>(), new List<string>(), 6, 18, new Dictionary<string, bool>(), 2, new Dictionary<string, string>(), "consent.signed");

        public ConsentTables(IReadOnlyList<string> topics, IReadOnlyDictionary<string, IReadOnlyList<ConsentElement>> procedures, IReadOnlyList<ConsentQuestion> questions, IReadOnlyList<string> askAfter, double understandingNeeded, int minorAge,
            IReadOnlyDictionary<string, bool> interpreters, double refusesAtCooperation, IReadOnlyDictionary<string, string> items, string signedFlag)
        {
            Topics = topics; Procedures = procedures; Questions = questions; AskAfter = askAfter; UnderstandingNeeded = understandingNeeded; MinorAge = minorAge; Interpreters = interpreters;
            RefusesAtCooperation = refusesAtCooperation; Items = items; SignedFlag = signedFlag;
        }

        public IReadOnlyList<string> Topics { get; }
        public IReadOnlyDictionary<string, IReadOnlyList<ConsentElement>> Procedures { get; }
        public IReadOnlyList<ConsentQuestion> Questions { get; }
        public IReadOnlyList<string> AskAfter { get; }
        public double UnderstandingNeeded { get; }
        public int MinorAge { get; }
        /// <summary>Who can interpret: true for a trained interpreter, false for a family member or a child.</summary>
        public IReadOnlyDictionary<string, bool> Interpreters { get; }
        public double RefusesAtCooperation { get; }
        public IReadOnlyDictionary<string, string> Items { get; }
        public string SignedFlag { get; }

        public ConsentQuestion? FindQuestion(string id) => Questions.FirstOrDefault(q => q.Id == id);

        public static ConsentTables Parse(string json, string source = "consent")
        {
            var root = new JsonObj(MiniJson.Parse(json), source);
            var topics = new List<string>();
            foreach (var t in root.Arr("topics")) topics.Add(t as string ?? throw new ContentException(root.Sub("topics") + ": expected text"));
            var procs = new SortedDictionary<string, IReadOnlyList<ConsentElement>>(StringComparer.Ordinal);
            var rawP = root.Any("procedures") as Dictionary<string, object?> ?? throw new ContentException(root.Sub("procedures") + ": expected an object");
            foreach (var kv in rawP)
            {
                var o = new JsonObj(kv.Value, root.Sub("procedures") + "." + kv.Key);
                o.Str("label");
                var els = new List<ConsentElement>();
                var rawE = o.Arr("elements");
                for (int i = 0; i < rawE.Count; i++)
                {
                    var e = new JsonObj(rawE[i], JsonObj.Index(o.Sub("elements"), i));
                    string topic = e.Str("topic");
                    if (!topics.Contains(topic)) throw new ContentException(e.Sub("topic") + ": '" + topic + "' is not a consent topic");
                    els.Add(new ConsentElement(topic, e.Str("text"), e.Str("ref")));
                    e.Finish();
                }
                foreach (var t in topics) if (els.All(x => x.Topic != t)) throw new ContentException(o.Sub("elements") + ": the topic '" + t + "' is not covered");
                procs[kv.Key] = els;
                o.Finish();
            }
            var questions = new List<ConsentQuestion>();
            var rawQ = root.Arr("questions");
            for (int i = 0; i < rawQ.Count; i++)
            {
                var o = new JsonObj(rawQ[i], JsonObj.Index(root.Sub("questions"), i));
                var answers = new List<ConsentAnswer>();
                var rawA = o.Arr("answers");
                for (int a = 0; a < rawA.Count; a++)
                {
                    var an = new JsonObj(rawA[a], JsonObj.Index(o.Sub("answers"), a));
                    answers.Add(new ConsentAnswer(an.Str("id"), an.Str("text"), an.Enum("kind", "approved", "false_reassurance", "dismissive")));
                    an.Finish();
                }
                if (answers.Count(x => x.Kind == "approved") != 1) throw new ContentException(o.Sub("answers") + ": exactly one answer must be the approved one");
                string scenario = o.Str("scenario");
                if (!procs.ContainsKey(scenario)) throw new ContentException(o.Sub("scenario") + ": '" + scenario + "' has no procedure");
                questions.Add(new ConsentQuestion(o.Str("id"), scenario, o.Str("text"), answers, o.Str("ref")));
                o.Finish();
            }
            var askAfter = new List<string>();
            foreach (var t in root.Arr("ask_after")) { string s = t as string ?? throw new ContentException(root.Sub("ask_after") + ": expected text"); if (!topics.Contains(s)) throw new ContentException(root.Sub("ask_after") + ": '" + s + "' is not a topic"); askAfter.Add(s); }
            double needed = root.Num("understanding_needed");
            int minor = (int)root.Int("minor_age");
            var interp = new SortedDictionary<string, bool>(StringComparer.Ordinal);
            var rawI = root.Any("interpreters") as Dictionary<string, object?> ?? throw new ContentException(root.Sub("interpreters") + ": expected an object");
            foreach (var kv in rawI) interp[kv.Key] = kv.Value is bool b ? b : throw new ContentException(root.Sub("interpreters") + "." + kv.Key + ": expected true or false");
            double refuses = root.Num("refuses_at_cooperation");
            var items = new SortedDictionary<string, string>(root.StringMap("items"), StringComparer.Ordinal);
            string flag = root.Str("signed_flag");
            foreach (var r in root.Arr("refs")) if (!(r is string)) throw new ContentException(root.Sub("refs") + ": expected register ids");
            root.Finish();
            return new ConsentTables(topics, procs, questions, askAfter, needed, minor, interp, refuses, items, flag);
        }
    }

    /// <summary>
    /// Step 9, consent. The trainee explains each element of the talk (consent_discussion), answers the patient's questions from a menu (answer_question), checks capacity, arranges a trained interpreter when one is needed,
    /// does the teach-back and signs. Capacity, understanding and the patient's willingness come from the behaviour model; the engine never tells the trainee whether capacity is lost unless they check. Signing a patient without
    /// capacity is a critical item and the consent is not valid; a transfer without valid consent is blocked (B-CONSENT). In an emergency without capacity the senior's best-interests route is the way.
    /// </summary>
    public sealed class ConsentSubsystem : ISubsystem
    {
        public const string DiscussionVerb = "consent_discussion";
        public const string AnswerVerb = "answer_question";
        public const string TeachBackVerb = "teach_back";
        public const string CapacityVerb = "check_capacity";
        public const string InterpreterVerb = "request_interpreter";
        public const string PressVerb = "press_to_sign";
        public const string SignVerb = "sign_consent";
        public const string EmergencyVerb = "use_emergency_route";

        public const string MissingTopic = "MISSING_TOPIC";
        public const string UnknownTopic = "UNKNOWN_TOPIC";
        public const string MissingQuestion = "MISSING_QUESTION";
        public const string NoQuestionPending = "NO_QUESTION_PENDING";
        public const string MissingAnswer = "MISSING_ANSWER";
        public const string UnknownAnswer = "UNKNOWN_ANSWER";
        public const string NothingToCheck = "NOTHING_TO_CHECK";
        public const string MissingWho = "MISSING_WHO";
        public const string UnknownInterpreter = "UNKNOWN_INTERPRETER";
        public const string PatientRefuses = "PATIENT_REFUSES";
        public const string InterpreterNeeded = "INTERPRETER_NEEDED";
        public const string UnderstandingTooLow = "UNDERSTANDING_TOO_LOW";
        public const string AlreadySigned = "ALREADY_SIGNED";
        public const string NotAllowed = "NOT_ALLOWED";

        public const string InvalidFlag = "consent.invalid";
        public const string CapacityCheckedFlag = "consent.capacity_checked";
        public const string TeachBackFlag = "consent.teach_back_ok";
        public const string InterpreterFlag = "consent.interpreter";
        public const string EmergencyFlag = "consent.emergency_route";

        private readonly ContentPackage _content;
        private readonly List<GraphEvent> _history = new List<GraphEvent>();
        private readonly List<string> _explained = new List<string>();
        private readonly List<string> _pending = new List<string>();
        private readonly HashSet<string> _asked = new HashSet<string>(StringComparer.Ordinal);
        private Session _s = null!;

        public ConsentSubsystem(ContentPackage content) { _content = content; }

        public int Order => PipelineOrder.Investigations + 4;

        /// <summary>Elements, questions, capacity, teach-back and signing with sim steps and score items. The scorer and the debrief read this.</summary>
        public IReadOnlyList<GraphEvent> History => _history;

        public IReadOnlyList<string> Explained => _explained;

        /// <summary>The questions the patient has asked that are still waiting for an answer.</summary>
        public IReadOnlyList<string> Pending => _pending;

        public bool Signed => _s.Patient.Clinical.HasFlag(Tables.SignedFlag);

        private ConsentTables Tables => _content.Consent;

        private Behaviour.BehaviourSubsystem? Mind => _s.Behaviour;

        public void Init(Session session)
        {
            _s = session;
            session.ConsentStep = this;
            foreach (var verb in new[] { DiscussionVerb, AnswerVerb, TeachBackVerb, CapacityVerb, InterpreterVerb, PressVerb, SignVerb }) session.Verbs.Register(verb, requiresIdentity: true);
            session.Verbs.Register(EmergencyVerb, requiresIdentity: true);
        }

        public void OnAction(SimAction a, ActionOutcome o)
        {
            switch (a.Verb)
            {
                case DiscussionVerb: Discuss(a, o); break;
                case AnswerVerb: Answer(a, o); break;
                case TeachBackVerb: TeachBack(o); break;
                case CapacityVerb: Capacity(o); break;
                case InterpreterVerb: Interpreter(a, o); break;
                case PressVerb: Press(o); break;
                case SignVerb: Sign(o); break;
                case EmergencyVerb: Emergency(o); break;
            }
        }

        public void OnTick() { }

        private bool HasCapacity => Mind?.HasCapacity ?? true;

        private double Understanding => Mind?.State.Get("understanding") ?? 10;

        private IReadOnlyList<ConsentElement> Elements => Tables.Procedures.TryGetValue(_s.Case.Scenario, out var e) ? e : new List<ConsentElement>();

        private void Item(string kind, string eventKind, string id, ActionOutcome? o = null)
        {
            _history.Add(new GraphEvent(eventKind, id, _s.Clock.Steps, new[] { Tables.Items[kind] }));
        }

        // ---------------------------------------------------------------- explaining

        private void Discuss(SimAction a, ActionOutcome o)
        {
            if (!a.Params.TryGetValue("topic", out var topic) || topic.Length == 0) { o.Reject(MissingTopic); return; }
            if (!Tables.Topics.Contains(topic)) { o.Reject(UnknownTopic); return; }
            if (_explained.Contains(topic)) { o.Effects.Add("already_explained:" + topic); return; }
            if (!HasCapacity) { o.Effects.Add("patient_cannot_follow:" + topic); return; }         // treat the cause and try again
            _explained.Add(topic);
            o.Effects.Add("explained:" + topic);
            Item("element", "consent_explained:" + topic, topic);
            Mind?.Trigger("explain_before_action");
            if (Tables.AskAfter.Contains(topic))
            {
                var next = Tables.Questions.FirstOrDefault(q => q.Scenario == _s.Case.Scenario && !_asked.Contains(q.Id));
                if (next != null) { _asked.Add(next.Id); _pending.Add(next.Id); o.Effects.Add("patient_asks:" + next.Id); }
            }
        }

        // ---------------------------------------------------------------- the patient's questions (L9-03)

        private void Answer(SimAction a, ActionOutcome o)
        {
            if (!a.Params.TryGetValue("q", out var qid) || qid.Length == 0) { o.Reject(MissingQuestion); return; }
            if (!_pending.Contains(qid)) { o.Reject(NoQuestionPending); return; }
            if (!a.Params.TryGetValue("a", out var aid) || aid.Length == 0) { o.Reject(MissingAnswer); return; }
            var question = Tables.FindQuestion(qid)!;
            var answer = question.Answers.FirstOrDefault(x => x.Id == aid);
            if (answer == null) { o.Reject(UnknownAnswer); return; }
            _pending.Remove(qid);
            switch (answer.Kind)
            {
                case "approved":
                    Mind?.Trigger("answer_approved");
                    Item("answer_approved", "question_answered:" + qid, qid);
                    o.Effects.Add("answered:" + qid);
                    break;
                case "false_reassurance":
                    Mind?.Trigger("false_reassurance");                                               // the patient relaxes; the item is for the debrief (E9-B)
                    Item("false_reassurance", "false_reassurance:" + qid, qid);
                    o.Effects.Add("patient_relaxes");
                    break;
                default:
                    Mind?.Trigger("dismissive_reply");
                    Item("answer_dismissive", "question_dismissed:" + qid, qid);
                    o.Effects.Add("trust_falls");
                    break;
            }
        }

        // ---------------------------------------------------------------- teach-back

        private void TeachBack(ActionOutcome o)
        {
            if (_explained.Count == 0) { o.Reject(NothingToCheck); return; }
            Mind?.Trigger("teach_back");
            if (Understanding >= Tables.UnderstandingNeeded)
            {
                _s.Patient.Clinical.SetFlag(TeachBackFlag);
                Item("teach_back_ok", "teach_back_ok", "teach_back");
                o.Effects.Add("teach_back_ok");
            }
            else
            {
                _s.Patient.Clinical.ClearFlag(TeachBackFlag);
                o.Effects.Add("teach_back_failed");                                                   // explain it differently, use an interpreter or a leaflet
            }
        }

        // ---------------------------------------------------------------- capacity, interpreter, pressure

        private void Capacity(ActionOutcome o)
        {
            _s.Patient.Clinical.SetFlag(CapacityCheckedFlag);
            Item("capacity_checked", "capacity_checked", "capacity");
            if (HasCapacity) o.Effects.Add("capacity:yes");
            else { o.Effects.Add("capacity:no"); foreach (var c in Mind!.CapacityCauses) o.Effects.Add("cause:" + c); }
        }

        private void Interpreter(SimAction a, ActionOutcome o)
        {
            if (!a.Params.TryGetValue("who", out var who) || who.Length == 0) { o.Reject(MissingWho); return; }
            if (!Tables.Interpreters.TryGetValue(who, out var trained)) { o.Reject(UnknownInterpreter); return; }
            if (trained) { _s.Patient.Clinical.SetFlag(InterpreterFlag); o.Effects.Add("interpreter_arrives"); }
            else { Item("untrained_interpreter", "untrained_interpreter", who); o.Effects.Add("not_a_trained_interpreter"); }
        }

        private void Press(ActionOutcome o)
        {
            Mind?.Trigger("pressure");
            Item("pressure", "consent_pressure", "press_to_sign");
            o.Effects.Add("patient_pushes_back");
        }

        // ---------------------------------------------------------------- signing (L9-02)

        private bool NeedsInterpreter => Mind?.State.Persona.TryGetValue("communication_difficulty", out var d) == true && d == "language";

        private void Sign(ActionOutcome o)
        {
            var c = _s.Patient.Clinical;
            if (Signed) { o.Reject(AlreadySigned); return; }
            if (Mind != null && Mind.State.Get("cooperation") <= Tables.RefusesAtCooperation) { o.Effects.Add("patient_refuses"); o.Effects.Add("call:senior"); o.Reject(PatientRefuses); return; }   // respected: explore why, offer alternatives, senior, document
            if (NeedsInterpreter && !c.HasFlag(InterpreterFlag)) { o.Reject(InterpreterNeeded); return; }
            if (!HasCapacity)
            {
                c.SetFlag(InvalidFlag);
                Item("no_capacity_signed", "consent_signed_without_capacity", "sign");
                o.Effects.Add("consent_invalid:no_capacity");
                return;                                                                               // signed, but not a valid consent: a safety failure
            }
            bool practice = _s.Mode == SessionMode.Practice;
            if (practice && (!c.HasFlag(TeachBackFlag) || Understanding < Tables.UnderstandingNeeded)) { o.Effects.Add("understanding_low"); o.Reject(UnderstandingTooLow); return; }

            var missing = Tables.Topics.Where(t => !_explained.Contains(t)).ToList();
            foreach (var m in missing) o.Effects.Add("missing:" + m);
            var flagged = new List<string>();
            if (missing.Contains("risks")) flagged.Add(Tables.Items["risks_skipped"]);
            if (missing.Contains("alternatives")) flagged.Add(Tables.Items["alternatives_skipped"]);
            if (!practice && (!c.HasFlag(TeachBackFlag) || Understanding < Tables.UnderstandingNeeded)) flagged.Add(Tables.Items["low_understanding"]);
            if (!c.HasFlag(CapacityCheckedFlag)) flagged.Add(Tables.Items["capacity_not_checked"]);
            if (_pending.Count > 0) flagged.Add(Tables.Items["question_unanswered"]);
            if (_s.Case.Identity.Age < Tables.MinorAge && !(_s.DecisionStep?.Escalations.Any(e => e.Role == "senior") ?? false)) flagged.Add(Tables.Items["minor_no_senior"]);

            c.SetFlag(Tables.SignedFlag);
            if (flagged.Count == 0) flagged.Add(Tables.Items["complete"]);
            _history.Add(new GraphEvent("consent_signed", flagged.Any(i => i.StartsWith("major.", StringComparison.Ordinal)) ? "with_gaps" : "valid", _s.Clock.Steps, flagged));
            o.Effects.Add("consent_signed");
        }

        // S2, no capacity, urgent: the senior follows the emergency or best-interests route under local law
        private void Emergency(ActionOutcome o)
        {
            if (_s.Case.Scenario != "S2" || HasCapacity || !(_s.DecisionStep?.Escalations.Any(e => e.Role == "senior") ?? false)) { o.Reject(NotAllowed); return; }
            var c = _s.Patient.Clinical;
            c.SetFlag(EmergencyFlag);
            c.SetFlag(Tables.SignedFlag);                                                             // documented by the senior: consent is present
            Item("emergency_route", "consent_emergency_route", "senior");
            o.Effects.Add("consent_emergency_route");
        }
    }
}
