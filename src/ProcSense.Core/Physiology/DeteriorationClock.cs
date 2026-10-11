using System;
using System.Collections.Generic;
using System.Linq;
using ProcSense.Core.Content;

namespace ProcSense.Core.Physiology
{
    /// <summary>One timed reading of a case's own timeline (minutes from the start of the case). News2Book is the total the walkthrough prints, kept to check the chart against.</summary>
    public sealed record TimelinePoint(double Minutes, double HeartRate, double Sbp, double Dbp, double RespiratoryRate, double Spo2, double TemperatureC, string Acvpu, int News2Book, string Ref);

    /// <summary>The values the walkthrough states after the correct action.</summary>
    public sealed record TreatedTarget(double Sbp, double Dbp, double HeartRate, string Ref);

    public sealed record CaseTimeline(IReadOnlyList<TimelinePoint> Points, TreatedTarget Treated);

    /// <summary>The vital signs the clock sets (the rest of the patient's vitals are left as they are).</summary>
    public readonly struct ClockVitals
    {
        public ClockVitals(double hr, double sbp, double dbp, double rr, double spo2, double temp, string acvpu) { HeartRate = hr; Sbp = sbp; Dbp = dbp; RespiratoryRate = rr; Spo2 = spo2; TemperatureC = temp; Acvpu = acvpu; }
        public double HeartRate { get; }
        public double Sbp { get; }
        public double Dbp { get; }
        public double RespiratoryRate { get; }
        public double Spo2 { get; }
        public double TemperatureC { get; }
        public string Acvpu { get; }
    }

    /// <summary>The deterioration clock (content/physiology/deterioration.json): its rule, which treatments stop it, how long recovery takes and the timelines of cases that have one.</summary>
    public sealed class DeteriorationTables
    {
        public static readonly DeteriorationTables Empty = new DeteriorationTables(5, 10, 1, 30, new List<string>(), 30, new Dictionary<string, CaseTimeline>());

        public DeteriorationTables(int hrStepMin, int hrStepMax, int news2Step, int intervalMinutes, IReadOnlyList<string> treatedBy, int recoveryMinutes, IReadOnlyDictionary<string, CaseTimeline> timelines)
        {
            HrStepMin = hrStepMin; HrStepMax = hrStepMax; News2Step = news2Step; IntervalMinutes = intervalMinutes; TreatedBy = treatedBy; RecoveryMinutes = recoveryMinutes; Timelines = timelines;
        }

        public int HrStepMin { get; }
        public int HrStepMax { get; }
        public int News2Step { get; }
        public int IntervalMinutes { get; }
        /// <summary>Effect ids that stop the clock when given (a fluid bolus, an antibiotic).</summary>
        public IReadOnlyList<string> TreatedBy { get; }
        public int RecoveryMinutes { get; }
        public IReadOnlyDictionary<string, CaseTimeline> Timelines { get; }

        public static DeteriorationTables Parse(string json, string source = "deterioration")
        {
            var root = new JsonObj(MiniJson.Parse(json), source);
            var c = root.Obj("clock");
            int lo = (int)c.Int("hr_step_min"), hi = (int)c.Int("hr_step_max"), n2 = (int)c.Int("news2_step"), interval = (int)c.Int("interval_minutes");
            c.Str("ref"); c.Finish();
            if (lo < 0 || hi < lo || interval <= 0) throw new ContentException(root.Sub("clock") + ": needs 0 <= hr_step_min <= hr_step_max and a positive interval");
            var treated = new List<string>();
            foreach (var t in root.Arr("treated_by_effects")) treated.Add(t as string ?? throw new ContentException(root.Sub("treated_by_effects") + ": expected effect ids"));
            int recovery = (int)root.Int("recovery_minutes");
            if (recovery <= 0) throw new ContentException(root.Sub("recovery_minutes") + ": must be positive");
            var timelines = new SortedDictionary<string, CaseTimeline>(StringComparer.Ordinal);
            var raw = root.Any("timelines") as Dictionary<string, object?> ?? throw new ContentException(root.Sub("timelines") + ": expected an object");
            foreach (var kv in raw)
            {
                var o = new JsonObj(kv.Value, root.Sub("timelines") + "." + kv.Key);
                var points = new List<TimelinePoint>();
                var rawPoints = o.Arr("points");
                for (int i = 0; i < rawPoints.Count; i++)
                {
                    var p = new JsonObj(rawPoints[i], JsonObj.Index(o.Sub("points"), i));
                    points.Add(new TimelinePoint(p.Num("minutes"), p.Num("hr"), p.Num("sbp"), p.Num("dbp"), p.Num("rr"), p.Num("spo2"), p.Num("temp"), p.Str("acvpu"), (int)p.Int("news2_book"), p.Str("ref")));
                    p.Str("time"); p.Finish();
                }
                if (points.Count < 2) throw new ContentException(o.Sub("points") + ": a timeline needs at least two readings");
                for (int i = 1; i < points.Count; i++) if (points[i].Minutes <= points[i - 1].Minutes) throw new ContentException(o.Sub("points") + ": the readings must be in time order");
                var tr = o.Obj("treated");
                var target = new TreatedTarget(tr.Num("sbp"), tr.Num("dbp"), tr.Num("hr"), tr.Str("ref"));
                tr.Finish();
                o.Finish();
                timelines[kv.Key] = new CaseTimeline(points, target);
            }
            foreach (var r in root.Arr("refs")) if (!(r is string)) throw new ContentException(root.Sub("refs") + ": expected register ids");
            root.Finish();
            return new DeteriorationTables(lo, hi, n2, interval, treated, recovery, timelines);
        }
    }

