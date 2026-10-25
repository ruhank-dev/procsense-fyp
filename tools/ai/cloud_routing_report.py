"""Chooses a model per AI task from the stored benchmark runs and writes the evidence (ADR-0026): `uv run --project tools tools/ai/cloud_routing_report.py [--check]`.

Reads artifacts/ai-bench/cloud/*.json|*.stdout (the runs of tools/ai/eval_*.py on 25 Oct 2026, git-ignored) and writes reports/ai/cloud-routing-benchmark.json: for every task the measured figures of each candidate and the
model the rule picks. `--check` compares that file with what the rule gives from the figures stored in it (so the committed decision can be re-derived without the cloud and without the artifacts).

The rules, fixed before the choice:
  * the local model (qwen3.5:4b-q4_K_M) is the default and keeps the task unless a candidate is no worse on every safety figure AND better on the quality figure by the margin of the task;
  * real-time dialogue (patient, staff): quality at least equal, no more leaks than local, 95th percentile under the 4 s budget; among eligible models the best quality, then the lower median wins;
  * offline generation (narrative, critic, judge, trap writer): quality better by at least 5 points, false alarms no higher; speed alone never moves an offline task to the cloud;
  * a task with no benchmark, or whose candidate is not better, stays local.
Parameter count, popularity and medical branding play no part. Synthetic data only; nothing here decides credit or scoring.
"""
from __future__ import annotations

import argparse
import json
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
SRC = ROOT / "artifacts" / "ai-bench" / "cloud"
OUT = ROOT / "reports" / "ai" / "cloud-routing-benchmark.json"
LOCAL = "qwen3.5:4b-q4_K_M"
MODELS = {"qwen3.5_4b-q4_K_M": LOCAL, "gemma4_31b-cloud": "gemma4:31b-cloud", "gpt-oss_120b-cloud": "gpt-oss:120b-cloud", "gpt-oss_20b-cloud": "gpt-oss:20b-cloud", "nemotron-3-nano_30b-cloud": "nemotron-3-nano:30b-cloud"}
MARGIN = 0.05
BUDGET_MS = 4000


def jfile(name: str):
    p = SRC / name
    return json.loads(p.read_text(encoding="utf-8")) if p.exists() else None


def jtext(name: str):
    p = SRC / name
    if not p.exists():
        return None
    t = p.read_text(encoding="utf-8", errors="replace")
    return json.loads(t[t.index("{"): t.rindex("}") + 1])


def figures() -> dict:
    """task -> model -> the figures the rule uses, read from the stored runs."""
    out: dict = {"dialogue.patient": {}, "dialogue.staff": {}, "compose.narrative": {}, "critic": {}, "judge.scenario": {}, "trap.reword": {}, "compose_plan.extras": {}, "compose_plan.planner": {}, "debrief.rewrite": {},
                 "personalisation.interpret": {}}
    for key, model in MODELS.items():
        d = jfile(f"dialogue-{key}.json")
        if d:
            e, w = d["earned"], d["withheld"]
            out["dialogue.patient"][model] = {"n": e["n"], "fact_used": round(e["used_fact"] / e["n"], 3), "withheld_cannot_recall": round(w["cannot_recall"] / w["n"], 3), "leaks": w["word_leaks"] + w["validator_leaks"],
                                              "model_answered": round(e["model"] / e["n"], 3), "ms_p50": d["ms_p50"], "ms_p95": d["ms_p95"]}
        s = jfile(f"staff-{key}.json")
        m = jfile(f"multiagent-{key}.json")
        if s:
            out["dialogue.staff"][model] = {"accepted_share": s["accepted_share"], "nurse": s["by_role"]["nurse"], "anaesthesia": s["by_role"]["anaesthesia"], "fallback_share": s["fallback_share"], "ms_p95": s["ms_p95"],
                                            "three_voices_p95_s": m["all_three_s"]["p95"] if m else None, "three_voices_within_budget": m["all_three_within_budget"] if m else None}
        n = jfile(f"narrative-{key}.json")
        if n:
            out["compose.narrative"][model] = {"accept_rate": n["accept_rate"], "first_try_rate": n["first_try_rate"], "fallback_to_authored": n["fallback_to_authored"], "ms_p50": n["latency_ms_p50"]}
        c = jfile(f"critic-{key}.json")
        if c:
            out["critic"][model] = {"recall": c["recall"]["critic"], "false_alarm": c["critic_false_alarm_rate"], "union_with_rules": c["recall"]["union"], "seconds": c["seconds"]}
        j = jtext(f"judge-{key}.stdout")
        if j:
            out["judge.scenario"][model] = {"flagged_among_missed_by_rules": j["flagged_among_those_the_rules_missed"], "clean_flagged": j["clean_flagged_share"], "seconds": j["seconds"]}
        t = jtext(f"traps-{key}.stdout")
        if t:
            out["trap.reword"][model] = {"accepted_share": t["accepted_share"], "fairness_lower": t["cases_with_lower_trap_fairness"], "seconds_per_case_p50": t["seconds_per_case_p50"]}
        x = jtext(f"planextras-{key}.stdout")
        if x:
            out["compose_plan.extras"][model] = {"cases_with_events": x["cases_with_events"], "share_with_interaction": x["share_cases_with_interaction"], "refused": x["refused_by_validation"], "ineligible": x["ineligible_events_in_final_proposals"], "seconds_p50": x["seconds_p50"]}
        p = jfile(f"planner-{key}.json")
        if p:
            a = p["arms"]["planner"]
            out["compose_plan.planner"][model] = {"served": a["served"], "passed_gate": a["passed_gate"], "coverage": a["coverage_mean"], "distinct_block_sets": a["distinct_block_sets"], "seconds_p50": a["seconds_p50"]}
        b = jfile(f"debrief-{key}.json")
        if b:
            out["debrief.rewrite"][model] = {"accept_rate": b["accept_rate"], "within_8s": b["within_8s_share"], "ms_p50": b["latency_ms_p50"], "mutations_rejected": f'{b["mutations_rejected"]}/{b["mutations"]}'}
        i = jfile(f"interpreter-{key}.json")
        if i:
            out["personalisation.interpret"][model] = {"confirmed": i["confirmed_by_model"], "no_proposal": i["learners_with_no_accepted_proposal"], "seconds_per_learner": i["seconds_per_learner"]}
    return {k: v for k, v in out.items() if v}


