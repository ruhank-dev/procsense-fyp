using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using ProcSense.Core.Content;

namespace ProcSense.Core.Clinical
{
    /// <summary>One check a treatment can list: when it is judged (before the treatment is given or after) and what a missing or failed check costs (critical, major or minor).</summary>
    public sealed record CheckKind(string Id, string Phase, string Severity, string Meaning);

    /// <summary>Something that must already be in place (IV access) and the reason text the action is refused with when it is not.</summary>
    public sealed record Prerequisite(string Id, string RejectReason, string Meaning);

    /// <summary>A patient condition that restricts a treatment, and the register row that says so.</summary>
    public sealed record Restriction(string Tag, string Ref);

    /// <summary>
    /// A dose text shown beside a treatment, only where a named source gives a standard adult dose. It is never an instruction and never parsed. The reviewer is mandatory
    /// (rule DOSE_REQUIRES_REVIEWER).
    /// </summary>
    public sealed record DoseDisplay(string Text, string Source, string Reviewer);

    /// <summary>
    /// A treatment as content: a class of medicine, a fluid, oxygen or a procedure. It lists its own checks, what must be in place first, the conditions that restrict it and the
    /// physiology effect ids it causes. DosePolicy is EDUCATIONAL_EXAMPLE, PATIENT_SPECIFIC, INSTITUTIONAL or NONE; only EDUCATIONAL_EXAMPLE may carry a <see cref="Dose"/>.
    /// </summary>
    public sealed record TreatmentDef(string Id, string Kind, string Label, string? MedicineId, string DosePolicy, DoseDisplay? Dose, IReadOnlyList<string> Requires,
        IReadOnlyList<string> RequiredChecks, IReadOnlyList<Restriction> RestrictedBy, IReadOnlyList<string> Effects, IReadOnlyList<string> Refs, string? AllergenGroup = null);

    /// <summary>How long the clinician takes to confirm an order and the nurse to give it (design values from content, never from code).</summary>
    public sealed record WorkflowTiming(double ConfirmSeconds, double AdministerSeconds);

    /// <summary>An allergy to a whole group of medicines: the words in an allergy record that name the group (for example "penicillin" or "antibiotic").</summary>
    public sealed record AllergyGroup(string Id, IReadOnlyList<string> Keywords);

    /// <summary>
    /// How an allergy conflict is judged and what happens: the group words, the words that mean an immediate severe reaction, and the outcome per mode ("challenge" = the nurse refuses and says so,
    /// "reaction" = it is given, marked critical and the hypersensitivity event becomes eligible), plus the event name.
    /// </summary>
    public sealed record AllergyRules(IReadOnlyList<AllergyGroup> Groups, IReadOnlyList<string> SevereWords, string PracticeOutcome, string AdvancedOutcome, string AssessmentOutcome, string Event);

    /// <summary>The treatment library (content/treatments.json, built from the verified medicine table).</summary>
    public sealed class TreatmentTables
    {
        private static readonly Regex RefId = new Regex("^SR-[0-9]{4}$");
        private static readonly Regex EffectId = new Regex("^EFF-[A-Z0-9-]+$");
        private static readonly string[] Kinds = { "medicine", "fluid", "oxygen", "procedure" };
        private static readonly string[] Policies = { "EDUCATIONAL_EXAMPLE", "PATIENT_SPECIFIC", "INSTITUTIONAL", "NONE" };

        public static readonly TreatmentTables Empty = new TreatmentTables(new List<CheckKind>(), new List<Prerequisite>(), new List<string>(), new List<TreatmentDef>());

        public TreatmentTables(IReadOnlyList<CheckKind> checks, IReadOnlyList<Prerequisite> prerequisites, IReadOnlyList<string> effects, IReadOnlyList<TreatmentDef> treatments,
            WorkflowTiming? timing = null, IReadOnlyList<string>? oxygenDevices = null, AllergyRules? allergy = null, IReadOnlyDictionary<string, string>? doseLabels = null, IReadOnlyDictionary<string, string>? lines = null)
        {
            Checks = checks; Prerequisites = prerequisites; Effects = effects; Treatments = treatments;
            ById = treatments.ToDictionary(t => t.Id, StringComparer.Ordinal);
            Timing = timing ?? new WorkflowTiming(0, 0);
            OxygenDevices = oxygenDevices ?? new List<string>();
            Allergy = allergy ?? new AllergyRules(new List<AllergyGroup>(), new List<string>(), "challenge", "reaction", "reaction", "");
            DoseLabels = doseLabels ?? new Dictionary<string, string>();
            Lines = lines ?? new Dictionary<string, string>();
        }

