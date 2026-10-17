# Synthetic communication-act set, version 2: model-written, AI-judged, NOT independent

Written on 18 October 2026 by the language model that built the benchmark tooling, to test the communication-act classifier on a larger and more varied set than `tests/classifier_synthetic` while no human authors or
labellers are available. **No person wrote, labelled or reviewed any row.** It is not an independent benchmark, it is not real-world validation, and agreement between the AI judges is a consistency check, not proof that
a label is right. The independent set (`docs/research/classifier_benchmark`) is still blocked on people; this set changes nothing about that.

| Part | What | Files |
|---|---|---|
| Train | 840 sentences: 6 simulated author profiles R1-R6 x 20 labels x 7, scenario groups G1-G4 (ward, abdominal pain, day-case admission, medicines) | `authored_train_R*.txt` |
| Dev | 420 sentences: 3 profiles D1-D3 x 20 x 7, scenario groups G5-G6 (recovery and observations, theatre handover and equipment) | `authored_dev_D*.txt` |
| Test | 600 sentences: 3 profiles E1-E3 x 20 x 10, scenario groups G7-G8 (airway and induction, pregnancy, transfusion, diabetes) | `authored_test_E*.txt` |
| Rubric | the label rules both judges receive (the tie-break order of the annotation guide) | `rubric.md` |
| Judgements | blind labels by judge configurations J1 and J2 of `qwen3.5:4b-q4_K_M` (text only, never the intended act); large and regenerable, not committed | `judgements/` |
| Adjudication | contested rows (both judges agree with each other against the author's intent) settled by the builder, reading the sentence without its intended act | `adjudication.txt` |
| Frozen | merged rows, agreement figures and hashes | `train.jsonl`, `dev.jsonl`, `test.jsonl`, `agreement.json`, `manifest.json` |

Each line of an authored file is `label|bucket|scenario group|sentence`; buckets are plain (p), negation (n), correction (c), incomplete (i), ambiguous (a, dev and test only labelled by the tie-break order) and
recogniser-style noise (s: lower case, dropped punctuation, mis-heard words typed by the author; real recogniser errors are made later by `tools/bench/asr_roundtrip.py`).

No profile and no scenario group appears in two splits, so the test split also shifts topic, names and drugs. `tools/bench/synthetic2.py sources` checks structure, scenario groups and near-duplicates (token Jaccard
0.8) between every pair of splits; `--cosine` adds the embedding paraphrase check (0.9 listed, 0.95 refused).
