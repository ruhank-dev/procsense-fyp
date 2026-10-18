using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using ProcSense.Core.Actions;
using ProcSense.Core.Content;
using ProcSense.Core.Logging;
using ProcSense.Core.Pipeline;

namespace ProcSense.Core.Replay
{
    public sealed record ReplayResult(bool Match, string ExpectedHash, string ActualHash, Session Session);

    /// <summary>What the host adds to a saved session so a server can check it (header version 2): the id the host gave the session, the version of the content package it was played with and, for a generated scenario, the SHA-256 of its package.
    /// None of it is part of the log hash, so a session replays to the same hash with or without it.</summary>
    public sealed record SessionMeta(string SessionId, string ContentVersion, string ScenarioPackageHash = "");

    /// <summary>The header of a saved session as it was written. The version 2 fields are empty for a session saved without them.</summary>
    public sealed record SessionHeader(string Case, string Seed, string Mode, string Level, string Variant, bool Noise, string LogHash, string SessionId, string ContentVersion, string EngineVersion, string ScenarioPackageHash)
    {
        public bool IsVersion2 => SessionId.Length > 0 && ContentVersion.Length > 0 && EngineVersion.Length > 0;
    }

    /// <summary>Plays actions into a session and saves/replays sessions. A saved session is one header line (case, seed, mode, log hash) followed by the log.</summary>
    public static class SessionFile
    {
        /// <summary>Advances the clock to each action's time, then applies it. Actions must be in time order.</summary>
        public static void Play(Session session, IEnumerable<TimedAction> actions)
        {
            foreach (var a in actions)
            {
                double dt = a.T - session.Clock.SimSeconds;
                if (dt < 0) throw new ContentException("actions must be in time order: t=" + a.T.ToString(CultureInfo.InvariantCulture) + " is before the clock");
                session.Tick(dt);
                session.Apply(a.Action);
            }
        }

        /// <summary>The saved form of a session. With `meta` the header is version 2 (session id, content version, engine version, scenario package hash); without it the header is what it always was.</summary>
        public static string Write(Session s, SessionMeta? meta = null)
        {
            var sb = new StringBuilder("{\"header\":{\"case\":");
            JsonText.Quote(sb, s.Case.Id);
            sb.Append(",\"seed\":"); JsonText.Quote(sb, s.Seed.ToString(CultureInfo.InvariantCulture)); // text: a ulong can exceed JSON-safe integers
            sb.Append(",\"mode\":"); JsonText.Quote(sb, s.Mode.ToString().ToLowerInvariant());
            if (s.Level != TrainingLevel.Beginner) { sb.Append(",\"level\":"); JsonText.Quote(sb, s.Level.ToString().ToLowerInvariant()); }      // only when not the default, so older logs read the same
            if (s.Variant.Length > 0) { sb.Append(",\"variant\":"); JsonText.Quote(sb, s.Variant); }
            if (s.Noise) sb.Append(",\"noise\":true");
            if (s.PulseTrace.Length > 0) { sb.Append(",\"pulse_trace\":"); JsonText.Quote(sb, s.PulseTrace); }
            if (meta != null)
            {
                sb.Append(",\"session_id\":"); JsonText.Quote(sb, meta.SessionId);
                sb.Append(",\"content_version\":"); JsonText.Quote(sb, meta.ContentVersion);
                sb.Append(",\"engine_version\":"); JsonText.Quote(sb, CoreInfo.Version);
                sb.Append(",\"scenario_package_hash\":"); JsonText.Quote(sb, meta.ScenarioPackageHash);
            }
            sb.Append(",\"log_hash\":"); JsonText.Quote(sb, s.Log.Hash());
            sb.Append("}}\n").Append(s.Log.ToJsonl());
            return sb.ToString();
        }

        /// <summary>Reads the header line of a saved session (throws a ContentException that names the problem when it is not a header).</summary>
        public static SessionHeader HeaderOf(string text)
        {
            var header = new JsonObj(MiniJson.Parse(text.Split('\n')[0]), "").Obj("header");
            string Opt(string key) => header.Has(key) ? header.Str(key) : "";
            return new SessionHeader(header.Str("case"), header.Str("seed"), header.Str("mode"), Opt("level"), Opt("variant"), header.Has("noise") && header.Bool("noise"), header.Str("log_hash"),
                                     Opt("session_id"), Opt("content_version"), Opt("engine_version"), Opt("scenario_package_hash"));
        }

        /// <summary>Which case a saved session was recorded on (needed to load content before replaying).</summary>
        public static string CaseIdOf(string text) => new JsonObj(MiniJson.Parse(text.Split('\n')[0]), "").Obj("header").Str("case");

        /// <summary>Re-applies a saved session from scratch and checks the log hash comes out identical.</summary>
        public static ReplayResult Replay(string text, ContentPackage content, Func<IEnumerable<ISubsystem>> subsystems)
        {
            var lines = text.Split('\n');
            var header = new JsonObj(MiniJson.Parse(lines[0]), "").Obj("header");
            string caseId = header.Str("case");
            ulong seed = ulong.Parse(header.Str("seed"), CultureInfo.InvariantCulture);
            var mode = (SessionMode)Enum.Parse(typeof(SessionMode), header.Str("mode"), true);
            var options = new SessionOptions(header.Has("level") ? (TrainingLevel)Enum.Parse(typeof(TrainingLevel), header.Str("level"), true) : TrainingLevel.Beginner, header.Has("variant") ? header.Str("variant") : "", header.Has("noise") && header.Bool("noise"), header.Has("pulse_trace") ? header.Str("pulse_trace") : "");
            string expected = header.Str("log_hash");

            if (!content.Cases.TryGetValue(caseId, out var card)) throw new ContentException("case '" + caseId + "' is not loaded");
            var session = new Session(card, seed, mode, subsystems(), options);

            var actions = new List<TimedAction>();
            for (int i = 1; i < lines.Length; i++)
            {
                if (lines[i].Length == 0) continue;
                var e = new JsonObj(MiniJson.Parse(lines[i]), "log line " + i.ToString(CultureInfo.InvariantCulture));
                double t = e.Int("step") * Time.SimClock.Step;
                var p = e.StringMap("params");
                var channel = (Channel)Enum.Parse(typeof(Channel), e.Str("channel"), true);
                actions.Add(new TimedAction(t, new SimAction(e.Str("verb"), e.Str("target"), channel, p)));
            }
            Play(session, actions);

            return new ReplayResult(session.Log.Hash() == expected, expected, session.Log.Hash(), session);
        }
    }
}
