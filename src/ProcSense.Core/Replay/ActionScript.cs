using System.Collections.Generic;
using ProcSense.Core.Actions;
using ProcSense.Core.Content;

namespace ProcSense.Core.Replay
{
    /// <summary>An action to apply once the session clock reaches T seconds.</summary>
    public sealed record TimedAction(double T, SimAction Action);

    /// <summary>Reads a *.actions.json file: a list of { "t", "verb", "target", "params" }. Anything else in an entry is an error.</summary>
    public static class ActionScript
    {
        public static List<TimedAction> Parse(string json)
        {
            if (!(MiniJson.Parse(json) is List<object?> list)) throw new ContentException("actions file: expected a list of actions");
            var result = new List<TimedAction>();
            for (int i = 0; i < list.Count; i++)
            {
                var o = new JsonObj(list[i], JsonObj.Index("actions", i));
                double t = o.Num("t");
                if (t < 0) throw new ContentException(o.Sub("t") + ": must not be negative");
                string verb = o.Str("verb");
                string target = o.Has("target") ? o.Str("target") : "";
                var parameters = o.Has("params") ? o.StringMap("params") : new SortedDictionary<string, string>();
                o.Finish();
                result.Add(new TimedAction(t, new SimAction(verb, target, Channel.Cli, parameters)));
            }
            return result;
        }
    }
}