        /// <summary>The clinician's and the nurse's delays (content/treatments.json, section workflow).</summary>
        public WorkflowTiming Timing { get; }
        /// <summary>The oxygen devices a trainee can ask for (nasal_cannula, simple_mask, non_rebreather_mask).</summary>
        public IReadOnlyList<string> OxygenDevices { get; }
        public AllergyRules Allergy { get; }
        /// <summary>The label shown beside a dose, by dose policy (empty text for NONE).</summary>
        public IReadOnlyDictionary<string, string> DoseLabels { get; }
        /// <summary>The approved lines the clinician and nurse say, by id, with {placeholders}.</summary>
        public IReadOnlyDictionary<string, string> Lines { get; }

        /// <summary>The checks a treatment can list, in the order they are reported.</summary>
        public IReadOnlyList<CheckKind> Checks { get; }
        public IReadOnlyList<Prerequisite> Prerequisites { get; }
        /// <summary>The physiology effect ids treatments may name (the physiology step defines what each one does).</summary>
        public IReadOnlyList<string> Effects { get; }
        public IReadOnlyList<TreatmentDef> Treatments { get; }
        public IReadOnlyDictionary<string, TreatmentDef> ById { get; }

        public static TreatmentTables Parse(string json, string source = "treatments")
        {
            var root = new JsonObj(MiniJson.Parse(json), source);

            var checks = new List<CheckKind>();
            var rawChecks = root.Arr("checks");
            for (int i = 0; i < rawChecks.Count; i++)
            {
                var o = new JsonObj(rawChecks[i], JsonObj.Index(root.Sub("checks"), i));
                var c = new CheckKind(o.Str("id"), o.Enum("phase", "before", "after"), o.Enum("severity", "critical", "major", "minor"), o.Str("meaning"));
                o.Finish();
                if (checks.Any(x => x.Id == c.Id)) throw new ContentException(root.Sub("checks") + ": duplicate check '" + c.Id + "'");
                checks.Add(c);
            }
            if (checks.Count == 0) throw new ContentException(root.Sub("checks") + ": needs at least one check");

            var prerequisites = new List<Prerequisite>();
            var rawPre = root.Arr("prerequisites");
            for (int i = 0; i < rawPre.Count; i++)
            {
                var o = new JsonObj(rawPre[i], JsonObj.Index(root.Sub("prerequisites"), i));
                var p = new Prerequisite(o.Str("id"), o.Str("reject"), o.Str("meaning"));
                o.Finish();
                if (prerequisites.Any(x => x.Id == p.Id)) throw new ContentException(root.Sub("prerequisites") + ": duplicate prerequisite '" + p.Id + "'");
                prerequisites.Add(p);
            }

            var effects = new List<string>();
            var rawEffects = root.Arr("effects");
            for (int i = 0; i < rawEffects.Count; i++)
            {
                var o = new JsonObj(rawEffects[i], JsonObj.Index(root.Sub("effects"), i));
                string id = o.Str("id");
                if (!EffectId.IsMatch(id)) throw new ContentException(o.Sub("id") + ": '" + id + "' is not an effect id like EFF-OXYGEN");
                if (effects.Contains(id)) throw new ContentException(root.Sub("effects") + ": duplicate effect '" + id + "'");
                o.Str("note"); o.Finish();
                effects.Add(id);
            }

            CheckRefs(root.Arr("refs"), root.Sub("refs"));

            var workflow = root.Obj("workflow");
            var timing = new WorkflowTiming(workflow.Num("confirm_seconds"), workflow.Num("administer_seconds"));
            CheckRef(workflow.Str("ref"), workflow.Sub("ref")); workflow.Finish();
            if (timing.ConfirmSeconds <= 0 || timing.AdministerSeconds <= 0) throw new ContentException(root.Sub("workflow") + ": the delays must be more than 0 seconds");

            var devices = new List<string>();
            var rawDevices = root.Arr("oxygen_devices");
            for (int i = 0; i < rawDevices.Count; i++)
            {
                var d = new JsonObj(rawDevices[i], JsonObj.Index(root.Sub("oxygen_devices"), i));
                string id = d.Str("id"); d.Str("label"); d.Finish();
                if (devices.Contains(id)) throw new ContentException(root.Sub("oxygen_devices") + ": duplicate device '" + id + "'");
                devices.Add(id);
            }

            var allergy = ParseAllergy(root.Obj("allergy"));

            var doseLabels = new Dictionary<string, string>();
            var labelsObj = root.Obj("dose_labels");
            foreach (var policy in Policies)
            {
                var l = labelsObj.Obj(policy);
                doseLabels[policy] = l.Str("text"); CheckRef(l.Str("ref"), l.Sub("ref")); l.Finish();
            }
            labelsObj.Finish();

            var lines = new Dictionary<string, string>();
            var rawLines = root.Arr("lines");
            for (int i = 0; i < rawLines.Count; i++)
            {
                var l = new JsonObj(rawLines[i], JsonObj.Index(root.Sub("lines"), i));
                string id = l.Str("id"), text = l.Str("text"); l.Finish();
                if (lines.ContainsKey(id)) throw new ContentException(root.Sub("lines") + ": duplicate line '" + id + "'");
                lines[id] = text;
            }

            var treatments = new List<TreatmentDef>();
            var rawTreatments = root.Arr("treatments");
            for (int i = 0; i < rawTreatments.Count; i++)
            {
                var t = ParseTreatment(new JsonObj(rawTreatments[i], JsonObj.Index(root.Sub("treatments"), i)), checks, prerequisites, effects);
                if (treatments.Any(x => x.Id == t.Id)) throw new ContentException(root.Sub("treatments") + ": duplicate treatment '" + t.Id + "'");
                treatments.Add(t);
            }
            foreach (var t in treatments)
                if (t.AllergenGroup != null && !allergy.Groups.Any(g => g.Id == t.AllergenGroup))
                    throw new ContentException(root.Sub("treatments") + ": " + t.Id + " names the allergy group '" + t.AllergenGroup + "', which is not defined");
            foreach (var needed in new[] { "confirm", "given", "challenge_allergy_conflict", "challenge_allergy_unknown", "challenge_restriction", "iv_in_place" })
                if (!lines.ContainsKey(needed)) throw new ContentException(root.Sub("lines") + ": the line '" + needed + "' is missing");
            root.Finish();
            return new TreatmentTables(checks, prerequisites, effects, treatments, timing, devices, allergy, doseLabels, lines);
        }

