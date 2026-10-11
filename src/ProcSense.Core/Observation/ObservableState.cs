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
    /// <summary>One NEWS2 on the chart: when it was worked out, the total, whether a single reading scored 3, the response tier and what to do (frequency and who responds), and a note (for example that NEWS2 is not used in pregnancy).</summary>
    /// <summary>A transfer as the trainee sees it: state, destination, how the patient goes, with whom and how monitored, when it arrives, and a message (for example that the porter is unavailable).</summary>
    public sealed record ObservedTransport(string State, string To, string Mode, IReadOnlyList<string> Escort, string Monitoring, bool Oxygen, string Arrives, string Message);

    /// <summary>A test the trainee ordered: its name, how many times it has been ordered, the state, when it is expected (once the sample is taken or the patient is in the scan room), what the indication rule said (practice mode only) and, once the result has been viewed, which results it holds.</summary>
    /// <summary>The report of an imaging test as the trainee reads it: the text, the measurements stated in it and the approved picture; the engine's finding code and category are never shown.</summary>
    public sealed record ObservedReport(string Text, string ImageId, IReadOnlyList<string> Measurements, bool Synthetic);

    public sealed record ObservedResult(string Id, string Analyte, string Value, string Unit, string Range, string Flag, string Comment, string Quality, string Origin);

    public sealed record ObservedInvestigation(string Test, string Label, int Instance, string State, string Ready, string Indication, IReadOnlyList<ObservedResult> Results, string Note, ObservedReport? Report);

    /// <summary>The lab phone: empty state when no call is waiting; RINGING (nothing is said until it is answered), then the caller's words and how far the call has got.</summary>
    public sealed record ObservedPhone(string State, string Message);

    /// <summary>A treatment the trainee asked for: its state (REQUESTED, CONFIRMED, ADMINISTERED, CHALLENGED), what the clinician and the nurse said, and the dose line once the clinician has confirmed (only under its dose policy).</summary>
    public sealed record ObservedOrStation(string Id, string State);

    public sealed record ObservedChecklist(IReadOnlyList<ObservedOrStation> Phases, IReadOnlyList<ObservedOrStation> Items);

    public sealed record ObservedOrder(int Id, string Treatment, string Label, string State, string Device, string Dose, IReadOnlyList<string> Lines);

    public sealed record ObservedNews2(string At, int Total, bool Single3, string Tier, string Frequency, string Responder, string Note);

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
        IReadOnlyList<ObservedFinding> Findings, IReadOnlyList<ObservedAirway> Airway, FastingChip Fasting, ObservedRisk Risk, IReadOnlyList<ObservedNews2> News2, string TraineeLocation, ObservedTransport? Transport, IReadOnlyList<ObservedInvestigation> Investigations, ObservedPhone Phone, IReadOnlyList<ObservedOrder>? Orders = null, IReadOnlyList<ObservedOrStation>? OrStations = null, ObservedChecklist? Checklist = null, string? BehaviourMode = null)
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
            sb.Append(",\"news2\":[");
            for (int i = 0; i < News2.Count; i++)
            {
                if (i > 0) sb.Append(',');
                sb.Append("{\"at\":"); JsonText.Quote(sb, News2[i].At);
                sb.Append(",\"total\":").Append(News2[i].Total.ToString(System.Globalization.CultureInfo.InvariantCulture));
                sb.Append(",\"single3\":").Append(News2[i].Single3 ? "true" : "false");
                sb.Append(",\"tier\":"); JsonText.Quote(sb, News2[i].Tier);
                sb.Append(",\"frequency\":"); JsonText.Quote(sb, News2[i].Frequency);
                sb.Append(",\"responder\":"); JsonText.Quote(sb, News2[i].Responder);
                sb.Append(",\"note\":"); JsonText.Quote(sb, News2[i].Note);
                sb.Append('}');
            }
            sb.Append(']');
            sb.Append(",\"trainee_location\":"); JsonText.Quote(sb, TraineeLocation);
            sb.Append(",\"transport\":");
            if (Transport == null) sb.Append("null");
            else
            {
                sb.Append("{\"state\":"); JsonText.Quote(sb, Transport.State);
                sb.Append(",\"to\":"); JsonText.Quote(sb, Transport.To);
                sb.Append(",\"mode\":"); JsonText.Quote(sb, Transport.Mode);
                sb.Append(",\"escort\":"); AppendList(sb, Transport.Escort);
                sb.Append(",\"monitoring\":"); JsonText.Quote(sb, Transport.Monitoring);
                sb.Append(",\"oxygen\":").Append(Transport.Oxygen ? "true" : "false");
                sb.Append(",\"arrives\":"); JsonText.Quote(sb, Transport.Arrives);
                sb.Append(",\"message\":"); JsonText.Quote(sb, Transport.Message);
                sb.Append('}');
            }
            sb.Append(",\"investigations\":[");
            for (int i = 0; i < Investigations.Count; i++)
            {
                if (i > 0) sb.Append(',');
                sb.Append("{\"test\":"); JsonText.Quote(sb, Investigations[i].Test);
                sb.Append(",\"label\":"); JsonText.Quote(sb, Investigations[i].Label);
                sb.Append(",\"instance\":").Append(Investigations[i].Instance.ToString(System.Globalization.CultureInfo.InvariantCulture));
                sb.Append(",\"state\":"); JsonText.Quote(sb, Investigations[i].State);
                sb.Append(",\"ready\":"); JsonText.Quote(sb, Investigations[i].Ready);
                sb.Append(",\"indication\":"); JsonText.Quote(sb, Investigations[i].Indication);
                sb.Append(",\"results\":[");
                for (int j = 0; j < Investigations[i].Results.Count; j++)
                {
                    var r = Investigations[i].Results[j];
                    if (j > 0) sb.Append(',');
                    sb.Append("{\"id\":"); JsonText.Quote(sb, r.Id);
                    sb.Append(",\"analyte\":"); JsonText.Quote(sb, r.Analyte);
                    sb.Append(",\"value\":"); JsonText.Quote(sb, r.Value);
                    sb.Append(",\"unit\":"); JsonText.Quote(sb, r.Unit);
                    sb.Append(",\"range\":"); JsonText.Quote(sb, r.Range);
                    sb.Append(",\"flag\":"); JsonText.Quote(sb, r.Flag);
                    sb.Append(",\"comment\":"); JsonText.Quote(sb, r.Comment);
                    sb.Append(",\"quality\":"); JsonText.Quote(sb, r.Quality);
                    sb.Append(",\"origin\":"); JsonText.Quote(sb, r.Origin);
                    sb.Append('}');
                }
                sb.Append("],\"note\":"); JsonText.Quote(sb, Investigations[i].Note);
                sb.Append(",\"report\":");
                var rep = Investigations[i].Report;
                if (rep == null) sb.Append("null");
                else
                {
                    sb.Append("{\"text\":"); JsonText.Quote(sb, rep.Text);
                    sb.Append(",\"image\":"); JsonText.Quote(sb, rep.ImageId);
                    sb.Append(",\"measurements\":"); AppendList(sb, rep.Measurements);
                    sb.Append(",\"synthetic\":").Append(rep.Synthetic ? "true" : "false");
                    sb.Append('}');
                }
                sb.Append('}');
            }
            sb.Append(']');
            sb.Append(",\"phone\":{\"state\":"); JsonText.Quote(sb, Phone.State);
            sb.Append(",\"message\":"); JsonText.Quote(sb, Phone.Message);
            sb.Append('}');
            if (Orders != null && Orders.Count > 0)   // only once a treatment has been asked for, so a session without one keeps the hash it always had
            {
                sb.Append(",\"orders\":[");
                for (int i = 0; i < Orders.Count; i++)
                {
                    if (i > 0) sb.Append(',');
                    sb.Append("{\"id\":").Append(Orders[i].Id.ToString(System.Globalization.CultureInfo.InvariantCulture));
                    sb.Append(",\"treatment\":"); JsonText.Quote(sb, Orders[i].Treatment);
                    sb.Append(",\"label\":"); JsonText.Quote(sb, Orders[i].Label);
                    sb.Append(",\"state\":"); JsonText.Quote(sb, Orders[i].State);
                    sb.Append(",\"device\":"); JsonText.Quote(sb, Orders[i].Device);
                    sb.Append(",\"dose\":"); JsonText.Quote(sb, Orders[i].Dose);
                    sb.Append(",\"lines\":"); AppendList(sb, Orders[i].Lines);
                    sb.Append('}');
                }
                sb.Append(']');
            }
            if (OrStations != null && OrStations.Count > 0)   // only once the theatre step has run
            {
                sb.Append(",\"or\":[");
                for (int i = 0; i < OrStations.Count; i++)
                {
                    if (i > 0) sb.Append(',');
                    sb.Append("{\"id\":"); JsonText.Quote(sb, OrStations[i].Id);
                    sb.Append(",\"state\":"); JsonText.Quote(sb, OrStations[i].State);
                    sb.Append('}');
                }
                sb.Append(']');
            }
            if (Checklist != null && (Checklist.Phases.Count > 0 || Checklist.Items.Count > 0))   // only once the checklist has been used
            {
                sb.Append(",\"checklist\":{\"phases\":[");
                for (int i = 0; i < Checklist.Phases.Count; i++)
                {
                    if (i > 0) sb.Append(',');
                    sb.Append("{\"id\":"); JsonText.Quote(sb, Checklist.Phases[i].Id);
                    sb.Append(",\"state\":"); JsonText.Quote(sb, Checklist.Phases[i].State);
                    sb.Append('}');
                }
                sb.Append("],\"items\":[");
                for (int i = 0; i < Checklist.Items.Count; i++)
                {
                    if (i > 0) sb.Append(',');
                    sb.Append("{\"id\":"); JsonText.Quote(sb, Checklist.Items[i].Id);
                    sb.Append(",\"state\":"); JsonText.Quote(sb, Checklist.Items[i].State);
                    sb.Append('}');
                }
                sb.Append("]}");
            }
            if (BehaviourMode != null) { sb.Append(",\"behaviour\":{\"mode\":"); JsonText.Quote(sb, BehaviourMode); sb.Append('}'); }
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

        private static ObservedPhone BuildPhone(State.ClinicalLayer c)
        {
            var call = c.CriticalCalls.FirstOrDefault(x => x.State != "DONE");
            return call == null ? new ObservedPhone("", "") : new ObservedPhone(call.State, call.State == "RINGING" ? "" : call.Message);
        }

        private static List<ObservedInvestigation> BuildInvestigations(Session s, bool practice)
        {
            var list = new List<ObservedInvestigation>();
            foreach (var o in s.Patient.Clinical.Investigations)
            {
                bool processing = o.State == "PROCESSING" || o.State == "RESULT_AVAILABLE" || o.State == "VIEWED" || o.State == "ACKNOWLEDGED";
                bool seen = o.State == "VIEWED" || o.State == "ACKNOWLEDGED";
                list.Add(new ObservedInvestigation(o.Test, o.Label, o.Instance, o.State, processing ? At(s, o.ReadyStep).Substring(0, 5) : "", practice ? o.Indication.ToLowerInvariant() : "", seen ? o.Results.Select(r => new ObservedResult(r.Id, r.Analyte, r.Value, r.Unit, r.Range, r.Flag, r.Comment, r.Quality, r.Origin)).ToList() : new List<ObservedResult>(), o.State == "REJECTED" || seen || o.State == "RESULT_AVAILABLE" ? o.Note : "",
                    seen && o.Report != null ? new ObservedReport(o.Report.Text, o.Report.ImageId, o.Report.Measurements.Select(kv => kv.Key + ": " + kv.Value).ToList(), o.Report.Synthetic) : null));
            }
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
                BuildFindings(c), BuildAirway(c), new FastingChip(c.Fasting?.Status ?? "unknown", c.FastingSafeFrom, c.Fasting?.Plan ?? ""), BuildRisk(c.Risk, practice),
                practice ? c.News2.Select(n => new ObservedNews2(At(s, n.Step), n.Total, n.Single3, n.Tier, n.Frequency, n.Responder, n.Note)).ToList() : new List<ObservedNews2>(),
                c.TraineeLocation, c.Transport == null ? null : new ObservedTransport(c.Transport.State, c.Transport.To, c.Transport.Mode, c.Transport.Escort, c.Transport.Monitoring, c.Transport.Oxygen, c.Transport.Arrives, c.Transport.Message),
                BuildInvestigations(s, practice), BuildPhone(s.Patient.Clinical),
                c.Orders.Select(o => new ObservedOrder(o.Id, o.Treatment, o.Label, o.State, o.Device, o.State == "REQUESTED" ? "" : o.DoseText, o.Lines.ToList())).ToList(),
                c.OrStations.Select(kv => new ObservedOrStation(kv.Key, kv.Value)).ToList(),
                new ObservedChecklist(c.ChecklistPhases.Select(kv => new ObservedOrStation(kv.Key, kv.Value)).ToList(), c.ChecklistItems.Select(kv => new ObservedOrStation(kv.Key, kv.Value)).ToList()),
                s.BehaviourStep?.Mode);
        }
    }
}
