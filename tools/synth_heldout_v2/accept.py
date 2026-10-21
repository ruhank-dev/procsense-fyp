"""Final acceptance run of the Gemma speech evaluator on the FINAL FRESH HELD-OUT set v2 (HO2) (ADR-0025): `server/.venv/Scripts/python.exe tools/synth_heldout_v2/accept.py freeze | run | report`.

freeze   records, before any model call, the SHA-256 of the dataset and its manifest, the acceptance criteria, the prompt (version and hash), the evaluator, the contract, the engine's scoring files and the model name in tests/synthetic_heldout_v2/eval_config.json.
run      refuses unless the frozen configuration is intact and no scored run exists; asks the model (free plan, hard call and token ceilings, stops on 401/402/403/429) for the set and for the partial-credit addendum; stores every raw reply; writes RUN_LOCK.json.
         There is exactly one scored run: a second one is refused.
report   recomputes everything from the stored replies with no model call (credits come from the engine itself through the CLI), checks the criteria of acceptance.json and writes reports/ai/speech-evaluation-heldout.json.
AI-written and AI-reviewed synthetic data: the result is acceptance evidence for the software, not independent human validation."""
from __future__ import annotations

import hashlib
import json
import math
import os
import subprocess
import sys
from datetime import datetime, timezone
from pathlib import Path

HERE = Path(__file__).resolve().parent
ROOT = HERE.parents[1]
SET = ROOT / "tests" / "synthetic_heldout_v2"
os.environ["SYNTH_SET_DIR"] = str(SET)                      # the synth_dev harness reads this
sys.path.insert(0, str(ROOT))
sys.path.insert(0, str(ROOT / "tools" / "synth_dev"))
CONFIG, LOCK = SET / "eval_config.json", SET / "RUN_LOCK.json"
RAW, RAW_PARTIAL = SET / "run_raw.json", SET / "run_partial_raw.json"
REPORT = ROOT / "reports" / "ai" / "speech-evaluation-heldout-v2.json"
CODE = ["server/evaluate/prompt.py", "server/evaluate/examples.py", "server/evaluate/wording.json", "server/evaluate/contract.py", "server/evaluate/reader.py", "server/evaluate/rubric.py", "server/evaluate/provider.py", "server/evaluate/service.py",
        "content/assessment/mapping.json", "content/assessment/taxonomy.json", "content/scoring/rubric.json", "src/ProcSense.Core/Scoring/Evidence.cs", "src/ProcSense.Core/Scoring/CommunicationRubric.cs",
        "src/ProcSense.Core/Scoring/CommunicationScorer.cs", "src/ProcSense.Core/Scoring/SpeechScoreCheck.cs", "src/ProcSense.Core/Speech/SpeechStateGate.cs", "tools/synth_dev/evaluator_run.py", "tools/synth_dev/evaluator_metrics.py", "tools/synth_dev/engine_score.py"]
DATA = ["dataset.jsonl", "manifest.json", "specs.json", "utterances.json", "acceptance.json", "partial_credit.jsonl", "partial_credit.manifest.json"]


def sha(p: Path) -> str:
    return hashlib.sha256(p.read_bytes().replace(b"\r\n", b"\n")).hexdigest()


def wilson(k: int, n: int) -> list | None:
    if not n:
        return None
    z, p = 1.96, k / n
    d = 1 + z * z / n
    c = (p + z * z / (2 * n)) / d
    h = z * math.sqrt(p * (1 - p) / n + z * z / (4 * n * n)) / d
    return [round(max(0.0, c - h), 4), round(min(1.0, c + h), 4)]


def current(model: str) -> dict:
    from server.evaluate import prompt as P
    return {"prompt_version": P.VERSION, "prompt_hash": P.prompt_hash(), "model": model, "data_sha256": {f: sha(SET / f) for f in DATA}, "code_sha256": {f: sha(ROOT / f) for f in CODE}}


