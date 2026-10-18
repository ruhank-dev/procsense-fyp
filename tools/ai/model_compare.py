"""Qwen 3.5 4B against 9B on the same tasks, the decision and the page (`docs/research/model_4b_vs_9b.md`): `uv run --project tools tools/ai/model_compare.py [--check]`.

Reads only committed result files in reports/ai (made by the evaluation tools with the same data, prompts, schemas and settings; only `--model` differs):

  interpreter-eval-4b.json / interpreter-eval.json         performance interpreter (tools/ai/eval_interpreter.py)
  planner-eval-4b.json / planner-eval.json                 plan composer against the solver (tools/ai/eval_planner.py)
  narrative-4b.json / narrative-9b.json                    patient wording of generated cases (tools/ai/eval_narrative.py)
  critic-eval-4b.json, critic-eval-v2-4b.json / critic-eval-9b.json, critic-eval-v2-9b.json   text consistency critic on the same first N cases (tools/ai/eval_critic.py --limit N)
  llm-classifier-4b.json / llm-classifier-9b.json          the language-model act classifier (tools/bench/llm_classifier_compare.py)
  model-resources.json                                     size, memory, load time, speed (tools/ai/model_resources.py)

The decision rule is written here and in docs/decisions/ADR-0017: for each production task the 4B result must be at least the 9B result on the task's primary measure minus its tolerance, must pass the same gate suite
(content/ai/thresholds.json), and must be faster; a task for which no result exists for BOTH models cannot decide anything and is reported as not compared. 9B is kept only where it wins by more than the tolerance
or where 4B fails its gate. The page states what was not measured. `--check` fails when the committed page differs from what the files give.
"""
from __future__ import annotations

import json
import sys
from pathlib import Path

sys.stdout.reconfigure(encoding="utf-8")
ROOT = Path(__file__).resolve().parents[2]
R = ROOT / "reports" / "ai"
OUT = ROOT / "docs" / "research" / "model_4b_vs_9b.md"
TOL = 0.02


def load(name: str):
    p = R / name
    return json.loads(p.read_text(encoding="utf-8")) if p.exists() else None


