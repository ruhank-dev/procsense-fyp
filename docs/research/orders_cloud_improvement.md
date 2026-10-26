# Spoken-order fallback: 77.8 to 96.3 percent on the builder's own set, 90.2 and 72.6 percent on sets written by other models (B23.c2, C32.c1)

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
- The gate `order_fallback` (`content/ai/thresholds.json`) now reads set 8: every accuracy, safety and latency suite passes, but a suite that asks for independent evidence (a second, human author, EX-18) fails, so the switch stays OFF and the features are engineering complete with the switch off. The routes `orders.kind` and `orders.map` use the cloud model only with the institutional approval and the person's own consent (ADR-0027) and fall back to the local model.

## Limits

One AI author, 54 orders: one wrong row moves the figure by 1.9 points. The labels of two rows in older sets ("surgical registrar", "warfarin" against "anticoagulant") are arguable and were left out of the new sets rather than changed. Accented speech and real recogniser errors are not modelled (DT-205). The Quest check of the spoken path is DC-08 and DC-10.

## Independent re-evaluation (EX-18, 25 Oct 2026 UTC): the 96.3 percent does not hold

Sets 6 to 8 were written by the same assistant family that builds the mapper. To measure against something less tied to the builder, two further sets were made by **other model families** from a fixed authoring prompt (`tools/ai/make_orders_set.py`), never edited by hand, with the label of each row being the item the author was asked to paraphrase: set 9 by `gpt-oss:120b-cloud` (61 orders, 24 non-orders) and set 10 by `nemotron-3-nano:30b-cloud` (62 orders, 24 non-orders). They are **machine-written, not human-authored, and do not satisfy EX-18**. Set 9 was scored once and studied; the notes of five medicines were then clarified (prophylactic or treatment antibiotics, sugar by mouth or into the vein); set 10 was scored once afterwards.

| Set (author) | Model | Correct | Wrong item | Menu | Non-orders offered | p50 / p95 | Safety-related wrong offers | Released without a yes |
|---|---|---|---|---|---|---|---|---|
| 8 (builder's assistant) | Gemma 4 31B Cloud | 96.3 % | 0 % | 3.7 % | 1 of 10 | 1.25 s / 1.9 s | 0 | 0 |
| 9 (gpt-oss) | Gemma 4 31B Cloud | 90.2 % | 4.9 % | 4.9 % | 3 of 24 | 0.07 s / not reported | see file | 0 |
| **10 (nemotron), after the note changes** | Gemma 4 31B Cloud | **72.6 %** | 6.5 % | 21.0 % | 8 of 24 | 0.38 s / 1.9 s | 2 | 0 |

(`reports/ai/orders-mapper-compare-v9_gemma.json`, `v10_gemma.json`.) The misses, split by cause in `reports/ai/orders-breakdown-v9.json` and `v10.json` (a second run of the cloud model, so close to the scored figure, not equal):

- **The question guard.** The grammar refuses to read back a question or a condition ("can I get morphine", "should I hold the metformin"), on purpose (DT-208 safety review). Polite English requests are often phrased as questions, so these go to the menu. That is safe and it is a product decision, not a mapper error; it accounts for 3 of 61 rows in set 9 and 6 of 62 in set 10. Counting only the rows the guard lets through, 94.8 percent (set 9) and 80.4 percent (set 10) were read back correctly.
- **Sentences that name no item** ("Stop that home med", "Hold the home medicine") whose label is the particular medicine the author was asked about: no listener could answer these.
- **Items that fit two entries** (a broad-spectrum antibiotic is the treatment, the sepsis bundle or the prophylactic dose; a glucose bolus is by mouth or into the vein): the read-back puts the other item first or asks a question.
- **Non-orders.** 3 of 24 and 8 of 24 sentences were offered, several of them first-person statements ("I need to administer adrenaline immediately", "I'm going to give an antiemetic") that the author labelled as not asking for anything but a trainee might well mean as an order. Four of the eight in set 10 are offered by the grammar alone.
- Nothing was released without a yes in any run.

**Conclusion, stated plainly.** The acceptance of B23.c2 (90 percent and 10 points above the grammar) was met on sets 8 and 9 and not on set 10; the acceptance of C32.c1 (95 percent) was met on set 8 only. The figures depend strongly on who writes the sentences, so a single AI author cannot settle the question and a human-authored set (EX-18) is needed. Until then the features are **partly built** (built and measured, below acceptance on the independent sets), the gate `order_fallback` reads set 10 and stays **OFF**, and the earlier claim that B23 and C32 were engineering complete is withdrawn.
