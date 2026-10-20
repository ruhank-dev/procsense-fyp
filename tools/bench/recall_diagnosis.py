"""Per-example false-rejection diagnosis of legitimate speech recall (ADR-0022 follow-up): `tools/.venv/Scripts/python.exe tools/bench/recall_diagnosis.py --split dev [--write]`.

Traces every legitimate row (status actual, categories paraphrase and asr_error) through the stages of the interpretation path in the order production applies them, from cached embeddings (no service needed once cached):
  1 transcript            the text as recognised (asr_error rows carry the benchmark's seeded damage)
  2 context               the row's act is eligible in the state the benchmark derives from the engine's windows (tools/bench/interpretation_bench.py)
  3 candidate generation  the hybrid head's probabilities: is the row's act the top act, at what probability and rank
  4 critical-mass cap     the head's probability for ANY critical act against `critical_mass_max`
  5 per-act threshold     the top act's own credit threshold (None: the act is never credited)
  6 critical-content cue  content/ai/critical_cues.json
  7 second reader         the TF-IDF head must name the same act
  8 assertion layer       server/classify/assertion.py (denied, hypothetical, reported, intended, other speaker, incomplete)
  9 status and context    question form for a statement act, waiting question, waiting read-back, behaviour already credited
 10 evidence store        the engine's EvidenceStore (not reached by a row refused earlier; window, threshold, duplicate and critical checks are exercised by the engine tests and the benchmark)
For a missed row it records EVERY stage that blocks it (not only the first), a primary cause (the first blocker in that order, with a wrong top act reported as its own cause), and whether the miss is an INCORRECT rejection (the sentence does perform the act and is eligible) or a CORRECT
or AMBIGUOUS one (a human reading of the sentence is the judge; the benchmark label is by construction, and every sentence flagged here as arguable is listed for a person).
It then measures, on the same split, what removing or changing ONE check would do to legitimate recall AND to the unsafe rows (critical, non-assertive, wrong-act credit): the safety cost of each relaxation. Dev only: held-out splits are traced after the fixes are frozen.
"""
from __future__ import annotations

import argparse
import collections
import copy
import json
import sys
from pathlib import Path

sys.stdout.reconfigure(encoding="utf-8")
ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / "server"))
sys.path.insert(0, str(ROOT / "tools" / "bench"))
from classify.assertion import non_assertive  # noqa: E402
from classify.ensemble import EnsembleActClassifier  # noqa: E402
from classify.guard import critical_cue  # noqa: E402
from interpret.decide import Interpreter  # noqa: E402
from interpret.status import status_of  # noqa: E402
from interpret.validate import phrases  # noqa: E402
import engine_acceptance as E  # noqa: E402
import interpretation_bench as B  # noqa: E402
import nli_zero_shot as N  # noqa: E402

CACHE = ROOT / "artifacts" / "ai-bench" / "interp"
LEGIT = ("paraphrase", "asr_error")


def load_embeddings(split: str) -> dict:
    return json.loads((CACHE / f"emb-{split}.json").read_text(encoding="utf-8"))


