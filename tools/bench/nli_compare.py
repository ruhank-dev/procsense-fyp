"""BART-large-MNLI zero-shot against the current classifier on the SAME sentences with the SAME ground truth: `tools/.venv/Scripts/python.exe tools/bench/nli_compare.py [--write]` (needs artifacts/ai-bench/nli/<split>.json and baseline-<split>.json).

Ground truth (tools/bench/nli_config.json `ground_truth`): tests/action_bench_v1 rows, labels by construction, synthetic. CORE rows (status actual): the 20-way label is the family's act (a critical act, or NONE for small talk). ADVERSARIAL rows (denied, intended, hypothetical, reported, other_actor,
incomplete): correct credit is none; for 20-way label scores the label is the derived NONE (rubric rule 10 extended), reported apart and flagged DERIVED. No model labelled anything; there is no human-labelled set (EX-01, EX-18).

Split discipline. The abstention threshold and the per-label credit thresholds are computed from the DEV split only (functions below take only dev). TEST and TEST2 are then scored with those fixed numbers. The dev numbers are shown but are fitted. The functions refuse to read a threshold from any other split.

Reported per split and for each of the two systems: accuracy and macro-F1 (forced choice, and with abstention), per-class precision/recall/F1, the confusion matrix, the rows of the confusing pairs, and, through the credit gate, false credits per category, legitimate-credit recall and credited precision.
For BART the gate is the existing policy with the parts that have a counterpart: no credit for a critical label or NONE, the per-label threshold from classify.calibrate (credited precision 0.8 on dev), and in the guarded variant the deterministic cue and assertion checks of the production gate. The production gate's critical-mass cap and second-reader
agreement need a trained head's class probabilities and a second head and are not applied. Latency, device, memory, revision and versions come from the result files.
"""
from __future__ import annotations

import argparse
import collections
import json
import sys
from pathlib import Path

sys.stdout.reconfigure(encoding="utf-8")
ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / "server"))
from classify import classifier as C  # noqa: E402
from classify.assertion import non_assertive  # noqa: E402
from classify.guard import critical_cue  # noqa: E402

NLI = ROOT / "artifacts" / "ai-bench" / "nli"
BENCH = ROOT / "tests" / "action_bench_v1"
CONFIG = json.loads((ROOT / "tools" / "bench" / "nli_config.json").read_text(encoding="utf-8"))
TAX = C.taxonomy()
LABELS = [a["id"] for a in TAX["acts"]]
CRITICAL = {a["id"] for a in TAX["acts"] if a["critical"]}
ORDINARY = set(LABELS) - CRITICAL - {"NONE"}
ADVERSARIAL = {"negation", "intention", "hypothetical", "reported", "actor_change", "incomplete"}
PAIRS = [("CHECK_UNDERSTANDING", "ASK_TOPIC"), ("CLARIFY", "CHECK_UNDERSTANDING"), ("ESCALATE_SPEECH", "VOICE_ORDER"), ("CLOSED_LOOP", "ACKNOWLEDGE_FACT"), ("EXPLAIN_ACTION", "EXPLAIN_PROCEDURE"), ("EXPLAIN_TEST", "EXPLAIN_PROCEDURE"),
         ("REASSURE", "ACKNOWLEDGE_EMOTION"), ("SBAR_ELEMENT", "HANDOVER_CRITICAL_ELEMENT"), ("ANSWER_QUESTION", "EXPLAIN_PROCEDURE")]


def rows_of(split: str) -> list[dict]:
    return [json.loads(l) for l in (BENCH / f"{split}.jsonl").read_text(encoding="utf-8").splitlines() if l.strip()]


def truth(r: dict) -> str:
    return r["act"] if r["status"] in ("actual", "none") else "NONE"


