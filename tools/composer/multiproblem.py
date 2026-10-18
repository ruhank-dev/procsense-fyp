"""Multi-problem composition engine (p2-content-extensions-s02, DT-220): the structure of a NEW combination of problems, built by the walkthrough's 14B.5 recipe from problems of approved cases.

    uv run --project tools tools/composer/multiproblem.py                 # write tests/composition/compositions.json (seed 20261018, 12 compositions)
    uv run --project tools tools/composer/multiproblem.py --check         # fail if the committed file is out of date or any composition breaks a rule (CI)
    uv run --project tools tools/composer/multiproblem.py --seed 7 --count 5 --out FILE

The recipe, as the engine runs it (docs/context/ProcSense_Medical_Walkthrough_v5_1.md 14B.5; rules in tools/composer/composition/rules.json):
  1 each problem and its branch   a module = one problem of an approved case (type, label, expected actions, source case and problem id)
  2 mark the type                 the module keeps the type its case gave it (life threat, safety blocker, time-critical, pathway-changing, optimisable, information-only)
  3 order by the ladder           life threat first ... information-only last; ties keep the source rank
  4 check drugs, fluids, scans    the seven cross-check categories the problems touch are listed; checking the VALUES is authoring work
  5 decide the end state          S1 with an optimisable problem ends postponed; otherwise the most diverted end state of the sources; S2 as its sources
  6 deterioration clock           set by the first (most dangerous) problem of the ladder; no numbers are written (they live in the deterioration tables)
  7 handover items                every problem + action + time (a time only where a source action states one)
  8 approval                      recorded as not done: ADR-0008 makes review optional, so nothing here waits for it, and nothing here claims it
Constraints that make a combination coherent, all checked by `validate`: one scenario (S1 or S2); 2 to 4 problems; at most 3 life threats; no two problems from the same source case; no repeated topic; sex and age requirements
(pregnancy needs a female patient, "a minor" needs a minor) must not conflict; no action that one problem forbids and another requires; the combination must be NEW (its topic set is not the topic set of any authored case);
every source case must be approved. The engine writes NO clinical value: the observations, results and history that make the problems true together still have to be authored from sourced tables, each with a source row, and
then pass Stage B and the golden runs. Every output is a synthetic DRAFT planning fixture (`clinically_validated: false`, `practice_only: true`), never a case file.
"""
from __future__ import annotations

import argparse
import hashlib
import itertools
import json
import random
import re
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
RULES = Path(__file__).resolve().parent / "composition" / "rules.json"
CASES = ROOT / "content" / "case_files"
OUT = ROOT / "tests" / "composition" / "compositions.json"
SEED, COUNT = 20261018, 12
APPROVED = {"APPROVED_FOR_TRAINING", "APPROVED_FOR_ASSESSMENT"}
TIME = re.compile(r"within \d+\s*(?:minutes?|mins?|hours?|hrs?|h)\b|\bnow\b|\d+\s*minutes?", re.IGNORECASE)
STOP = {"the", "and", "with", "for", "any", "from", "that", "this", "then", "also"}
SCHEMA = {
    "type": "object", "required": ["id", "scenario", "problems", "end_state", "end_state_basis", "clock", "handover_items", "cross_checks", "requires", "authoring_required", "status", "synthetic", "clinically_validated", "practice_only", "recipe"],
    "additionalProperties": False,
    "properties": {
        "id": {"type": "string", "pattern": "^MP-S[12]-[0-9a-f]{8}$"}, "scenario": {"enum": ["S1", "S2"]}, "status": {"const": "DRAFT"}, "synthetic": {"const": True}, "clinically_validated": {"const": False}, "practice_only": {"const": True},
        "problems": {"type": "array", "minItems": 2, "maxItems": 4, "items": {"type": "object", "required": ["order", "type", "label", "source_case", "source_problem", "source_rank", "topics", "checks"], "additionalProperties": False, "properties": {
            "order": {"type": "integer", "minimum": 1}, "type": {"type": "string"}, "label": {"type": "string"}, "source_case": {"type": "string"}, "source_problem": {"type": "string"}, "source_rank": {"type": "integer"},
            "topics": {"type": "array", "items": {"type": "string"}}, "checks": {"type": "array", "items": {"type": "string"}}}}},
        "end_state": {"enum": ["ready", "delayed", "postponed", "redirected"]}, "end_state_basis": {"type": "string"},
        "clock": {"type": "object", "required": ["set_by", "type", "note"], "additionalProperties": False, "properties": {"set_by": {"type": "string"}, "type": {"type": "string"}, "note": {"type": "string"}}},
        "handover_items": {"type": "array", "items": {"type": "object", "required": ["problem", "action", "time"], "additionalProperties": False, "properties": {"problem": {"type": "string"}, "action": {"type": "string"}, "time": {"type": ["string", "null"]}}}},
        "cross_checks": {"type": "array", "items": {"type": "string"}}, "requires": {"type": "object", "additionalProperties": False, "properties": {"sex": {"type": ["string", "null"]}, "minor": {"type": "boolean"}}},
        "authoring_required": {"type": "array", "items": {"type": "string"}}, "recipe": {"type": "object"}}}