    /// <summary>
    /// The deterioration clock. A case with its own timeline follows it when untreated (straight lines between its readings, held after the last); after the first treatment that stops the clock the heart rate and
    /// blood pressure move to the values the walkthrough states over the recovery time, and the other readings are held where they were. Any other case drifts by the table's rule: the heart rate rises
    /// 5 to 10 about every 30 minutes until a treatment stops it. All of this is a simulator design value (provisional).
    /// </summary>
    public sealed class DeteriorationClock
    {
        private readonly DeteriorationTables _t;

        public DeteriorationClock(DeteriorationTables tables) { _t = tables; }

        /// <summary>The timeline reading at this many minutes, with no treatment.</summary>
        public ClockVitals Untreated(CaseTimeline line, double minutes)
        {
            var pts = line.Points;
            if (minutes <= pts[0].Minutes) return From(pts[0]);
            if (minutes >= pts[pts.Count - 1].Minutes) return From(pts[pts.Count - 1]);
            int i = 1;
            while (pts[i].Minutes < minutes) i++;
            var a = pts[i - 1]; var b = pts[i];
            double f = (minutes - a.Minutes) / (b.Minutes - a.Minutes);
            return new ClockVitals(Lerp(a.HeartRate, b.HeartRate, f), Lerp(a.Sbp, b.Sbp, f), Lerp(a.Dbp, b.Dbp, f), Lerp(a.RespiratoryRate, b.RespiratoryRate, f), Lerp(a.Spo2, b.Spo2, f),
                Lerp(a.TemperatureC, b.TemperatureC, f), f < 1 ? a.Acvpu : b.Acvpu);
        }

        /// <summary>The reading at this many minutes when the first stopping treatment was given at treatedAt (null: untreated).</summary>
        public ClockVitals At(CaseTimeline line, double minutes, double? treatedAt)
        {
            if (!treatedAt.HasValue || minutes <= treatedAt.Value) return Untreated(line, minutes);
            var held = Untreated(line, treatedAt.Value);
            double f = Math.Min(1, (minutes - treatedAt.Value) / _t.RecoveryMinutes);
            var target = line.Treated;
            return new ClockVitals(Lerp(held.HeartRate, target.HeartRate, f), Lerp(held.Sbp, target.Sbp, f), Lerp(held.Dbp, target.Dbp, f), held.RespiratoryRate, held.Spo2, held.TemperatureC, held.Acvpu);
        }

        /// <summary>One step of the generic clock: how much the heart rate rises this interval (the caller draws it from the seeded stream between the table's limits).</summary>
        public int HeartRateStep(Func<int, int> nextInt) => _t.HrStepMin + nextInt(_t.HrStepMax - _t.HrStepMin + 1);

        private static ClockVitals From(TimelinePoint p) => new ClockVitals(p.HeartRate, p.Sbp, p.Dbp, p.RespiratoryRate, p.Spo2, p.TemperatureC, p.Acvpu);

        private static double Lerp(double a, double b, double f) => a + (b - a) * f;
    }
}
