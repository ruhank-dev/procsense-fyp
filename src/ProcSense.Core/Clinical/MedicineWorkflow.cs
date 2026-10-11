using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using ProcSense.Core.Actions;
using ProcSense.Core.Pipeline;
using ProcSense.Core.State;
using ProcSense.Core.Time;
using ProcSense.Core.Workflow;

namespace ProcSense.Core.Clinical
{
    /// <summary>Published on the EventBus when the clinician confirms an order, the nurse gives it, or the nurse refuses it (Kind is confirmed, administered or challenged).</summary>
    public sealed record MedicineEvent(string Kind, string Treatment, int OrderId);

    /// <summary>Published when a treatment has been given: the physiology effect ids to apply (physiology, when it exists, subscribes).</summary>
    public sealed record TreatmentGiven(string Treatment, IReadOnlyList<string> Effects, int OrderId);

    /// <summary>
    /// Walkthrough Part 8.1 and Implementation Guide B-M14 step 5: the trainee requests, a clinician confirms and a nurse gives. The trainee can only request a medicine, a fluid, oxygen or IV access;
    /// there is no verb to give anything. The clinician and the nurse are this subsystem: after the content's delays the clinician confirms (a dose is shown only under its dose policy: an educational
    /// example with its source, or the words "the clinician decides"), and the nurse then gives it. Before giving, the nurse judges the treatment's own required checks and the patient's allergies.
    /// A critical problem (an allergy to the medicine's group, an applying restriction, allergies not yet established) is handled by mode, from content: practice has the nurse challenge and not give,
    /// assessment has the nurse give it, the problem becomes a critical item and, for an allergen, the hypersensitivity event becomes eligible (T-TR3).
    /// </summary>
    public sealed class MedicineWorkflowSubsystem : ISubsystem
    {
        public const string RequestMedicationVerb = "request_medication";
        public const string RequestFluidsVerb = "request_fluids";
        public const string RequestOxygenVerb = "request_oxygen";
        public const string RequestIvAccessVerb = "request_iv_access";
        public const string CheckTimingVerb = "check_treatment_timing";
        public const string DocumentVerb = "document_treatment";

        public const string MissingTreatment = "MISSING_TREATMENT";
        public const string UnknownTreatment = "UNKNOWN_TREATMENT";
        public const string WrongKind = "WRONG_KIND";
        public const string MissingDevice = "MISSING_DEVICE";
        public const string UnknownDevice = "UNKNOWN_DEVICE";
        public const string OrderPending = "ORDER_PENDING";
        public const string AlreadyInPlace = "ALREADY_IN_PLACE";
        public const string NotGiven = "NOT_GIVEN";

        public const string IvInPlaceFlag = "iv_access.in_place";
        public const string ConditionPrefix = "condition.";
        public const string ReactionEligibleFlag = "allergy.reaction_eligible";

        private const string ClassParam = "class", TypeParam = "type", DeviceParam = "device", IndicationParam = "indication", TreatmentParam = "treatment";

        private readonly Content.ContentPackage _content;
        private readonly List<GraphEvent> _history = new List<GraphEvent>();
        private readonly HashSet<int> _documentationReported = new HashSet<int>();
        private Session _s = null!;

        public MedicineWorkflowSubsystem(Content.ContentPackage content) { _content = content; }

        public int Order => PipelineOrder.Treatments;

        /// <summary>Requests, confirmations, treatments given, challenges and failed checks, with the sim step and score items. The scorer and the debrief read this.</summary>
        public IReadOnlyList<GraphEvent> History => _history;

        private ClinicalLayer Clinical => _s.Patient.Clinical;
        private TreatmentTables Tables => _content.Treatments;

        public void Init(Session session)
        {
            _s = session;
            session.Medicine = this;
            foreach (var verb in new[] { RequestMedicationVerb, RequestFluidsVerb, RequestOxygenVerb, RequestIvAccessVerb, CheckTimingVerb, DocumentVerb })
                session.Verbs.Register(verb, requiresIdentity: true);
        }

        // ---------------------------------------------------------------- the trainee's actions: requests only

