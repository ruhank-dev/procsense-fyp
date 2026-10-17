"""Measures the AI debrief on real sessions (p1-ai-language-s08, EXP-AI-4): `uv run --project tools tools/ai/eval_debrief.py --model qwen3.5:4b-q4_K_M [--sessions 16]`.

Plays golden scripts (the correct, edge, novice and dangerous paths of P0 cases) through the engine, makes each session's report, and gives it to the production rewriter (server/debrief/rewrite.py). Reported:
how many rewrites the strict validator accepted (the AI version replaced the template), how many fell back to the template and why, latency (the plan wants the rewrite within 8 s, shown after the template),
and the mutation rejection rate of the validator on accepted rewrites (each accepted rewrite is corrupted in four ways: a number changed, a verdict flipped, a medicine added, a fact dropped; every corruption must be refused).
The accepted rewrites are written to the output for a person to read: the validator is lexical and cannot judge whether the wording is kind or faithful in spirit (DT-203).
"""
import argparse
import copy
import json
import re
import statistics
import subprocess
import sys
import tempfile
import time
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / "server"))
sys.path.insert(0, str(ROOT / "tools"))
CLI = ["dotnet", "run", "--no-build", "-c", "Release", "--project", "src/ProcSense.Cli", "--"]


def report_of(case, script, tmp):
    r = subprocess.run(CLI + ["run", "--case", case, "--seed", "42", "--actions", str(script), "--out", str(tmp / "o.json"), "--allow-draft"], cwd=ROOT, capture_output=True, text=True)
    if r.returncode:
        return None
    r = subprocess.run(CLI + ["report", "--case", case, "--log", str(tmp / "o.session.jsonl"), "--out", str(tmp / "r.html"), "--json", str(tmp / "r.json"), "--allow-draft"], cwd=ROOT, capture_output=True, text=True)
    return json.loads((tmp / "r.json").read_text(encoding="utf-8")) if r.returncode == 0 else None


if __name__ == "__main__":
    ap = argparse.ArgumentParser()
    ap.add_argument("--model", default="qwen3.5:4b-q4_K_M")
    ap.add_argument("--sessions", type=int, default=16)
    ap.add_argument("--timeout", type=float, default=90.0)
    ap.add_argument("--version", type=int, default=None, help="prompt version of debrief.rewrite (default: the latest)")
    ap.add_argument("--tag", default="", help="suffix for the result file, so two versions can be kept side by side")
    a = ap.parse_args()
    from debrief import rewrite as R
    from llm import client as L
    client = L.OllamaClient()
    if a.model not in client.models():
        raise SystemExit(f"{a.model} is not installed")
    rw = R.Rewriter(client, model=a.model, timeout=a.timeout, version=a.version)
    scripts = []
    for kind in ("correct", "dangerous", "novice", "edge"):
        for p in sorted((ROOT / "tests" / "golden").glob(f"*_{kind}.actions.json")):
            case = p.name.split("_")[0]
            if (ROOT / "content" / "case_files" / f"{case}.json").exists():
                scripts.append((case, p))
    scripts = scripts[:: max(1, len(scripts) // a.sessions)][: a.sessions]
    used = {"ai": 0, "template": 0}
    reasons, lat, samples = {}, [], []
    mut_total = mut_rejected = 0
    for case, script in scripts:
        with tempfile.TemporaryDirectory() as t:
            rep = report_of(case, script, Path(t))
        if rep is None:
            continue
        out = rw.make(rep)
        used[out["used"]] += 1
        lat.append(out["latency_ms"])
        for r in out["reasons"]:
            reasons[r] = reasons.get(r, 0) + 1
        if out["used"] == "ai":
            facts = R.facts_of(rep)
            secs = out["ai"]
            muts = []
            m = copy.deepcopy(secs); m[0]["text"] += " It took 37 minutes."; muts.append(m)
            m = copy.deepcopy(secs); m[0]["text"] += " You also gave penicillin."; muts.append(m)
            m = copy.deepcopy(secs); m[0]["fact_ids"] = m[0]["fact_ids"][1:] or m[0]["fact_ids"]; muts.append(m if m[0]["fact_ids"] != secs[0]["fact_ids"] else None)
            m = copy.deepcopy(secs)
            for s in m:
                if all(f["verdict"] == "credit" for f in facts if f["id"] in s["fact_ids"]):
                    s["text"] = "You made the wrong decision and forgot the checks."
                    muts.append(m)
                    break
            for m in muts:
                if m is None:
                    continue
                mut_total += 1
                mut_rejected += bool(R.check(m, facts, rw.lexicon))
            if len(samples) < 6:
                samples.append({"case": case, "template": out["template"], "ai": out["ai"]})
    out = {"model": a.model, "prompt_version": a.version, "digest": client.models().get(a.model, ""), "sessions": sum(used.values()), "ai_used": used["ai"], "template_kept": used["template"], "accept_rate": round(used["ai"] / max(sum(used.values()), 1), 3),
           "reasons": reasons, "latency_ms_p50": round(statistics.median(lat)) if lat else 0, "latency_ms_max": max(lat) if lat else 0, "within_8s_share": round(sum(x <= 8000 for x in lat) / max(len(lat), 1), 3),
           "mutations": mut_total, "mutations_rejected": mut_rejected, "samples": samples}
    path = ROOT / "artifacts" / "ai-bench" / f"debrief-eval{a.tag}.json"
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(out, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")
    print(json.dumps({k: v for k, v in out.items() if k != "samples"}, indent=1))