def rules() -> dict:
    return json.loads(RULES.read_text(encoding="utf-8"))


def topics_of(text: str, r: dict) -> list[str]:
    low = f" {text.lower()} "
    return sorted(t for t, words in r["topics"].items() if any(w in low for w in words))


def modules(r: dict | None = None) -> list[dict]:
    """Every problem of every approved case, as a module."""
    r = r or rules()
    out = []
    for f in sorted(CASES.glob("*.json")):
        c = json.loads(f.read_text(encoding="utf-8"))
        if c["status"] not in APPROVED:
            continue
        for p in c["problems"]:
            out.append({"source_case": c["id"], "scenario": c["scenario"], "end_state": c["expected_end_state"], "source_problem": p["id"], "source_rank": p["rank"], "type": p["type"], "label": p["label"],
                        "checks": list(p["expected_actions"]), "topics": topics_of(p["label"] + " " + " ".join(p["expected_actions"]), r)})
    return out


def corpus_topic_sets(r: dict) -> set[frozenset]:
    sets = set()
    for f in CASES.glob("*.json"):
        c = json.loads(f.read_text(encoding="utf-8"))
        sets.add(frozenset(t for p in c["problems"] for t in topics_of(p["label"] + " " + " ".join(p["expected_actions"]), r)))
    return sets


def _stems(text: str) -> set[str]:
    return {w[:6] for w in re.findall(r"[a-z][a-z-]+", text.lower()) if len(w) > 3 and w not in STOP}


def contradictions(mods: list[dict], r: dict) -> list[str]:
    """Actions that one problem forbids and another requires (plain text, can only miss a conflict, never invent one)."""
    found = []
    for a, b in itertools.permutations(mods, 2):
        for x in a["checks"]:
            low = x.lower()
            for m in r["contradiction_markers"]:
                if low.startswith(m):
                    phrase = _stems(low[len(m):])
                    if phrase and any(phrase <= _stems(y) and not any(y.lower().startswith(k) for k in r["contradiction_markers"]) for y in b["checks"]):
                        found.append(f"{a['source_case']}:{a['source_problem']} says '{x}' against {b['source_case']}:{b['source_problem']}")
    return found


def requirements(mods: list[dict], r: dict) -> tuple[dict, list[str]]:
    sexes = {r["requires_sex"][t] for m in mods for t in m["topics"] if t in r["requires_sex"]}
    minor = any(any(k in m["label"].lower() for k in r["requires_minor"]) for m in mods)
    problems = []
    if len(sexes) > 1:
        problems.append("conflicting sex requirements")
    return {"sex": sorted(sexes)[0] if len(sexes) == 1 else None, "minor": minor}, problems