        public void OnAction(SimAction a, ActionOutcome o)
        {
            switch (a.Verb)
            {
                case RequestMedicationVerb: Request(a, o, a.Params.GetValueOrDefault(ClassParam), "medicine"); break;
                case RequestFluidsVerb: Request(a, o, a.Params.GetValueOrDefault(TypeParam), "fluid"); break;
                case RequestOxygenVerb: RequestOxygen(a, o); break;
                case RequestIvAccessVerb: RequestIvAccess(a, o); break;
                case CheckTimingVerb: CheckTiming(a, o); break;
                case DocumentVerb: Document(a, o); break;
                case Workflow.Steps.RiskSubsystem.DecideReadyVerb: if (o.Accepted) ReportUndocumented(); break;
            }
        }

        private bool Find(string? id, ActionOutcome o, out TreatmentDef def)
        {
            def = null!;
            if (string.IsNullOrEmpty(id)) { o.Reject(MissingTreatment); return false; }
            if (!Tables.ById.TryGetValue(id!, out var found)) { o.Reject(UnknownTreatment); return false; }
            def = found;
            return true;
        }

        private void Request(SimAction a, ActionOutcome o, string? id, string kind)
        {
            if (!Find(id, o, out var def)) return;
            if (def.Kind != kind) { o.Reject(WrongKind); return; }
            Place(a, o, def, "");
        }

        private void RequestOxygen(SimAction a, ActionOutcome o)
        {
            if (!a.Params.TryGetValue(DeviceParam, out var device) || device.Length == 0) { o.Reject(MissingDevice); return; }
            if (!Tables.OxygenDevices.Contains(device)) { o.Reject(UnknownDevice); return; }
            var def = Tables.Treatments.FirstOrDefault(t => t.Kind == "oxygen");
            if (def == null) { o.Reject(UnknownTreatment); return; }
            Place(a, o, def, device);
        }

        private void RequestIvAccess(SimAction a, ActionOutcome o)
        {
            if (Clinical.HasFlag(IvInPlaceFlag)) { o.Reject(AlreadyInPlace); return; }
            var def = Tables.Treatments.FirstOrDefault(t => t.Kind == "procedure");
            if (def == null) { o.Reject(UnknownTreatment); return; }
            Place(a, o, def, "");
        }

        private void Place(SimAction a, ActionOutcome o, TreatmentDef def, string device)
        {
            if (Clinical.Orders.Any(x => x.Treatment == def.Id && (x.State == "REQUESTED" || x.State == "CONFIRMED"))) { o.Reject(OrderPending); return; }
            var before = RequiredChecks.Evaluate(Tables, def, Context(def, null), "before");
            if (!before.Allowed) { o.Reject(before.RejectReason); return; }   // for example IV_ACCESS_REQUIRED (T-TR5)

            var order = new MedicineOrder
            {
                Id = Clinical.Orders.Count + 1,
                Treatment = def.Id,
                Label = def.Label,
                Kind = def.Kind,
                State = "REQUESTED",
                Device = device,
                Indication = a.Params.GetValueOrDefault(IndicationParam) ?? "",
                RequestedStep = _s.Clock.Steps,
            };
            order.ConfirmAtStep = order.RequestedStep + Steps(Tables.Timing.ConfirmSeconds);
            Clinical.Orders.Add(order);
            Record("requested", order, new List<string>(), order.RequestedStep);
            o.Effects.Add("order_requested:" + def.Id);
            o.Effects.Add("waiting_for:clinician");
        }

        private void CheckTiming(SimAction a, ActionOutcome o)
        {
            if (!Find(a.Params.GetValueOrDefault(TreatmentParam), o, out var def)) return;
            Clinical.SetFlag(Flag(def.Id, "timing_checked"));
            o.Effects.Add("timing_checked:" + def.Id);
        }

        private void Document(SimAction a, ActionOutcome o)
        {
            if (!Find(a.Params.GetValueOrDefault(TreatmentParam), o, out var def)) return;
            var given = Clinical.Orders.LastOrDefault(x => x.Treatment == def.Id && x.State == "ADMINISTERED");
            if (given == null) { o.Reject(NotGiven); return; }
            Clinical.SetFlag(Flag(def.Id, "documented"));
            o.Effects.Add("documented:" + def.Id);
        }