def trace(clf: EnsembleActClassifier, row: dict, ctx) -> dict:
    """Every stage's verdict for one row. Nothing here changes a decision: it explains the decision the interpreter makes."""
    text, label = row["text"], row["act"]
    result, probs = clf.classify_with_probs(text)
    first = clf.embedding
    top, p_top = result.act, probs[result.act]
    ranked = sorted(probs, key=lambda a: -probs[a])
    mass, cap = first.critical_mass(probs), first.head.get("critical_mass_max")
    thr = first.head["thresholds"].get(top)
    other = clf.tfidf.classify(text).act
    status = status_of(text)
    elig = [e for e in ctx.eligible if e.act == top]
    t = {"id": row["id"], "text": text, "category": row["category"], "label": label, "top": top, "p_top": round(p_top, 3), "p_label": round(probs[label], 3), "rank_of_label": ranked.index(label) + 1,
         "second": ranked[1] if ranked[0] == top else ranked[0], "mass": round(mass, 4), "cap": cap, "cap_hit": cap is not None and mass >= cap, "threshold": thr, "above_threshold": thr is not None and p_top >= thr,
         "cue": critical_cue(text), "tfidf_act": other, "heads_agree": other == top, "non_assertive": non_assertive(text), "status": status,
         "top_is_critical": top in first.critical, "top_is_none": top == "NONE", "top_eligible": bool(elig), "gate_credited": bool(result.credited), "gate_refused": result.refused}
    blockers = []
    if top == "NONE":
        blockers.append("TOP_IS_NONE")
    elif top in first.critical:
        blockers.append("TOP_IS_CRITICAL_ACT")
    elif top != label:
        blockers.append("WRONG_ACT")
    if t["cap_hit"]:
        blockers.append("CRITICAL_MASS_CAP")
    if top not in first.critical and top != "NONE" and not t["above_threshold"]:
        blockers.append("BELOW_THRESHOLD" if thr is not None else "ACT_NEVER_CREDITED")
    if t["cue"]:
        blockers.append("CRITICAL_CUE")
    if top not in first.critical and top != "NONE" and not t["heads_agree"]:
        blockers.append("HEADS_DISAGREE")
    if t["non_assertive"]:
        blockers.append("ASSERTION_" + t["non_assertive"].upper())
    if status == "question" and top not in phrases()["question_ok_acts"]:
        blockers.append("QUESTION_FORM_FOR_STATEMENT_ACT")
    if not elig and top not in ("NONE",) and top not in first.critical:
        blockers.append("TOP_NOT_ELIGIBLE")
    t["blockers"] = blockers
    t["primary"] = blockers[0] if blockers else "NONE"
    t["only_blocker"] = blockers[0] if len(blockers) == 1 else None
    return t


def interpreter_with(clf: EnsembleActClassifier, cap="keep") -> Interpreter:
    c = copy.copy(clf)
    c.embedding = copy.copy(clf.embedding)
    c.embedding.head = copy.copy(clf.embedding.head)
    if cap != "keep":
        if cap is None:
            c.embedding.head.pop("critical_mass_max", None)
        else:
            c.embedding.head["critical_mass_max"] = cap
    return Interpreter(c)


def outcome(interp: Interpreter, rows: list[dict]) -> dict:
    legit = [r for r in rows if r["category"] in LEGIT]
    adv = [r for r in rows if r["category"] in B.NC.ADVERSARIAL]
    crit = [r for r in rows if r["category"].startswith("critical")]
    offers = {}
    for r in rows:
        o = interp.interpret(B.conditions(r)["in_window"], r["text"], r["id"], 0)
        offers[r["id"]] = o["evidence"]["act"] if o["decision"] == "OFFER_EVIDENCE" else None
    right = sum(1 for r in legit if offers[r["id"]] == r["act"])
    wrong_legit = sum(1 for r in legit if offers[r["id"]] and offers[r["id"]] != r["act"])
    return {"legit_right": right, "legit_wrong_act": wrong_legit, "legit_missed": len(legit) - right - wrong_legit, "adversarial_offered": sum(1 for r in adv if offers[r["id"]]), "critical_offered": sum(1 for r in crit if offers[r["id"]]),
            "offers_total": sum(1 for v in offers.values() if v), "denominators": {"legit": len(legit), "adversarial": len(adv), "critical": len(crit)}, "_offers": offers}


