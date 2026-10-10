using System.Collections.Generic;
using System.Linq;
using System.Text;
using ProcSense.Core.Content;
using ProcSense.Core.Logging;
using ProcSense.Core.Workflow;

namespace ProcSense.Core.Observation
{
    public sealed record DisclosedFact(string Topic, string Answer);

    /// <summary>One value on the monitor or chart, with the clock time it was read.</summary>
    public sealed record ObservedVital(string Name, string Value, string At);

    /// <summary>One goal as the trainee's "known / pending" panel shows it. State is pending, complete or missed; At is the clock time it finished ("" while pending).</summary>
    public sealed record GoalStatus(string Id, string Label, string State, string At);

    /// <summary>One stage of the scenario map as the tablet shows it: state is active, suspended (stopped by an emergency), done or skipped.</summary>
    public sealed record FlowStageStatus(string Id, string Title, string Track, string State);

    /// <summary>One examination finding the trainee has found (region and wording), and one airway check with what it showed.</summary>
    public sealed record ObservedFinding(string Region, string Text);
    public sealed record ObservedAirway(string Item, string Text);

    /// <summary>The fasting chip: unknown until both food and drink are asked, then meets, breach or uncertain; SafeFrom is the clock time the rule will be met ("" when it already is); Plan is "rsi" for an emergency patient with a full stomach.</summary>
    public sealed record FastingChip(string Status, string SafeFrom, string Plan);

    /// <summary>
    /// What Step 5 shows: the grade the trainee proposed, the chip comparing it with the case (credit, minor or major; practice mode only), the clot form result (plan_required, no_plan or ""),
    /// the scores calculated so far, the tests the tablet suggests for a missing score item, what the anaesthetist has said, the agreed plan, the readiness decision and whether readiness is blocked.
    /// </summary>
    public sealed record ObservedRisk(string Asa, string AsaChip, string Vte, IReadOnlyList<string> Scores, IReadOnlyList<string> Prompts, IReadOnlyList<string> AnaesthetistLines, string Plan, string Decision, bool Blocked);

    public sealed record DecisionOption(string Id, string Label);

    /// <summary>A decision the trainee has to make. The expected option is never part of the card.</summary>
    public sealed record DecisionCard(string Id, string Label, IReadOnlyList<DecisionOption> Options);

