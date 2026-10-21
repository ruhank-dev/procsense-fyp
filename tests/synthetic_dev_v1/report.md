# synthetic_dev_v1: validation report (21 October 2026)

**DEVELOPMENT data. AI-written and AI-reviewed (the same model), synthetic: not independent, not human-labelled, not real-world validation.** Generated with no cloud call. Specifications (objective, situation, actor and recipient, step and window, waiting-question and read-back state, contrast set, style, persona, derived gold) were derived from the approved rules and frozen with a SHA-256 manifest before any sentence was written (`specs.json`, `manifest.json`); utterances (`utterances/*.jsonl`) carry no label; a failing sentence was rewritten (`attempts.jsonl`) or the specification dropped (`discards.jsonl`), never relabelled. The machine-readable numbers are in `report.json`; `dataset.jsonl` joins the accepted utterances to their frozen gold.

| | planned | accepted |
|---|---|---|
| items | 500 | **493** (7 dropped, see `discards.jsonl`) |
| A credit-eligible ordinary | 150 | 146 |
| B performed but ineligible (29 out of window, 15 state absent) | 45 | 44 |
| C ordinary not performed | 140 | 140 |
| D genuine critical (never credited from speech) | 55 | 54 |
| E critical topic without claiming completion | 30 | 30 |
| F no act (incl. near misses) | 50 | 49 |
| G multi-intent (per-act gold) | 30 | 30 |
| credit-eligible items / no credit | 180 / 320 | 176 / 317 |
| safety-sensitive | 91 | 91 |
| contrast sets complete | 33 | 33 (198 items) |
Diversity (accepted): distinct-1/2/3 0.153 / 0.573 / 0.790, self-BLEU 0.352, top three-word opening 2.2 percent ("if I were", "the nurse said"), top skeleton 0.8 percent, stock repair phrases 0 percent. Leakage against every historical sentence of the repository (exact, Jaccard 0.7, local embedding 0.92): 20 hits found in the first pass, all rewritten (up to three attempts); 0 remain. Near-duplicates: two pairs inside a contrast set (minimal pairs by design). Cue warnings (34) were each read and accepted or fixed (`review.jsonl`).
Personas used: P1 92, P2 136, P3 96, P4 116, P6 53; P5 and P7 reserved and unused. Styles: concise 84, conversational 92, formal 54, informal 65, hesitant 37, indirect 42, verbose 36, understandable non-native 53, self-correction 30; 28 items carry hedging on content; 77 items have a prior turn.
Open issues and excluded areas: `open_issues.md` (DT-241 to DT-244).
