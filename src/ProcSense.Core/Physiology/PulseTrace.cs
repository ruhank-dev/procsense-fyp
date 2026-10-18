using System;
using System.Collections.Generic;
using System.Globalization;
using ProcSense.Core.Content;

namespace ProcSense.Core.Physiology
{
    /// <summary>One recorded moment of a Pulse run: the mapped vital signs at a time since the start of the recording (seconds).</summary>
    public sealed record PulseTraceSample(double T, IReadOnlyDictionary<string, double> Values);

    /// <summary>An action the recording applied to Pulse, by the id of the Core effect or pattern it stands for (content/physio/pulse_mapping.json).</summary>
    public sealed record PulseTraceAction(double T, string Key);

    /// <summary>
    /// A recording of the Pulse Physiology Engine (Kitware, Apache-2.0) running one scenario, made offline by tools/pulse/pulse_bridge.py. A session never calls Pulse: it replays this data, so the values
    /// are logged data and a replay is identical on every machine. The sample values are the Core's own variables (HR, SBP, DBP, RR, SpO2, Temp) already converted from Pulse's units.
    /// </summary>
    public sealed class PulseTrace
    {
        public static readonly string[] Variables = { "HR", "SBP", "DBP", "RR", "SpO2", "Temp" };

        public PulseTrace(string id, string engineVersion, string engineGitHash, double sampleEverySeconds, IReadOnlyList<PulseTraceAction> actions, IReadOnlyList<PulseTraceSample> samples)
        {
            if (samples.Count < 2) throw new ContentException("pulse trace " + id + ": needs at least two samples");
            if (actions.Count == 0) throw new ContentException("pulse trace " + id + ": needs at least one action (it is anchored on the first)");
            for (int i = 1; i < samples.Count; i++)
                if (samples[i].T <= samples[i - 1].T) throw new ContentException("pulse trace " + id + ": sample times must increase");
            Id = id; EngineVersion = engineVersion; EngineGitHash = engineGitHash; SampleEverySeconds = sampleEverySeconds; Actions = actions; Samples = samples;
        }

        public string Id { get; }
        public string EngineVersion { get; }
        public string EngineGitHash { get; }
        public double SampleEverySeconds { get; }
        public IReadOnlyList<PulseTraceAction> Actions { get; }
        public IReadOnlyList<PulseTraceSample> Samples { get; }

        /// <summary>The Core effect or pattern that starts the recording when it is applied in a session: the first action of the recording.</summary>
        public PulseTraceAction Anchor => Actions[0];

        public double Duration => Samples[Samples.Count - 1].T;

        /// <summary>The recorded value of a variable at a time since the start of the recording, linear between samples, held at the first and last sample outside the recording.</summary>
        public double At(string variable, double t)
        {
            if (t <= Samples[0].T) return Samples[0].Values[variable];
            if (t >= Duration) return Samples[Samples.Count - 1].Values[variable];
            int lo = 0, hi = Samples.Count - 1;
            while (hi - lo > 1) { int mid = (lo + hi) / 2; if (Samples[mid].T <= t) lo = mid; else hi = mid; }
            double span = Samples[hi].T - Samples[lo].T, f = (t - Samples[lo].T) / span;
            return Samples[lo].Values[variable] + (Samples[hi].Values[variable] - Samples[lo].Values[variable]) * f;
        }

        public static PulseTrace Parse(string json, string path)
        {
            var o = new JsonObj(MiniJson.Parse(json), path);
            string id = o.Str("scenario");
            var eng = o.Obj("engine");
            string version = eng.Str("version"), hash = eng.Str("git_hash");
            eng.Str("name"); eng.Str("build"); eng.Finish();
            o.Str("state_file"); o.Str("recorded_on"); o.Num("wall_seconds_per_simulated_second");
            double every = o.Num("sample_every_s");
            var actions = new List<PulseTraceAction>();
            var rawA = o.Arr("actions");
            for (int i = 0; i < rawA.Count; i++)
            {
                var a = new JsonObj(rawA[i], JsonObj.Index(o.Sub("actions"), i));
                actions.Add(new PulseTraceAction(a.Num("t"), a.Str("key")));
                a.Finish();
            }
            var samples = new List<PulseTraceSample>();
            var rawS = o.Arr("samples");
            for (int i = 0; i < rawS.Count; i++)
            {
                var s = new JsonObj(rawS[i], JsonObj.Index(o.Sub("samples"), i));
                var values = new Dictionary<string, double>(StringComparer.Ordinal);
                foreach (var v in Variables) values[v] = s.Num(v);
                samples.Add(new PulseTraceSample(s.Num("t"), values));
                s.Num("t"); s.Finish();
            }
            o.Finish();
            return new PulseTrace(id, version, hash, every, actions, samples);
        }
    }

    /// <summary>The traces a content package carries (content/physio/pulse_traces/index.json lists them). A package without the folder has none, and a session that asks for one is refused.</summary>
    public sealed class PulseTraceTables
    {
        public static readonly PulseTraceTables Empty = new PulseTraceTables(new Dictionary<string, PulseTrace>(StringComparer.Ordinal));

        public PulseTraceTables(IReadOnlyDictionary<string, PulseTrace> traces) { Traces = traces; }

        public IReadOnlyDictionary<string, PulseTrace> Traces { get; }

        public static PulseTraceTables Parse(string indexJson, Func<string, string> read)
        {
            var o = new JsonObj(MiniJson.Parse(indexJson), "physio/pulse_traces/index");
            var traces = new Dictionary<string, PulseTrace>(StringComparer.Ordinal);
            foreach (var item in o.Arr("traces"))
            {
                if (!(item is string id)) throw new ContentException("physio/pulse_traces/index.traces: expected scenario ids");
                var t = PulseTrace.Parse(read(id), "physio/pulse_traces/" + id);
                if (t.Id != id) throw new ContentException("physio/pulse_traces/" + id + ": the file holds the scenario " + t.Id);
                traces[id] = t;
            }
            o.Finish();
            return new PulseTraceTables(traces);
        }
    }
}
