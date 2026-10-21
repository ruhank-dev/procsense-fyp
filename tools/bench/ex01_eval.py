"""Layered evaluation of the Gemma speech evaluator on the frozen independent set (EX-01, phase two; ADR-0025): `server/.venv/Scripts/python.exe tools/bench/ex01_eval.py config-freeze|run ...`.

It answers one question per layer, on the SAME items, so that a failure of understanding can be told from a rejection by the engine of a judgement that was right:
  1 evaluator   what the model judged BEFORE the engine (the objectives it found performed by the trainee now, its critical-topic report, strict-validated)
  2 final       what the ENGINE credits from that judgement and the item's scenario state (window, waiting question or read-back, performed-now, graded credit; the CLI command speech-score)
For each layer: legitimate recall, credited precision, confusion by objective, critical and adversarial false credits, abstentions, schema compliance, latency, and the engine rule behind every miss.
Gold labels are the frozen annotators' and adjudicator's. The tool REFUSES a set that is not frozen (tools/bench/ex01_kit.py verify), a configuration that was not frozen first (config-freeze) or changed afterwards, and a second scored run (a repeat is development data, --development).
Nothing here tunes anything. Zero observed unsafe credits is a count with a denominator, not proof of safety. The engine's credit is the final word: no model output is a score."""
from __future__ import annotations

import argparse
import collections
import hashlib
import json
import math
import statistics
import sys
from datetime import datetime, timezone
from pathlib import Path

sys.stdout.reconfigure(encoding="utf-8")
ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT))
sys.path.insert(0, str(ROOT / "tools" / "bench"))
sys.path.insert(0, str(ROOT / "tools" / "synth_dev"))
import ex01_kit as KIT  # noqa: E402
from server.evaluate import prompt as P  # noqa: E402
from server.evaluate.reader import Reader  # noqa: E402

MAPPING = KIT.MAPPING
CRITICAL = set(MAPPING["critical_acts"])
MEANINGFUL = 0.5                                   # a communication credit of at least half counts as credited in the reports (the engine reports the exact number)
CODE_FILES = ["server/evaluate/prompt.py", "server/evaluate/examples.py", "server/evaluate/wording.json", "server/evaluate/contract.py", "server/evaluate/reader.py", "server/evaluate/rubric.py", "content/assessment/mapping.json", "content/assessment/taxonomy.json",
              "content/scoring/rubric.json", "src/ProcSense.Core/Scoring/Evidence.cs", "src/ProcSense.Core/Scoring/CommunicationRubric.cs", "src/ProcSense.Core/Scoring/CommunicationScorer.cs", "src/ProcSense.Core/Scoring/SpeechScoreCheck.cs", "src/ProcSense.Core/Speech/SpeechStateGate.cs"]


def context_of(item: dict) -> dict:
    """the engine-shaped context of an item: the scene's step, the acts eligible there by the approved windows, the pending flags the authors stated, the earlier turns. No label, no family."""
    step = int(item["scenario_step"])
    turns = []
    for t in item["prior_turns"].split(" | "):
        if t.strip() and ":" in t:
            turns.append({"speaker": t.split(":", 1)[0].strip(), "text": t.split(":", 1)[1].strip()})
    return {"steps": [step], "eligible": [{"act": a, "behaviour": b["id"], "critical": a in CRITICAL} for b in MAPPING["behaviours"] if step in b["steps"] for a in b["ai_acts"]], "patient_question_pending": item["patient_question_pending"] == "yes",
            "readback_pending": item["readback_pending"] == "yes", "last_turns": turns, "verified_actions": [], "speaker": "trainee", "addressee": "patient"}


def wilson(k: int, n: int, z: float = 1.96):
    if not n:
        return None
    p = k / n
    d = 1 + z * z / n
    c = p + z * z / (2 * n)
    m = z * math.sqrt(p * (1 - p) / n + z * z / (4 * n * n))
    return [round((c - m) / d, 3), round((c + m) / d, 3)]


