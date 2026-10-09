using System.Globalization;
using ProcSense.Core;
using ProcSense.Core.Content;
using ProcSense.Core.Replay;
using ProcSense.Core.Workflow;

namespace ProcSense.Cli;

/// <summary>Thin command layer: parse arguments, call Core, print. No rules live here.</summary>
internal static class Commands
{
    private const string Usage =
        "usage:\n" +
        "  procsense-sim run --case VAR-S1-A --seed 42 --actions file.actions.json --out result.json [--log session.jsonl] [--mode practice|assessment] [--content dir]\n" +
        "  procsense-sim replay --log session.jsonl [--content dir]";

    public static int Run(string[] args)
    {
        try
        {
            if (args.Length == 0) { Console.WriteLine($"{CoreInfo.Name} {CoreInfo.Version}\n{Usage}"); return 0; }
            var opts = Options(args.Skip(1).ToArray());
            return args[0] switch
            {
                "run" => RunCase(opts),
                "replay" => ReplayLog(opts),
                _ => Fail($"unknown command '{args[0]}'\n{Usage}"),
            };
        }
        catch (Exception e) when (e is ContentException or ArgumentException or IOException or FormatException)
        {
            return Fail(e.Message);
        }
    }

    private static int RunCase(Dictionary<string, string> o)
    {
        string caseId = Need(o, "case");
        ulong seed = ulong.Parse(Need(o, "seed"), CultureInfo.InvariantCulture);
        var mode = Enum.Parse<SessionMode>(o.GetValueOrDefault("mode", "practice"), true);
        string outPath = Need(o, "out");
        string logPath = o.GetValueOrDefault("log", Path.ChangeExtension(outPath, ".session.jsonl"));

        var content = ContentLoader.Load(new FileContentReader(ContentDir(o)), caseId);
        var script = ActionScript.Parse(File.ReadAllText(Need(o, "actions")));
        var session = new Session(content.Cases[caseId], seed, mode, Subsystems.Default());
        SessionFile.Play(session, script);

        File.WriteAllText(logPath, SessionFile.Write(session));
        File.WriteAllText(outPath, $"{{\"case\":\"{caseId}\",\"seed\":\"{seed}\",\"log_hash\":\"{session.Log.Hash()}\",\"state_hash\":\"{session.Observable.Hash()}\",\"observable\":{session.Observable.ToJson()}}}\n");
        Console.WriteLine($"actions applied: {session.Log.Count}");
        Console.WriteLine($"log hash:   {session.Log.Hash()}");
        Console.WriteLine($"state hash: {session.Observable.Hash()}");
        Console.WriteLine($"wrote {outPath} and {logPath}");
        return 0;
    }

    private static int ReplayLog(Dictionary<string, string> o)
    {
        string text = File.ReadAllText(Need(o, "log"));
        string caseId = SessionFile.CaseIdOf(text);
        var content = ContentLoader.Load(new FileContentReader(ContentDir(o)), caseId);
        var r = SessionFile.Replay(text, content, Subsystems.Default);
        if (r.Match) { Console.WriteLine($"replay OK, hash match ({r.ActualHash})"); return 0; }
        return Fail($"replay MISMATCH: log says {r.ExpectedHash}, replay gave {r.ActualHash}");
    }

    private static string ContentDir(Dictionary<string, string> o)
    {
        if (o.TryGetValue("content", out var dir)) return dir;
        for (var d = new DirectoryInfo(Directory.GetCurrentDirectory()); d != null; d = d.Parent)
            if (Directory.Exists(Path.Combine(d.FullName, "content", "cases"))) return Path.Combine(d.FullName, "content");
        throw new ArgumentException("content/ folder not found; pass --content <dir>");
    }

    private static Dictionary<string, string> Options(string[] a)
    {
        var d = new Dictionary<string, string>();
        for (int i = 0; i < a.Length; i += 2)
        {
            if (!a[i].StartsWith("--") || i + 1 >= a.Length) throw new ArgumentException($"bad argument '{a[i]}'\n{Usage}");
            d[a[i][2..]] = a[i + 1];
        }
        return d;
    }

    private static string Need(Dictionary<string, string> o, string key) =>
        o.TryGetValue(key, out var v) ? v : throw new ArgumentException($"missing --{key}\n{Usage}");

    private static int Fail(string message) { Console.Error.WriteLine(message); return 1; }
}
