# Synthetic diverse communication-act set: model-written, model-labelled, NOT independent

Written on 17 October 2026 by the language model that built the benchmark tooling, to check whether a classifier trained on some writing styles holds up on styles it has not seen. **No person wrote, labelled or
reviewed any row.** It is not an independent benchmark and nothing here is evidence about real trainee speech. The independent set (docs/research/classifier_benchmark) still needs real authors, a second labeller and
an adjudicator.

| Part | What | Files |
|---|---|---|
| Train | 320 sentences, 16 per act of the 20 in `content/assessment/taxonomy.json`, four simulated author profiles T1 to T4 (terse registrar, chatty junior nurse, formal consultant, anxious student); used with the old template set (`tests/classifier`) | `authored_train_1.txt`, `authored_train_2.txt` |
| Dev | 100 sentences for the cards `D001` to `D100` of `docs/research/classifier_benchmark/cards_dev.csv`, profiles DA (second-language English) and DB (text-message style) | `authored_dev.txt` |
| Test | 200 sentences for the cards `T001` to `T200`, profiles TC (rushed night-shift fragments) and TD (over-polite, hesitant) | `authored_test.txt` |
| Second labels | a separate pass over a blind sheet (text only, no act) holding every critical-act row and a quarter of the others: 43 dev rows, 106 test rows; **made by the same model**, so not an independent labeller | `blind_sheet_*.txt`, `second_labels_*.txt` |
| Adjudication | the 4 rows where the second label differed from the intended act, settled by the same model with the tie-break rules of the annotation guide | `adjudication.txt` |
| Frozen | the merged rows with the final labels, and the hashes the benchmark checks | `train.jsonl`, `dev.jsonl`, `test.jsonl`, `manifest.json` |

Each profile appears in one split only. Hard cases are in every split: negation, self-correction, incomplete speech, deliberate ambiguity, NONE. Recogniser copies of the plain and paraphrased test sentences (real speech
recogniser errors, `tools/bench/asr_roundtrip.py --synthetic`) are in `asr_test.jsonl`.

Two test sentences (T110, T138) were reworded before anything was evaluated because the leakage check found near-duplicates of training sentences; the leakage check now finds none between dev or test and anything in training.
The test split is scored once (`tools/bench/bench_classifier.py --data synthetic`, a lock file in `artifacts/ai-bench` records it). `tools/bench/synthetic.py check` fails if a file differs from what the sources give.
