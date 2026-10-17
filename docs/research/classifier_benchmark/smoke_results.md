# Smoke results (17 October 2026): the pipeline runs; this is not an evaluation

`tools/bench/bench_classifier.py --data smoke` on the old synthetic set (`tests/classifier`, SD-006: 319 train, 83 dev, 78 test rows). The result file is `artifacts/ai-bench/classifier-bench-smoke.json`
(git-ignored) and says `valid_for_claims: false`. **Nothing below says how any candidate will do on real trainee speech, and no winner is chosen.** The independent dev and test set does not exist yet (README).

## Why the set cannot support a claim

| Measured on the set | Value |
|---|---|
| dev sentences with a near-duplicate (token overlap 0.8 or more) in training | 32 of 83 (39%) |
| test sentences with a near-duplicate in training | 9 of 78 (12%) |
| dev and test near-duplicate pairs | 13 |
| dev / test sentences with embedding cosine 0.9 or more to a training sentence | 63% / 47% |

Each act has 8 templates (12 for NONE) shared by all splits; the generator is the only labeller.

## What ran (decoding for Qwen: temperature 0, top_k 1, seed 0, this process only)

| Candidate | Macro-F1 (95% interval, resampled by speaker) | Credited / precision | False credits | Critical acts credited from speech | Recogniser copies, clean / phone (24 + 24 rows) | Latency |
|---|---|---|---|---|---|---|
| TF-IDF + logistic regression | 0.938 (0.884 to 0.978) | 43 / 0.977 | 1 | 0 | 0.875 / 0.875 | 0.01 ms a sentence in a batch |
| Embeddings (`all-minilm:22m`) + logistic regression | 0.963 (0.913 to 1.000) | 34 / 1.000 | 0 | 0 | 0.958 / 0.875 | 121 ms a sentence (single Ollama call, p95 131 ms) |
| Qwen 3.5 4B (existing classifier, unchanged prompt) | 0.634 (0.528 to 0.688) | 56 / 0.571 | 24 | 0 | 0.500 / 0.625 | 3.1 s a sentence (p95 3.3 s) |

- Paired macro-F1 difference TF-IDF minus embeddings: -0.025 (interval -0.090 to 0.027): not separated, as expected on shared templates.
- Both statistical models beat Qwen by about 0.3 macro-F1 here (intervals exclude 0); this repeats the earlier 0.641 and is about agreement with the generator.
- In 8 of the 78 test sentences a critical act was said and Qwen's speech credit went to a different, non-critical act. No critical act was ever credited from speech by any candidate (the rule excludes them, and the engine refuses them).
- Resources: benchmark process about 146 MB; GPU memory used went from 1.7 GB to 5.2 GB while Qwen 4B was loaded; the embedding model is 46 MB. The recogniser copies (48 rows from 24 sentences, one synthetic voice, `tools/bench/asr_roundtrip.py`) differ from their sources mostly in punctuation, spelling (`anesthetist`) and an occasional dropped first word; real speech will be harder.

## Found on the way

The speech engine (`server/tts/engine.py`) wrote Piper's WAV through the process's standard output; on Windows that text-mode stream rewrites line-feed bytes and the speech came out garbled (the recogniser heard "I love you"). The fix (write to a temporary file; the same sentences are then transcribed correctly) is in the speech work that is not committed yet, so `asr_roundtrip.py` needs that work to be pushed first.
