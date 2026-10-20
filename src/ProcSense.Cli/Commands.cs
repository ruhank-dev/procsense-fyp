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
        "  procsense-sim run --case VAR-S1-A --seed 42 --actions file.actions.json --out result.json [--log session.jsonl] [--mode practice|assessment] [--level beginner|advanced] [--variant B2] [--noise] [--pulse-trace ID] [--session-id guid] [--package-hash sha256] [--content dir] [--scenario generated.package.json] [--allow-draft] [--skip-integrity]\n" +
        "  procsense-sim replay --log session.jsonl [--content dir] [--scenario generated.package.json]\n" +
        "  procsense-sim verify --log session.jsonl [--out verdict.json] [--html report.html] [--package package.json] [--content dir] [--skip-integrity]\n" +
        "  procsense-sim report --log session.jsonl --out report.html [--json report.json] [--content dir] [--skip-integrity]\n" +
        "  procsense-sim next-case --history dir --trainee pseudonym [--mode practice|assessment] [--content dir]\n" +
        "  procsense-sim record-session --history dir --trainee pseudonym --facts report.json\n" +
        "  procsense-sim golden [--dir tests/golden] [--update] [--content dir]\n" +
        "  procsense-sim evidence-check --offers offers.json [--out verdicts.json] [--per-act thresholds.json] [--content dir]";

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
                "verify" => Verify(opts),
                "report" => Report(opts),
                "next-case" => NextCase(opts),
                "record-session" => RecordSession(opts),
                "golden" => Golden(opts),
                "evidence-check" => EvidenceCheck(opts),
                _ => Fail($"unknown command '{args[0]}'\n{Usage}"),
            };
        }
        catch (Exception e) when (e is ContentException or ArgumentException or IOException or FormatException)
        {
            return Fail(e.Message);
        }
    }

    /// <summary>
    /// Runs offered speech evidence through the engine's own <see cref="ProcSense.Core.Scoring.EvidenceStore"/> (ADR-0022 follow-up): each offer in the file ({"id","act","confidence","steps":[...]}) is submitted to a fresh store, so one offer never changes another's answer,
    /// and the verdict (accepted, reason, behaviour) is written as JSON. It measures what the engine would accept; it changes nothing and decides nothing.
    /// </summary>
    private static int EvidenceCheck(Dictionary<string, string> o)
    {
        var content = ContentLoader.Load(Reader(o), "VAR-S1-A");
        var offers = (List<object?>)MiniJson.Parse(File.ReadAllText(Need(o, "offers")))!;
        var tables = content.Scoring;
        if (o.TryGetValue("per-act", out var perActPath))                  // a what-if: judge the same offers with other per-act thresholds (a JSON object, act to number); nothing is written
        {
            var per = ((Dictionary<string, object?>)MiniJson.Parse(File.ReadAllText(perActPath))!).ToDictionary(kv => kv.Key, kv => Convert.ToDouble(kv.Value, CultureInfo.InvariantCulture));
            tables = new ProcSense.Core.Scoring.ScoreTables(tables.Behaviours, tables.Rules, tables.Points, tables.Weights, tables.DefaultThreshold, per, tables.CriticalActs, tables.Sentences);
        }
        var verdicts = new List<string>();
        var stores = new Dictionary<string, ProcSense.Core.Scoring.EvidenceStore>();
        foreach (var item in offers)
        {
            var d = (Dictionary<string, object?>)item!;
            var steps = ((List<object?>)d["steps"]!).Select(x => (int)Convert.ToInt64(x, CultureInfo.InvariantCulture)).ToList();
            // an offer with a "group" shares one store with the others of that group, in file order (a session: duplicates are judged against what came before); one without gets a fresh store
            string group = d.TryGetValue("group", out var g) && g is string gs ? gs : "";
            ProcSense.Core.Scoring.EvidenceStore store;
            if (group.Length == 0) store = new ProcSense.Core.Scoring.EvidenceStore(tables);
            else if (!stores.TryGetValue(group, out store!)) stores[group] = store = new ProcSense.Core.Scoring.EvidenceStore(tables);
            string utterance = d.TryGetValue("utterance", out var u) && u is string us ? us : "";
            var r = store.SubmitUtterance(utterance, (string)d["act"]!, "", Convert.ToDouble(d["confidence"], CultureInfo.InvariantCulture), steps, new[] { 0 });
            verdicts.Add($"{{\"id\":\"{d["id"]}\",\"accepted\":{(r.Accepted ? "true" : "false")},\"reason\":\"{r.Reason.Replace("\"", "'")}\",\"behaviour\":\"{r.Behaviour}\"}}");
        }
        string text = "[" + string.Join(",\n", verdicts) + "]\n";
        if (o.TryGetValue("out", out var path)) File.WriteAllText(path, text); else Console.Write(text);
        return 0;
    }

    private static int RunCase(Dictionary<string, string> o)
    {
        string caseId = ScenarioOf(o)?.CaseId is { } fromPackage && !o.ContainsKey("case") ? fromPackage : Need(o, "case");
        ulong seed = ulong.Parse(Need(o, "seed"), CultureInfo.InvariantCulture);
        var mode = Enum.Parse<SessionMode>(o.GetValueOrDefault("mode", "practice"), true);
        string outPath = Need(o, "out");
        string logPath = o.GetValueOrDefault("log", Path.ChangeExtension(outPath, ".session.jsonl"));

        var content = ContentLoader.Load(Reader(o), caseId);
        if (Guard(o, content, caseId, mode) is { } refused) return refused;
        var script = ActionScript.Parse(File.ReadAllText(Need(o, "actions")));
        var options = new SessionOptions(Enum.Parse<TrainingLevel>(o.GetValueOrDefault("level", "beginner"), true), o.GetValueOrDefault("variant", ""), o.ContainsKey("noise"), o.GetValueOrDefault("pulse-trace", ""), o.GetValueOrDefault("events", ScenarioOf(o)?.PlannedEvents ?? ""));
        var session = new Session(content.Cases[caseId], seed, mode, Subsystems.Default(content), options);
        SessionFile.Play(session, script);

        // header version 2: the host gives the session an id and says which content it was played with, so a server can check it (p1-backend-s09)
        string sessionId = o.TryGetValue("session-id", out var given) ? Guid.Parse(given).ToString() : Guid.NewGuid().ToString();
        string contentVersion;
        try { contentVersion = ContentManifestCheck.Version(Reader(o)); }
        catch (Exception e) when (e is IOException or ContentException) { contentVersion = "unversioned"; }
        File.WriteAllText(logPath, SessionFile.Write(session, new SessionMeta(sessionId, contentVersion, o.GetValueOrDefault("package-hash", ScenarioOf(o)?.PackageHash ?? ""))));
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
            list.Add(new ProcSense.Core.Difficulty.SessionRecord((string)d["case"]!, by, d.TryGetValue("tier", out var tier) ? tier as string : null));
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
            sb.Append('}');
            if (sessions[i].Tier != null) { sb.Append(",\"tier\":"); ProcSense.Core.Logging.JsonText.Quote(sb, sessions[i].Tier!); }
            sb.Append('}');
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
        string? tier = facts.TryGetValue("generated", out var generated) && generated is Dictionary<string, object?> g ? g["tier"] as string : null;     // a generated case carries its tier into the history
        history.Add(new ProcSense.Core.Difficulty.SessionRecord((string)facts["case"]!, by, tier));
        WriteHistory(path, Need(o, "trainee"), history);
        Console.WriteLine($"recorded session {history.Count} for {Need(o, "trainee")}");
        return 0;
    }

    // Says whether a saved session replays to its own hash with this engine and this content, and what it scored. Prints one line of JSON (the verdict); exit code 0 only when the session is verified.
    private static int Verify(Dictionary<string, string> o)
    {
        string text = File.ReadAllText(Need(o, "log"));
        byte[]? package = o.TryGetValue("package", out var packagePath) ? File.ReadAllBytes(packagePath) : null;
        var result = SessionVerifier.Verify(text, Reader(o), package, checkIntegrity: !o.ContainsKey("skip-integrity"));
        int headerVersion;
        try { headerVersion = SessionFile.HeaderOf(text).IsVersion2 ? 2 : 1; } catch (Exception e) when (e is ContentException or ArgumentException or FormatException or InvalidCastException or KeyNotFoundException) { headerVersion = 0; }
        string json = result.ToJson(headerVersion);
        if (o.TryGetValue("out", out var outPath)) File.WriteAllText(outPath, json + "\n");
        if (o.TryGetValue("html", out var htmlPath) && result.Facts != null) File.WriteAllText(htmlPath, result.Facts.ToHtml());
        Console.WriteLine(json);
        Console.Error.WriteLine(result.Ok ? $"verified: {result.Result}, end state {(result.EndState.Length == 0 ? "not reached" : result.EndState)}" : $"not verified: {result.Reason}: {result.Detail}");
        return result.Ok ? 0 : 1;
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
        if (facts.Generated != null) facts.Generated = facts.Generated with { PackageHash = o.GetValueOrDefault("package-hash", ScenarioOf(o)?.PackageHash ?? "") };
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
        // a golden run of a generated case has its package next to it (<name>.package.json) and is played from that package; the others share one load of the real content
        var plain = scripts.Where(s => !File.Exists(s.Replace(".actions.json", ".package.json"))).ToList();
        var caseIds = plain.Select(s => s.Replace(".actions.json", ".expected.json")).Where(File.Exists)
            .Select(f => (string)((Dictionary<string, object?>)MiniJson.Parse(File.ReadAllText(f))!)["case"]!).Distinct().ToArray();
        var shared = caseIds.Length == 0 ? null : ContentLoader.Load(Reader(o), caseIds);
        int pass = 0, total = 0;
        foreach (var script in scripts)
        {
            string expectedPath = script.Replace(".actions.json", ".expected.json");
            if (!File.Exists(expectedPath)) continue;
            total++;
            string name = Path.GetFileName(script).Replace(".actions.json", "");
            string scriptText = File.ReadAllText(script), expectedText = File.ReadAllText(expectedPath);
            string packagePath = script.Replace(".actions.json", ".package.json");
            var content = shared;
            if (File.Exists(packagePath))
            {
                var package = new ScenarioPackageReader(File.ReadAllText(packagePath));
                content = ContentLoader.Load(new CompositeContentReader(package, Reader(o)), package.CaseId);
            }
            if (content == null) return Fail($"no content for {name}");
            if (update)
            {
                var h = (Dictionary<string, object?>)MiniJson.Parse(expectedText)!;
                var fresh = GoldenRun.Expected(scriptText, (string)h["case"]!, ulong.Parse((string)h["seed"]!, CultureInfo.InvariantCulture),
                    Enum.Parse<SessionMode>((string)h["mode"]!, true), content, GoldenRun.Options(expectedText));
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
        var real = new CompositeContentReader(new FileContentReader(dir), new FileContentReader(fixtures));
        // a generated scenario package is a content folder of one case file, put in front of the real content (p1-live-scenario-generation-s10)
        return ScenarioOf(o) is { } scenario ? new CompositeContentReader(scenario, real) : real;
    }

    private static ScenarioPackageReader? ScenarioOf(Dictionary<string, string> o) =>
        o.TryGetValue("scenario", out var path) ? new ScenarioPackageReader(File.ReadAllText(path)) : null;

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
