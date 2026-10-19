"""Scores the rubric evaluator (tools/bench/rubric_eval.py) against the unchanged baseline gate on the SAME rows with the SAME ground truth: `tools/.venv/Scripts/python.exe tools/bench/rubric_compare.py --tag TAG --split dev|test|test2 [--stride N] [--write]`.

Ground truth: tests/action_bench_v1 (labels by construction; synthetic; one author family; NOT independent; the dev split is where the prompt was chosen, test and test2 were scored once with the frozen prompt). No model labelled anything.
Baseline: the unchanged production gate (EnsembleActClassifier), as recorded by tools/bench/nli_baseline.py in artifacts/ai-bench/nli/baseline-<split>.json; `reconcile` checks those recorded numbers against the committed reports/ai/act-benchmark*.json.

Objective-level view. Every (row, objective) pair of the 19 objectives is one decision. TRUE if the row's status is `actual` and its act is the objective (so a critical act IS a semantic positive: the interpretation is judged apart from credit). PREDICTED if the evaluator says SUPPORTED with
actor trainee, modality actual and evidence found in the utterance. The benchmark labels ONE act per sentence, so a second SUPPORTED objective on an `actual` row is counted as a false positive (strict); the lists of such cases are printed so a reader can see which are arguable.

Credit view (kept apart from interpretation): rubric credit = rubric_eval.credit (SUPPORTED, trainee, actual, evidence verified, non-critical objective); +guards adds the production critical-content cue and non-assertive checks, unchanged; AND-baseline credits only when the production gate also credits the SAME act. A row with
more than one credited objective counts as credited under a wrong act ("MULTI"). The baseline's recorded decisions are used as served. Abstention coverage = rows without a usable decision (an UNCERTAIN finding, a failed call or a malformed answer).
"""
from __future__ import annotations

import argparse
import collections
import json
import statistics
import sys
from pathlib import Path

sys.stdout.reconfigure(encoding="utf-8")
ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / "tools" / "bench"))
import nli_compare as NC  # noqa: E402
import rubric_eval as R  # noqa: E402

RUB = ROOT / "artifacts" / "ai-bench" / "rubric"
NLI = NC.NLI
ALL = [o["id"] for o in R.objectives()]


def load(tag: str, split: str, stride: int):
    rows = NC.rows_of(split)[::stride]
    raw = {}
    for l in (RUB / f"{tag}-{split}.jsonl").read_text(encoding="utf-8").splitlines():
        if l.strip():
            d = json.loads(l)
            raw[d["id"]] = d["raw"]
    rows = [r for r in rows if r["id"] in raw]
    base = {x["id"]: x for x in json.loads((NLI / f"baseline-{split}.json").read_text(encoding="utf-8"))["results"]}
    return rows, raw, base


def positive(ev: dict, o: str) -> bool:
    f = ev["results"][o]
    return f["verdict"] == "SUPPORTED" and f.get("actor") == "trainee" and f.get("modality") == "actual" and bool(f.get("evidence_in_utterance"))


def pct(a: int, b: int) -> str:
    return f"{a}/{b}"


def lat(xs: list[float]) -> dict:
    xs = sorted(xs)
    return {"n": len(xs), "median": round(statistics.median(xs)), "p95": round(xs[max(0, int(len(xs) * 0.95) - 1)]), "max": round(xs[-1])} if xs else {}