    /// <summary>
    /// Read-only snapshot of what the trainee can perceive right now. Unity and AI read only this, never PatientState,
    /// so undisclosed facts cannot leak. Vitals appear only for attached sensors and readings the trainee has taken.
    /// </summary>
    public sealed record ObservableState(
        string ClockTime, string Location, bool IdentityConfirmed, bool AllergiesKnown,
        Wristband? Wristband, NotesIdentifiers? Notes, IReadOnlyList<DisclosedFact> DisclosedFacts, IReadOnlyList<ObservedVital> Vitals, string Nurse, bool AllergyBand, string LastRejection,
        IReadOnlyList<GoalStatus> Goals, IReadOnlyList<DecisionCard> Decisions, string Hint, string BlockMessage, IReadOnlyList<FlowStageStatus> Flow,
        IReadOnlyList<ObservedFinding> Findings, IReadOnlyList<ObservedAirway> Airway, FastingChip Fasting, ObservedRisk Risk)
    {
        /// <summary>Canonical JSON (fixed key order) so its hash is the same on every machine.</summary>
        public string ToJson()
        {
            var sb = new StringBuilder();
            sb.Append("{\"clock\":"); JsonText.Quote(sb, ClockTime);
            sb.Append(",\"location\":"); JsonText.Quote(sb, Location);
            sb.Append(",\"identity_confirmed\":").Append(IdentityConfirmed ? "true" : "false");
            sb.Append(",\"allergies_known\":").Append(AllergiesKnown ? "true" : "false");
            sb.Append(",\"wristband\":");
            if (Wristband == null) sb.Append("null");
            else
            {
                sb.Append("{\"name\":"); JsonText.Quote(sb, Wristband.NameText);
                sb.Append(",\"dob\":"); JsonText.Quote(sb, Wristband.DobText);
                sb.Append(",\"mrn\":"); JsonText.Quote(sb, Wristband.MrnText);
                sb.Append('}');
            }
            sb.Append(",\"notes\":");
            if (Notes == null) sb.Append("null");
            else
            {
                sb.Append("{\"name\":"); JsonText.Quote(sb, Notes.FullName);
                sb.Append(",\"dob\":"); JsonText.Quote(sb, Notes.Dob);
                sb.Append(",\"mrn\":"); JsonText.Quote(sb, Notes.Mrn);
                sb.Append('}');
            }
            sb.Append(",\"disclosed\":[");
            for (int i = 0; i < DisclosedFacts.Count; i++)
            {
                if (i > 0) sb.Append(',');
                sb.Append("{\"topic\":"); JsonText.Quote(sb, DisclosedFacts[i].Topic);
                sb.Append(",\"answer\":"); JsonText.Quote(sb, DisclosedFacts[i].Answer);
                sb.Append('}');
            }
            sb.Append("],\"vitals\":[");
            for (int i = 0; i < Vitals.Count; i++)
            {
                if (i > 0) sb.Append(',');
                sb.Append("{\"name\":"); JsonText.Quote(sb, Vitals[i].Name);
                sb.Append(",\"value\":"); JsonText.Quote(sb, Vitals[i].Value);
                sb.Append(",\"at\":"); JsonText.Quote(sb, Vitals[i].At);
                sb.Append('}');
            }
            sb.Append("],\"nurse\":"); JsonText.Quote(sb, Nurse);
            sb.Append(",\"allergy_band\":").Append(AllergyBand ? "true" : "false");
            sb.Append(",\"last_rejection\":"); JsonText.Quote(sb, LastRejection);
            sb.Append(",\"goals\":[");
            for (int i = 0; i < Goals.Count; i++)
            {
                if (i > 0) sb.Append(',');
                sb.Append("{\"id\":"); JsonText.Quote(sb, Goals[i].Id);
                sb.Append(",\"label\":"); JsonText.Quote(sb, Goals[i].Label);
                sb.Append(",\"state\":"); JsonText.Quote(sb, Goals[i].State);
                sb.Append(",\"at\":"); JsonText.Quote(sb, Goals[i].At);
                sb.Append('}');
            }
            sb.Append("],\"decisions\":[");
            for (int i = 0; i < Decisions.Count; i++)
            {
                if (i > 0) sb.Append(',');
                sb.Append("{\"id\":"); JsonText.Quote(sb, Decisions[i].Id);
                sb.Append(",\"label\":"); JsonText.Quote(sb, Decisions[i].Label);
                sb.Append(",\"options\":[");
                for (int j = 0; j < Decisions[i].Options.Count; j++)
                {
                    if (j > 0) sb.Append(',');
                    sb.Append("{\"id\":"); JsonText.Quote(sb, Decisions[i].Options[j].Id);
                    sb.Append(",\"label\":"); JsonText.Quote(sb, Decisions[i].Options[j].Label);
                    sb.Append('}');
                }
                sb.Append("]}");
            }
            sb.Append("],\"hint\":"); JsonText.Quote(sb, Hint);
            sb.Append(",\"block_message\":"); JsonText.Quote(sb, BlockMessage);
            sb.Append(",\"flow\":[");
            for (int i = 0; i < Flow.Count; i++)
            {
                if (i > 0) sb.Append(',');
                sb.Append("{\"id\":"); JsonText.Quote(sb, Flow[i].Id);
                sb.Append(",\"title\":"); JsonText.Quote(sb, Flow[i].Title);
                sb.Append(",\"track\":"); JsonText.Quote(sb, Flow[i].Track);
                sb.Append(",\"state\":"); JsonText.Quote(sb, Flow[i].State);
                sb.Append('}');
            }
            sb.Append(']');
            sb.Append(",\"findings\":[");
            for (int i = 0; i < Findings.Count; i++)
            {
                if (i > 0) sb.Append(',');
                sb.Append("{\"region\":"); JsonText.Quote(sb, Findings[i].Region);
                sb.Append(",\"text\":"); JsonText.Quote(sb, Findings[i].Text);
                sb.Append('}');
            }
            sb.Append("],\"airway\":[");
            for (int i = 0; i < Airway.Count; i++)
            {
                if (i > 0) sb.Append(',');
                sb.Append("{\"item\":"); JsonText.Quote(sb, Airway[i].Item);
                sb.Append(",\"text\":"); JsonText.Quote(sb, Airway[i].Text);
                sb.Append('}');
            }
            sb.Append("],\"fasting\":{\"status\":"); JsonText.Quote(sb, Fasting.Status);
            sb.Append(",\"safe_from\":"); JsonText.Quote(sb, Fasting.SafeFrom);
            sb.Append(",\"plan\":"); JsonText.Quote(sb, Fasting.Plan);
            sb.Append('}');
            sb.Append(",\"risk\":{\"asa\":"); JsonText.Quote(sb, Risk.Asa);
            sb.Append(",\"asa_chip\":"); JsonText.Quote(sb, Risk.AsaChip);
            sb.Append(",\"vte\":"); JsonText.Quote(sb, Risk.Vte);
            sb.Append(",\"scores\":"); AppendList(sb, Risk.Scores);
            sb.Append(",\"prompts\":"); AppendList(sb, Risk.Prompts);
            sb.Append(",\"anaesthetist\":"); AppendList(sb, Risk.AnaesthetistLines);
            sb.Append(",\"plan\":"); JsonText.Quote(sb, Risk.Plan);
            sb.Append(",\"decision\":"); JsonText.Quote(sb, Risk.Decision);
            sb.Append(",\"blocked\":").Append(Risk.Blocked ? "true" : "false");
            sb.Append('}');
            return sb.Append('}').ToString();
        }

        private static void AppendList(StringBuilder sb, IReadOnlyList<string> items)
        {
            sb.Append('[');
            for (int i = 0; i < items.Count; i++)
            {
                if (i > 0) sb.Append(',');
                JsonText.Quote(sb, items[i]);
            }
            sb.Append(']');
        }

        public string Hash() => JsonText.Sha256Hex(ToJson());
    }

