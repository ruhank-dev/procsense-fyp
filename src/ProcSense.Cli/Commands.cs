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
        "  procsense-sim run --case VAR-S1-A --seed 42 --actions file.actions.json --out result.json [--log session.jsonl] [--mode practice|assessment] [--content dir] [--allow-draft] [--skip-integrity]\n" +
        "  procsense-sim replay --log session.jsonl [--content dir]\n" +
        "  procsense-sim report --log session.jsonl --out report.html [--json report.json] [--content dir] [--skip-integrity]\n" +
        "  procsense-sim golden [--dir tests/golden] [--update] [--content dir]";

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
                "report" => Report(opts),
                "golden" => Golden(opts),
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

        var content = ContentLoader.Load(Reader(o), caseId);
        if (Guard(o, content, caseId, mode) is { } refused) return refused;
        var script = ActionScript.Parse(File.ReadAllText(Need(o, "actions")));
        var session = new Session(content.Cases[caseId], seed, mode, Subsystems.Default(content));
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
        var content = ContentLoader.Load(Reader(o), caseId);
        if (Integrity(o) is { } refused) return refused;
        var r = SessionFile.Replay(text, content, () => Subsystems.Default(content));
        if (r.Match) { Console.WriteLine($"replay OK, hash match ({r.ActualHash})"); return 0; }
        return Fail($"replay MISMATCH: log says {r.ExpectedHash}, replay gave {r.ActualHash}");
    }

    // Replays a session log and writes what happened as a single readable page (inline CSS, no scripts) and the same facts as JSON.
    private static int Report(Dictionary<string, string> o)
    {
        string text = File.ReadAllText(Need(o, "log"));
        string outPath = Need(o, "out");
        string caseId = SessionFile.CaseIdOf(text);
        var content = ContentLoader.Load(Reader(o), caseId);
        if (Integrity(o) is { } refused) return refused;
        var r = SessionFile.Replay(text, content, () => Subsystems.Default(content));
        if (!r.Match) return Fail($"replay MISMATCH: log says {r.ExpectedHash}, replay gave {r.ActualHash}; no report is made from a log that does not replay");
        var facts = ProcSense.Core.Scoring.DebriefFacts.Build(r.Session, content);
        string jsonPath = o.GetValueOrDefault("json", Path.ChangeExtension(outPath, ".json"));
        File.WriteAllText(outPath, facts.ToHtml());
        File.WriteAllText(jsonPath, facts.ToJson() + "\n");
        Console.WriteLine($"result: {facts.Result}; end state: {(facts.EndState.Length == 0 ? "not reached" : facts.EndState)}; {facts.Lines.Count} items");
        Console.WriteLine($"wrote {outPath} and {jsonPath}");
        return 0;
    }

    // Runs every *.actions.json that has a matching *.expected.json and compares. --update rewrites the expected files (review the change before committing).
    private static int Golden(Dictionary<string, string> o)
    {
        string dir = o.GetValueOrDefault("dir", "tests/golden");
        bool update = o.ContainsKey("update");
        var scripts = Directory.GetFiles(dir, "*.actions.json").OrderBy(f => f, StringComparer.Ordinal).ToList();
        var caseIds = scripts.Select(s => s.Replace(".actions.json", ".expected.json")).Where(File.Exists)
            .Select(f => (string)((Dictionary<string, object?>)MiniJson.Parse(File.ReadAllText(f))!)["case"]!).Distinct().ToArray();
        var content = ContentLoader.Load(Reader(o), caseIds);
        int pass = 0, total = 0;
        foreach (var script in scripts)
        {
            string expectedPath = script.Replace(".actions.json", ".expected.json");
            if (!File.Exists(expectedPath)) continue;
            total++;
            string name = Path.GetFileName(script).Replace(".actions.json", "");
            string scriptText = File.ReadAllText(script), expectedText = File.ReadAllText(expectedPath);
            if (update)
            {
                var h = (Dictionary<string, object?>)MiniJson.Parse(expectedText)!;
                var fresh = GoldenRun.Expected(scriptText, (string)h["case"]!, ulong.Parse((string)h["seed"]!, CultureInfo.InvariantCulture),
                    Enum.Parse<SessionMode>((string)h["mode"]!, true), content);
                File.WriteAllText(expectedPath, fresh);
                Console.WriteLine($"UPDATED {name}");
                pass++;
                continue;
            }
            var diffs = GoldenRun.Check(scriptText, expectedText, content);
            if (diffs.Count == 0) { Console.WriteLine($"PASS    {name}"); pass++; }
            else
            {
                Console.WriteLine($"FAIL    {name}");
                foreach (var d in diffs) Console.WriteLine("          " + d);
            }
        }
        if (total == 0) return Fail($"no golden runs found in {dir} (need <name>.actions.json + <name>.expected.json)");
        Console.WriteLine($"golden: {pass}/{total} {(update ? "updated" : "pass")}");
        return pass == total ? 0 : 1;
    }

    // content/ first, then tests/fixtures (test-only cases such as TEST-ID-MISMATCH live there).
    private static IContentReader Reader(Dictionary<string, string> o)
    {
        string dir = ContentDir(o);
        string fixtures = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(dir))!, "tests", "fixtures");
        return new CompositeContentReader(new FileContentReader(dir), new FileContentReader(fixtures));
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
            if (!a[i].StartsWith("--")) throw new ArgumentException($"bad argument '{a[i]}'\n{Usage}");
            bool flag = i + 1 >= a.Length || a[i + 1].StartsWith("--"); // a bare flag such as --update
            d[a[i][2..]] = flag ? "true" : a[i + 1];
            if (flag) i--; // the loop adds 2; a flag only used one slot
        }
        return d;
    }

    private static string Need(Dictionary<string, string> o, string key) =>
        o.TryGetValue(key, out var v) ? v : throw new ArgumentException($"missing --{key}\n{Usage}");

    // The review-status gate (a DRAFT case is never played; assessment needs APPROVED_FOR_ASSESSMENT) and the content integrity check. --allow-draft and --skip-integrity are for developers and say so.
    private static int? Guard(Dictionary<string, string> o, ContentPackage content, string caseId, SessionMode mode)
    {
        if (content.CaseFiles.TryGetValue(caseId, out var file))
        {
            if (o.ContainsKey("allow-draft")) Console.WriteLine("warning: --allow-draft: the review-status gate is off for " + caseId + " (" + file.Status + ")");
            else
                try { ContentGate.Require(file, mode); }
                catch (ContentException e) { return Fail(e.Message + " (a developer can pass --allow-draft)"); }
        }
        return Integrity(o);
    }

    private static int? Integrity(Dictionary<string, string> o)
    {
        if (o.ContainsKey("skip-integrity")) { Console.WriteLine("warning: --skip-integrity: the content was not checked against content/manifest.json"); return null; }
        var bad = ContentManifestCheck.Verify(Reader(o));
        return bad.Count == 0 ? (int?)null : Fail("content does not match content/manifest.json: " + string.Join(", ", bad.Take(5)) + (bad.Count > 5 ? " and " + (bad.Count - 5).ToString(CultureInfo.InvariantCulture) + " more" : "") + " (rebuild the manifest, or pass --skip-integrity)");
    }

    private static int Fail(string message) { Console.Error.WriteLine(message); return 1; }
}
