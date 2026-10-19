# ADR-0021 — The spoken-order mapper, compared with its predecessor on the same sentences

**Date:** 19 October 2026 (fifth session) · **Status:** Accepted for the engineering; `order_fallback` stays OFF · **Related:** ADR-0018 (decision 2 and its correction), ADR-0020

## Context
Two earlier reports compared the single-call mapper (fourth session) with the mapper of the fifth session on DIFFERENT sets (76.4 percent on set 2, 58.9 percent on set 3) and so showed no regression or improvement. This record compares them on the same sentences.

## Method
`tools/ai/compare_order_mappers.py`: the grammar alone, the fourth-session mapper reproduced from commit 4e97297 (`legacy`: one model call over every item, prompt `orders.map@2`, confidence floor 0.9) and the current mapper (`current`: kind stage, content notes, deterministic re-ranking, agreement of two readings, a question when they disagree), all behind the same present grammar and synonym table (`reports/ai/orders-mapper-compare.json`, `-v4.json`). Sets 1 to 3 are development sets (their failures were analysed; the synonym table and the grammar were extended from them); set 4 (64 rows: 54 orders and 10 non-orders, `tests/orders/paraphrases_v4.json`) was written before the final change of the mapper and scored once. All synthetic, one author family.

## Results (correct / wrong / non-orders offered; the model is not deterministic: three runs of `legacy` on set 1 gave 77.0, 77.0 and 82.0 percent correct)
| Set | grammar alone | legacy | current |
|---|---|---|---|
| 1 (development) | 8.2 / 0 / 0 | 82.0 / 8.2 / 30 % | 65.6 / 4.9 / 0 % |
| 2 (development) | 18.2 / 0 / 0 | 83.6 / 7.3 / 20 % | 67.3 / 5.5 / 0 % |
| 3 (development) | 8.9 / 1.8 / 0 | 82.1 / 12.5 / 40 % | 64.3 / 1.8 / 0 % |
| **4 (frozen, scored once)** | 27.8 / 0 / 0 | 83.3 / 7.4 / **70 %** | **77.8 / 3.7 / 0 %** (18.5 percent to the menu, 3 questions between two items, 1 containing the right item) |

On set 4 by kind (current): tests 17 of 23, medicines 10 of 16, places 6 of 6, staff 5 of 5, held medicines 4 of 4. Latency per hearing: median 3.6 s, p95 6.6 s (the plan allows 3 s). Nothing was released without a yes in any run.

## Findings
* The earlier "regression" was mostly a change of set. Measured on the same sentences the current mapper offers about 5 to 17 points fewer correct read-backs than the single call, with half or less of its wrong ones, and, decisively, it turns no small talk into an order, where the single call turned 20 to 70 percent of the non-orders into a read-back (a read-back still needs a yes, but a trainee who is asked to confirm things they did not say learns to say yes).
* The diagnostic on set 1 showed where the current mapper loses recall: the kind stage names no kind for 5 of 61 orders, the first reading says NONE for 4, and 5 are read back wrongly (CRP read as a full blood count, a CT as an MRI, prophylactic antibiotics as treatment antibiotics). Letting the model skip the kind stage raised recall by 2 points and the share of non-orders offered from 0 to 20 percent, so it was not kept.
* The model's stated confidence does not separate right from wrong picks (a wrong pick at 0.95 is common), which is why agreement of two readings is used instead.

## Decision
Keep the current mapper behind the gate. Targets (90 to 95 percent correct, at most 5 percent wrong, 3 s) are not met: set 4 shows 77.8 and 3.7 percent and 6.6 s at p95. The gate `order_fallback` now reads the set-4 result and fails on accuracy and latency; it stays OFF. Not tried because no valid held-out data could judge it: a larger model, a fine-tuned mapper, a retrieval model for the near neighbours (P3-02). A second author for the paraphrases is EX-18.
