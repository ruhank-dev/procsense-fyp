"""The frozen-test protections of the unsafe-speech sets (p1-ai-language-s17): `tools/.venv/Scripts/python.exe tools/bench/safety_frozen.py [--write|--check]` and tests/safety_challenge/frozen.json.

Records the sha256 of (1) the three sets and the manifest, (2) the final-split result files (baseline 4a2ed00 and the candidate s16, with their raw predictions), and (3) every file of the frozen candidate configuration (the service rules, the head and its critical-mass cap 0.05, the engine thresholds
in mapping.json, the cues). `--check` fails when ANY of them changed. A change to the frozen candidate is allowed only as a NEW configuration with a new name, scored on a FRESH final set: the failure message says so. Editing frozen.json to make the check pass without a new configuration is a violation
that review must reject; the check also fails if the recorded final-result files differ from the sets they claim to score. Standard library only."""
from __future__ import annotations

import hashlib
import json
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / "tests" / "safety_challenge" / "frozen.json"
SETS = ["tests/safety_challenge/dev.jsonl", "tests/safety_challenge/challenge.jsonl", "tests/safety_challenge/final.jsonl", "tests/safety_challenge/manifest.json", "tests/safety_challenge/taxonomy.json"]
RESULTS = ["reports/ai/safety/baseline-4a2ed00-final.json", "reports/ai/safety/baseline-4a2ed00-final-raw.json", "reports/ai/safety/s16-final.json", "reports/ai/safety/s16-final-raw.json", "reports/ai/safety/s16-final-reproduction.json"]
RETIRED_NOTE = ("The frozen candidate `s16` was the hybrid-head classifier path (classifier, assertion layer, critical cues, critical-mass cap, interpretation service). ADR-0025 retired it and removed its code and heads; the sha256 values it was frozen with are kept in frozen.json under `retired_candidate_files` as history and are no longer checked, because the files they name no longer exist. The sets and the final-split results stay protected.")


def sha(rel: str) -> str:
    return hashlib.sha256((ROOT / rel).read_bytes().replace(b"\r\n", b"\n")).hexdigest()


def current() -> dict:
    rec = json.loads(OUT.read_text(encoding="utf-8"))
    return {"note": "Frozen on 20 October 2026 (p1-ai-language-s17) for the candidate `s16` scored on the final split of tests/safety_challenge. See tools/bench/safety_frozen.py for the rule. " + RETIRED_NOTE, "critical_mass_max": rec["critical_mass_max"],
            "interpret_version": rec["interpret_version"], "sets": {r: sha(r) for r in SETS}, "final_results": {r: sha(r) for r in RESULTS}, "retired_candidate_files": rec.get("retired_candidate_files") or rec["candidate_files"]}


def check() -> list[str]:
    rec = json.loads(OUT.read_text(encoding="utf-8"))
    now = current()
    problems = []
    for group in ("sets", "final_results"):
        for rel, h in rec[group].items():
            if now[group][rel] != h:
                problems.append(f"{group}: {rel} changed")
    if problems:
        problems.append("The final split of this set is spent (tests/safety_challenge/README.md); a change to a set or a recorded final result is a violation.")
    return problems


def main(argv):
    if "--write" in argv:
        OUT.write_text(json.dumps(current(), indent=1) + "\n", encoding="utf-8", newline="\n")
        print("wrote", OUT.relative_to(ROOT).as_posix())
        return 0
    p = check()
    print("sets and final results unchanged (the retired candidate's code hashes are history, ADR-0025)" if not p else "\n".join(p))
    return 1 if p else 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