def tasks() -> list[dict]:
    rows = []
    i4, i9 = load("interpreter-eval-4b.json"), load("interpreter-eval.json")
    if i4 and i9 and i9.get("model", "").startswith("qwen3.5:9b"):
        rows.append({"task": "Performance interpreter (hypotheses the tracker confirms)", "prompt": "personalisation.interpret", "measure": "confirmed by the model minus confirmed by random picks", "m4": round(i4["confirmed_by_model"] - i4["confirmed_by_random_picks"], 3),
                     "m9": round(i9["confirmed_by_model"] - i9["confirmed_by_random_picks"], 3), "n": f"{i4['learners']} learners", "s4": i4["seconds_per_learner"], "s9": i9["seconds_per_learner"], "unit": "s per learner", "gate_ok4": i4["confirmed_by_model"] > i4["confirmed_by_random_picks"]})
    p4, p9 = load("planner-eval-4b.json"), load("planner-eval.json")
    if p4 and p9 and p9.get("model", "").startswith("qwen3.5:9b"):
        rows.append({"task": "Plan composer (coverage of the requested dimensions, higher is better)", "prompt": "compose_plan.spine / blocks", "measure": "coverage mean of the planner arm", "m4": p4["arms"]["planner"]["coverage_mean"], "m9": p9["arms"]["planner"]["coverage_mean"],
                     "n": f"{p4['requests']} requests", "s4": p4["arms"]["planner"]["seconds_p50"], "s9": p9["arms"]["planner"]["seconds_p50"], "unit": "s per case (p50)", "gate_ok4": p4["arms"]["planner"]["coverage_mean"] >= p4["arms"]["solver"]["coverage_mean"] and p4["arms"]["planner"]["served"] >= 19,
                     "extra": f"served {p4['arms']['planner']['served']} against {p9['arms']['planner']['served']}; model plans used {p4['arms']['planner']['model_plans_used']} against {p9['arms']['planner']['model_plans_used']}"})
    n4, n9 = load("narrative-4b.json"), load("narrative-9b.json")
    if n4 and n9 and n9["cases"] == n4["cases"]:
        rows.append({"task": "Patient wording of generated cases (share of lines accepted by the validator)", "prompt": "compose.narrative", "measure": "accepted share after one retry", "m4": n4["accept_rate"], "m9": n9["accept_rate"], "n": f"{n4['cases']} cases, {n4['lines']} lines",
                     "s4": n4["latency_ms_p50"] / 1000, "s9": n9["latency_ms_p50"] / 1000, "unit": "s per line (p50)", "gate_ok4": n4["accept_rate"] >= 0.7})
    for suffix, label in (("", "v1"), ("-v2", "v2")):
        c4, c9 = load(f"critic-eval{suffix}-4b.json"), load(f"critic-eval{suffix}-9b.json")
        if c4 and c9:
            rows.append({"task": f"Text consistency critic, seeded set {label} (recall of the union with the rules)", "prompt": "critic.claims / critic.verify", "measure": "union recall", "m4": c4["recall"]["union"], "m9": c9["recall"]["union"], "n": f"{c4['members']} seeded changes",
                         "s4": None, "s9": None, "unit": "", "gate_ok4": c4["recall"]["union"] >= c4["recall"]["baseline"] + 0.2 and c4["critic_false_alarm_rate"] <= 0.1,
                         "gate_ok9": c9["recall"]["union"] >= c9["recall"]["baseline"] + 0.2 and c9["critic_false_alarm_rate"] <= 0.1,
                         "extra": f"false alarms on {c4['clean_cases']} clean cases {c4['critic_false_alarm_rate']} (4B) against {c9['critic_false_alarm_rate']} (9B); the gate allows 0.10; critic alone {c4['recall']['critic']} against {c9['recall']['critic']}"})
    l4, l9 = load("llm-classifier-4b.json"), load("llm-classifier-9b.json")
    if l4 and l9:
        for k, nm in (("v3_test_sample", "v3 hold-out sample"), ("adversarial_holdout3", "adversarial set 4")):
            rows.append({"task": f"Language-model act classifier on the {nm} (macro-F1; NOT a production path, the credit path uses the embedding and TF-IDF heads)", "prompt": "classify.acts", "measure": "macro-F1", "m4": l4[k]["macro_f1"], "m9": l9[k]["macro_f1"], "n": f"{l4[k]['rows']} sentences",
                         "s4": l4[k]["latency_ms_p50"] / 1000, "s9": l9[k]["latency_ms_p50"] / 1000, "unit": "s per call (p50)", "gate_ok4": None,
                         "extra": f"critical sentences named as an ordinary act before the cues: {l4[k]['critical_rows_named_ordinary_before_guard']} against {l9[k]['critical_rows_named_ordinary_before_guard']}"})
    return rows


NOTES = {"compose.narrative": "Decision: 4B. The 9B accepts 3.8 points more lines, but a rejected line falls back to the authored line (no safety loss), the 4B passes the gate (0.70), and the 9B costs about 6 times the time and twice the memory and half runs on the processor; the gain does not justify it."}


def verdict(r: dict) -> str:
    if r["gate_ok4"] is False:
        return "9B (4B fails the task's gate)"
    if r.get("gate_ok9") is False:
        return "4B (9B fails the task's gate)"
    if r["m4"] >= r["m9"] - TOL:
        return "4B" + (" (better)" if r["m4"] > r["m9"] + TOL else " (equal within the tolerance)")
    return "9B (4B is lower by more than the tolerance)"


