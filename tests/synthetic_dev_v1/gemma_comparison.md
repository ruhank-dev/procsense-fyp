# synthetic_dev_v1: gemma4:31b semantic reader against the shipped classifier baseline (21 October 2026)

DEVELOPMENT evaluation, AI-written data, not generalisation evidence. The existing reader, prompt, schema, retries (max 2 attempts), time-outs (8 s), SemanticInterpreter, `cap_policy=hard`, `confidence_mode=head`, cues, thresholds, validation and scoring were used unchanged; the reader was asked about EVERY item for the raw view, and the gated pipeline was then run on the same recorded replies.
Usage: 507 provider calls, 722,426 tokens (697,019 prompt, 25,407 output), free plan, ceilings 700 calls / 1,000,000 tokens not reached, no 401/402/403/429. Files: `gemma_results.jsonl`, `gemma_summary.json`, `tools/synth_dev/gemma_eval.py`, `tools/synth_dev/compare_views.py`.

| view | recall | offered = credited precision | false credits | wrong act credited | critical false offer / credit | no-credit not offered |
|---|---|---|---|---|---|---|
| shipped classifier (baseline) | 84/176 = 47.7 % | 84/99 = 84.8 % | 13 | 2 | 0 / 0 | 304/317 = 95.9 % |
| Gemma raw (a single supported non-critical objective counted as an offer, no gate) | 140/176 = 79.5 % | 140/219 = 63.9 % | 74 (41 are performed sentences made ineligible by window or state, which the deterministic layer is for) | 5 | 3 / 3 | 243/317 = 76.7 % |
| Gemma + existing gates | 77/176 = 43.8 % | 77/79 = 97.5 % | 2 | 0 | 0 / 0 | 315/317 = 99.4 % |
| final credit (engine) | 77/176 = 43.8 % | 77/79 = 97.5 % | 2 | 0 | 0 / 0 | 315/317 = 99.4 % |
