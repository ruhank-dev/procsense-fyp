"""Writes docs/research/model_benchmarks.md from the benchmark runs of this machine (p1-ai-infrastructure-s02): `uv run --project tools tools/ai/bench_report.py`.

Inputs are the one-line-per-call result files of tools/ai/benchmark.py in artifacts/ai-bench (git-ignored; BT0 speed and memory, BT1 structured output, BT2 grounding) and the committed evaluation results in
reports/ai (debrief, dialogue, planner, critic, interpreter, multi-agent), which are the BT4 debrief, BT5 composition and BT6 judge runs of the plan done on the production code. The report states which
machine was measured and which of the plan's runs were NOT made (the laptop, Unity running, the laptop served over the LAN). It reads files and writes one document; it changes nothing else.
"""
from __future__ import annotations

import glob
import json
import statistics
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / "docs" / "research" / "model_benchmarks.md"
ART = ROOT / "artifacts" / "ai-bench"
REPORTS = ROOT / "reports" / "ai"


def runs(test: str) -> dict[str, list[dict]]:
    out: dict[str, list[dict]] = {}
    for p in sorted(glob.glob(str(ART / f"*-{test}-*.jsonl"))):
        rows = [json.loads(line) for line in Path(p).read_text(encoding="utf-8").splitlines() if line.strip()]
        head = next(r for r in rows if r["record"] == "run")
        out[head["model"]] = rows
    return out


REMOVED = {"qwen3:4b": " (baseline; removed from the machine at the owner's request on 17 October 2026)"}


def name(model: str) -> str:
    return model + REMOVED.get(model, "")


def pct(x: float) -> str:
    return f"{round(100 * x)}%"


def b0_table() -> list[str]:
    lines = ["| Model | Digest | Cold start | Warm request p50 | Generation speed | Share on the GPU | Size |", "|---|---|---|---|---|---|---|"]
    for model, rows in runs("b0").items():
        head = next(r for r in rows if r["record"] == "run")
        calls = [r for r in rows if r["record"] == "call"]
        warm = [c["wall_s"] for c in calls if not c["cold"]]
        res = next((r for r in rows if r["record"] == "residency"), {})
        lines.append(f"| {name(model)} | {head['digest']} | {round(next(c['wall_s'] for c in calls if c['cold']), 1)} s | {round(statistics.median(warm), 2)} s | {round(statistics.median(c['gen_tps'] for c in calls if not c['cold']), 1)} tok/s | "
                     f"{pct(res.get('gpu_share', res.get('gpu_share', 0)))} | {res.get('size_gb', '?')} GB |")
    return lines


def b1_table() -> list[str]:
    lines = ["| Model | Calls | Valid JSON with a real intent | Request p50 |", "|---|---|---|---|"]
    for model, rows in runs("b1").items():
        calls = [r for r in rows if r["record"] == "call"]
        lines.append(f"| {name(model)} | {len(calls)} | {pct(sum(c['valid'] for c in calls) / len(calls))} | {round(statistics.median(c['wall_s'] for c in calls), 2)} s |")
    return lines


def b2_table() -> list[str]:
    lines = ["| Model | Earned replies passing the validator | Withheld replies passing the validator | Request p50 |", "|---|---|---|---|"]
    for model, rows in runs("b2").items():
        calls = [r for r in rows if r["record"] == "call"]
        e = [c for c in calls if c["condition"] == "earned"]
        w = [c for c in calls if c["condition"] == "withheld"]
        lines.append(f"| {name(model)} | {sum(c['ok'] for c in e)} of {len(e)} | {sum(c['ok'] for c in w)} of {len(w)} | {round(statistics.median(c['wall_s'] for c in calls), 2)} s |")
    return lines


def load(name: str) -> dict | None:
    p = REPORTS / name
    return json.loads(p.read_text(encoding="utf-8")) if p.exists() else None