def rate(k: int, n: int) -> dict:
    r = {"k": k, "n": n, "rate": round(k / n, 4) if n else None, "wilson95": wilson(k, n)}
    if n and k == 0:
        r["exact95_upper_bound_for_zero_events"] = round(1 - 0.025 ** (1 / n), 4)           # Clopper-Pearson, two-sided 95 percent: what 0 observed events still allows
    return r


TARGETS = {"legitimate_recall": 0.90, "credited_precision": 0.95, "schema_compliance": 0.995}


def assess(name: str, r: dict, target: float | None = None, zero: bool = False) -> dict:
    """The engineering targets are checked on the POINT ESTIMATE; the 95 percent Wilson interval is reported next to it and whether its lower bound also clears the target is a separate, differently named fact (a different statistical claim). A zero-count target is reported with its upper bound."""
    out = {"target": "0 observed" if zero else f">= {target:.1%}".replace(".0%", "%"), "observed": f"{r['k']}/{r['n']}", "point_estimate": r["rate"], "wilson95": r["wilson95"]}
    if zero:
        out["point_estimate_meets_target"] = r["k"] == 0
        out["upper_bound_95"] = r.get("exact95_upper_bound_for_zero_events") if r["k"] == 0 else (r["wilson95"][1] if r["wilson95"] else None)
        out["note"] = "an observed zero is a count with a denominator, not proof of zero underlying risk"
    else:
        out["point_estimate_meets_target"] = r["rate"] is not None and r["rate"] >= target
        out["interval_lower_bound_at_or_above_target"] = r["wilson95"] is not None and r["wilson95"][0] >= target
        out["note"] = "the point estimate is the engineering-target test; the interval shows the remaining uncertainty and its lower bound is a different, stricter claim"
    return out


def target_assessment(final: dict, evaluator: dict | None) -> dict:
    t = {"legitimate_recall": assess("recall", final["legitimate_recall"], TARGETS["legitimate_recall"]), "credited_precision": assess("precision", final["credited_precision"], TARGETS["credited_precision"]),
         "critical_false_credits": assess("critical", final["critical_false_credits"], zero=True), "adversarial_false_credits": assess("adversarial", final["adversarial_false_credits"], zero=True),
         "critical_acts_credited_from_speech": assess("critical acts", final["critical_acts_credited_from_speech"], zero=True)}
    if evaluator:
        t["schema_compliance_final"] = assess("schema", evaluator["schema_compliance"]["final_valid"], TARGETS["schema_compliance"])
        t["schema_compliance_first_attempt"] = assess("schema first", evaluator["schema_compliance"]["first_attempt_valid"], TARGETS["schema_compliance"])
    t["all_point_estimate_targets_met"] = all(v["point_estimate_meets_target"] for v in t.values() if isinstance(v, dict))
    return t


def gold_of(item: dict) -> dict:
    acts = {a for a in item["gold_acts"].split(";") if a} - {"NONE"}
    ordinary, crit = acts - CRITICAL, acts & CRITICAL
    step = int(item["scenario_step"])
    kind = "critical" if crit else ("positive" if item["gold_credit_eligible"] == "yes" else "nonperformed")
    in_window = bool(ordinary) and all(step in KIT.window_of(a) for a in ordinary) if kind == "positive" else None
    return {"kind": kind, "ordinary": ordinary, "critical": crit, "in_window": in_window}


def load_set(d: Path, allow_unfrozen: bool = False) -> list[dict]:
    if not allow_unfrozen:
        bad = KIT.verify(d)
        if bad:
            raise SystemExit("refused: " + "; ".join(bad))
    return KIT.read(d / "items.csv")