def analyse(tag: str, split: str, stride: int = 1) -> dict:
    rows, raw, base = load(tag, split, stride)
    elig = R.eligible(None)
    evs = {r["id"]: R.evaluate(r["text"], raw[r["id"]], elig) for r in rows}
    out: dict = {"tag": tag, "split": split, "rows": len(rows), "stride": stride}
    # ---- call health
    probs = collections.Counter(p for e in evs.values() for p in e["problems"])
    errs = collections.Counter(raw[r["id"]].get("error", "") for r in rows if raw[r["id"]].get("error"))
    out["call_health"] = {"calls": len(rows), "transport_errors": dict(errs), "not_json_or_schema": probs.get("NOT_JSON_OR_SCHEMA", 0), "truncated_at_token_cap": sum(1 for r in rows if raw[r["id"]].get("done_reason") == "length"),
                          "thinking_present": probs.get("THINKING_PRESENT", 0), "findings_defects": {k: v for k, v in probs.items() if k not in ("NOT_JSON_OR_SCHEMA", "THINKING_PRESENT")}, "rows_with_any_defect": sum(1 for e in evs.values() if e["problems"])}
    # ---- objective-level decisions
    pairs = []
    for r in rows:
        for o in ALL:
            pairs.append((r["act"] if (r["status"] == "actual" and r["act"] == o) else "-", o if positive(evs[r["id"]], o) else "-", o))
    tp = sum(1 for t, p, o in pairs if t != "-" and p != "-")
    fp = sum(1 for t, p, o in pairs if t == "-" and p != "-")
    fn = sum(1 for t, p, o in pairs if t != "-" and p == "-")
    pr, rc = (tp / (tp + fp) if tp + fp else 0.0), (tp / (tp + fn) if tp + fn else 0.0)
    per = {}
    for o in ALL:
        a = sum(1 for t, p, oo in pairs if oo == o and t != "-" and p != "-")
        b = sum(1 for t, p, oo in pairs if oo == o and t == "-" and p != "-")
        c = sum(1 for t, p, oo in pairs if oo == o and t != "-" and p == "-")
        if a + c:
            p_, r_ = (a / (a + b) if a + b else 0.0), a / (a + c)
            per[o] = {"tp": a, "fp": b, "fn": c, "precision": round(p_, 3), "recall": round(r_, 3), "f1": round(2 * p_ * r_ / (p_ + r_), 3) if p_ + r_ else 0.0}
    out["objective_level"] = {"decisions": len(pairs), "tp": tp, "fp": fp, "fn": fn, "precision": round(pr, 3), "recall": round(rc, 3), "f1": round(2 * pr * rc / (pr + rc), 3) if pr + rc else 0.0,
                              "macro_f1_over_objectives_with_support": round(sum(v["f1"] for v in per.values()) / len(per), 3) if per else None, "per_objective": per}
    # ---- label view on the core rows (same rows and truth as the NLI report)
    core = [r for r in rows if r["category"] in ("paraphrase", "asr_error", "critical", "critical_asr", "none")]
    def first_label(r):
        for f in (evs[r["id"]]["results"] and []):
            pass
        ev = raw[r["id"]]
        try:
            fs = json.loads(ev["content"])["findings"]
        except (KeyError, ValueError, TypeError):
            return "NONE"
        for f in fs:
            if f.get("objective") in ALL and positive(evs[r["id"]], f["objective"]):
                return f["objective"]
        return "NONE"
    rub_pairs = [(NC.truth(r), first_label(r)) for r in core]
    base_pairs = [(NC.truth(r), base[r["id"]]["act"]) for r in core]
    out["label_view_core"] = {"rows": len(core), "rubric_first_supported_else_NONE": {k: v for k, v in NC.prf(rub_pairs).items() if k != "per_class"}, "baseline_head": {k: v for k, v in NC.prf(base_pairs).items() if k != "per_class"},
                              "rubric_per_class": NC.prf(rub_pairs)["per_class"], "baseline_per_class": NC.prf(base_pairs)["per_class"], "rubric_confusion": NC.confusion(rub_pairs), "baseline_confusion": NC.confusion(base_pairs)}
    # ---- category breakdown of the interpretation
    cat = collections.defaultdict(lambda: collections.Counter())
    for r in rows:
        e, c = evs[r["id"]], cat[r["category"]]
        c["n"] += 1
        sup = [o for o in ALL if positive(e, o)]
        if r["status"] == "actual":
            c["named_act_supported"] += r["act"] in sup
            c["other_objective_also_supported"] += any(o != r["act"] for o in sup)
            c["no_objective_supported"] += not sup
        else:
            c["named_act_supported (WRONG)"] += r["act"] in sup
            c["any_objective_supported (WRONG)"] += bool(sup)
        c["critical_content_supported_under_an_ordinary_objective"] += (r["critical"] and any(o not in R.critical_ids() for o in sup))
        c["has_uncertain"] += any(v["verdict"] == "UNCERTAIN" for v in e["results"].values())
    out["by_category"] = {k: dict(v) for k, v in sorted(cat.items())}
    # ---- credit view
    def rubric_decision(r, guards: bool, and_base: bool):
        e = evs[r["id"]]
        cr = [o for o in ALL if R.credit(r["text"], e, o, guards)[0]]
        if and_base:
            cr = [o for o in cr if base[r["id"]]["credited"] and base[r["id"]]["act"] == o]
        return (bool(cr), cr[0] if len(cr) == 1 else ("MULTI" if cr else "NONE"))
    systems = {"baseline_gate_as_served": [(base[r["id"]]["credited"], base[r["id"]]["act"]) for r in rows],
               "rubric_credit_only": [rubric_decision(r, False, False) for r in rows],
               "rubric_credit_plus_production_guards": [rubric_decision(r, True, False) for r in rows],
               "rubric_AND_baseline_gate": [rubric_decision(r, True, True) for r in rows]}
    out["credit"] = {k: NC.credit_report(rows, v) for k, v in systems.items()}
    unusable = [r for r in rows if evs[r["id"]]["problems"] and not evs[r["id"]]["ok"] or any(v["verdict"] == "UNCERTAIN" for v in evs[r["id"]]["results"].values())]
    out["abstention_coverage"] = {"rows_without_usable_decision": len(unusable), "of": len(rows)}
    # ---- latency
    ok = [raw[r["id"]] for r in rows if not raw[r["id"]].get("error")]
    out["latency_ms"] = {"wall": lat([x["wall_ms"] for x in ok][1:]), "prompt_eval": lat([x["prompt_ms"] for x in ok][1:]), "generation": lat([x["gen_ms"] for x in ok][1:]),
                         "prompt_tokens_median": round(statistics.median([x["prompt_tokens"] for x in ok])) if ok else None, "output_tokens_median": round(statistics.median([x["output_tokens"] for x in ok])) if ok else None,
                         "output_tokens_p95": lat([x["output_tokens"] for x in ok]).get("p95") if ok else None, "first_call_excluded": True, "baseline_gate_ms": json.loads((NLI / f"baseline-{split}.json").read_text(encoding="utf-8"))["meta"]["latency_ms_per_utterance"]}
    # ---- examples
    ex = {"model_wrong_legit_not_supported": [], "model_wrong_adversarial_supported": [], "model_critical_under_ordinary": [], "gate_wrong_credit": [], "gate_missed_legit": []}
    dec = systems["rubric_credit_plus_production_guards"]
    for r, d in zip(rows, dec):
        e = evs[r["id"]]
        sup = [o for o in ALL if positive(e, o)]
        short = {"id": r["id"], "text": r["text"], "truth": r["act"], "status": r["status"], "category": r["category"], "supported": sup, "findings": [{"objective": oo, **{k: v for k, v in f.items() if k != "reported"}} for oo, f in e["results"].items() if f.get("reported")]}
        if r["status"] == "actual" and r["category"] in ("paraphrase", "asr_error") and r["act"] not in sup:
            ex["model_wrong_legit_not_supported"].append(short)
        if r["status"] != "actual" and r["status"] != "none" and sup:
            ex["model_wrong_adversarial_supported"].append(short)
        if r["critical"] and any(o not in R.critical_ids() for o in sup):
            ex["model_critical_under_ordinary"].append(short)
        if d[0] and not (r["expect_credit"] and d[1] == r["act"]):
            ex["gate_wrong_credit"].append({**short, "credited_as": d[1]})
        if r["expect_credit"] and r["act"] in sup and not d[0]:
            ex["gate_missed_legit"].append({**short, "why": [R.credit(r["text"], e, r["act"], True)[1]]})
    out["examples"] = {k: v[:6] for k, v in ex.items()}
    out["example_counts"] = {k: len(v) for k, v in ex.items()}
    return out