def freeze(argv) -> int:
    man = json.loads((SET / "manifest.json").read_text(encoding="utf-8"))
    if not man.get("frozen_before_scoring") or man.get("dataset_sha256") != sha(SET / "dataset.jsonl"):
        raise SystemExit("REFUSED: the dataset is not built and frozen (tools/synth_heldout/build.py --write)")
    if CONFIG.exists() or LOCK.exists():
        raise SystemExit("REFUSED: the configuration is already frozen")
    acc = json.loads((SET / "acceptance.json").read_text(encoding="utf-8"))
    rec = {**current(acc["run"]["model"]), "frozen_utc": datetime.now(timezone.utc).strftime("%Y-%m-%dT%H:%M:%SZ"), "note": "frozen before any model call on this set"}
    CONFIG.write_text(json.dumps(rec, indent=1) + "\n", encoding="utf-8", newline="\n")
    print("froze", CONFIG.relative_to(ROOT).as_posix(), rec["prompt_version"], rec["prompt_hash"][:12])
    return 0


def run(argv) -> int:
    if LOCK.exists():
        raise SystemExit("REFUSED: the held-out set was already scored once (RUN_LOCK.json); it is not scored a second time")
    if not CONFIG.exists():
        raise SystemExit("REFUSED: freeze the configuration first")
    frozen = json.loads(CONFIG.read_text(encoding="utf-8"))
    acc = json.loads((SET / "acceptance.json").read_text(encoding="utf-8"))
    now = current(acc["run"]["model"])
    drift = [k for k in ("prompt_version", "prompt_hash", "model", "data_sha256", "code_sha256") if now[k] != frozen[k]]
    if drift:
        raise SystemExit("REFUSED: changed after it was frozen: " + ", ".join(drift))
    import evaluator_run as ER
    cfg = acc["run"]
    items = ER.dataset_items()
    d = ER.run(items, cfg["max_calls"], cfg["max_tokens"], workers=cfg["workers"], model=cfg["model"])
    RAW.write_text(json.dumps(d, ensure_ascii=False), encoding="utf-8")
    LOCK.write_text(json.dumps({"scored_utc": datetime.now(timezone.utc).strftime("%Y-%m-%dT%H:%M:%SZ"), "prompt_hash": d["prompt_hash"], "calls": d["calls"], "tokens": d["tokens"], "stopped": d["stopped"]}, indent=1) + "\n", encoding="utf-8", newline="\n")
    prows = [json.loads(l) for l in (SET / "partial_credit.jsonl").read_text(encoding="utf-8").splitlines() if l.strip()]
    for it in prows:
        it["context"] = ER.context_of(it["spec"], it.get("verified"))
    dp = ER.run(prows, max(0, cfg["max_calls"] - d["calls"]), max(0, cfg["max_tokens"] - d["tokens"]), workers=cfg["workers"], model=cfg["model"])
    RAW_PARTIAL.write_text(json.dumps(dp, ensure_ascii=False), encoding="utf-8")
    st = {}
    for v in d["results"].values():
        st[v["status"]] = st.get(v["status"], 0) + 1
    print(json.dumps({"calls": d["calls"] + dp["calls"], "tokens": d["tokens"] + dp["tokens"], "stopped": d["stopped"] or dp["stopped"], "status": st, "wall_s": d["wall_s"] + dp["wall_s"]}))
    return 0