def choose(task: str, f: dict) -> tuple[str, str]:
    """(model, why) for one task from its figures."""
    loc = f.get(LOCAL)
    if loc is None:
        return LOCAL, "no local baseline measured"
    others = {m: v for m, v in f.items() if m != LOCAL}
    ok: dict[str, tuple] = {}
    for m, v in others.items():
        if task == "dialogue.patient":
            if v["leaks"] <= loc["leaks"] and v["withheld_cannot_recall"] >= loc["withheld_cannot_recall"] and v["fact_used"] >= loc["fact_used"] and v["ms_p95"] <= BUDGET_MS:
                ok[m] = (v["withheld_cannot_recall"], v["fact_used"], -v["ms_p50"])
        elif task == "dialogue.staff":
            if v["accepted_share"] >= loc["accepted_share"] and v["fallback_share"] <= loc["fallback_share"] and (v["three_voices_p95_s"] or 99) * 1000 <= BUDGET_MS:
                ok[m] = (v["accepted_share"], -(v["three_voices_p95_s"] or 99))
        elif task == "compose.narrative":
            if v["accept_rate"] >= loc["accept_rate"] + MARGIN:
                ok[m] = (v["accept_rate"], -v["ms_p50"])
        elif task == "critic":
            if v["recall"] >= loc["recall"] + MARGIN and v["false_alarm"] <= loc["false_alarm"]:
                ok[m] = (v["recall"], -v["false_alarm"])
        elif task == "judge.scenario":
            if v["flagged_among_missed_by_rules"] >= loc["flagged_among_missed_by_rules"] + MARGIN and v["clean_flagged"] <= loc["clean_flagged"]:
                ok[m] = (v["flagged_among_missed_by_rules"],)
        elif task == "trap.reword":
            if v["accepted_share"] >= loc["accepted_share"] + MARGIN and v["fairness_lower"] <= loc["fairness_lower"]:
                ok[m] = (v["accepted_share"],)
        elif task == "compose_plan.extras":
            if v["cases_with_events"] >= loc["cases_with_events"] + MARGIN * 65 and v["refused"] <= loc["refused"] and v["ineligible"] <= loc["ineligible"]:
                ok[m] = (v["cases_with_events"],)
        elif task == "compose_plan.planner":
            if v["passed_gate"] >= loc["passed_gate"] and v["coverage"] >= loc["coverage"] and v["distinct_block_sets"] >= loc["distinct_block_sets"] + 1:
                ok[m] = (v["distinct_block_sets"],)
        elif task == "debrief.rewrite":
            if v["accept_rate"] >= loc["accept_rate"] + MARGIN:
                ok[m] = (v["accept_rate"],)
        elif task == "personalisation.interpret":
            if v["confirmed"] >= loc["confirmed"] + MARGIN:
                ok[m] = (v["confirmed"],)
    if not ok:
        return LOCAL, "no candidate is better by the rule's margin without a worse safety figure"
    best = max(ok, key=lambda m: ok[m])
    return best, "best eligible candidate by the rule"


def build() -> dict:
    fig = figures()
    decisions = {}
    for task, f in fig.items():
        model, why = choose(task, f)
        decisions[task] = {"figures": f, "selected": model, "why": why}
    return {"adr": "ADR-0026", "date": "2026-10-25", "local_baseline": LOCAL,
            "rule": __doc__.split("The rules, fixed before the choice:")[1].split("Parameter count")[0].strip(),
            "data": "synthetic only: authored cases, AI-written test sets (not human-labelled); runs of tools/ai/eval_*.py through a signed-in Ollama on the free plan; no real trainee data was sent",
            "decisions": decisions}


def rederive(doc: dict) -> dict:
    return {t: choose(t, d["figures"])[0] for t, d in doc["decisions"].items()}


def main(argv: list[str]) -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--check", action="store_true")
    a = ap.parse_args(argv)
    if a.check:
        doc = json.loads(OUT.read_text(encoding="utf-8"))
        bad = [t for t, m in rederive(doc).items() if doc["decisions"][t]["selected"] != m]
        for t in bad:
            print(f"FAIL {t}: the file says {doc['decisions'][t]['selected']}, the rule gives {rederive(doc)[t]}")
        print("cloud routing benchmark: decisions follow the rule" if not bad else f"{len(bad)} decision(s) differ")
        return 1 if bad else 0
    doc = build()
    OUT.write_text(json.dumps(doc, indent=1, ensure_ascii=False) + "\n", encoding="utf-8", newline="\n")
    for t, d in doc["decisions"].items():
        print(f"{t:28} -> {d['selected']}  ({d['why']})")
    print("wrote", OUT.relative_to(ROOT))
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