def reconcile() -> dict:
    """The baseline numbers recorded for this experiment against the committed benchmark reports (approach E_existing_gate)."""
    res = {}
    for split, f in (("test", "act-benchmark.json"), ("test2", "act-benchmark-test2.json")):
        p = ROOT / "reports" / "ai" / f
        if not (p.exists() and (NLI / f"baseline-{split}.json").exists()):
            continue
        d = json.loads(p.read_text(encoding="utf-8"))["approaches"]["E_existing_gate"]
        rows = NC.rows_of(split)
        base = {x["id"]: x for x in json.loads((NLI / f"baseline-{split}.json").read_text(encoding="utf-8"))["results"]}
        rep = NC.credit_report(rows, [(base[r["id"]]["credited"], base[r["id"]]["act"]) for r in rows])
        res[split] = {"recorded_in_reports": {k: (v if not isinstance(v, dict) else None) for k, v in d.items() if k in ("legit_recall", "credited_precision", "critical_false_credit", "non_assertive_false_credit")},
                      "report_keys": [k for k in d if not isinstance(d[k], dict)][:12], "rerun_now": {k: rep[k] for k in ("critical_false_credit", "adversarial_false_credit", "legitimate_credit_recall", "credited_total", "credited_precision")}}
    return res


def main(argv: list[str]) -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--tag", required=True)
    ap.add_argument("--split", required=True, choices=["dev", "test", "test2"])
    ap.add_argument("--stride", type=int, default=1)
    ap.add_argument("--write", action="store_true")
    ap.add_argument("--brief", action="store_true")
    a = ap.parse_args(argv)
    out = analyse(a.tag, a.split, a.stride)
    if a.write:
        p = ROOT / "reports" / "ai" / f"rubric-eval-{a.tag}-{a.split}.json"
        p.write_text(json.dumps(out, indent=1) + "\n", encoding="utf-8", newline="\n")
        print("wrote", p.relative_to(ROOT).as_posix())
    brief = {k: out[k] for k in ("rows", "call_health", "abstention_coverage", "latency_ms", "example_counts")}
    brief["objective_level"] = {k: v for k, v in out["objective_level"].items() if k != "per_objective"}
    brief["label_view_core"] = {k: out["label_view_core"][k] for k in ("rows", "rubric_first_supported_else_NONE", "baseline_head")}
    brief["by_category"] = out["by_category"]
    brief["credit"] = {k: {x: v[x] for x in ("critical_false_credit", "adversarial_false_credit", "legitimate_credit_recall", "credited_total", "credited_precision")} for k, v in out["credit"].items()}
    print(json.dumps(brief, indent=1))
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
