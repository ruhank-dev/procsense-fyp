# Communication-act classifier benchmark

Decides how ProcSense should classify the trainee's spoken communication acts (`content/assessment/taxonomy.json`, 20 labels, 5 of them critical). Three candidates only: **TF-IDF + logistic regression**
(the existing small classifier), **sentence embeddings + logistic regression**, and the **existing Qwen 3.5 4B classifier** (`server/classify/classifier.py`). SetFit and a zero-shot model are deferred
unless these three give insufficient results and the extra cost is justified.

## Status

| Part | State |
|---|---|
| SBAR_ELEMENT made a critical act in the engine (`critical_acts`), regression test, taxonomy-to-mapping consistency check | Done (commit `7de6281`) |
| Scenario cards (100 dev + 200 test), annotation guide | Done: `cards_dev.csv`, `cards_test.csv`, [annotation_guide.md](annotation_guide.md) |
| Benchmark script, leakage checks, freeze step, tests | Done: `tools/bench/` |
| Embedding model | `all-minilm:22m` verified and pulled (below) |
| **Independent dev and test text and labels** | **BLOCKED on people** (below). Nothing has been written, labelled or generated to stand in for them. |
| Winner chosen | **No.** Results so far are smoke tests on the old synthetic set and say nothing about real trainees: [smoke_results.md](smoke_results.md). |

## What the old data can and cannot show

`tests/classifier` (480 rows, SD-006) is smoke-test data. Measured by the benchmark (`reports` in `artifacts/ai-bench/classifier-bench-smoke.json`): 8 templates per act are shared by every split, so 39% of dev
and 12% of test sentences have a near-duplicate (token overlap 0.8 or more) in training, and 63% and 47% have an embedding cosine of 0.9 or more with a training sentence. Its labels come from the
generator that wrote the sentences. Any score on it is an optimistic bound on agreement with that generator.

## Embedding model: verified

| Check | Result | Source |
|---|---|---|
| Model | `all-minilm:22m` on Ollama, 22.6M parameters, 46 MB, F16 | Ollama library page |
| Licence | Apache-2.0 | Ollama library page and the Hugging Face card of `sentence-transformers/all-MiniLM-L6-v2` |
| Dimension | 384 | HF card; confirmed here: 384 numbers per sentence |
| Context | 256 word pieces (HF: longer text truncated; Ollama `num_ctx` 256); sentences here are far shorter | both |
| API | `POST /api/embed` accepts an array of strings, returns `embeddings`; Ollama 0.40.5 installed (the library page lists 0.1.26 or later) | Ollama API docs; tested here with a batch of three |
| Normalisation | Not promised by the docs; the returned vectors had length 1.0 here, and the benchmark normalises anyway | tested |
| Sanity | Two allergy questions had cosine 0.82, an unrelated sentence 0.10 | tested |
| Resources | 46 MB on disk; trivial next to Qwen 4B (3.3 GB) on the 6 GB GPU; the jobs run one after another | measured sizes |

Not verified: that Ollama's F16 conversion gives numerically identical vectors to the original sentence-transformers model. It does not matter for the comparison (every candidate sees the same pipeline) but means
published numbers for MiniLM do not transfer exactly.

## Dataset (blocked on people)

Needed before any claim: **at least two authors per split, with dev and test authors different** (100 dev + 200 test sentences, about 1.5 hours each), **a second labeller** for the 100 critical rows plus a quarter of the others
(about 1 hour), and **an adjudicator** who is a third person for the rows where the two labels differ. Optionally a clinician to look over the critical-act rows (the existing Q8, Q9). Whoever wrote the generator
and the taxonomy must not write or label these rows, because they would be grading their own wording. Steps and rules: [annotation_guide.md](annotation_guide.md).

## Reading a result

`bench_classifier.py` reports macro-F1 with a group-resampled 95% interval (by author), per-class precision, recall and false positives, credited precision, abstention, false credits, latency, memory and the
number of critical acts credited from speech (must be 0). A winner is declared only on independent data, when the paired macro-F1 interval of the best two excludes zero and the best candidate's credited-precision
lower bound meets 0.8. Otherwise it says **no clear winner** and the next step is more data. Per-act precision is reported but not gated: about 8 rows per act cannot prove 0.8.

## Safety

Deterministic action-based scoring stays authoritative. Speech is never credited for a critical act (the engine refuses it in `EvidenceStore.Submit`, and the benchmark counts any violation). Nothing in this folder or in
`tools/bench` writes `content/assessment/thresholds.json`, the mapping, a gate or a scoring file; results go to `artifacts/ai-bench/` (git-ignored).
