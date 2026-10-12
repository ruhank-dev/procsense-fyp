# Research log

One row per question researched online: what was asked, where the answer came from, when it was read, what it says, and how sure we are. Policy: INSTRUCTIONS.md section 8j (research uncertain information, record the source and date, never claim a source says what it does not). Times are UTC.

## 12 October 2026: planning update (AI features and live generation)

| # | Question | Source (URL) | Accessed | Finding | Certainty and open points |
|---|---|---|---|---|---|
| R-001 | Can a locally served model return JSON that follows a schema? | https://ollama.com/blog/structured-outputs | 2026-10-12 | Ollama supports structured outputs by passing a JSON schema in the `format` parameter (blog post dated 6 December 2024). | High for the feature; the validity rate per model on our schemas must be measured (OQ-01). |
| R-002 | Qwen3 licence, size and context | https://huggingface.co/Qwen/Qwen3-8B | 2026-10-12 | Apache-2.0; 8.2 B parameters; 32,768-token native context (131K with YaRN); thinking mode on by default, switchable (`enable_thinking=False`). | High (model card). |
| R-003 | Qwen3 sizes available in the local runtime | https://ollama.com/library/qwen3 | 2026-10-12 | Tags include 1.7b (about 1.4 GB), 4b (about 2.5 GB, 256K context listed) and 8b (about 5.2 GB, 40K context listed). | High for download sizes; VRAM use with context and the 6 GB limit must be measured. |
| R-004 | Speech recognition library, licence, VRAM | https://github.com/SYSTRAN/faster-whisper | 2026-10-12 | MIT licence; upstream benchmark lists large-v2 with int8 and beam 5 at about 2,926 MB VRAM on GPU. | High for licence; the benchmark GPU differs from ours, so our VRAM and latency must be measured. |
| R-005 | Piper TTS licence and status | https://github.com/rhasspy/piper | 2026-10-12 | Repository archived (6 October 2025), MIT; development moved to OHF-Voice/piper1-gpl, which is GPL. Voice models carry their own licences. | High for the archive and the move; the successor's exact GPL version and each voice's licence must be read before use (OQ-03). |
| R-006 | Graph visualisation format for the pipeline JSON | https://js.cytoscape.org | 2026-10-12 | Cytoscape.js (MIT) takes an `elements` array: nodes with `data.id`, edges with `data.source` and `data.target`, compound nodes with `data.parent`. | High. Import of this JSON into Cytoscape Desktop is expected to work but was not tried here. |
| R-007 | ECG simulation for rate and noise variants | https://neuropsychology.github.io/NeuroKit/functions/ecg.html and https://github.com/neuropsychology/NeuroKit | 2026-10-12 | `ecg_simulate` offers the ECGSYN model (McSharry et al. 2003) or a simple model, with `heart_rate`, `noise` and `random_state` parameters; NeuroKit2 is MIT. | High for the API; clinical realism of variants is checked against PTB-XL profiles (p1-dataset-analysis-s03). |
| R-008 | Tabular synthetic-data library (SDV) licence | https://github.com/sdv-dev/SDV | 2026-10-12 | Business Source License; offers GaussianCopula and CTGAN synthesizers. | High. Decision: not used; own seeded copula with numpy/scipy instead (MC-08). |
| R-009 | Similarity search for novelty fingerprints | https://github.com/ekzhu/datasketch | 2026-10-12 | MIT; MinHash and LSH for Jaccard similarity estimation. | High. |
| R-010 | Medical language model terms (MedGemma) | https://huggingface.co/google/medgemma-4b-it | 2026-10-12 | Gated access under the Health AI Developer Foundations terms; outputs are not intended for direct clinical decisions. | High for the gating; the full terms must be accepted by the owner before download (E61, deferred). |

| R-011 | What hardware does the development desktop have? | Measured on the machine (Windows CIM query) | 2026-10-12 | 16 GB RAM; AMD Ryzen 5 2600 (6 cores, 12 threads); NVIDIA GeForce GTX 1660 SUPER. | High (measured). The laptop (RTX 4050 Laptop, 6 GB) was not available; its RAM, CPU and power limit are DT-118. That the GTX 16 series has no tensor cores is general hardware knowledge, not re-checked online today. |

## Not researched in this update (stated so nobody assumes otherwise)

| Topic | Why it matters | Where it is tracked |
|---|---|---|
| Llama 3.2 3B licence terms | Alternative to Qwen3 | OQ-08 |
| FastAPI, numpy, scipy, Mermaid licences | Recorded from general knowledge (MIT, BSD-3-Clause, BSD-3-Clause, MIT); not re-read today | Licence register rows marked "verify at implementation" |
| Embedding model licences | Only needed if lexical retrieval is not enough | MC-07 |
| Real latency and VRAM on the GTX 1660 SUPER | All latency numbers in the plan are targets | OQ-01, p1-ai-language-s02 |
| Per-condition lab correlations from published sources | The MIMIC-IV demo is small | OQ-02 |
