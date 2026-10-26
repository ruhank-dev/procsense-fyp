# Spoken-order fallback: from 77.8 to 96.3 percent (B23.c2, C32.c1)

Date: 25 Oct 2026 (UTC). **Synthetic and AI-written**: every paraphrase set was written by an AI assistant before any row was run, so the sets are not independent of the builder; the second human author (EX-18) does not exist yet. The figures are a development result, not a clinician or trainee result.

## What was wrong

The order mapper (`server/orders/llm_fallback.py`: a kind question, a re-ranking of the items, a choice among them read twice) was tuned for the small local model. Its frozen result on set 4 was 77.8 percent. Read with the large model the same pipeline scored 78 to 79 percent on the development sets, and the misses were of three kinds:

1. A request put as a question ("is she pregnant", "how is her blood count looking", "does she clot normally") was answered NONE at the kind stage.
2. A test with no analytes in its definition (group and save, MRCP) gave the model nothing to match ("keep some blood back in case he bleeds").
3. Two deterministic corrections written for the small model overrode a correct kind from the large one ("the warfarin should be stopped" was demoted from a home medicine because the word list lacked "stopped").

## What was changed (prompts and content only; no new classifier, gate or checker model)

- `prompts/orders/kind_v4.txt` and `map_v5.txt`, scope `cloud`: they say that a question whose answer needs an item asks for it, that a sentence about what a person thinks or did asks for no one, that blood matched or kept ready is the group and save, and that a named bundle to start is a medicine. A cloud leg now uses the newest cloud-scope prompt even when the caller pins a version for the local one (`server/llm/gateway.py`); the local model keeps its pinned prompts.
- `content/ai/order_notes.json`: one plain sentence per test, per staff role and per medicine saying what it is for (awaiting clinician review; the model only chooses among items). It also lifted the local model.
- For a cloud answer the home-medicine correction is not applied; the rule that a place which is also a scan is a place only when the sentence moves the patient is kept.

## Measurements

Sets 6, 7 and 8 have 64 rows each (54 orders, 10 non-orders). Set 6 and set 7 were scored once and then studied, so they became development sets; set 8 was written after that work and scored once per model.

| Set | Model | Correct | Wrong item | Menu | Non-orders offered | p50 / p95 |
|---|---|---|---|---|---|---|
| Set 4 (frozen, ADR-0021) | local, old prompts | 77.8 % | 3.7 % | | 0 of 10 | 3.6 s / 6.6 s |
| Set 6, first look | Gemma 4 31B Cloud, first cloud prompts | 94.4 % | 3.7 % | 1.9 % | 0 of 10 | 1.2 s / 1.8 s |
| Set 7, first look | Gemma 4 31B Cloud, tuned on set 6 | 92.6 % | 5.6 % | 1.9 % | 0 of 10 | 1.2 s / 1.5 s |
| **Set 8 (held out)** | **Gemma 4 31B Cloud** | **96.3 %** | **0 %** | 3.7 % | 1 of 10 | **1.25 s / 1.9 s** |
| Set 8 (second look) | local qwen3.5:4b, same prompts and notes | 90.7 % | 0 % | 9.3 % | 2 of 10 | 4.6 s |
| Set 8 | grammar alone | 31.5 % | 0 % | 68.5 % | 1 of 10 | 28 ms |

Nothing was released without a yes in any run. Files: `reports/ai/orders-mapper-compare-v6_gemma.json`, `v7_gemma`, `v8_gemma`, `v8_qwen_second_look`.

## Acceptance

- B23.c2: at least 90 percent and at least 10 points above the grammar alone: 96.3 percent and 64.8 points above. **Met.**
- C32.c1: at least 95 percent and above the grammar alone: 96.3 percent. **Met on the held-out set**, not on the two earlier looks (94.4 and 92.6 percent), which is why the figure to trust is the last one and why it is still one author.
- The gate `order_fallback` (`content/ai/thresholds.json`) now reads set 8 and is ON. The routes `orders.kind` and `orders.map` use the cloud model only with the institutional approval and the person's own consent (ADR-0027) and fall back to the local model.

## Limits

One AI author, 54 orders: one wrong row moves the figure by 1.9 points. The labels of two rows in older sets ("surgical registrar", "warfarin" against "anticoagulant") are arguable and were left out of the new sets rather than changed. Accented speech and real recogniser errors are not modelled (DT-205). The Quest check of the spoken path is DC-08 and DC-10.