        /// <summary>When the trainee decides on readiness, each treatment that was given and never recorded is a minor item (once).</summary>
        private void ReportUndocumented()
        {
            foreach (var order in Clinical.Orders.Where(x => x.State == "ADMINISTERED"))
            {
                var def = Tables.ById[order.Treatment];
                var report = RequiredChecks.Evaluate(Tables, def, Context(def, order), "after");
                if (report.Items.Count == 0 || !_documentationReported.Add(order.Id)) continue;
                order.Items.AddRange(report.Items);
                _history.Add(new GraphEvent("not_documented", Key(order), _s.Clock.Steps, report.Items.Select(i => Score(i, false)).ToList()));
            }
        }

        // ---------------------------------------------------------------- the clinician and the nurse (the engine)

        public void OnTick()
        {
            if (Clinical.Orders.Count == 0) return;
            long now = _s.Clock.Steps;
            foreach (var order in Clinical.Orders)
            {
                if (order.State == "REQUESTED" && now >= order.ConfirmAtStep) Confirm(order);
                else if (order.State == "CONFIRMED" && now >= order.AdministerAtStep && Clinical.Location == PatientFactory.PatientBay) Give(order);
            }
        }

        private void Confirm(MedicineOrder order)
        {
            var def = Tables.ById[order.Treatment];
            order.State = "CONFIRMED";
            order.AdministerAtStep = order.ConfirmAtStep + Steps(Tables.Timing.AdministerSeconds);   // from when it was due, so one long tick and many short ones give the same times
            order.Lines.Add(Say("confirm", ("treatment", def.Label)));
            order.DoseText = DoseLine(def);
            var caution = Allergy(def);
            if (caution.HasValue && !caution.Value.Severe) order.Lines.Add(Say("confirm_allergy_caution", ("allergy", caution.Value.Text)));
            Record("confirmed", order, new List<string>(), order.ConfirmAtStep);
            _s.Events.Publish(new MedicineEvent("confirmed", def.Id, order.Id));
        }

        private void Give(MedicineOrder order)
        {
            var def = Tables.ById[order.Treatment];
            bool practice = _s.Mode == SessionMode.Practice;
            string outcome = practice ? Tables.Allergy.PracticeOutcome : Tables.Allergy.AssessmentOutcome;

            var report = RequiredChecks.Evaluate(Tables, def, Context(def, order), "before");
            var items = new List<CheckItem>(report.Items);
            var allergy = Allergy(def);
            bool conflict = allergy.HasValue && allergy.Value.Severe;
            if (conflict) items.Insert(0, new CheckItem(def.Id, "allergy_conflict", "critical", def.AllergenGroup ?? ""));
            var critical = items.Where(i => i.Severity == "critical").ToList();

            if (!report.Allowed || (critical.Count > 0 && outcome == "challenge"))
            {
                order.State = "CHALLENGED";
                order.Items.AddRange(items);
                order.Lines.Add(!report.Allowed ? Tables.Prerequisites.First(x => x.RejectReason == report.RejectReason).Meaning : ChallengeLine(critical.Count > 0 ? critical[0] : null, allergy));
                Record("challenged", order, items.Select(i => Score(i, true)).ToList(), order.AdministerAtStep);
                _s.Events.Publish(new MedicineEvent("challenged", def.Id, order.Id));
                _s.RequestAttention("nurse_challenge");
                return;
            }

            order.State = "ADMINISTERED";
            order.Items.AddRange(items);
            order.Lines.Add(Say("given", ("treatment", def.Label)));
            Clinical.SetFlag(Flag(def.Id, "given"));
            if (def.Kind == "procedure") { Clinical.SetFlag(IvInPlaceFlag); order.Lines.Add(Say("iv_in_place")); }
            Record("administered", order, items.Select(i => Score(i, false)).ToList(), order.AdministerAtStep);
            _s.Events.Publish(new MedicineEvent("administered", def.Id, order.Id));
            _s.Events.Publish(new TreatmentGiven(def.Id, def.Effects, order.Id));
            if (conflict)
            {
                // advanced setting (T-TR3): the allergen is given; the reaction becomes eligible (the event director draws it with the seeded stream)
                Clinical.SetFlag(ReactionEligibleFlag);
                _s.Events.Publish(new WorkflowEvent(Tables.Allergy.Event, "medicine_workflow"));
                _history.Add(new GraphEvent("allergen_given", Key(order), order.AdministerAtStep, new List<string> { "critical.treatment_allergy_conflict" }));
            }
        }

        // ---------------------------------------------------------------- what the checks read

