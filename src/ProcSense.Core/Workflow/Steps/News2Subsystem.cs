using System;
using System.Globalization;
using ProcSense.Core.Actions;
using ProcSense.Core.Clinical;
using ProcSense.Core.Content;
using ProcSense.Core.Pipeline;
using ProcSense.Core.State;

namespace ProcSense.Core.Workflow.Steps
{
    /// <summary>
    /// Works out a NEWS2 from the observations the trainee has gathered, as soon as the six readings are all known (SpO2 and pulse from the probe, blood pressure from the cuff cycle, breathing rate,
    /// temperature and ACVPU as they are taken; whether the patient is on oxygen is seen at once), and again whenever a reading is taken again. Each score is kept with its sim time
    /// (clinical.news2[]). Once the trainee knows the patient is pregnant NEWS2 is no longer the tool, so the record says so instead of giving a tier. Scale 2 is only used when the clinician
    /// has set it (flag news2.scale2). Nothing here decides an outcome: the tier and who responds are information for the trainee and the scorer.
    /// </summary>
    public sealed class News2Subsystem : ISubsystem
    {
        public const string ComputedFlag = "news2.computed";
        public const string Single3Flag = "news2.single3";
        public const string Scale2Flag = "news2.scale2";
        public const string NotUsedNote = "NEWS2 is not used in pregnancy; use the hospital's maternity early warning score.";

        private readonly ContentPackage _content;
        private readonly CaseFile? _fixed;
        private readonly News2 _news2;
        private CaseFile? _file;
        private Session _s = null!;
        private string _signature = "";
        private long _bpStep = -1;

        /// <param name="caseFile">Test hook: use this case file instead of the one named like the case card.</param>
        public News2Subsystem(ContentPackage content, CaseFile? caseFile = null)
        {
            _content = content; _fixed = caseFile; _news2 = new News2(content.News2);
        }

        public int Order => PipelineOrder.Clinical + 3;

        private PhysiologicalLayer Physio => _s.Patient.Physiological;

        public void Init(Session session)
        {
            _s = session;
            _file = _fixed ?? (_content.CaseFiles.TryGetValue(session.Case.Id, out var f) ? f : null);
        }

        public void OnAction(SimAction a, ActionOutcome o)
        {
            if (o.Accepted) Evaluate(o);
        }

        // The blood pressure cycle finishes on a tick; looking up one key and comparing a number allocates nothing.
        public void OnTick()
        {
            if (Physio.Measured.TryGetValue("systolic_bp", out var m) && m.AtStep != _bpStep) Evaluate(null);
        }

        private double? Measured(string name) =>
            Physio.Measured.TryGetValue(name, out var m) && double.TryParse(m.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out double d) ? d : (double?)null;

        private void Evaluate(ActionOutcome? o)
        {
            var v = Physio.Vitals;
            bool probe = Physio.IsAttached(ObservationsSubsystem.Spo2Probe);
            var bp = Physio.Measured.TryGetValue("systolic_bp", out var b) ? b : null;
            _bpStep = bp?.AtStep ?? -1;
            string? acvpu = Physio.Measured.TryGetValue("acvpu", out var a) ? a.Value : null;
            var input = new News2Input(Measured("respiratory_rate"), probe ? v.Spo2 : (double?)null, Measured("systolic_bp"), probe ? v.HeartRate : (double?)null, Measured("temperature_c"), acvpu, v.Oxygen == "supplemental");

            // Only a change in what is known (a sensor on, a reading taken or taken again) makes a new score.
            bool pregnant = _file != null && _file.Pregnant && _s.Patient.Clinical.IsRevealed("pregnancy");
            string signature = string.Join("|", probe ? "p" : "-", pregnant ? "x" : "-", Stamp("respiratory_rate"), Stamp("systolic_bp"), Stamp("temperature_c"), Stamp("acvpu"));
            if (signature == _signature) return;
            _signature = signature;

            int scale = _s.Patient.Clinical.HasFlag(Scale2Flag) ? 2 : 1;
            var r = _news2.Score(input, scale, pregnant);
            var clinical = _s.Patient.Clinical;
            if (r.Disabled)
            {
                if (clinical.News2.Count == 0 || clinical.News2[clinical.News2.Count - 1].Tier != "not_used")
                    clinical.News2.Add(new News2Record(_s.Clock.Steps, 0, false, "not_used", "", "", NotUsedNote));
                o?.Effects.Add("news2:not_used");
                return;
            }
            if (!r.Complete) return;
            clinical.News2.Add(new News2Record(_s.Clock.Steps, r.Total, r.Single3, r.Tier, r.Frequency, r.Responder, ""));
            clinical.SetFlag(ComputedFlag);
            if (r.Single3) clinical.SetFlag(Single3Flag);
            o?.Effects.Add("news2:" + r.Total.ToString(CultureInfo.InvariantCulture) + " " + r.Tier);
        }

        private string Stamp(string name) => Physio.Measured.TryGetValue(name, out var m) ? m.AtStep.ToString(CultureInfo.InvariantCulture) : "-";
    }
}