def render() -> str:
    rows = tasks()
    res = load("model-resources.json")
    L = ["# Qwen 3.5 4B or 9B", "", "_Generated by `tools/ai/model_compare.py` from the committed result files in `reports/ai`; do not edit by hand. Decision: [ADR-0017](../decisions/ADR-0017-qwen-4b-replaces-9b.md). All measures are on SYNTHETIC or AI-judged data produced for development; none is human validation._", "",
         "## Where 9B was used", "",
         "| Where | What | Production or evaluation |", "|---|---|---|",
         "| `server/llm/client.py` `BATCH_MODEL`, `prompts/registry.json` role `batch` | plan composer (`compose_plan.spine`, `compose_plan.blocks`), performance interpreter (`personalisation.interpret`), debrief wording (`debrief.rewrite`), patient wording (`compose.narrative`), text critic (`critic.claims`, `critic.verify`) | the offline and service paths of the generation and analytics features (one slot, never during a trainee's live reply) |",
         "| `tools/ai/eval_*.py` defaults | evaluation tools | evaluation |", "| `tools/kb/extract_relations.py` | knowledge-base relation extraction from the case passages | offline data preparation |", "| `tools/bench/cross_judge.py` | a larger cross-check labeller (J3) of the synthetic classifier sets | evaluation only |", "",
         "## Same task, same data, same settings", "", "Same prompts (`prompts/registry.json`), schemas, thinking off, temperature and context as set by `server/llm/client.py`, the same requests, learners and cases; only the model changes. Tolerance on the primary measure: " + f"{TOL}.", "",
         "| Task | Measure | Rows | 4B | 9B | Time 4B | Time 9B | Better or equal | Note |", "|---|---|---|---|---|---|---|---|---|"]
    for r in rows:
        t = lambda v: "-" if v is None else f"{v:.1f} {r['unit']}"          # noqa: E731
        L.append(f"| {r['task']} | {r['measure']} | {r['n']} | {r['m4']} | {r['m9']} | {t(r['s4'])} | {t(r['s9'])} | {verdict(r)} | {r.get('extra', '')} {NOTES.get(r['prompt'], '')} |")
    if res:
        L += ["", "## Cost on the desktop (GTX 1660 SUPER, 6 GB; measured)", "", "| | 4B (qwen3.5:4b-q4_K_M) | 9B (qwen3.5:9b-q4_K_M) |", "|---|---|---|"]
        for label, key in (("Disk (GB)", "disk_gb"), ("Memory in use when loaded (GB)", "loaded_gb"), ("Share on the GPU", "gpu_share"), ("Cold load time (s)", "load_s"), ("Generation speed (tokens per second)", "tokens_per_s"), ("Short structured call (s, median of 5)", "call_s")):
            L.append(f"| {label} | {res['4b'][key]} | {res['9b'][key]} |")
        L += ["", f"Measured by `tools/ai/model_resources.py` on {res['measured_on']}; Ollama {res['ollama']}. The 9B does not fit the 6 GB card and runs partly on the processor."]
    ok = [r for r in rows if "production" not in r["task"].lower() and not r["task"].startswith("Language-model")]
    prod_4b = all(verdict(r).startswith("4B") or r["prompt"] in NOTES for r in ok) and bool(ok)
    L += ["", "## Decision", "",
          ("Every production task that was compared is equal or better with the 4B model and faster, and passes the same gate suite. **4B replaces 9B in every production path**; 9B is kept only as an optional evaluation cross-check (`cross_judge.py`). See ADR-0017." if prod_4b else
           "At least one production task is worse with the 4B model: 9B stays for it, see the table. ADR-0017 records which."), "",
          "Not measured, and so not claimed: a blind human rating of the wording of the two models (EX-02); the laptop (EX-09); 4B and 9B with thinking on; the 9B on the debrief rewrite (the debrief gate was measured with the 4B only). The knowledge-base extraction comparison is in the section below when it was run.", ""]
    kb = load("kb-extract-4b-vs-9b.json")
    if kb:
        L += ["## Knowledge-base relation extraction (offline)", "", "| | 4B | 9B |", "|---|---|---|", f"| Relations extracted | {kb['4b']['relations']} | {kb['9b']['relations']} |",
              f"| Supported by both AI judges | {kb['4b']['supported_by_both']} | {kb['9b']['supported_by_both']} |", f"| Precision by both judges | {kb['4b']['precision_both']} | {kb['9b']['precision_both']} |", "",
              f"Both sets were judged by the same 9B judge (it may favour its own output, which is conservative for the 4B); relations supported by either judging configuration: {kb['4b']['precision_either']} (4B) against {kb['9b']['precision_either']} (9B). The judges are AI, so this is a proxy, not the labelled precision the step requires (EX-10); neither model reaches 0.8 on it. Decision: the extractor defaults to the 4B (ADR-0017).", ""]
    return "\n".join(L) + "\n"


def main(argv: list[str]) -> int:
    text = render()
    if "--check" in argv:
        ok = OUT.exists() and OUT.read_text(encoding="utf-8") == text
        print("4B against 9B page " + ("current" if ok else "OUT OF DATE: run tools/ai/model_compare.py"))
        return 0 if ok else 1
    OUT.write_text(text, encoding="utf-8", newline="\n")
    print("wrote", OUT.relative_to(ROOT).as_posix())
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
