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
        "  procsense-sim run --case VAR-S1-A --seed 42 --actions file.actions.json --out result.json [--log session.jsonl] [--mode practice|assessment] [--level beginner|advanced] [--variant B2] [--noise] [--content dir] [--allow-draft] [--skip-integrity]\n" +
        "  procsense-sim replay --log session.jsonl [--content dir]\n" +
        "  procsense-sim report --log session.jsonl --out report.html [--json report.json] [--content dir] [--skip-integrity]\n" +
        "  procsense-sim next-case --history dir --trainee pseudonym [--mode practice|assessment] [--content dir]\n" +
        "  procsense-sim record-session --history dir --trainee pseudonym --facts report.json\n" +
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
                "next-case" => NextCase(opts),
                "record-session" => RecordSession(opts),
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
        var options = new SessionOptions(Enum.Parse<TrainingLevel>(o.GetValueOrDefault("level", "beginner"), true), o.GetValueOrDefault("variant", ""), o.ContainsKey("noise"));
        var session = new Session(content.Cases[caseId], seed, mode, Subsystems.Default(content), options);
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

    // The trainee is a pseudonym (letters, digits, dash, underscore): never a name. The history is one small JSON file per trainee.
    private static string HistoryPath(Dictionary<string, string> o)
    {
        string trainee = Need(o, "trainee");
        if (!System.Text.RegularExpressions.Regex.IsMatch(trainee, "^[A-Za-z0-9_-]{1,40}$")) throw new ArgumentException("the trainee must be a pseudonym of letters, digits, dash and underscore (no names)");
        return Path.Combine(Need(o, "history"), trainee + ".json");
    }

    private static List<ProcSense.Core.Difficulty.SessionRecord> ReadHistory(string path)
    {
        var list = new List<ProcSense.Core.Difficulty.SessionRecord>();
        if (!File.Exists(path)) return list;
        var root = (Dictionary<string, object?>)MiniJson.Parse(File.ReadAllText(path))!;
        foreach (var s in (List<object?>)root["sessions"]!)
        {
            var d = (Dictionary<string, object?>)s!;
            var by = new Dictionary<string, (int, int)>(StringComparer.Ordinal);
            foreach (var kv in (Dictionary<string, object?>)d["behaviours"]!)
            {
                var v = (Dictionary<string, object?>)kv.Value!;
                by[kv.Key] = ((int)(long)v["credits"]!, (int)(long)v["penalties"]!);
            }
            list.Add(new ProcSense.Core.Difficulty.SessionRecord((string)d["case"]!, by));
        }
        return list;
    }

    private static void WriteHistory(string path, string trainee, List<ProcSense.Core.Difficulty.SessionRecord> sessions)
    {
        var sb = new System.Text.StringBuilder("{\"trainee\":");
        ProcSense.Core.Logging.JsonText.Quote(sb, trainee);
        sb.Append(",\"sessions\":[");
        for (int i = 0; i < sessions.Count; i++)
        {
            if (i > 0) sb.Append(',');
            sb.Append("{\"case\":"); ProcSense.Core.Logging.JsonText.Quote(sb, sessions[i].Case); sb.Append(",\"behaviours\":{");
            bool first = true;
            foreach (var kv in sessions[i].Behaviours.OrderBy(k => k.Key, StringComparer.Ordinal))
            {
                if (!first) sb.Append(',');
                first = false;
                ProcSense.Core.Logging.JsonText.Quote(sb, kv.Key);
                sb.Append(":{\"credits\":").Append(kv.Value.Credits.ToString(CultureInfo.InvariantCulture)).Append(",\"penalties\":").Append(kv.Value.Penalties.ToString(CultureInfo.InvariantCulture)).Append('}');
            }
            sb.Append("}}");
        }
        sb.Append("]}"); sb.Append('\n');
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, sb.ToString());
    }

    // Says which case to play next. Practice adapts to the weakest behaviour after three sessions; assessment follows the fixed order.
    private static int NextCase(Dictionary<string, string> o)
    {
        string path = HistoryPath(o);
        var mode = Enum.Parse<SessionMode>(o.GetValueOrDefault("mode", "practice"), true);
        var reader = Reader(o);
        var ids = Directory.GetFiles(Path.Combine(ContentDir(o), "case_files"), "*.json").Select(Path.GetFileNameWithoutExtension).OrderBy(x => x, StringComparer.Ordinal).Select(x => x!).ToArray();
        var content = ContentLoader.Load(reader, Array.Empty<string>(), ids);
        var history = ReadHistory(path);
        var ratings = ProcSense.Core.Difficulty.Ratings.From(history, content.Difficulty, id => content.CaseFiles.TryGetValue(id, out var f) ? f.Tier : "P0");
        var pick = ProcSense.Core.Difficulty.Selector.Next(ratings, history.Select(h => h.Case).ToList(), content.CaseFiles, mode, id => content.Scoring.Find(id)?.Label ?? id);
        Console.WriteLine($"case: {pick.CaseId}\ntier: {pick.Tier}\nwhy: {pick.Reason}\n{pick.Explanation}");
        return pick.CaseId.Length == 0 ? 1 : 0;
    }

    // Adds a finished session (the credits and penalties per behaviour from a report's JSON) to the trainee's history.
    private static int RecordSession(Dictionary<string, string> o)
    {
        string path = HistoryPath(o);
        var facts = (Dictionary<string, object?>)MiniJson.Parse(File.ReadAllText(Need(o, "facts")))!;
        var by = new Dictionary<string, (int, int)>(StringComparer.Ordinal);
        foreach (var c in (List<object?>)facts["competencies"]!)
        {
            var d = (Dictionary<string, object?>)c!;
            if ((long)d["items"]! > 0) by[(string)d["behaviour"]!] = ((int)(long)d["credits"]!, (int)(long)d["penalties"]!);
        }
        var history = ReadHistory(path);
        history.Add(new ProcSense.Core.Difficulty.SessionRecord((string)facts["case"]!, by));
        WriteHistory(path, Need(o, "trainee"), history);
        Console.WriteLine($"recorded session {history.Count} for {Need(o, "trainee")}");
        return 0;
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