def reasons_against(mods: list[dict], r: dict, known: set[frozenset]) -> list[str]:
    """Why this set of modules is NOT a coherent new combination (empty list: it is)."""
    why = []
    if not r["min_problems"] <= len(mods) <= r["max_problems"]:
        why.append("size")
    if len({m["scenario"] for m in mods}) > 1:
        why.append("mixes S1 and S2")
    if len({m["source_case"] for m in mods}) < len(mods):
        why.append("two problems from the same source case")
    if sum(m["type"] == "life_threat" for m in mods) > r["max_life_threats"]:
        why.append("too many life threats")
    seen = [t for m in mods for t in m["topics"]]
    if len(seen) != len(set(seen)):
        why.append("a topic is repeated")
    req, bad = requirements(mods, r)
    why += bad
    why += [f"contradiction: {c}" for c in contradictions(mods, r)]
    if frozenset(seen) in known or not seen:
        why.append("not new: same topics as an authored case")
    return why


def compose(mods: list[dict], r: dict) -> dict:
    """The composition record for a set of modules that passed `reasons_against`."""
    rank = {t: i for i, t in enumerate(r["ladder"])}
    ordered = sorted(mods, key=lambda m: (rank[m["type"]], m["source_rank"], m["source_case"]))
    scenario = ordered[0]["scenario"]
    ids = "|".join(sorted(f"{m['source_case']}:{m['source_problem']}" for m in ordered))
    order_end = r["end_state_order"]
    worst = max((m["end_state"] for m in ordered), key=order_end.index)
    if scenario == "S1" and any(m["type"] == "optimisable" for m in ordered):
        end, basis = r["s1_optimisable_end_state"], "recipe step 5: S1 with an optimisable problem ends " + r["s1_optimisable_end_state"]
    else:
        end, basis = worst, "recipe step 5: the most diverted end state of the source cases"
    if end_idx(order_end, end) < end_idx(order_end, worst):
        end, basis = worst, basis + "; raised to the most diverted source end state"
    req, _ = requirements(ordered, r)
    touched = sorted({t for m in ordered for t in m["topics"] if t in r["cross_checks"]} | ({"blood thinners"} if any("blood thinners" in m["topics"] for m in ordered) else set()))
    return {
        "id": f"MP-{scenario}-{hashlib.sha1(ids.encode()).hexdigest()[:8]}", "scenario": scenario,
        "problems": [{"order": i + 1, "type": m["type"], "label": m["label"], "source_case": m["source_case"], "source_problem": m["source_problem"], "source_rank": m["source_rank"], "topics": m["topics"], "checks": m["checks"]} for i, m in enumerate(ordered)],
        "end_state": end, "end_state_basis": basis,
        "clock": {"set_by": ordered[0]["source_problem"], "type": ordered[0]["type"], "note": "recipe step 6: the most dangerous untreated problem sets the speed; the numbers are the deterioration tables'"},
        "handover_items": [{"problem": m["label"], "action": a, "time": (TIME.search(a).group(0) if TIME.search(a) else None)} for m in ordered for a in m["checks"]],
        "cross_checks": touched, "requires": req,
        "authoring_required": ["observations, results, history, medicines and timings that make these problems true together (each value with a source row, ADR-0008)", "the decision prompts and options for the combined case", "Stage B and golden runs of the authored case"],
        "status": "DRAFT", "synthetic": True, "clinically_validated": False, "practice_only": True,
        "recipe": {"source": r["source"], "steps_run": [1, 2, 3, 4, 5, 6, 7], "step_8": "approval not recorded (optional under ADR-0008); nothing here claims it"},
    }


def end_idx(order: list[str], v: str) -> int:
    return order.index(v)