def main(argv: list[str]) -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--split", default="dev", choices=["dev", "test", "test2"])
    ap.add_argument("--cap", type=float, default=0.03, help="the critical-mass cap to diagnose (default 0.03: the cap before the 20 Oct 2026 revision)")
    ap.add_argument("--engine", default="default", choices=["default", "calibrated"], help="the engine thresholds to judge the offers with (default: the 0.8 placeholder, as before the revision)")
    ap.add_argument("--write", action="store_true")
    a = ap.parse_args(argv)
    rows = N.load_rows(a.split)
    emb = load_embeddings(a.split)
    clf = EnsembleActClassifier(lambda t: emb.get(t))
    clf.embedding.head = copy.copy(clf.embedding.head)
    clf.embedding.head["critical_mass_max"] = a.cap
    base = interpreter_with(clf)
    ctxs = {r["id"]: B.conditions(r)["in_window"] for r in rows}
    traces = [trace(clf, r, ctxs[r["id"]]) for r in rows if r["category"] in LEGIT]
    off = outcome(base, rows)
    for t in traces:
        t["offered"] = off["_offers"][t["id"]]
        t["missed"] = t["offered"] != t["label"]
    # stage 10, the engine: every offer is run through the real EvidenceStore
    calibrated = {act: __import__("math").floor(t * 1000) / 1000 for act, t in clf.embedding.head["thresholds"].items() if t is not None}
    items = []
    for r in rows:
        o = base.interpret(ctxs[r["id"]], r["text"], r["id"], 0)
        if o["decision"] == "OFFER_EVIDENCE":
            items.append({"id": r["id"], "act": o["evidence"]["act"], "confidence": o["evidence"]["confidence"], "steps": ctxs[r["id"]].steps})
    verdicts = {v["id"]: v for v in E.accept(items, {} if a.engine == "default" else calibrated)}
    for t in traces:
        v = verdicts.get(t["id"])
        t["engine"] = None if v is None else ("accepted" if v["accepted"] else v["reason"])
        t["accepted_right"] = bool(v and v["accepted"] and t["offered"] == t["label"])
        if t["primary"] == "NONE" and v is not None and not v["accepted"]:
            t["primary"] = "ENGINE_" + v["reason"]                      # the gate and the rules offered it; the engine's evidence store refused it
            t["blockers"] = [t["primary"]]
        t["missed"] = not t["accepted_right"]
    missed = [t for t in traces if t["missed"]]
    by_primary = collections.Counter(t["primary"] for t in missed)
    by_any = collections.Counter(b for t in missed for b in t["blockers"])
    only = collections.Counter(t["only_blocker"] for t in missed if t["only_blocker"])
    cap_tab = collections.Counter(("label_is_top" if t["top"] == t["label"] else "label_not_top", "cap_hit" if t["cap_hit"] else "cap_not_hit") for t in missed)
    cap_variants = {}
    for name, cap in (("baseline (cap 0.03)", "keep"), ("cap removed", None), ("cap 0.05", 0.05), ("cap 0.10", 0.10), ("cap 0.20", 0.20), ("cap 0.50", 0.50)):
        o = outcome(interpreter_with(clf, cap), rows)
        cap_variants[name] = {k: v for k, v in o.items() if k != "_offers"}
    out = {"split": a.split, "cap": a.cap, "engine_thresholds": a.engine, "synthetic": True, "independent": False, "legitimate_rows": len(traces), "missed": len(missed), "offered_but_refused_by_engine": sum(1 for t in traces if t["engine"] not in (None, "accepted")), "right": len(traces) - len(missed),
           "primary_cause_of_missed": dict(by_primary.most_common()), "any_blocker_of_missed": dict(by_any.most_common()), "only_blocker_of_missed": dict(only.most_common()),
           "cap_vs_top_act": {f"{k[0]} / {k[1]}": v for k, v in cap_tab.items()}, "cap_variants_in_window": cap_variants,
           "by_category": {c: {"rows": sum(1 for t in traces if t["category"] == c), "missed": sum(1 for t in missed if t["category"] == c)} for c in LEGIT},
           "by_label": {l: {"rows": sum(1 for t in traces if t["label"] == l), "missed": sum(1 for t in missed if t["label"] == l)} for l in sorted({t["label"] for t in traces})},
           "traces": traces}
    if a.write:
        p = ROOT / "reports" / "ai" / f"recall-diagnosis-{a.split}{'' if a.cap == 0.03 and a.engine == 'default' else '-shipped'}.json"
        p.write_text(json.dumps(out, indent=1) + "\n", encoding="utf-8", newline="\n")
        print("wrote", p.relative_to(ROOT).as_posix())
    print(json.dumps({k: v for k, v in out.items() if k != "traces"}, indent=1))
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