def partial_summary(dp: dict) -> dict:
    import engine_score as ES
    from evaluator_run import context_of
    rows = [json.loads(l) for l in (SET / "partial_credit.jsonl").read_text(encoding="utf-8").splitlines() if l.strip()]
    items = []
    for r in rows:
        ev = dp["results"][r["id"]]["evaluation"]
        c = context_of(r["spec"])
        acts = [] if not ev else [{"act": a["objective"], "assertion": a["assertion"], "actor": a["actor"], "form": a["form"], "relevance": a["relevance"], "correctness": a["correctness"], "completeness": a["completeness"],
                                    "span": a["evidence"] or a["objective"], "claims_physical": ev["claims_physical_action"]} for a in ev["acts"]]
        items.append({"id": r["id"], "steps": c["steps"], "patient_question_pending": c["patient_question_pending"], "readback_pending": c["readback_pending"], "acts": acts})
    eng = {x["id"]: x for x in ES.score(items)}
    out, per_band = [], {}
    for r in rows:
        total = eng[r["id"]]["communication_total"]
        lo, hi = r["expected_score"]
        inside = total >= lo if r["band"] == "full" else lo <= total <= hi
        per_band.setdefault(r["band"], []).append(total)
        out.append({"id": r["id"], "objective": r["objective"], "band": r["band"], "score": total, "in_band": inside})
    n_in = sum(x["in_band"] for x in out)
    return {"n": len(out), "in_band": n_in, "in_band_rate": round(n_in / len(out), 4), "in_band_by_band": {b: [sum(x["in_band"] for x in out if x["band"] == b), len([x for x in out if x["band"] == b])] for b in per_band},
            "mean_score_by_band": {b: round(sum(v) / len(v), 3) for b, v in per_band.items()}, "items": out}