def load(split: str):
    rows = rows_of(split)
    nli = json.loads((NLI / f"{split}.json").read_text(encoding="utf-8"))
    base = json.loads((NLI / f"baseline-{split}.json").read_text(encoding="utf-8"))
    by_n = {x["id"]: x for x in nli["results"]}
    by_b = {x["id"]: x for x in base["results"]}
    assert [r["id"] for r in rows] == [x["id"] for x in nli["results"]] == [x["id"] for x in base["results"]], f"{split}: the three files do not hold the same rows in the same order"
    return rows, by_n, by_b, nli["meta"], base["meta"]


def top(scores: dict) -> tuple[str, float]:
    k = max(scores, key=scores.get)
    return k, scores[k]


# ---- thresholds: from DEV only ------------------------------------------------------------------------------------------------------------------------------------------------------------
def abstention_tau(dev_rows, dev_nli) -> float:
    """The smallest tau on a 0.01 grid at which the accuracy of the answered CORE dev rows reaches the target."""
    core = [(truth(r), *top(dev_nli[r["id"]]["scores"])) for r in dev_rows if r["status"] in ("actual", "none")]
    for i in range(0, 100):
        tau = i / 100
        ans = [(t, p) for t, p, s in core if s >= tau]
        if ans and sum(t == p for t, p in ans) / len(ans) >= CONFIG["abstention_rule"]["target_answered_accuracy"]:
            return tau
    return 1.0


def credit_thresholds(dev_rows, dev_nli) -> dict:
    items = [(truth(r), *top(dev_nli[r["id"]]["scores"])) for r in dev_rows if r["status"] in ("actual", "none")]
    return C.calibrate(items, TAX, C.MIN_PRECISION)


# ---- metrics ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------
def prf(pairs: list[tuple[str, str | None]]) -> dict:
    labels = sorted({t for t, _ in pairs})
    per = {}
    for l in labels:
        tp = sum(1 for t, p in pairs if t == l and p == l)
        fp = sum(1 for t, p in pairs if t != l and p == l)
        fn = sum(1 for t, p in pairs if t == l and p != l)
        pr, rc = (tp / (tp + fp) if tp + fp else 0.0), (tp / (tp + fn) if tp + fn else 0.0)
        per[l] = {"precision": round(pr, 3), "recall": round(rc, 3), "f1": round(2 * pr * rc / (pr + rc), 3) if pr + rc else 0.0, "support": tp + fn}
    n = len(pairs)
    return {"n": n, "accuracy": round(sum(t == p for t, p in pairs) / n, 3) if n else None, "macro_f1": round(sum(v["f1"] for v in per.values()) / len(per), 3) if per else None, "per_class": per}


def confusion(pairs) -> dict:
    m: dict[str, dict[str, int]] = collections.defaultdict(lambda: collections.defaultdict(int))
    for t, p in pairs:
        m[t][p or "ABSTAIN"] += 1
    return {t: dict(v) for t, v in sorted(m.items())}


def gate_nli(r: dict, scores: dict, thr: dict, guarded: bool) -> tuple[bool, str]:
    act, s = top(scores)
    if act in CRITICAL or act == "NONE":
        return False, act
    t = thr.get(act)
    if t is None or s < t:
        return False, act
    if guarded and (critical_cue(r["text"]) or non_assertive(r["text"])):
        return False, act
    return True, act