def evaluator_layer(items: list, reader: Reader) -> tuple[dict, list]:
    rows = []
    for it in items:
        o = reader.read(context_of(it), it["utterance"], key=it["item_id"])
        acts = o.evaluation.acts if o.status == "VALID" else []
        rows.append({"item": it, "status": o.status, "attempts": o.attempts, "codes": o.codes, "wall_ms": o.wall_ms, "tokens": o.prompt_tokens + o.output_tokens, "evaluation": o.evaluation,
                     "performed": sorted(a.objective for a in acts if a.performed_now and a.objective not in CRITICAL), "critical_named": sorted(a.objective for a in acts if a.objective in CRITICAL and a.performed_now)})
    pos = [r for r in rows if gold_of(r["item"])["kind"] == "positive"]
    pos_in = [r for r in pos if gold_of(r["item"])["in_window"]]
    ok = lambda r: bool(r["performed"]) and set(r["performed"]) <= gold_of(r["item"])["ordinary"] and not r["critical_named"]      # noqa: E731
    non = [r for r in rows if gold_of(r["item"])["kind"] == "nonperformed"]
    crit = [r for r in rows if gold_of(r["item"])["kind"] == "critical"]
    lat = [r["wall_ms"] for r in rows if r["status"] == "VALID"]
    conf = collections.Counter((sorted(gold_of(r["item"])["ordinary"])[0], ",".join(r["performed"]) or "NONE_PERFORMED") for r in pos)
    m = {"understood_positive_items": rate(sum(ok(r) for r in pos), len(pos)), "understood_positive_items_in_window": rate(sum(ok(r) for r in pos_in), len(pos_in)),
         "false_performance_of_nonperformed": rate(sum(bool(r["performed"]) for r in non), len(non)), "critical_content_recognised": rate(sum(bool(r["critical_named"]) for r in crit), len(crit)),
         "ordinary_performance_on_critical_items": rate(sum(bool(r["performed"]) for r in crit), len(crit)),
         "schema_compliance": {"first_attempt_valid": rate(sum(r["status"] == "VALID" and r["attempts"] == 1 for r in rows), len(rows)), "final_valid": rate(sum(r["status"] == "VALID" for r in rows), len(rows)),
                               "unavailable_or_invalid_codes": dict(collections.Counter(c.split(":")[0] for r in rows for c in r["codes"]))},
         "latency_ms": {"median": statistics.median(lat) if lat else None, "p95": sorted(lat)[int(len(lat) * 0.95)] if lat else None, "max": max(lat) if lat else None}, "tokens": sum(r["tokens"] for r in rows),
         "confusion_by_objective": {f"{g} -> {c}": n for (g, c), n in sorted(conf.items())}, "misunderstood_positive_items_by_family": dict(collections.Counter(r["item"]["family"] for r in pos if not ok(r)))}
    return m, rows


def final_layer(items: list, rows: list, score) -> list[dict]:
    """score(engine_items) -> the engine's per-item verdicts (tools/synth_dev/engine_score.score); credit is the engine's graded communication credit of each judged objective for the item's own scenario state."""
    eng_items = []
    for r in rows:
        it, ev = r["item"], r["evaluation"]
        c = context_of(it)
        eng_items.append({"id": it["item_id"], "steps": c["steps"], "patient_question_pending": c["patient_question_pending"], "readback_pending": c["readback_pending"], "verified": [],
                          "acts": [] if ev is None else [{"act": a.objective, "assertion": a.assertion, "actor": a.actor, "form": a.form, "relevance": a.relevance, "correctness": a.correctness, "completeness": a.completeness, "span": a.evidence or a.objective,
                                                          "claims_physical": ev.claims_physical_action} for a in ev.acts]})
    verdicts = {v["id"]: v for v in score(eng_items)}
    outs = []
    for r in rows:
        v = verdicts[r["item"]["item_id"]]
        credit = {a["act"]: round(a["grade"] * (0.25 if a["act"] in CRITICAL else 1.0), 4) for a in v["acts"]}
        reasons = {a["act"]: a["reason"] for a in v["acts"] if not a["accepted"]}
        outs.append({"item": r["item"], "credit": credit, "reasons": reasons, "credited": sorted(o for o, c in credit.items() if c >= MEANINGFUL and o not in CRITICAL), "critical_credit": sorted(o for o, c in credit.items() if o in CRITICAL and c > 0)})
    return outs


