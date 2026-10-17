using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using ProcSense.Core.Content;
using ProcSense.Core.Logging;

namespace ProcSense.Core.Twin
{
    /// <summary>
    /// What a patient carries from one encounter into the next (p2-ai-advanced-simulation-s02, feature E48): the case, the clock time, the vital signs, the facts the engine has set about the patient
    /// (flags), the behaviour attributes, the three behaviour counters and the treatments that were given. Nothing else: the twin is kept to the systems the case already has, because a twin that tries to
    /// carry every subsystem's private state explodes (the step's own warning). Workflow progress (which checklist item is done, which event fired) is NOT carried: a follow-up encounter is a new
    /// encounter with the same patient, not a pause of the old one.
    /// </summary>
    public sealed record TwinSnapshot(
        int Version, string CaseId, string ClockTime, Vitals Vitals, IReadOnlyList<string> Flags, IReadOnlyDictionary<string, double> Behaviour,
        int Trust, int Anxiety, int Understanding, IReadOnlyList<string> Treatments)
    {
        public const int CurrentVersion = 1;

        /// <summary>Canonical JSON (keys in a fixed order, numbers in the round-trip format), the same text for the same state.</summary>
        public string ToJson()
        {
            var sb = new StringBuilder("{\"version\":").Append(Version.ToString(CultureInfo.InvariantCulture));
            sb.Append(",\"case\":"); JsonText.Quote(sb, CaseId);
            sb.Append(",\"clock\":"); JsonText.Quote(sb, ClockTime);
            sb.Append(",\"vitals\":{\"hr\":").Append(Num(Vitals.HeartRate)).Append(",\"rhythm\":"); JsonText.Quote(sb, Vitals.HeartRhythm);
            sb.Append(",\"sbp\":").Append(Num(Vitals.SystolicBp)).Append(",\"dbp\":").Append(Num(Vitals.DiastolicBp)).Append(",\"rr\":").Append(Num(Vitals.RespiratoryRate)).Append(",\"spo2\":").Append(Num(Vitals.Spo2));
            sb.Append(",\"oxygen\":"); JsonText.Quote(sb, Vitals.Oxygen);
            sb.Append(",\"temp\":").Append(Num(Vitals.TemperatureC)).Append(",\"acvpu\":"); JsonText.Quote(sb, Vitals.Acvpu);
            sb.Append(",\"pain\":").Append(Vitals.PainScore.ToString(CultureInfo.InvariantCulture)).Append('}');
            sb.Append(",\"flags\":["); Join(sb, Flags); sb.Append(']');
            sb.Append(",\"behaviour\":{");
            bool first = true;
            foreach (var kv in Behaviour.OrderBy(k => k.Key, StringComparer.Ordinal)) { if (!first) sb.Append(','); first = false; JsonText.Quote(sb, kv.Key); sb.Append(':').Append(Num(kv.Value)); }
            sb.Append("},\"counters\":{\"trust\":").Append(Trust.ToString(CultureInfo.InvariantCulture)).Append(",\"anxiety\":").Append(Anxiety.ToString(CultureInfo.InvariantCulture)).Append(",\"understanding\":").Append(Understanding.ToString(CultureInfo.InvariantCulture));
            sb.Append("},\"treatments\":["); Join(sb, Treatments); sb.Append("]}");
            return sb.ToString();
        }

        /// <summary>The fingerprint of the snapshot; a follow-up session is replayed with the snapshot of this hash.</summary>
        public string Hash() => JsonText.Sha256Hex(ToJson());

        public static TwinSnapshot FromJson(string json)
        {
            var o = new JsonObj(MiniJson.Parse(json), "twin snapshot");
            int version = (int)o.Int("version");
            if (version != CurrentVersion) throw new ContentException("twin snapshot version " + version.ToString(CultureInfo.InvariantCulture) + " is not supported (this engine reads version " + CurrentVersion.ToString(CultureInfo.InvariantCulture) + ")");
            var v = o.Obj("vitals");
            var vitals = new Vitals(v.Num("hr"), v.Str("rhythm"), v.Num("sbp"), v.Num("dbp"), v.Num("rr"), v.Num("spo2"), v.Str("oxygen"), v.Num("temp"), v.Str("acvpu"), v.Int("pain"));
            var behaviour = new SortedDictionary<string, double>(StringComparer.Ordinal);
            foreach (var kv in (Dictionary<string, object?>)o.Any("behaviour")!)
                behaviour[kv.Key] = kv.Value is long l ? l : kv.Value is double d ? d : throw new ContentException("twin snapshot: behaviour." + kv.Key + " is not a number");
            var c = o.Obj("counters");
            var snapshot = new TwinSnapshot(version, o.Str("case"), o.Str("clock"), vitals, o.Arr("flags").Select(x => (string)x!).ToList(), behaviour, (int)c.Int("trust"), (int)c.Int("anxiety"), (int)c.Int("understanding"),
                                    o.Arr("treatments").Select(x => (string)x!).ToList());
            v.Finish(); c.Finish(); o.Finish();                                       // an unknown field is an error, not something to skip
            return snapshot;
        }

        private static string Num(double d) => d.ToString("R", CultureInfo.InvariantCulture);

        private static void Join(StringBuilder sb, IEnumerable<string> items)
        {
            bool first = true;
            foreach (var s in items) { if (!first) sb.Append(','); first = false; JsonText.Quote(sb, s); }
        }
    }