def report(argv) -> int:
    if not LOCK.exists():
        raise SystemExit("REFUSED: there is no scored run")
    import evaluator_metrics as EM
    d, items = EM.load(str(RAW), engine=True)
    m = EM.analyse(d, items, with_baseline=False)
    dp = json.loads(RAW_PARTIAL.read_text(encoding="utf-8"))
    part = partial_summary(dp)
    c05 = m["credit"]["meaningful_ge_0.5"]
    rk, rn = c05["4_legitimate_credit_recall"]
    pk, pn = c05["5_precision"][0], c05["5_precision"][1]
    crit = m["8_critical"]
    reader = m["reader"]
    perf_k, perf_n = m["2_performed_vs_not"]["performed_found"]
    acc = json.loads((SET / "acceptance.json").read_text(encoding="utf-8"))
    gold_c = lambda x: set(x["gold"]["credit_acts"])
    nonperf = [x for x in items if x["spec"]["situation"] in ("negated", "intended_later", "hypothetical", "reported_about_someone_else", "other_actor_does_it")]
    nonperf_bad = [x for x in nonperf if EM.credited(x)]
    singles = [x for x in items if x["cls"] in ("D", "E")]
    unjust = [x for x in singles if EM.credited(x) - gold_c(x)]
    mixed = [x for x in items if "mixed_critical" in x["spec"]["tags"]]
    mixed_ok = [x for x in mixed if gold_c(x) <= EM.credited(x)]
    multi = [x for x in items if x["cls"] == "G"]
    multi_found = sum(len(gold_c(x) & EM.credited(x)) for x in multi)
    multi_total = sum(len(gold_c(x)) for x in multi)
    multi_extra = [x for x in multi if EM.credited(x) - gold_c(x)]
    cs = m["10_contrast_sets"]
    values = {"critical_physical_completions": crit["physical_completion_credit"], "window_state_bypass_items": sum(1 for x in items if x["cls"] == "B" and x["objective"] in EM.credited(x)), "legitimate_recall": rk / rn if rn else None, "credited_precision": pk / pn if pn else None,
              "performed_vs_not_accuracy": m["2_performed_vs_not"]["accuracy"], "valid_reply_rate": reader["valid_rate"], "partial_in_band_rate": part["in_band_rate"], "median_latency_ms": reader["wall_ms_per_call"]["median"],
              "non_performed_false_credit_rate": len(nonperf_bad) / len(nonperf), "unjustified_ordinary_credit_rate": len(unjust) / len(singles), "mixed_ordinary_recall": len(mixed_ok) / len(mixed) if mixed else None,
              "multi_intent_act_recall": multi_found / multi_total, "multi_intent_extra_credit_items": len(multi_extra), "contrast_set_rate": cs["correct"] / cs["n"]}
    ordinary_on_critical = [{"id": x["id"], "text": x["text"], "objective": x["objective"], "situation": x["situation"], "credited": sorted(EM.credited(x)), "listed_independent_ordinary_in_specification": sorted(gold_c(x)),
                             "justified_by_specification": not (EM.credited(x) - gold_c(x)), "class": x["cls"]} for x in items if (x["gold"]["critical"] or x["spec"]["critical_topic"]) and EM.credited(x)]
    detail = {"non_performed_items_credited": [{"id": x["id"], "text": x["text"], "situation": x["situation"], "credited": sorted(EM.credited(x))} for x in nonperf_bad], "non_performed_n": len(nonperf),
              "single_critical_n": len(singles), "unjustified_ordinary_credit_items": [x["id"] for x in unjust], "mixed_n": len(mixed), "mixed_ok": len(mixed_ok), "multi_intent": {"items": len(multi), "acts_found": multi_found, "acts_total": multi_total, "extra_credit_items": [x["id"] for x in multi_extra]},
              "contrast": cs}
    ops = {"==": lambda a, b: a == b, ">=": lambda a, b: a >= b, "<=": lambda a, b: a <= b}
    results = []
    for c in acc["criteria"]:
        v = values[c["metric"]]
        results.append({**c, "observed": None if v is None else round(v, 4), "passed": v is not None and ops[c["op"]](v, c["value"])})
    blocked = [r for r in results if r["blocking"] and not r["passed"]]
    missed = [r for r in results if not r["blocking"] and not r["passed"]]
    verdict = "BLOCKED" if blocked else ("NOT APPROVED (owner decision)" if missed else "APPROVED")
    rep = {"set": "synthetic_heldout_v2", "kind": acc["evidence_class"], "run_utc": json.loads(LOCK.read_text(encoding="utf-8"))["scored_utc"], "prompt_version": d["prompt_version"], "prompt_hash": d["prompt_hash"], "model": d["model"],
           "verdict": verdict, "criteria": results, "detail": detail, "ordinary_credit_on_critical_content_items": ordinary_on_critical, "intervals_wilson95": {"multi_intent_act_recall": wilson(multi_found, multi_total), "legitimate_recall": wilson(rk, rn), "credited_precision": wilson(pk, pn), "performed_vs_not": wilson(perf_k, perf_n), "valid_replies": wilson(round(reader["valid_rate"] * m["n"]), m["n"]),
                                                                          "partial_in_band": wilson(part["in_band"], part["n"])},
           "counts": {"items": m["n"], "eligible": rn, "recall": [rk, rn], "precision": [pk, pn], "non_performed_false_credit_items": len(nonperf_bad), "false_credit_items_on_no_credit_items": c05["6_false_credit_items_on_no_credit"],
                      "wrong_or_extra_on_eligible": c05["7_wrong_or_extra_acts_on_eligible"], "performed_found": [perf_k, perf_n]},
           "metrics": m, "partial_credit": part, "free_plan": {"calls": d["calls"] + dp["calls"], "tokens": d["tokens"] + dp["tokens"], "stopped": d["stopped"] or dp["stopped"], "paid_api": False},
           "caveat": "AI-written and AI-reviewed synthetic data; the same assistant wrote the development set, the prompt guidance and this set. The real Meta Quest and ASR end-to-end validation (DC-09, Sannan) is still to be done."}
    REPORT.parent.mkdir(parents=True, exist_ok=True)
    REPORT.write_text(json.dumps(rep, indent=1, ensure_ascii=False, default=list) + "\n", encoding="utf-8", newline="\n")
    print(json.dumps({"verdict": verdict, "criteria": [(r["id"], r["observed"], r["passed"]) for r in results]}))
    return 0


def main(argv):
    cmd = argv[0] if argv else ""
    if cmd not in ("freeze", "run", "report"):
        print(__doc__)
        return 2
    return {"freeze": freeze, "run": run, "report": report}[cmd](argv[1:])


if __name__ == "__main__":
    raise SystemExit(main(sys.argv[1:]))