        private static AllergyRules ParseAllergy(JsonObj o)
        {
            var groups = new List<AllergyGroup>();
            var rawGroups = o.Arr("groups");
            for (int i = 0; i < rawGroups.Count; i++)
            {
                var g = new JsonObj(rawGroups[i], JsonObj.Index(o.Sub("groups"), i));
                var keywords = Strings(g, "keywords");
                if (keywords.Count == 0) throw new ContentException(g.Sub("keywords") + ": needs at least one word");
                groups.Add(new AllergyGroup(g.Str("id"), keywords.Select(k => k.ToLowerInvariant()).ToList()));
                g.Finish();
            }
            var severe = Strings(o, "severe_reaction_words").Select(w => w.ToLowerInvariant()).ToList();
            if (severe.Count == 0) throw new ContentException(o.Sub("severe_reaction_words") + ": needs at least one word");
            var outcome = o.Obj("outcome");
            string practice = outcome.Enum("practice", "challenge", "reaction"), advanced = outcome.Enum("advanced", "challenge", "reaction"), assessment = outcome.Enum("assessment", "challenge", "reaction");
            outcome.Finish();
            string ev = o.Str("event");
            foreach (var r in o.Arr("refs")) CheckRef(r as string ?? "", o.Sub("refs"));
            o.Finish();
            return new AllergyRules(groups, severe, practice, advanced, assessment, ev);
        }