def production_runs() -> list[str]:
    rows = ["| Run | Result | File |", "|---|---|---|"]
    d = load("debrief-eval.json")
    if d:
        rows.append(f"| BT4 debrief rewrite ({d['model']}, {d['sessions']} sessions) | accepted {pct(d['accept_rate'])}, within 8 s {pct(d['within_8s_share'])}, p50 {round(d['latency_ms_p50'] / 1000, 1)} s | reports/ai/debrief-eval.json |")
    d = load("planner-eval.json")
    if d:
        a = d["arms"]
        rows.append(f"| BT5 composition ({d['model']}, {d['requests']} requests) | planner served {a['planner']['served']} of {d['requests']}, coverage {a['planner']['coverage_mean']} against the solver's {a['solver']['coverage_mean']}, p50 {a['planner']['seconds_p50']} s | reports/ai/planner-eval.json |")
    d = load("critic-eval.json")
    if d:
        rows.append(f"| BT6 judge, the text critic ({d['model']}, {d['members']} seeded cases) | recall {pct(d['recall']['critic'])}, false alarms {pct(d['critic_false_alarm_rate'])} | reports/ai/critic-eval.json |")
    d = load("dialogue-eval.json")
    if d:
        rows.append(f"| Dialogue ({d['model']}, {d['cases']} cases) | p50 {round(d['ms_p50'] / 1000, 2)} s, p95 {round(d['ms_p95'] / 1000, 2)} s, word leaks {d['withheld']['word_leaks']} | reports/ai/dialogue-eval.json |")
    d = load("multiagent-eval.json")
    if d:
        rows.append(f"| Three characters in turn ({d['model']}) | first reply p95 {d['first_reply_s']['p95']} s, all three p95 {d['all_three_s']['p95']} s | reports/ai/multiagent-eval.json |")
    d = load("interpreter-eval.json")
    if d:
        rows.append(f"| Performance interpreter ({d['model']}, {d['learners']} learners) | confirmed {pct(d['confirmed_by_model'])} against {pct(d['confirmed_by_random_picks'])} for random picks | reports/ai/interpreter-eval.json |")
    return rows


def build() -> str:
    machine = next(iter(runs("b0").values()))[0]["machine"] if runs("b0") else {}
    out = ["# Local language model benchmarks on the development desktop", "",
           "Generated by `tools/ai/bench_report.py` (p1-ai-infrastructure-s02). Do not edit by hand. Measured on **one machine**: " + (f"{machine.get('gpu', '?')}, {machine.get('vram_total_mb', '?')} MB of video memory." if machine else "no run found."),
           "", "## What was measured and what was not", "",
           "Done: BT0 (speed and memory), BT1 (structured output) and BT2 (grounding) for the candidate models on the desktop with nothing else heavy running, and the production-code evaluations that stand for BT4 (debrief), BT5 (composition) and BT6 (judge).",
           "**Not done, and why:** the laptop (RTX 4050) was not available to this session; Unity (with the VR scene) and the speech recogniser were not loaded beside the model, so the shared-GPU frame-time cost is unknown; the desktop serving the laptop over the LAN was not run; BT3, the blind human rating of wording, needs people. "
           "The default model per task below is therefore a desktop-only recommendation.", "",
           "## BT0: speed and memory (dialogue-shaped request, 200-token prompt)", ""] + b0_table() + [
           "", "The 4B model fits wholly on the 6 GB card and answers in about a second; the 9B model is partly on the processor (about 12 tokens a second) and is used only for offline batch work (ADR-0010).", "",
           "## BT1: structured output (valid JSON with an intent from the real role list)", ""] + b1_table() + [
           "", "## BT2: grounding (the same question asked once with the fact earned and once withheld; every reply goes through the grounding validator)", ""] + b2_table() + [
           "", "## Production-code evaluations (BT4 to BT6 and the later features)", ""] + production_runs() + [
           "", "## Default per task on this desktop", "",
           "| Task | Model | Why |", "|---|---|---|",
           "| Live dialogue, classification alternative, staff replies, debrief rewrite | qwen3.5:4b-q4_K_M | fits the card, about 1 s per request, structured output and grounding measured above |",
           "| Offline batch: scenario composition, narrative, critic, interpreter, analytics explanations | qwen3.5:9b-q4_K_M | better wording, slow (about 12 tokens a second), acceptable offline |",
           "| Communication-act classification | all-minilm:22m embeddings + logistic regression | 22M parameters, no card needed (ADR-0012); the 4B model stays as an alternative |",
           "| Speech recognition | faster-whisper small.en on the processor | measured word error rate in reports/ai/asr-wer.json |",
           "| Speech output | Piper en_GB-cori-high on the processor | first sentence 2.19 s at the 95th percentile on a quiet processor |", ""]
    return "\n".join(out)


def main() -> int:
    text = build()
    OUT.write_text(text, encoding="utf-8", newline="\n")
    print("wrote", OUT.relative_to(ROOT).as_posix())
    return 0


if __name__ == "__main__":
    sys.exit(main())
