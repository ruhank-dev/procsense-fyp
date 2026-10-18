# ADR-0017 — Qwen 3.5 4B replaces 9B in every production path

**Date:** 18 October 2026 (third session) · **Status:** Accepted (engineer, on measured results; supersedes the batch half of ADR-0010) · **Related:** ADR-0010, ADR-0016, p1-ai-language-s02, p1-ai-infrastructure-s02

## Context
ADR-0010 chose the 9B model for offline batch prompts (plan composer, performance interpreter, debrief wording, patient wording, text critic) on an index score, before any of them was measured with both models. Since then every gate that switches those features on was passed by the 4B model (the debrief and critic gates were never run with the 9B at all). The 9B does not fit the 6 GB card: 57 percent on the GPU, 9.2 tokens per second, 3.84 s for a short structured call, 6.26 GB loaded.

## Decision
1. **4B for everything** (`server/llm/client.py` `BATCH_MODEL`, `check_hosts` no longer requires the 9B, the evaluation tools default to the 4B). Evidence, same data, prompts, schemas and settings (`docs/research/model_4b_vs_9b.md`, `tools/ai/model_compare.py`): interpreter 4B +0.268 over random against 9B +0.105, 3 times faster; plan composer coverage 0.895 against 0.877, 3 times faster; text critic: 4B passes the gate (false alarms 0.067, limit 0.10) and the 9B fails it (0.267) although its recall is 0.067 higher; patient wording: 9B accepts 3.8 points more lines (0.841 against 0.803; the 4B passes the 0.70 gate and a rejected line falls back to the authored line), at 6 times the time. For the debrief only the 4B was ever measured.
2. **Not a classifier either way**: as the act classifier the 9B is better than the 4B (macro-F1 0.803 against 0.591 on the same sentences) but both are far below the heads and name critical content as ordinary acts; the credit path uses no language model (ADR-0016).
3. **The 9B stays installable** only for `tools/bench/cross_judge.py` (a larger cross-check labeller) and as the judge of the knowledge-base comparison. **Knowledge-base relation extraction moved to the 4B on 18 October 2026 (fourth session):** the same extraction run with each model, both judged by the same 9B judge (reports/ai/kb-extract-4b-vs-9b.json): 4B 86 relations, 26 supported by both judging configurations (30.2 percent), 45 by either (52.3 percent); 9B 87 relations, 21 supported by both (24.1 percent), 46 by either (52.9 percent). The 4B is equal or better on this proxy, so `tools/kb/extract_relations.py` defaults to it. Neither reaches 0.8; the judge is AI and the 9B judge may favour its own output (conservative for the 4B); precision against people is EX-10. The extraction output is still only a queue a person approves.
4. Thinking stays off for all prompts; nothing here was run with thinking on.

## Alternatives
Keep the 9B for batch (the ADR-0010 plan): no measured quality gain on any gate-bearing task, one false-alarm failure, 3 to 6 times slower, needs CPU offload. Keep it only for patient wording: a 3.8 point gain in lines accepted does not justify a 6.5 GB model that half runs on the processor when the fallback is the authored line.

## Consequences
One model to install, load and benchmark; batch work no longer competes for the card; the single model slot (ADR-0011) serves every call. Wording variety in generated patient lines may be slightly lower. Quality was measured on synthetic and AI-judged data only; a blind human rating (EX-02) can still reopen this.

## Revisit if
The blind rating (EX-02) prefers the 9B's wording clearly; a task is added where the 4B fails its gate and the 9B passes; the laptop (EX-09) shows the 9B fits and is fast there; a new Qwen size is released.