def summarise_final(outs: list) -> dict:
    pos_in = [x for x in outs if gold_of(x["item"])["kind"] == "positive" and gold_of(x["item"])["in_window"]]
    pos_out = [x for x in outs if gold_of(x["item"])["kind"] == "positive" and gold_of(x["item"])["in_window"] is False]
    crit = [x for x in outs if gold_of(x["item"])["kind"] == "critical"]
    non = [x for x in outs if gold_of(x["item"])["kind"] == "nonperformed"]
    right = [x for x in pos_in if set(x["credited"]) & gold_of(x["item"])["ordinary"]]
    credited_acts = [(x, o) for x in outs for o in x["credited"]]
    good = [(x, o) for x, o in credited_acts if gold_of(x["item"])["kind"] == "positive" and gold_of(x["item"])["in_window"] and o in gold_of(x["item"])["ordinary"]]
    missed = [x for x in pos_in if x not in right]
    conf = collections.Counter((sorted(gold_of(x["item"])["ordinary"])[0], ",".join(x["credited"]) or "NOT_CREDITED") for x in pos_in)
    return {
        "legitimate_recall": rate(len(right), len(pos_in)), "credited_precision": rate(len(good), len(credited_acts)),
        "critical_false_credits": rate(sum(1 for x in crit if x["credited"]), len(crit)), "adversarial_false_credits": rate(sum(1 for x in non if x["credited"]), len(non)),
        "critical_acts_credited_from_speech": rate(sum(1 for x in outs if any(o in CRITICAL for o in x["credited"])), len(outs)),
        "out_of_window_valid_items": {"n": len(pos_out), "credited": sum(1 for x in pos_out if x["credited"]), "note": "semantically performed but outside the approved behaviour window: the engine must not credit them"},
        "confusion_by_objective": {f"{g} -> {c}": n for (g, c), n in sorted(conf.items())},
        "misses_by_engine_rule": dict(collections.Counter(next(iter(x["reasons"].values()), "no judged objective credited").split(":")[0] if x["reasons"] else "no judged objective credited" for x in missed).most_common()),
        "by_family": {f: rate(sum(1 for x in right if x["item"]["family"] == f), sum(1 for x in pos_in if x["item"]["family"] == f)) for f in sorted({x["item"]["family"] for x in pos_in})}}


def attribution(rows: list, outs: list) -> dict:
    """Separates a failure of UNDERSTANDING from a rejection by the engine of a judgement that was right (in-window legitimate items only)."""
    rd = {r["item"]["item_id"]: r for r in rows}
    out, by = collections.Counter(), collections.Counter()
    for x in outs:
        g = gold_of(x["item"])
        if g["kind"] != "positive" or not g["in_window"]:
            continue
        r = rd[x["item"]["item_id"]]
        understood = bool(r["performed"]) and set(r["performed"]) <= g["ordinary"] and not r["critical_named"]
        if set(x["credited"]) & g["ordinary"]:
            out["understood_and_credited" if understood else "credited_although_the_model_did_not_understand"] += 1
        elif understood:
            out["understood_but_rejected_by_the_engine"] += 1
            by[next(iter(x["reasons"].values()), "below the credit floor").split(":")[0]] += 1
        else:
            out["not_understood_by_the_model"] += 1
    return {"counts": dict(out), "understood_but_rejected_by_engine_rule": dict(by.most_common())}


def config_record() -> dict:
    h = lambda p: hashlib.sha256((ROOT / p).read_bytes()).hexdigest()      # noqa: E731
    return {"evaluator_prompt_version": P.VERSION, "evaluator_prompt_sha256": P.prompt_hash(), "code_sha256": {p: h(p) for p in CODE_FILES}}


