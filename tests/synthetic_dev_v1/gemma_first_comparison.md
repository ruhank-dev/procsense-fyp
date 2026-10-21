# synthetic_dev_v1: Gemma-first evaluator (experiment B) against the shipped pipeline (A), 21 October 2026

DEVELOPMENT evaluation on AI-written data; EXPERIMENT only, the production pipeline is untouched. B = gemma4:31b structured evaluation (prompt, parser and scorer frozen by hash in `tools/experiments/gemma_first/frozen.json` before the 493 rows were evaluated) + authoritative scenario evidence (window, waiting question / read-back, verified Unity events: none exist in this dataset) + the experimental graded score in `scoring.py` (weights labelled EXPERIMENTAL). 497 calls, 1,229,158 tokens (2,493 per item), free plan, ceilings 650 calls / 2,000,000 tokens not reached. Files: `gemma_first_summary.json`, `gemma_first_results.jsonl`, `gemma_first_posthoc_rules.json`.

| measure | A shipped | B Gemma-first |
|---|---|---|
| credit-eligible items credited (B: score >= 0.5) | 84/176 = 47.7 % | 172/176 = 97.7 % (>= 0.9: 159/176) |
| per-act recall incl. multi-intent | 9/54 on the multi-intent items | 196/200 overall, 54/54 on multi-intent |
| precision of credited acts | 84.8 % | 82.7 % (full credit: 84.2 %) |
| false credits on no-credit items | 13 | 26 (negated 9, hypothetical 2, intended 2, another actor 1, reported 1, out-of-window/state 3, ordinary act on critical content 7, no-act 1) |
| wrong or extra act on an eligible item | 2 | 15 (mostly REASSURE added to ACKNOWLEDGE_EMOTION or an answer) |
| critical physical completion | 0 | 0 (physical credit 0 on all 493; the model claimed a physical action on 89 items, ignored) |
| ordinary credit on critical items (91) | 0 | 8 |
| no-credit items given no credit | 304/317 = 95.9 % | 291/317 = 91.8 % |
| contrast sets all correct | 16/33 | 20/33 (ordinary 16/28, critical 4/5) |
| schema | not applicable | 493/493 valid after 4 retries (3 SCHEMA, 1 FENCED), 0 time-outs |
| latency | median 17 ms | median 2.3 s per call, p95 4.7 s, max 9.8 s (3 parallel workers) |
