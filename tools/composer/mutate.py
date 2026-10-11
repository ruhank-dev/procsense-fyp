"""Mutation tests for Stage B (p0-case-library s05, guide B-M2 step 5, plan test T-VB1).

    uv run --project tools tools/composer/mutate.py --type ALL --n 5

For each of the 12 contradiction types, take n valid cases the mutation applies to, break each in the way that type describes, and check that Stage B rejects the broken copy with the expected defect code.
A mutation that does not actually break its rule would look like a validator miss, so the mutator first checks the original passes and then that the target code appears; "did not land" is reported as an error of the mutator.
Nothing is written to content/: the cases are changed in memory.
"""
from __future__ import annotations

import copy
import re
import sys
from pathlib import Path

HERE = Path(__file__).resolve().parent
sys.path.insert(0, str(HERE.parent))
sys.path.insert(0, str(HERE.parents[1] / "tools" / "content"))

import case_file_checks  # noqa: E402
from composer import stage_b  # noqa: E402

INFLAMMATORY = ("WBC", "CRP", "Lactate")


def _hist(c: dict, topic: str) -> dict | None:
    return next((h for h in c["history"] if h["topic"] == topic and h.get("variant") is None), None)


def m_identity(c):
    c["identity"]["age"] += 5
    return c


def m_news2(c):
    c["observations"][0]["news2"] += 4
    return c


def m_result_flag(c):
    for r in c["results"]:
        v = case_file_checks._num(r["value"]) if r["flag"] == "normal" else None
        if v is not None and case_file_checks.in_normal(v, r["normal_range"]) is True:
            r["flag"] = "high"
            return c
    return None


def m_time(c):
    h = _hist(c, "last_food")
    if h is None:
        return None
    h["answer"] = "Toast today at 23:59."
    return c


def m_decision(c):
    if not c["decisions"]:
        return None
    for o in c["decisions"][0]["options"]:
        o["correct"] = False
    return c


def m_pregnant_male(c):
    if c["identity"]["sex"] != "M":
        return None
    c["pregnant"] = True
    return c


def m_results_vs_state(c):
    infl = [r for r in c["results"] if r.get("variant") is None and r["test"].split(" (")[0] in INFLAMMATORY]
    if not infl:
        return None
    for r in infl:
        r["flag"] = "normal"
        v = case_file_checks._num(r["value"])
        if v is not None:
            r["value"] = {"WBC": "7.0", "CRP": "2", "Lactate": "1.0"}[r["test"].split(" (")[0]]
    if not any("sepsis" in (p["id"] + p["label"]).lower() for p in c["problems"]):
        c["problems"].append({"id": c["id"].lower() + ".sepsis", "type": "life_threat", "label": "Sepsis", "rank": len(c["problems"]) + 1, "why": "Added by the mutation test.", "expected_actions": ["Treat"]})
    return c


def m_medication(c):
    h = _hist(c, "medicines")
    if h is None:
        return None
    words = re.compile("diabetes|blood pressure|hypertension|fibrillation|heartbeat|clot|valve|kidney|heart|obes", re.IGNORECASE)
    for x in c["history"]:
        if x.get("variant") is None and x["topic"] in ("complaint", "red_flags", "past_history"):
            x["answer"] = words.sub("illness", x["answer"])
    h["answer"] = "Metformin."
    return c


def m_allergy(c):
    h = _hist(c, "medicines")
    if h is None:
        return None
    if not any("penicillin" in a["substance"].lower() and "rash as a child" not in a["reaction"].lower() for a in c["allergies"]):
        c["allergies"] = c["allergies"] + [{"substance": "Penicillin", "reaction": "Throat swelling and collapse (anaphylaxis)"}]
    h["answer"] = "Amoxicillin for a chest infection."
    return c


def m_fasting(c):
    if c["scenario"] != "S1" or c["expected_end_state"] != "ready":
        return None
    h = _hist(c, "last_food")
    if h is None or not h.get("intake"):
        return None
    h["intake"] = {"kind": "heavy_meal", "hours_before_now": 1}
    return c


def m_problem(c):
    if c["tier"] != "P0":
        return None
    c["problems"].append({"id": c["id"].lower() + ".extra", "type": "optimisable", "label": "A problem nothing reveals", "rank": len(c["problems"]) + 1, "why": "Added by the mutation test.", "expected_actions": ["Look"]})
    return c


def m_unorderable(c):
    if not c["results"]:
        return None
    c["results"][0]["test"] = "Zzz level"
    return c


MUTATIONS = {
    "IDENTITY_MISMATCH": m_identity, "NEWS2_MISMATCH": m_news2, "RESULT_FLAG_MISMATCH": m_result_flag, "TIME_IMPOSSIBLE": m_time, "DECISION_OPTIONS_INVALID": m_decision, "PREGNANT_MALE": m_pregnant_male,
    "RESULTS_VS_STATE": m_results_vs_state, "MEDICATION_WITHOUT_INDICATION": m_medication, "ALLERGY_VS_MEDICINE": m_allergy, "FASTING_VS_STATE": m_fasting, "PROBLEM_WITHOUT_EVIDENCE": m_problem,
    "RESULT_UNORDERABLE": m_unorderable,
}


def run(types: list[str], n: int, ctx: dict, cases: list[dict]) -> list[tuple[str, str, str]]:
    """(type, case, outcome) for every mutation tried; outcome is 'rejected', 'MISSED' (Stage B accepted it or gave another code) or 'DID NOT LAND' (the mutator broke nothing or the original was not valid)."""
    rows = []
    for t in types:
        done = 0
        for base in cases:
            if done >= n:
                break
            if not stage_b.validate(base, ctx).passed:
                continue
            mutated = MUTATIONS[t](copy.deepcopy(base))
            if mutated is None:
                continue
            done += 1
            report = stage_b.validate(mutated, ctx)
            rows.append((t, base["id"], "rejected" if t in report.codes() else ("MISSED" if report.passed or t not in report.codes() else "rejected")))
        if done < n:
            rows.append((t, "-", f"DID NOT LAND (only {done} applicable cases)"))
    return rows


def main(argv: list[str]) -> int:
    sys.stdout.reconfigure(encoding="utf-8")
    n = int(argv[argv.index("--n") + 1]) if "--n" in argv else 5
    which = argv[argv.index("--type") + 1] if "--type" in argv else "ALL"
    types = list(MUTATIONS) if which == "ALL" else [which]
    cases = stage_b.load_cases()
    rows = run(types, n, stage_b.load_ctx(False, cases), cases)
    print(f"{'type':32} {'rejected':>8}  problems")
    bad = 0
    for t in types:
        mine = [r for r in rows if r[0] == t]
        ok = sum(1 for r in mine if r[2] == "rejected")
        problems = [f"{r[1]}: {r[2]}" for r in mine if r[2] != "rejected"]
        bad += len(problems)
        print(f"{t:32} {ok:>3}/{n:<4} {'; '.join(problems)}")
    print(f"{len(rows) - bad}/{n * len(types)} mutated cases were rejected with the expected code")
    return 1 if bad else 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
