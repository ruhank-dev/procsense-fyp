"""Chooses a model per AI task from the stored benchmark runs and writes the evidence (ADR-0026): `uv run --project tools tools/ai/cloud_routing_report.py [--check]`.

Reads artifacts/ai-bench/cloud/*.json|*.stdout (the runs of tools/ai/eval_*.py on 25 Oct 2026, git-ignored) and writes reports/ai/cloud-routing-benchmark.json: for every task the measured figures of each candidate and the
model the rule picks. `--check` compares that file with what the rule gives from the figures stored in it (so the committed decision can be re-derived without the cloud and without the artifacts).

The rules, fixed before the choice:
  * the local model (qwen3.5:4b-q4_K_M) is the default and keeps the task unless a candidate is no worse on every safety figure AND better on the quality figure by the margin of the task;
  * spoken orders (kind and map, measured together on a held-out set): correct read-backs better by at least 5 points, no more wrong items or false offers than local, 95th percentile under the 4 s budget (ADR-0026 amendment);
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
SRC = ROOT / "artifacts" / "ai-bench" / "cloud"            # the runs with the first prompts (v1)
SRC3 = ROOT / "artifacts" / "ai-bench" / "cloud3"          # the runs of the spoken-action interpreter (ADR-0027), a task that had no first prompt
SRC2 = ROOT / "artifacts" / "ai-bench" / "cloud2"          # the runs with the prompts of ADR-0026 (v2), the ones shipped; a file missing here (a task whose prompt did not change) is read from SRC
OUT = ROOT / "reports" / "ai" / "cloud-routing-benchmark.json"
LOCAL = "qwen3.5:4b-q4_K_M"
MODELS = {"qwen3.5_4b-q4_K_M": LOCAL, "gemma4_31b-cloud": "gemma4:31b-cloud", "gpt-oss_120b-cloud": "gpt-oss:120b-cloud", "gpt-oss_20b-cloud": "gpt-oss:20b-cloud", "nemotron-3-nano_30b-cloud": "nemotron-3-nano:30b-cloud"}
MARGIN = 0.05
BUDGET_MS = 4000


_dirs = [SRC]


def _path(name: str):
    return next((d / name for d in _dirs if (d / name).exists()), None)


def jfile(name: str):
    p = _path(name)
    return json.loads(p.read_text(encoding="utf-8")) if p else None


def jtext(name: str):
    p = _path(name)
    if not p:
        return None
    t = p.read_text(encoding="utf-8", errors="replace")
    return json.loads(t[t.index("{"): t.rindex("}") + 1])


CHANGED = {"speech.action", "dialogue.patient", "dialogue.staff", "compose.narrative", "critic", "judge.scenario", "trap.reword"}      # the tasks whose prompt ADR-0026 rewrote
LOCAL_FIRST_PROMPT = {"dialogue.patient", "dialogue.staff", "critic"}                                                  # their new prompt is cloud-scope: the local model keeps the first one (it costs it seconds)


def dirs_for(task: str, model: str) -> list:
    """Where to read a model's figures for a task: the runs with the prompt that model actually gets. A task whose prompt did not change is read from the first runs; a changed one from the v2 runs, except for the local
    model on a task where it keeps the first prompt. A cloud model measured only with the first prompt is therefore absent from a changed task: it is not compared on different wording."""
    if task == "speech.action":
        return [SRC3]
    if task not in CHANGED:
        return [SRC]
    return [SRC] if model == LOCAL and task in LOCAL_FIRST_PROMPT else [SRC2]


def figures(only_first: bool = False) -> dict:
    """task -> model -> the figures the rule uses, read from the stored runs. `only_first=True` reads the first prompts' runs for every task (what ADR-0026 started from)."""
    global _dirs
    out: dict = {"speech.action": {}, "dialogue.patient": {}, "dialogue.staff": {}, "compose.narrative": {}, "critic": {}, "judge.scenario": {}, "trap.reword": {}, "compose_plan.extras": {}, "compose_plan.planner": {}, "debrief.rewrite": {},
                 "personalisation.interpret": {}}
    for key, model in MODELS.items():
        def at(task):
            global _dirs
            _dirs = [SRC] if only_first else dirs_for(task, model)
        at("speech.action")
        sp = jfile(f"spoken-{key}.json")
        if sp:
            out["speech.action"][model] = {"exact_match": sp["exact_match"], "false_action_rate": sp["false_action_rate"], "recall_of_expected": sp["recall_of_expected"], "fallback_share": sp["fallback_share"], "ms_p50": sp["ms_p50"], "ms_p95": sp["ms_p95"]}
        at("dialogue.patient")
        d = jfile(f"dialogue-{key}.json")
        if d:
            e, w = d["earned"], d["withheld"]
            out["dialogue.patient"][model] = {"n": e["n"], "fact_used": round(e["used_fact"] / e["n"], 3), "withheld_cannot_recall": round(w["cannot_recall"] / w["n"], 3), "leaks": w["word_leaks"] + w["validator_leaks"],
                                              "model_answered": round(e["model"] / e["n"], 3), "ms_p50": d["ms_p50"], "ms_p95": d["ms_p95"]}
        at("dialogue.staff")
        s_ = jfile(f"staff-{key}.json")
        m = jfile(f"multiagent-{key}.json")
        if s_:
            out["dialogue.staff"][model] = {"accepted_share": s_["accepted_share"], "nurse": s_["by_role"]["nurse"], "anaesthesia": s_["by_role"]["anaesthesia"], "fallback_share": s_["fallback_share"], "ms_p95": s_["ms_p95"], "stay_silent_when_not_addressed": s_.get("stay_silent_when_not_addressed"),
                                            "three_voices_p95_s": m["all_three_s"]["p95"] if m else None, "three_voices_within_budget": m["all_three_within_budget"] if m else None}
        at("compose.narrative")
        n = jfile(f"narrative-{key}.json")
        if n:
            out["compose.narrative"][model] = {"accept_rate": n["accept_rate"], "first_try_rate": n["first_try_rate"], "fallback_to_authored": n["fallback_to_authored"], "ms_p50": n["latency_ms_p50"]}
        at("critic")
        c = jfile(f"critic-{key}.json")
        if c:
            out["critic"][model] = {"recall": c["recall"]["critic"], "false_alarm": c["critic_false_alarm_rate"], "union_with_rules": c["recall"]["union"], "seconds": c["seconds"]}
        at("judge.scenario")
        j = jtext(f"judge-{key}.stdout")
        if j:
            out["judge.scenario"][model] = {"flagged_among_missed_by_rules": j["flagged_among_those_the_rules_missed"], "clean_flagged": j["clean_flagged_share"], "seconds": j["seconds"]}
        at("trap.reword")
        t = jtext(f"traps-{key}.stdout")
        if t:
            out["trap.reword"][model] = {"accepted_share": t["accepted_share"], "fairness_lower": t["cases_with_lower_trap_fairness"], "seconds_per_case_p50": t["seconds_per_case_p50"]}
        at("compose_plan.extras")
        x = jtext(f"planextras-{key}.stdout")
        if x:
            out["compose_plan.extras"][model] = {"cases_with_events": x["cases_with_events"], "share_with_interaction": x["share_cases_with_interaction"], "refused": x["refused_by_validation"], "ineligible": x["ineligible_events_in_final_proposals"], "seconds_p50": x["seconds_p50"]}
        at("compose_plan.planner")
        p = jfile(f"planner-{key}.json")
        if p:
            a_ = p["arms"]["planner"]
            out["compose_plan.planner"][model] = {"served": a_["served"], "passed_gate": a_["passed_gate"], "coverage": a_["coverage_mean"], "distinct_block_sets": a_["distinct_block_sets"], "seconds_p50": a_["seconds_p50"]}
        at("debrief.rewrite")
        b_ = jfile(f"debrief-{key}.json")
        if b_:
            out["debrief.rewrite"][model] = {"accept_rate": b_["accept_rate"], "within_8s": b_["within_8s_share"], "ms_p50": b_["latency_ms_p50"], "mutations_rejected": f'{b_["mutations_rejected"]}/{b_["mutations"]}'}
        at("personalisation.interpret")
        i = jfile(f"interpreter-{key}.json")
        if i:
            out["personalisation.interpret"][model] = {"confirmed": i["confirmed_by_model"], "no_proposal": i["learners_with_no_accepted_proposal"], "seconds_per_learner": i["seconds_per_learner"]}
    out["orders.interpret"] = interpret_figures()
    return {k: v for k, v in out.items() if v}


