"""The golden suite's other three paths for each P0 case (p0-evaluation-performance-s02): novice, dangerous and edge, derived from the case's correct path.

    uv run --project tools tools/golden/paths.py --write     # write tests/golden/<case>_<path>.actions.json (and an expected stub for a new one; then: task golden -- --update)
    uv run --project tools tools/golden/paths.py --check     # the scripts are up to date, and every expected file behaves as its path promises

The paths (the step's definitions):
- novice: the correct path without what a novice forgets that is not critical (introducing oneself, summarising back to the patient, answering the patient's question); the case must end
  as the correct path does, and cost something: a lost item, fewer credits, or a patient who trusts the trainee less (introducing oneself and summarising change only trust today, DT-175).
- dangerous: a safety rule is broken: the trainee orders and takes blood before the patient's identity is confirmed (the walkthrough's first safety lesson). The engine must refuse it
  (a blocker), or the case must end in a safety failure or with a critical item.
- edge: timing corner cases that must change nothing: a pause and a resume at once after the identity check, and every time-skip split in two halves. The final state must equal the correct path's
  (the same state hash); only the log differs.
Deriving the paths from the correct ones keeps them in step when a correct path changes; --check proves each still means what it says.
"""
from __future__ import annotations

import json
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
GOLDEN = ROOT / "tests" / "golden"
PATHS = ("novice", "dangerous", "edge")
NOVICE_DROPS = {"introduce_self", "summarise", "answer_question"}


def cases() -> list[str]:
    return sorted(p.name[: -len("_correct.actions.json")] for p in GOLDEN.glob("*_correct.actions.json") if p.name[0].isupper() and "-" in p.name)


def load(case: str, path: str = "correct") -> list[dict]:
    return json.loads((GOLDEN / f"{case}_{path}.actions.json").read_text(encoding="utf-8"))


def novice(rows: list[dict]) -> list[dict]:
    return [r for r in rows if r["verb"] not in NOVICE_DROPS]


def dangerous(rows: list[dict]) -> list[dict]:
    first = rows[0]["t"] if rows else 0.0
    early = [{"t": first, "verb": "order_investigation", "params": {"test": "fbc"}}, {"t": first, "verb": "take_sample", "params": {"test": "fbc", "tube": "edta_purple"}}]
    return early + rows                                     # bloods before the identity check, then the case goes on as the correct path


def edge(rows: list[dict]) -> list[dict]:
    out: list[dict] = []
    for r in rows:
        if r["verb"] == "time_skip":
            half = float(r["params"]["minutes"]) / 2
            text = repr(half) if half != int(half) else str(int(half))
            out.append({"t": r["t"], "verb": "time_skip", "params": {"minutes": text}})
            out.append({"t": round(r["t"] + half * 60, 1), "verb": "time_skip", "params": {"minutes": text}})   # the clock has moved by the first half
            continue
        out.append(r)
        if r["verb"] == "confirm_identity":
            out.append({"t": r["t"], "verb": "pause"})
            out.append({"t": r["t"], "verb": "resume"})
    return out


MAKERS = {"novice": novice, "dangerous": dangerous, "edge": edge}


def text(rows: list[dict]) -> str:
    return "[\n" + ",\n".join("  " + json.dumps(r, ensure_ascii=False) for r in rows) + "\n]\n"


def expected(case: str, path: str) -> dict | None:
    p = GOLDEN / f"{case}_{path}.expected.json"
    return json.loads(p.read_text(encoding="utf-8")) if p.exists() else None


def lost(e: dict) -> int:
    s = e.get("score", {})
    return len(s.get("minor", [])) + len(s.get("major", [])) + len(s.get("critical", []))


def problems() -> list[str]:
    out = []
    for case in cases():
        rows = load(case)
        correct = expected(case, "correct")
        for path in PATHS:
            want = text(MAKERS[path](rows))
            script = GOLDEN / f"{case}_{path}.actions.json"
            if not script.exists() or script.read_text(encoding="utf-8") != want:
                out.append(f"{script.name} is out of date: run tools/golden/paths.py --write, then task golden -- --update")
                continue
            e = expected(case, path)
            if e is None or correct is None or "score" not in e:
                out.append(f"{case}_{path}.expected.json is missing or has no score: run task golden -- --update")
                continue
            facts, cfacts = e["facts"], correct["facts"]
            if path == "novice":
                if facts.get("end_state") != cfacts.get("end_state"):
                    out.append(f"{case} novice: ends {facts.get('end_state')}, the correct path {cfacts.get('end_state')}; a forgotten courtesy must not change the outcome")
                cheaper = lost(e) > lost(correct) or e["score"]["credits"] < correct["score"]["credits"] or facts.get("trust", 0) < cfacts.get("trust", 0)
                if not cheaper:
                    out.append(f"{case} novice: costs nothing compared with the correct path (no lost item, no credit fewer, the patient's trust not lower)")
            elif path == "dangerous":
                refused = any(r.get("reason") == "IDENTITY_NOT_CONFIRMED" for r in facts.get("rejections", [])) and len(facts.get("rejections", [])) > len(cfacts.get("rejections", []))
                failed = facts.get("end_state") == "SAFETY_FAILURE" or e["score"]["result"] == "FAIL" or len(e["score"]["critical"]) > 0
                if not (refused or failed):
                    out.append(f"{case} dangerous: the engine neither refused it nor failed the case")
            elif path == "edge":
                if e["state_hash"] != correct["state_hash"]:
                    out.append(f"{case} edge: the final state differs from the correct path's (a pause or a split skip changed something)")
    return out


def write() -> None:
    for case in cases():
        rows = load(case)
        for path in PATHS:
            (GOLDEN / f"{case}_{path}.actions.json").write_text(text(MAKERS[path](rows)), encoding="utf-8", newline="\n")
            stub = GOLDEN / f"{case}_{path}.expected.json"
            if not stub.exists():
                c = expected(case, "correct") or {}
                stub.write_text(json.dumps({"case": case, "seed": c.get("seed", "42"), "mode": c.get("mode", "practice")}) + "\n", encoding="utf-8", newline="\n")
    print(f"wrote {len(cases()) * len(PATHS)} scripts for {len(cases())} cases; now run: task golden -- --update")


def main(argv: list[str]) -> int:
    sys.stdout.reconfigure(encoding="utf-8")
    if "--write" in argv:
        write()
        return 0
    probs = problems()
    for p in probs:
        print("  - " + p)
    n = len(cases())
    print(f"golden paths: {n} cases x 4 paths = {n * 4} scripts; {'all behave as their path promises' if not probs else str(len(probs)) + ' problems'}")
    return 1 if probs else 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