def credit_report(rows, decisions: list[tuple[bool, str]]) -> dict:
    cats = collections.defaultdict(lambda: {"n": 0, "credited": 0, "right_act": 0})
    for r, (cr, act) in zip(rows, decisions):
        c = cats[r["category"]]
        c["n"] += 1
        c["credited"] += cr
        c["right_act"] += cr and act == r["act"]
    out = {}
    for k, c in sorted(cats.items()):
        d = {"rows": c["n"], "credited": c["credited"]}
        if k in ("paraphrase", "asr_error"):
            d["recall"] = f"{c['right_act']}/{c['n']}"
        else:
            d["false_credit"] = f"{c['credited']}/{c['n']}"
        out[k] = d
    legit = [(r, d) for r, d in zip(rows, decisions) if r["category"] in ("paraphrase", "asr_error")]
    credited = [(r, d) for r, d in zip(rows, decisions) if d[0]]
    wrong_credit = [(r, d) for r, d in credited if not (r["expect_credit"] and d[1] == r["act"])]
    return {"by_category": out, "critical_false_credit": f"{sum(1 for r, d in credited if r['category'].startswith('critical'))}/{sum(1 for r in rows if r['category'].startswith('critical'))}",
            "adversarial_false_credit": f"{sum(1 for r, d in credited if r['category'] in ADVERSARIAL)}/{sum(1 for r in rows if r['category'] in ADVERSARIAL)}",
            "legitimate_credit_recall": f"{sum(1 for r, d in legit if d[0] and d[1] == r['act'])}/{len(legit)}", "credited_total": len(credited),
            "credited_precision": f"{len(credited) - len(wrong_credit)}/{len(credited)}" if credited else "0/0"}


def evaluate(split: str, tau: float, thr: dict) -> dict:
    rows, by_n, by_b, nmeta, bmeta = load(split)
    core_i = [i for i, r in enumerate(rows) if r["status"] in ("actual", "none")]
    adv_i = [i for i, r in enumerate(rows) if r["status"] not in ("actual", "none")]
    nli_top = [top(by_n[r["id"]]["scores"]) for r in rows]
    out = {"split": split, "rows": len(rows), "core_rows": len(core_i), "adversarial_rows": len(adv_i), "nli_meta": {k: nmeta[k] for k in ("model", "revision", "device", "dtype", "torch", "transformers", "peak_gpu_memory_mb", "process_rss_mb", "latency_ms_per_utterance")},
           "baseline_latency_ms_per_utterance": bmeta["latency_ms_per_utterance"]}
    systems = {"bart_forced": lambda i: nli_top[i][0], "bart_with_abstention": lambda i: nli_top[i][0] if nli_top[i][1] >= tau else None, "current": lambda i: by_b[rows[i]["id"]]["act"]}
    for name, f in systems.items():
        core = [(truth(rows[i]), f(i)) for i in core_i]
        adv = [(truth(rows[i]), f(i)) for i in adv_i]
        answered = [(t, p) for t, p in core if p is not None]
        out[name] = {"core": {**prf(core), "answered": len(answered), "accuracy_of_answered": round(sum(t == p for t, p in answered) / len(answered), 3) if answered else None, "confusion": confusion(core)},
                     "adversarial_derived": {"n": len(adv), "label_accuracy_derived_NONE": round(sum(t == p for t, p in adv) / len(adv), 3) if adv else None,
                                             "predicted_as_ordinary_act": sum(1 for _, p in adv if p in ORDINARY), "predicted_as_critical_act": sum(1 for _, p in adv if p in CRITICAL)}}
        pc = collections.Counter()
        for t, p in core:
            if p and t != p and any({t, p} == set(pair) for pair in PAIRS):
                pc[f"{t}->{p}"] += 1
        out[name]["confusing_pair_errors"] = dict(pc)
    out["credit_gate"] = {"current_gate_as_served": credit_report(rows, [(by_b[r["id"]]["credited"], by_b[r["id"]]["act"]) for r in rows]),
                          "bart_thresholds_only": credit_report(rows, [gate_nli(r, by_n[r["id"]]["scores"], thr, False) for r in rows]),
                          "bart_thresholds_plus_production_cue_and_assertion_checks": credit_report(rows, [gate_nli(r, by_n[r["id"]]["scores"], thr, True) for r in rows])}
    # by-category label accuracy (forced choice), core categories only
    cat = collections.defaultdict(lambda: [0, 0, 0])
    for i in core_i:
        c = cat[rows[i]["category"]]
        c[0] += 1
        c[1] += nli_top[i][0] == truth(rows[i])
        c[2] += by_b[rows[i]["id"]]["act"] == truth(rows[i])
    out["core_label_accuracy_by_category"] = {k: {"n": v[0], "bart": f"{v[1]}/{v[0]}", "current": f"{v[2]}/{v[0]}"} for k, v in sorted(cat.items())}
    wrong = [(r, nli_top[i]) for i, r in enumerate(rows) if r["status"] == "actual" and r["category"] == "paraphrase" and nli_top[i][0] != r["act"]]
    out["bart_failure_examples"] = [{"text": r["text"], "truth": r["act"], "bart": p[0], "score": round(p[1], 3), "ranked": sorted(by_n[r["id"]]["scores"], key=by_n[r["id"]]["scores"].get, reverse=True)[:3], "current": by_b[r["id"]]["act"]} for r, p in wrong[:10]]
    return out