def validate(comp: dict, mods_by_key: dict, r: dict, known: set[frozenset]) -> list[str]:
    """Problems with one composition record: schema, ladder order, end state, every problem traceable to an approved source problem, and the coherence rules again."""
    import jsonschema
    errs = [e.message for e in jsonschema.Draft202012Validator(SCHEMA).iter_errors(comp)]
    if errs:
        return errs
    rank = {t: i for i, t in enumerate(r["ladder"])}
    if [rank[p["type"]] for p in comp["problems"]] != sorted(rank[p["type"]] for p in comp["problems"]):
        errs.append("not in ladder order")
    if [p["order"] for p in comp["problems"]] != list(range(1, len(comp["problems"]) + 1)):
        errs.append("order numbers")
    mods = []
    for p in comp["problems"]:
        m = mods_by_key.get((p["source_case"], p["source_problem"]))
        if m is None:
            errs.append(f"{p['source_case']}:{p['source_problem']} is not a problem of an approved case")
        elif (m["type"], m["label"], m["checks"]) != (p["type"], p["label"], p["checks"]):
            errs.append(f"{p['source_problem']} differs from its source")
        else:
            mods.append(m)
    if len(mods) == len(comp["problems"]):
        errs += reasons_against(mods, r, known)
        if compose(mods, r) != comp:
            errs.append("does not match what the recipe produces from its sources")
    return errs


def build(seed: int = SEED, count: int = COUNT) -> list[dict]:
    r = rules()
    mods = modules(r)
    known = corpus_topic_sets(r)
    rng = random.Random(seed)
    cands = []
    for scen in ("S1", "S2"):
        pool = [m for m in mods if m["scenario"] == scen]
        for size in (2, 3, 4):
            if size == 4:
                combos = (tuple(rng.sample(pool, 4)) for _ in range(4000))
            else:
                combos = itertools.combinations(pool, size)
            for combo in combos:
                if not reasons_against(list(combo), r, known):
                    cands.append(compose(list(combo), r))
    uniq = {c["id"]: c for c in cands}
    ids = sorted(uniq)
    rng.shuffle(ids)
    chosen, used = [], {}
    for i in ids:                                              # diversity: no source problem is used more than twice, and the scenarios alternate
        c = uniq[i]
        keys = [(p["source_case"], p["source_problem"]) for p in c["problems"]]
        if all(used.get(k, 0) < 2 for k in keys) and sum(x["scenario"] == c["scenario"] for x in chosen) < (count + 1) // 2:
            chosen.append(c)
            for k in keys:
                used[k] = used.get(k, 0) + 1
        if len(chosen) == count:
            break
    return sorted(chosen, key=lambda c: c["id"])


def document(seed: int, count: int) -> dict:
    comps = build(seed, count)
    return {"synthetic": True, "clinically_validated": False, "practice_only": True, "seed": seed, "count": len(comps), "rules": "tools/composer/composition/rules.json",
            "note": "Synthetic DRAFT planning fixtures made by tools/composer/multiproblem.py from problems of approved cases by the walkthrough's 14B.5 recipe. They are not case files and not clinically validated; the clinical values that make each combination true must still be authored from sourced tables (p2-content-extensions-s02, DT-220).",
            "compositions": comps}


def main(argv: list[str]) -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--seed", type=int, default=SEED)
    ap.add_argument("--count", type=int, default=COUNT)
    ap.add_argument("--out", default=str(OUT))
    ap.add_argument("--check", action="store_true")
    a = ap.parse_args(argv)
    doc = document(a.seed, a.count)
    text = json.dumps(doc, indent=1, ensure_ascii=False) + "\n"
    if a.check:
        r = rules()
        by_key = {(m["source_case"], m["source_problem"]): m for m in modules(r)}
        known = corpus_topic_sets(r)
        committed = json.loads(Path(a.out).read_text(encoding="utf-8"))
        bad = [(c["id"], e) for c in committed["compositions"] for e in validate(c, by_key, r, known)]
        if bad:
            print("composition rules broken:", *bad, sep="\n  ")
            return 1
        if Path(a.out).read_text(encoding="utf-8") != text:
            print(f"{a.out} is out of date: run tools/composer/multiproblem.py")
            return 1
        print(f"ok: {len(committed['compositions'])} compositions valid and current")
        return 0
    Path(a.out).parent.mkdir(parents=True, exist_ok=True)
    Path(a.out).write_text(text, encoding="utf-8", newline="\n")
    print(f"wrote {a.out}: {doc['count']} compositions")
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