def main(argv: list[str]) -> int:
    ap = argparse.ArgumentParser()
    sub = ap.add_subparsers(dest="cmd", required=True)
    c = sub.add_parser("config-freeze")
    c.add_argument("--dir", default=str(KIT.DEFAULT_SET))
    c.add_argument("--model", required=True)
    r = sub.add_parser("run")
    r.add_argument("--dir", default=str(KIT.DEFAULT_SET))
    r.add_argument("--live", required=True, help="the model, e.g. gemma4:31b-cloud through the signed-in local Ollama (free plan only; needs the owner's approval for THIS run)")
    r.add_argument("--ollama", default="http://127.0.0.1:11434")
    r.add_argument("--max-calls", type=int, required=True)
    r.add_argument("--max-tokens", type=int, required=True)
    r.add_argument("--timeout", type=float, default=15.0)
    r.add_argument("--development", action="store_true", help="a repeat look: the report is marked NOT independent")
    a = ap.parse_args(argv)
    d = Path(a.dir)
    if a.cmd == "config-freeze":
        bad = KIT.verify(d)
        if bad:
            raise SystemExit("refused: " + "; ".join(bad))
        rec = {**config_record(), "model": a.model, "frozen_utc": datetime.now(timezone.utc).strftime("%Y-%m-%dT%H:%M:%SZ"), "manifest_sha256": KIT.sha(d / "manifest.json")}
        (d / "eval_config.json").write_text(json.dumps(rec, indent=1) + "\n", encoding="utf-8", newline="\n")
        print("froze", d / "eval_config.json")
        return 0
    # ---- run
    items = load_set(d)
    cfg_path, lock = d / "eval_config.json", d / "RUN_LOCK.json"
    if not cfg_path.exists():
        raise SystemExit("refused: freeze the configuration first (config-freeze)")
    cfg = json.loads(cfg_path.read_text(encoding="utf-8"))
    now = config_record()
    drift = [k for k in ("evaluator_prompt_version", "evaluator_prompt_sha256", "code_sha256") if cfg[k] != now[k]]
    if drift or cfg["model"] != a.live:
        raise SystemExit(f"refused: the configuration changed after it was frozen ({drift or 'model'})")
    if lock.exists() and not a.development:
        raise SystemExit("refused: the independent set was already scored once; a second look is development data (--development)")
    import engine_score as ES
    from evaluator_run import Budget
    from server.evaluate.provider import OllamaCloudProvider
    prov = Budget(OllamaCloudProvider(a.live, a.ollama, "", timeout=a.timeout), a.max_calls, a.max_tokens)
    em, rows = evaluator_layer(items, Reader(prov, max_attempts=2, budget_ms=25000, call_timeout=a.timeout))
    outs = final_layer(items, rows, ES.score)
    final = summarise_final(outs)
    manifest = json.loads((d / "manifest.json").read_text(encoding="utf-8"))
    rep = {"independent": not a.development and bool(manifest["independent"]), "set_version": manifest["version"], "config": cfg, "run_utc": datetime.now(timezone.utc).strftime("%Y-%m-%dT%H:%M:%SZ"),
           "budget": {"calls": prov.calls, "tokens": prov.tokens, "stopped": prov.stopped, "max_calls": a.max_calls, "max_tokens": a.max_tokens},
           "layers": {"1_evaluator_judgement_before_the_engine": em, "2_final_engine_credit": final}, "failure_attribution_understanding_vs_engine": attribution(rows, outs),
           "engineering_targets": {"final_engine_credit": target_assessment(final, em)},
           "caveat": "Counts with denominators and Wilson 95% intervals; zero observed unsafe credits is not proof of safety; the authors are not trainees; no real audio; no headset."}
    out = ROOT / "reports" / "ai" / ("ex01-speech-evaluation.json" if rep["independent"] else "ex01-speech-evaluation-development.json")
    out.write_text(json.dumps(rep, indent=1, ensure_ascii=False) + "\n", encoding="utf-8", newline="\n")
    if rep["independent"]:
        lock.write_text(json.dumps({"scored_utc": rep["run_utc"], "report_sha256": KIT.sha(out)}, indent=1) + "\n", encoding="utf-8", newline="\n")
    print("wrote", out.relative_to(ROOT).as_posix())
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
