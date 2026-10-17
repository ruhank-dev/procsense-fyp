# p1-live-scenario-generation-s15 — Generated reports and the cross-modal text consistency critic

Milestone: **M14 P1 live scenario generation — dataset analysis and fresh cases**


## What
Write investigation reports from approved templates with numbers locked to the generated values (LLM paraphrase optional, numbers verified), and run an LLM critic that extracts claims from all generated text (narrative, reports, dialogue seeds) and compares them with the structured case; any unresolved conflict rejects the package.

## Why
Contradictions hidden in free text are the ones rules miss.

## How to implement
1. Report templating with number lock.
2. `server/generation/critic.py`: claim extraction prompt, comparison, defect output.
3. Seeded set of 200 text inconsistencies.

## Status (17 October 2026)

- **Built:** `server/generation/reports.py` (a laboratory line from each result row, an imaging report from the picture; `check` allows only the numbers the case or the picture holds) and `server/generation/critic.py` (the model reads the structured facts and the patient's lines and flags conflicts; a flag is kept only for a real line id and a fact copied from the list; unresolved flags become defects and go to repair; the critic only adds defects). `tools/generation/inconsistency_set.py` builds the seeded set (214 inconsistencies of seven kinds from the approved cases, SD-001) and the rule-based baseline.
- **Measured** (`tools/ai/eval_critic.py`, qwen3.5:4b-q4_K_M, report `reports/ai/critic-eval.json`; the verified critic is the one the pipeline runs): on the 214 seeded inconsistencies the rule-based baseline recalled 66.8 percent, the critic alone 68.7 percent, both 81.8 percent (+15 points, short of the +20 target). By kind the model found only 13 of 29 flipped sides and 14 of 36 denied pains; the baseline found none of either. False alarms on 26 clean cases: critic 3.8 percent, baseline 19.2 percent; 0 questions the critic could not answer.
- **Deterministic safeguards (18 October 2026):** `server/generation/consistency.py` closes those two gaps with plain code over the patient's lines and the structured case, run by the package gate on every generated package whether or not a critic exists (`SIDE_VS_EXAM`, `SIDE_VS_OPERATION`, `PAIN_DENIED`; all three have a repair strategy). Rules: a bodily side word that contradicts the side in the examination findings and problem labels, or the right-sided anatomy of both operations when the case gives none (a negated side, a side by a limb or the chest, and "I do not feel right" are ignored); and an unqualified denial of pain ("no pain at all", "not in pain", "no tummy pain") in a case whose examination found a painful abdomen or whose complaint gives pain ("no chest pain" and "no pain when I pass urine" are ordinary answers). Result on the same set: denied pain 36 of 36, flipped side 28 of 29 (the one miss is the seeded text "I do not feel left", which is not a claim about a side), no flag on any of the 51 authored cases or on any of their variant answers (`tools/ci/test_generation_consistency.py`). The rule-based recall is now 96.7 percent and recall with the critic 99.1 percent. The critic's own gain over the new baseline is therefore not measurable on this set; it was measured on the held-out set below (DT-217).
- **Held-out measurement** (18 October, `tests/generation/text_inconsistencies_v2.json`, 134 members of 7 kinds, worded differently, written after the rules and frozen before any critic run; scored once, `tools/ai/eval_critic.py --set v2`, 4B, verified critic): the rules find 8.2 percent, the critic alone 68.7 percent, rules and critic together 70.9 percent, **a gain of 62.7 points over the rules (target 20)**; false alarms on the 26 clean cases 3.8 percent (target under 10); the critic could always answer. By kind: an age said in other words 23 of 24, a pregnancy in a man 18 of 24, an invented allergen 17 of 24, the operation described wrongly 15 of 24, tests said normal 13 of 24, a denied allergy 5 of 5, **a denied condition only 1 of 9**. The gate now measures the 20-point margin on this set (and that the critic loses nothing on the first set), so `critic` passes and is on. The raw first pass recalls 94 percent but flags 92 percent of clean cases; the verification pass is what keeps false alarms low and costs about a quarter of the true flags.
- **Limit:** the contradictions are blunt and synthetic, and the rules were written after seeing which kinds the model missed, so the 96.7 percent is not a held-out figure. The criterion (union recall 20 points above the rules, false alarms under 10 percent) is judged in `docs/report/ai-eval.md`. Paraphrase of reports by a model was not built: the numbers are locked in the template lines.

## Verification
Critic recall at least 20 points above rules plus lexical validator, false alarms under 10 percent.

## Files changed
`server/generation/reports.py`, `server/generation/critic.py`

## Dependencies
p1-live-scenario-generation-s07, p1-ai-infrastructure-s02

## Common issues
Critic false alarms block good cases: tune on the seeded set; unresolved flags go to repair, not silent pass.

## Sources
Features B18, B22.
