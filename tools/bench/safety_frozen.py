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
CANDIDATE = ["server/classify/assertion.py", "server/classify/ensemble.py", "server/classify/embedding.py", "server/classify/hybrid.py", "server/classify/tfidf.py", "server/classify/guard.py", "server/interpret/decide.py", "server/interpret/validate.py", "server/interpret/status.py",
             "content/assessment/act_head_hybrid.json", "content/assessment/mapping.json", "content/ai/critical_cues.json", "content/ai/interpret_phrases.json", "content/ai/lexicon.json", "content/ai/thresholds.json"]


def sha(rel: str) -> str:
    return hashlib.sha256((ROOT / rel).read_bytes().replace(b"\r\n", b"\n")).hexdigest()


def current() -> dict:
    head = json.loads((ROOT / "content/assessment/act_head_hybrid.json").read_text(encoding="utf-8"))
    return {"note": "Frozen on 20 October 2026 (p1-ai-language-s17) for the candidate `s16` scored on the final split of tests/safety_challenge. See tools/bench/safety_frozen.py for the rule.", "critical_mass_max": head["critical_mass_max"],
            "interpret_version": "interpret-2026-10-20.2", "sets": {r: sha(r) for r in SETS}, "final_results": {r: sha(r) for r in RESULTS}, "candidate_files": {r: sha(r) for r in CANDIDATE}}


def check() -> list[str]:
    rec = json.loads(OUT.read_text(encoding="utf-8"))
    now = current()
    problems = []
    for group in ("sets", "final_results", "candidate_files"):
        for rel, h in rec[group].items():
            if now[group][rel] != h:
                problems.append(f"{group}: {rel} changed")
    if now["critical_mass_max"] != rec["critical_mass_max"]:
        problems.append("critical_mass_max changed")
    if problems:
        problems.append("A change to the frozen candidate must be a NEW named configuration scored on a FRESH final set (tests/safety_challenge/README.md); the final split of this set is spent for every configuration after s16.")
    return problems


def main(argv):
    if "--write" in argv:
        OUT.write_text(json.dumps(current(), indent=1) + "\n", encoding="utf-8", newline="\n")
        print("wrote", OUT.relative_to(ROOT).as_posix())
        return 0
    p = check()
    print("frozen candidate, sets and final results unchanged" if not p else "\n".join(p))
    return 1 if p else 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