    public static class ObservableBuilder
    {
        private static readonly string[] VitalOrder =
            { "spo2", "oxygen", "heart_rate", "systolic_bp", "diastolic_bp", "respiratory_rate", "temperature_c", "acvpu", "pain_score" };

        // Only what the trainee has gathered: live values while the SpO2 probe is on, and the readings taken so far.
        private static List<ObservedVital> BuildVitals(Session s)
        {
            var p = s.Patient.Physiological;
            var v = p.Vitals;
            string now = At(s, s.Clock.Steps);
            var live = new Dictionary<string, string>();
            if (p.IsAttached(Workflow.Steps.ObservationsSubsystem.Spo2Probe))
            {
                live["spo2"] = Num(v.Spo2);
                live["oxygen"] = v.Oxygen;
                live["heart_rate"] = Num(v.HeartRate);
            }
            var list = new List<ObservedVital>();
            foreach (var name in VitalOrder)
            {
                if (live.TryGetValue(name, out var value)) list.Add(new ObservedVital(name, value, now));
                else if (p.Measured.TryGetValue(name, out var m)) list.Add(new ObservedVital(name, m.Value, At(s, m.AtStep)));
            }
            return list;
        }

        private static string At(Session s, long step) => Hms(s.Clock.Start + System.TimeSpan.FromSeconds(step * Time.SimClock.Step));