    /// <summary>Captures a patient at the end of an encounter and starts the next encounter from it. Nothing here changes how a session without a twin runs, so the golden runs are untouched.</summary>
    public static class PatientTwin
    {
        public const string CarriedFlag = "twin.carried";
        public const string PriorTreatmentPrefix = "twin.prior_treatment.";

        /// <summary>The state to carry: what the session ends with.</summary>
        public static TwinSnapshot Capture(Session s)
        {
            var p = s.Patient;
            var behaviour = new SortedDictionary<string, double>(StringComparer.Ordinal);
            if (s.Behaviour != null) foreach (var kv in s.Behaviour.State.Values) behaviour[kv.Key] = kv.Value;
            var treatments = p.Clinical.Orders.Where(o => o.State == "ADMINISTERED").Select(o => o.Treatment)
                .Concat(p.Clinical.Flags.Where(f => f.StartsWith(PriorTreatmentPrefix, StringComparison.Ordinal)).Select(f => f.Substring(PriorTreatmentPrefix.Length)))         // treatments of earlier encounters stay in the record
                .Distinct(StringComparer.Ordinal).OrderBy(x => x, StringComparer.Ordinal).ToList();
            var flags = p.Clinical.Flags.Where(f => f != CarriedFlag && !f.StartsWith(PriorTreatmentPrefix, StringComparison.Ordinal)).ToList();         // already sorted (ordinal)
            return new TwinSnapshot(TwinSnapshot.CurrentVersion, s.Case.Id, s.Clock.ClockTime.ToString(@"hh\:mm\:ss", CultureInfo.InvariantCulture), p.Physiological.Vitals, flags, behaviour,
                                    p.Behavioural.Trust, p.Behavioural.Anxiety, p.Behavioural.Understanding, treatments);
        }

        /// <summary>The card for the follow-up encounter: the same patient, starting at the carried clock time with the carried vital signs. The physiology starts from the card, so the carried vitals are the ones it integrates from.</summary>
        public static CaseCard FollowUp(CaseCard card, TwinSnapshot t)
        {
            if (t.CaseId != card.Id) throw new ContentException("the twin snapshot is of case " + t.CaseId + ", not " + card.Id);
            if (!TimeSpan.TryParseExact(t.ClockTime, @"hh\:mm\:ss", CultureInfo.InvariantCulture, out var at)) throw new ContentException("the twin clock time '" + t.ClockTime + "' is not hh:mm:ss");
            return card with { Timing = card.Timing with { TimeNow = at }, BaselineVitals = t.Vitals };
        }

        /// <summary>Puts the rest of the carried state into a session built from <see cref="FollowUp"/> before any action: the flags, the behaviour attributes and counters, and a flag for each treatment already given.</summary>
        public static void Apply(Session s, TwinSnapshot t)
        {
            if (t.CaseId != s.Case.Id) throw new ContentException("the twin snapshot is of case " + t.CaseId + ", not " + s.Case.Id);
            if (s.Log.Count > 0) throw new ContentException("a twin can only be applied to a session before its first action");
            foreach (var f in t.Flags) s.Patient.Clinical.SetFlag(f);
            foreach (var tr in t.Treatments) s.Patient.Clinical.SetFlag(PriorTreatmentPrefix + tr);
            s.Patient.Clinical.SetFlag(CarriedFlag);
            s.Patient.Behavioural.Trust = t.Trust;
            s.Patient.Behavioural.Anxiety = t.Anxiety;
            s.Patient.Behavioural.Understanding = t.Understanding;
            if (s.Behaviour != null) foreach (var kv in t.Behaviour) if (s.Behaviour.State.Values.ContainsKey(kv.Key)) s.Behaviour.State.Set(kv.Key, kv.Value);
        }
    }
}
