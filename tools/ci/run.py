"""Run the checks a change needs, reuse valid earlier passes, and say exactly what ran, what was reused and what was skipped.

    uv run --project tools python tools/ci/run.py [--base REF] [--head REF] [--staged] [--only a,b] [--heavy] [--dry-run] [--json]
    uv run --project tools python tools/ci/run.py --final        # the complete gate: every check, cache ignored, heavy Unity checks included

task change-check / task change-check-final are the shortcuts. Guide: docs/qa/change_aware_testing.md.
Heavy checks (full walkthrough diff, 4752-pose sweep, Android build, stereo benchmark) are listed as DEFERRED unless --heavy or --final.
Unity checks go through tools/ci/unity_check.py: one Unity at a time, hidden window. Independent groups (python, dotnet, backend) run in parallel.
Exit 1 when any executed check failed (a deferred or skipped check is never reported as a pass).
"""
from __future__ import annotations

import argparse
import json
import shutil
import subprocess
import sys
import time
from concurrent.futures import ThreadPoolExecutor
from pathlib import Path

import changeaware as ca

HERE = Path(__file__).resolve().parent


def commands_for(check: dict) -> list[list[str]]:
    if check.get("cmds"):
        return [list(c) for c in check["cmds"]]
    if check.get("pwsh"):
        return [[shutil.which("pwsh") or "pwsh", "-NoProfile", "-File", *check["pwsh"]]]
    u = check.get("unity")
    if u:
        cmd = [sys.executable, str(HERE / "unity_check.py"), u["mode"]]
        if u["mode"] == "tests":
            cmd += ["--platform", u["platform"]]
            if u.get("filter"):
                cmd += ["--filter", u["filter"]]
            for k, v in (u.get("env") or {}).items():
                cmd += ["--env", f"{k}={v}"]
            if u.get("frames_dir"):
                cmd += ["--frames-dir", u["frames_dir"]]
        return [cmd]
    raise ValueError(f"check {check['name']} has no cmds, pwsh or unity block")


def execute(check: dict) -> tuple[bool, float, str]:
    t0, tail = time.time(), ""
    for cmd in commands_for(check):
        exe = shutil.which(cmd[0])
        r = subprocess.run([exe or cmd[0], *cmd[1:]], cwd=ca.ROOT, capture_output=True, text=True, encoding="utf-8", errors="replace",
                           creationflags=0x08000000 if sys.platform == "win32" else 0)   # no console window: background-only
        out = (r.stdout or "") + (r.stderr or "")
        tail = out[-1500:]
        bad = check.get("fail_if_output") and check["fail_if_output"] in out
        if r.returncode != 0 or bad:
            return False, time.time() - t0, f"$ {' '.join(cmd)}\n{tail}"
    return True, time.time() - t0, tail


def main(argv: list[str]) -> int:
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--base"); ap.add_argument("--head"); ap.add_argument("--staged", action="store_true")
    ap.add_argument("--final", action="store_true", help="every check, no cache, heavy included")
    ap.add_argument("--heavy", action="store_true"); ap.add_argument("--only", default="")
    ap.add_argument("--dry-run", action="store_true"); ap.add_argument("--json", action="store_true")
    ap.add_argument("--no-cache", action="store_true")
    ap.add_argument("--skip", default="", help="comma list of check names to leave out; kind:unity leaves out every Unity check")
    a = ap.parse_args(argv)

    rules = ca.load_rules()
    if a.final:
        verdict = ca.select(rules, [], force_all=True, all_reason="--final: complete gate")
    else:
        try:
            verdict = ca.select(rules, ca.changed_files(a.base, a.head, a.staged))
        except Exception as exc:
            verdict = ca.select(rules, [], force_all=True, all_reason=f"selection error, so everything runs: {exc}")
    hashes = ca.file_hashes()
    only = {x for x in a.only.split(",") if x}
    skip = {x for x in a.skip.split(",") if x}
    use_cache = not (a.final or a.no_cache)

    plan: dict[str, tuple[str, str, str | None]] = {}   # name -> (state, why, key)
    for n, v in verdict["checks"].items():
        chk = rules["checks"][n]
        key = ca.cache_key(chk, hashes)
        if only and n not in only:
            plan[n] = ("skipped", "not in --only", key)
        elif n in skip or f"kind:{chk['kind']}" in skip:
            plan[n] = ("skipped", "left out by --skip", key)
        elif not v["run"]:
            plan[n] = ("skipped", v["why"], key)
        elif use_cache and (e := ca.cache_lookup(chk, key)):
            plan[n] = ("reused", f"passed earlier on identical inputs ({e['seconds']}s, key {key[:10]})", key)
        elif chk.get("heavy") and not (a.heavy or a.final):
            plan[n] = ("deferred", "heavy check; run with --heavy (or --final)", key)
        else:
            plan[n] = ("run", v["why"], key)

    results: dict[str, dict] = {}
    to_run = [n for n, (s, _, _) in plan.items() if s == "run"]
    if not a.dry_run:
        groups: dict[str, list[str]] = {}
        for n in to_run:
            groups.setdefault(rules["checks"][n].get("group", n), []).append(n)

        def run_group(names: list[str]):
            for n in names:
                chk = rules["checks"][n]
                if not a.json:
                    print(f"  running {n} ...", flush=True)
                ok, secs, note = execute(chk)
                if not chk.get("informational"):
                    ca.cache_store(chk, plan[n][2], ok, secs, note)
                results[n] = {"passed": ok, "seconds": round(secs, 1), "note": "" if ok else note}
                if not ok:
                    return   # fast failure inside a group: later checks of that group would only repeat the noise
        with ThreadPoolExecutor(max_workers=max(1, len(groups))) as pool:
            list(pool.map(run_group, groups.values()))

    summary = []
    failed = False
    for n, (state, why, key) in plan.items():
        r = results.get(n)
        if state == "run" and r is None:
            state = "dry-run" if a.dry_run else "not-run (earlier check in its group failed)"
            failed = failed or not a.dry_run
        elif r:
            state = "PASSED" if r["passed"] else "FAILED"
            failed = failed or not r["passed"]
        summary.append({"check": n, "state": state, "why": why, "seconds": (r or {}).get("seconds"), "key": key[:12]})
    report = {"full": verdict["full"], "reason": verdict["reason"], "final": a.final, "failed": failed, "checks": summary}
    try:
        (ca.cache_dir()).mkdir(parents=True, exist_ok=True)
        (ca.cache_dir() / "last_report.json").write_text(json.dumps(report, indent=1))
    except OSError:
        pass
    if a.json:
        print(json.dumps(report, indent=2))
    else:
        print(f"\nChange-aware run: {verdict['reason']}")
        for s in summary:
            print(f"  {s['state']:<9} {s['check']:<30} {'' if s['seconds'] is None else str(s['seconds']) + 's':>8}  {s['why'][:110]}")
        counts = {k: sum(1 for s in summary if s["state"] == k) for k in ("PASSED", "FAILED", "reused", "skipped", "deferred")}
        print("  " + ", ".join(f"{v} {k}" for k, v in counts.items() if v))
        for n, r in results.items():
            if not r["passed"]:
                print(f"\n--- {n} FAILED ---\n{r['note']}")
    return 1 if failed else 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