def interpret_figures() -> dict:
    """The single-call semantic order interpreter (ADR-0028), measured on the AI-assistant-authored held-out set 1: the frozen implementation with the cloud model (scored once) and, for the comparison only, the local model on the same sentences (a second look).
    `correct` is the share of ALL rows behaving as the label says; `false_offer` the share of non-order rows (non-orders and unsupported) that were read back."""
    fig = {}
    for model, name in ((LOCAL, "orders-ai-heldout_v1-qwen_second_look.json"), ("gemma4:31b-cloud", "orders-ai-heldout_v1.json")):
        p = ROOT / "reports" / "ai" / name
        if p.exists():
            d = json.loads(p.read_text(encoding="utf-8"))
            fig[model] = {"correct": d["accuracy"], "wrong": d["wrong_item_rate"], "false_offer": d["non_order_false_positive_rate"], "ms_p50": round(d["latency_s"]["median"] * 1000), "ms_p95": round(d["latency_s"]["p95"] * 1000)}
    return fig


def choose(task: str, f: dict) -> tuple[str, str]:
    """(model, why) for one task from its figures."""
    loc = f.get(LOCAL)
    if loc is None:
        return LOCAL, "no local baseline measured"
    others = {m: v for m, v in f.items() if m != LOCAL}
    ok: dict[str, tuple] = {}
    for m, v in others.items():
        if task == "speech.action":
            if v["false_action_rate"] <= loc["false_action_rate"] and v["exact_match"] >= loc["exact_match"] + MARGIN and v["fallback_share"] <= 0.02 and v["ms_p95"] <= BUDGET_MS:
                ok[m] = (v["exact_match"], -v["false_action_rate"], -v["ms_p50"])
        elif task == "dialogue.patient":
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
        elif task in ("orders.kind", "orders.map", "orders.interpret"):
            if v["correct"] >= loc["correct"] + MARGIN and v["wrong"] <= loc["wrong"] and v["false_offer"] <= loc["false_offer"] and v["ms_p95"] <= BUDGET_MS:
                ok[m] = (v["correct"], -v["wrong"], -v["ms_p50"])
        elif task == "personalisation.interpret":
            if v["confirmed"] >= loc["confirmed"] + MARGIN:
                ok[m] = (v["confirmed"],)
    if not ok:
        return LOCAL, "no candidate is better by the rule's margin without a worse safety figure"
    best = max(ok, key=lambda m: ok[m])
    return best, "best eligible candidate by the rule"


def build() -> dict:
    first = figures(only_first=True)
    fig = figures()
    decisions = {}
    for task, f in fig.items():
        model, why = choose(task, f)
        decisions[task] = {"figures": f, "selected": model, "why": why}
        if first.get(task) and first[task] != f:
            decisions[task]["figures_with_first_prompts"] = first[task]            # what the same task measured before ADR-0026 improved its prompt
    return {"adr": "ADR-0026", "date": "2026-10-25", "local_baseline": LOCAL,
            "rule": __doc__.split("The rules, fixed before the choice:")[1].split("Parameter count")[0].strip(),
            "caveat": "The prompts of the six cloud-selected tasks were written and adjusted while measuring on these same synthetic sets (the 12 approved cases for dialogue, an AI-written staff set, the seeded-defect sets), so the figures are development figures, not held-out evidence; human ratings and fresh sets are DT-250.",
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
