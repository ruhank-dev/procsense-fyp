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

        public static string Write(Session s)
        {
            var sb = new StringBuilder("{\"header\":{\"case\":");
            JsonText.Quote(sb, s.Case.Id);
            sb.Append(",\"seed\":"); JsonText.Quote(sb, s.Seed.ToString(CultureInfo.InvariantCulture)); // text: a ulong can exceed JSON-safe integers
            sb.Append(",\"mode\":"); JsonText.Quote(sb, s.Mode.ToString().ToLowerInvariant());
            sb.Append(",\"log_hash\":"); JsonText.Quote(sb, s.Log.Hash());
            sb.Append("}}\n").Append(s.Log.ToJsonl());
            return sb.ToString();
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
            string expected = header.Str("log_hash");

            if (!content.Cases.TryGetValue(caseId, out var card)) throw new ContentException("case '" + caseId + "' is not loaded");
            var session = new Session(card, seed, mode, subsystems());

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