        /// <summary>HH:MM:SS, with one decimal only when the time falls on a half second ("08:00:44.5").</summary>
        public static string Hms(System.TimeSpan t) =>
            t.Milliseconds == 0 ? t.ToString(@"hh\:mm\:ss", System.Globalization.CultureInfo.InvariantCulture) : t.ToString(@"hh\:mm\:ss\.f", System.Globalization.CultureInfo.InvariantCulture);

        public static string Num(double x) => x.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture);

        // What the trainee has found: region findings in the order they were found, and the airway checks by item.
        private static List<ObservedFinding> BuildFindings(State.ClinicalLayer c)
        {
            var list = new List<ObservedFinding>();
            foreach (var f in c.Findings)
                if (!f.Region.StartsWith(RuleState.AirwayRegion, System.StringComparison.Ordinal)) list.Add(new ObservedFinding(f.Region, f.Text));
            return list;
        }

        private static ObservedRisk BuildRisk(State.RiskState r, bool practice) => new ObservedRisk(
            r.ProposedAsa.HasValue ? r.ProposedAsa.Value.ToString(System.Globalization.CultureInfo.InvariantCulture) + (r.ProposedEmergency ? "E" : "") : "",
            practice ? r.AsaClass : "", r.Vte, r.Scores.Select(kv => kv.Key + ": " + kv.Value).ToList(), r.Prompts.ToList(), r.AnaesthetistLines.ToList(), r.Plan, r.Decision, r.Blocked);

        private static List<ObservedAirway> BuildAirway(State.ClinicalLayer c)
        {
            var list = new List<ObservedAirway>();
            foreach (var kv in c.Airway) list.Add(new ObservedAirway(kv.Key, kv.Value));
            return list;
        }

        private static string BlockMessage(Session s)
        {
            if (!s.LastRejection.StartsWith(Workflow.ReadinessSubsystem.BlockedPrefix, System.StringComparison.Ordinal)) return "";
            string id = s.LastRejection.Substring(Workflow.ReadinessSubsystem.BlockedPrefix.Length);
            foreach (var b in s.Graph.Blockers) if (b.Id == id) return b.Message;
            return "";
        }

        public static ObservableState Build(Session s)
        {
            var c = s.Patient.Clinical;
            var facts = new List<DisclosedFact>();
            foreach (var topic in c.RevealOrder) // the clerking note: oldest line first
            {
                string? answer = PatientAnswers.For(s.Case, topic);
                if (answer == null)
                    foreach (var f in s.Patient.History)
                        if (f.Topic == topic) { answer = f.Answer; break; }
                if (answer != null) facts.Add(new DisclosedFact(topic, answer));
            }

            bool practice = s.Mode == SessionMode.Practice;   // assessment mode shows no goals and no hints: nothing tells the trainee what is left
            return new ObservableState(
                Hms(s.Clock.ClockTime),
                c.Location, c.IdentityVerified, c.AllergiesKnown,
                c.WristbandRead ? c.CurrentWristband : null,
                c.NotesOpened ? s.Patient.Profile.Notes : null,
                facts, BuildVitals(s), c.NurseState, c.AllergyBand, s.LastRejection,
                practice && s.Tracker != null ? s.Tracker.GoalStatuses() : System.Array.Empty<GoalStatus>(),
                s.Tracker != null ? s.Tracker.OpenCards() : System.Array.Empty<DecisionCard>(),
                practice && s.Tracker != null ? s.Tracker.Hint() : "",
                practice ? BlockMessage(s) : "",
                practice && s.Flow != null ? s.Flow.Running() : System.Array.Empty<FlowStageStatus>(),
                BuildFindings(c), BuildAirway(c), new FastingChip(c.Fasting?.Status ?? "unknown", c.FastingSafeFrom, c.Fasting?.Plan ?? ""), BuildRisk(c.Risk, practice));
        }
    }
}