def probes() -> dict:
    """Latency probes and the wording probe, kept in the report because their files live outside Git."""
    out = {}
    for name, f in (("gpu_float16_first_run_dev_488", "dev_fp16_first_run.json"), ("gpu_float32_dev_first_40", "dev_probe_float32.json"), ("cpu_float32_dev_first_40", "dev_probe_cpu.json")):
        p = NLI / f
        if p.exists():
            m = json.loads(p.read_text(encoding="utf-8"))["meta"]
            out[name] = {"device": m["device"], "dtype": m["dtype"], "median_ms": m["latency_ms_per_utterance"]["median"], "p95_ms": m["latency_ms_per_utterance"]["p95"], "n": m["latency_ms_per_utterance"]["n"], "peak_gpu_memory_mb": m["peak_gpu_memory_mb"]}
    p = NLI / "dev_no_parentheses.json"
    if p.exists():
        d = json.loads(p.read_text(encoding="utf-8"))
        rows = {r["id"]: r for r in rows_of("dev")}
        core = [(truth(rows[x["id"]]), top(x["scores"])[0]) for x in d["results"] if rows[x["id"]]["status"] in ("actual", "none")]
        out["wording_probe_dev_only_no_parentheses"] = {"core_accuracy": round(sum(t == q for t, q in core) / len(core), 3), "n": len(core), "note": "definitions with their parentheses removed; dev only; the pre-registered wording stays the reported one"}
    return out


def main(argv: list[str]) -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--write", action="store_true")
    a = ap.parse_args(argv)
    dev_rows, dev_nli, _, _, _ = load("dev")
    tau, thr = abstention_tau(dev_rows, dev_nli), credit_thresholds(dev_rows, dev_nli)
    report = {"synthetic": True, "independent": False, "tau": tau, "per_label_credit_thresholds_from_dev": thr, "config": {k: CONFIG[k] for k in ("model", "mode", "hypothesis_template", "hypothesis_overrides", "abstention_rule")},
              "splits": {s: evaluate(s, tau, thr) for s in ("dev", "test", "test2")}, "probes": probes()}
    for s, d in report["splits"].items():
        print(f"{s:6s} tau {tau:.2f} | BART forced acc {d['bart_forced']['core']['accuracy']} macroF1 {d['bart_forced']['core']['macro_f1']} | with abstention acc-answered {d['bart_with_abstention']['core']['accuracy_of_answered']} answered {d['bart_with_abstention']['core']['answered']}/{d['core_rows']} | "
              f"current acc {d['current']['core']['accuracy']} macroF1 {d['current']['core']['macro_f1']}")
        for g, v in d["credit_gate"].items():
            print(f"        {g:58s} critical {v['critical_false_credit']}  adversarial {v['adversarial_false_credit']}  legit recall {v['legitimate_credit_recall']}  precision {v['credited_precision']}")
    if a.write:
        p = ROOT / "reports" / "ai" / "nli-zero-shot.json"
        p.write_text(json.dumps(report, indent=1) + "\n", encoding="utf-8", newline="\n")
        print("wrote", p.relative_to(ROOT).as_posix())
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