        /// <summary>The facts the required checks read, from what the trainee has actually done. <paramref name="order"/> is the order being judged (null while it is only being requested).</summary>
        private TreatmentContext Context(TreatmentDef def, MedicineOrder? order)
        {
            var c = new TreatmentContext { IdentityConfirmed = Clinical.IdentityVerified, AllergiesKnown = Clinical.AllergiesKnown };
            if (order != null)
            {
                if (order.Indication.Length > 0) c.Indicated.Add(def.Id);
                if (order.State != "REQUESTED") c.Ordered.Add(def.Id);   // the clinician has confirmed it
            }
            if (Clinical.HasFlag(Flag(def.Id, "timing_checked"))) c.TimingChecked.Add(def.Id);
            if (Clinical.HasFlag(Flag(def.Id, "documented"))) c.Documented.Add(def.Id);
            foreach (var flag in Clinical.Flags)
                if (flag.StartsWith(ConditionPrefix, StringComparison.Ordinal)) c.Restrictions.Add(flag.Substring(ConditionPrefix.Length));
            if (Clinical.HasFlag(IvInPlaceFlag)) c.Done.Add("iv_access");
            return c;
        }

        private readonly struct AllergyHit
        {
            public AllergyHit(string text, bool severe) { Text = text; Severe = severe; }
            public string Text { get; }
            public bool Severe { get; }
        }

        /// <summary>The patient's allergy that names this treatment's group, if any: severe when the reaction recorded is an immediate severe one. Judged on the chart, not on what the trainee has asked.</summary>
        private AllergyHit? Allergy(TreatmentDef def)
        {
            if (def.AllergenGroup == null) return null;
            var group = Tables.Allergy.Groups.FirstOrDefault(g => g.Id == def.AllergenGroup);
            if (group == null) return null;
            AllergyHit? mild = null;
            foreach (var allergy in _s.Patient.Profile.Allergies)
            {
                string substance = allergy.Substance.ToLowerInvariant();
                if (!group.Keywords.Any(k => substance.Contains(k))) continue;
                string reaction = allergy.Reaction.ToLowerInvariant();
                var hit = new AllergyHit(allergy.Substance + ": " + allergy.Reaction, Tables.Allergy.SevereWords.Any(w => reaction.Contains(w)));
                if (hit.Severe) return hit;
                mild = hit;
            }
            return mild;
        }

        // ---------------------------------------------------------------- lines and records

        private string DoseLine(TreatmentDef def)
        {
            string label = Tables.DoseLabels.TryGetValue(def.DosePolicy, out var l) ? l : "";
            switch (def.DosePolicy)
            {
                case "EDUCATIONAL_EXAMPLE": return def.Dose == null ? label : label + ": " + def.Dose.Text + " (" + def.Dose.Source + ")";
                case "PATIENT_SPECIFIC": return label + ": the clinician decides";
                case "INSTITUTIONAL": return label;
                default: return "";
            }
        }

        private string ChallengeLine(CheckItem? first, AllergyHit? allergy)
        {
            if (first == null) return Say("challenge_allergy_unknown");
            switch (first.Check)
            {
                case "allergy_conflict": return Say("challenge_allergy_conflict", ("allergy", allergy?.Text ?? ""));
                case "restriction": return Say("challenge_restriction", ("condition", first.Reason.Replace('_', ' ')));
                default: return Say("challenge_allergy_unknown");
            }
        }

        private string Say(string line, params (string key, string value)[] fill)
        {
            if (!Tables.Lines.TryGetValue(line, out var text)) return "";
            foreach (var (key, value) in fill) text = text.Replace("{" + key + "}", value);
            return text;
        }

        /// <summary>A score item name for the scorer: severity (or "challenged" when the nurse caught it), then the check and its reason.</summary>
        private static string Score(CheckItem i, bool challenged) =>
            (challenged ? "challenged" : i.Severity) + ".treatment_" + i.Check + (i.Check == "restriction" || i.Check == "allergy_conflict" ? "." + i.Reason : "");

        private void Record(string kind, MedicineOrder order, IReadOnlyList<string> score, long step) => _history.Add(new GraphEvent(kind, Key(order), step, score));

        private static string Key(MedicineOrder o) => o.Treatment + "#" + o.Id.ToString(CultureInfo.InvariantCulture);

        private static string Flag(string treatment, string what) => "treatment." + treatment + "." + what;

        private static long Steps(double seconds) => (long)Math.Ceiling(seconds / SimClock.Step);
    }
}