        private static TreatmentDef ParseTreatment(JsonObj o, List<CheckKind> checks, List<Prerequisite> prerequisites, List<string> effects)
        {
            string id = o.Str("id");
            string where = o.Sub("id") + " " + id;
            string kind = o.Enum("kind", Kinds);
            string label = o.Str("label");

            string? medicineId = null;
            var medicine = o.ObjOrNull("medicine");
            if (medicine != null)
            {
                medicineId = medicine.Str("id");
                CheckRef(medicine.Str("ref"), medicine.Sub("ref"));
                medicine.Finish();
            }
            else if (kind == "medicine") throw new ContentException(where + ": a medicine treatment must name its row in the medicine table");

            string policy = o.Enum("dose_policy", Policies);
            DoseDisplay? dose = null;
            var doseObj = o.ObjOrNull("dose_display");
            if (doseObj != null)
            {
                dose = new DoseDisplay(doseObj.Str("text"), doseObj.Str("source"), doseObj.Str("reviewer"));
                doseObj.Finish();
                // DOSE_REQUIRES_REVIEWER: a dose text with no named reviewer or no source is a content error, and so is a dose on a row whose policy forbids one.
                if (dose.Reviewer.Trim().Length == 0) throw new ContentException(where + ": DOSE_REQUIRES_REVIEWER: the dose text has no reviewer");
                if (dose.Source.Trim().Length == 0) throw new ContentException(where + ": DOSE_REQUIRES_REVIEWER: the dose text has no source");
                if (dose.Text.Trim().Length == 0) throw new ContentException(where + ": the dose text is empty");
                if (policy != "EDUCATIONAL_EXAMPLE") throw new ContentException(where + ": only an EDUCATIONAL_EXAMPLE treatment may show a dose (policy is " + policy + ")");
            }
            else if (policy == "EDUCATIONAL_EXAMPLE") throw new ContentException(where + ": an EDUCATIONAL_EXAMPLE treatment must carry its dose text, source and reviewer");

            var requires = Strings(o, "requires");
            foreach (var r in requires)
                if (!prerequisites.Any(p => p.Id == r)) throw new ContentException(where + ": requires '" + r + "', which is not a known prerequisite");

            var required = Strings(o, "required_checks");
            if (required.Count == 0) throw new ContentException(where + ": must list its required checks (none are implied)");
            foreach (var c in required)
                if (!checks.Any(k => k.Id == c)) throw new ContentException(where + ": the check '" + c + "' is not in the check list");
            if (required.Distinct().Count() != required.Count) throw new ContentException(where + ": lists a check twice");
            // The checks are reported in the order of the check list, so the file must keep that order.
            var ordered = required.OrderBy(c => checks.FindIndex(k => k.Id == c)).ToList();
            if (!ordered.SequenceEqual(required)) throw new ContentException(where + ": required_checks must follow the order of the check list");

            var restrictions = new List<Restriction>();
            var rawRestrictions = o.Arr("restricted_by");
            for (int i = 0; i < rawRestrictions.Count; i++)
            {
                var r = new JsonObj(rawRestrictions[i], JsonObj.Index(o.Sub("restricted_by"), i));
                var restriction = new Restriction(r.Str("tag"), r.Str("ref"));
                CheckRef(restriction.Ref, r.Sub("ref"));
                r.Finish();
                restrictions.Add(restriction);
            }
            if (required.Contains("restriction") && restrictions.Count == 0) throw new ContentException(where + ": lists the restriction check but names no restriction");
            if (!required.Contains("restriction") && restrictions.Count > 0) throw new ContentException(where + ": names restrictions but does not list the restriction check");

            var effectIds = Strings(o, "effects");
            foreach (var e in effectIds)
                if (!effects.Contains(e)) throw new ContentException(where + ": the effect '" + e + "' is not in the effect list");

            var labelRow = o.ObjOrNull("label_row"); // the label data row reviewers read (never a dose)
            if (labelRow != null) { labelRow.Str("id"); CheckRef(labelRow.Str("ref"), labelRow.Sub("ref")); labelRow.Finish(); }

            string? allergenGroup = o.Any("allergen_group") as string;   // null in the file means this treatment is not tied to an allergy group

            var refs = Strings(o, "refs");
            if (refs.Count == 0) throw new ContentException(where + ": needs a source reference");
            foreach (var r in refs) CheckRef(r, o.Sub("refs"));
            o.Finish();
            return new TreatmentDef(id, kind, label, medicineId, policy, dose, requires, required, restrictions, effectIds, refs, allergenGroup);
        }

        private static List<string> Strings(JsonObj o, string key)
        {
            var list = new List<string>();
            foreach (var item in o.Arr(key)) list.Add(item as string ?? throw new ContentException(o.Sub(key) + ": expected a list of text"));
            return list;
        }

        private static void CheckRefs(List<object?> refs, string path)
        {
            foreach (var r in refs) CheckRef(r as string ?? "", path);
        }

        private static void CheckRef(string value, string path)
        {
            if (!RefId.IsMatch(value)) throw new ContentException(path + ": '" + value + "' is not a register id like SR-0001");
        }
    }
}
