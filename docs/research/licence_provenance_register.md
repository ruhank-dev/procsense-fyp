# Licence and provenance register

Every dataset, model, library and voice that ProcSense uses or plans to use, with its licence, what that allows, what we keep in the repository and what we must do. Policy (INSTRUCTIONS.md section 8j): **licensing is not a blanket blocker.** Read the terms, record them here, and use the material in the way the terms allow. Do not upload restricted material to a public repository. The dataset details are in `docs/research/dataset_register.csv`; the sources read are in [research_log.md](research_log.md).

Decision words: **ALLOWED** (use freely), **ALLOWED WITH CONDITIONS** (follow the conditions column), **NOT USED** (not needed or not checked), **REJECTED** (terms conflict with our use), **VERIFY** (terms not re-read yet; read them before the item is installed).

## Datasets

| ID | Item | Licence | Decision | What the repository holds | Conditions and notes | Checked |
|---|---|---|---|---|---|---|
| DS-0001 | VitalDB | CC BY 4.0 | ALLOWED WITH CONDITIONS | Aggregates only (`tools/data/derived/vitaldb_noise.json`) | Attribution | 2026-10-10 |
| DS-0002 | PTB-XL 1.0.3 | CC BY 4.0 | ALLOWED WITH CONDITIONS | Templates and selected strips | Attribution | 2026-10-10 |
| DS-0003 | MIMIC-IV Clinical Demo 2.2 | ODbL 1.0 | ALLOWED WITH CONDITIONS | Aggregate percentiles only | Share-alike applies to derived databases; keep only aggregate statistics; reference and tests only | 2026-10-10 |
| DS-0004 | MIMIC-IV-ED Demo 2.2 | ODbL 1.0 | ALLOWED WITH CONDITIONS | Aggregates only | As DS-0003 | 2026-10-10 |
| DS-0005 | UIdataGB v2 | CC BY 4.0 | ALLOWED WITH CONDITIONS | Selected images | Attribution | 2026-10-10 |
| DS-0006 | NIH ChestX-ray14 | NIH terms (attribution and citation) | ALLOWED WITH CONDITIONS | Selected images | Cite the dataset and paper | 2026-10-10 |
| DS-0007 | openFDA drug labels | CC0 1.0 | ALLOWED | Extract | None | 2026-10-10 |
| DS-0008 | Regensburg Pediatric Appendicitis | not confirmed | NOT USED | Nothing | Confirm the licence before any use | 2026-10-10 |
| DS-0009 | PriMock57 | not checked | NOT USED | Nothing | Check before use | 2026-10-10 |
| DS-0010 | Mozilla Common Voice | not read | NOT USED | Nothing | Check before use | 2026-10-10 |
| DS-0011 | ProcSense labelled synthetic images | ProcSense generated | ALLOWED | Images | Always labelled synthetic (ADR-0003) | 2026-10-10 |
| DS-0012 | ProcSense EMR distractor identities | ProcSense authored | ALLOWED | CSV | Invented names only | 2026-10-10 |
| DS-0013 | Indiana University chest X-ray (Open-i) | CC BY-NC-ND 4.0 | REJECTED | Nothing | No-derivatives conflicts with cropping and labelling | 2026-10-10 |
| DS-0014 | AMOS 2022 | CC BY 4.0 | ALLOWED WITH CONDITIONS | Selected slices | Attribution; painted findings labelled synthetic | 2026-10-10 |

## Models

| ID | Item | Licence | Decision | Held in repository | Conditions and notes | Checked |
|---|---|---|---|---|---|---|
| MC-01, MC-02, MC-04, MC-12 | Qwen3 (1.7B, 4B, 8B) | Apache-2.0 | ALLOWED WITH CONDITIONS | No (downloaded by the runtime) | Keep the licence notice; record the model digest in every package | 2026-10-12 (R-002, R-003) |
| MC-03 | P2 small classifier base model | to choose | VERIFY | No | Choose in p2-ai-extensions-s03 | — |
| MC-05 | Whisper weights via faster-whisper | MIT | ALLOWED | No | None | 2026-10-12 (R-004) |
| MC-06 | Piper voices | per voice | VERIFY | No | Read each voice's licence before bundling (OQ-03) | — |
| MC-07 | Embedding model (only if needed) | to choose | VERIFY | No | Lexical retrieval first | — |
| MC-13 | MedGemma 4B | Health AI Developer Foundations terms (gated) | ALLOWED WITH CONDITIONS (deferred) | No | Owner accepts the terms before download; outputs never used for clinical decisions | 2026-10-12 (R-010) |
| — | Llama 3.2 3B | not read | VERIFY | No | OQ-08 | — |

## Libraries and runtimes

| ID | Item | Licence | Decision | Conditions and notes | Checked |
|---|---|---|---|---|---|
| EXT-OLLAMA | Ollama | MIT | ALLOWED | Local runtime | VERIFY (general knowledge) |
| EXT-FWHISPER | faster-whisper | MIT | ALLOWED | — | 2026-10-12 (R-004) |
| EXT-PIPER | Piper | MIT (archived) or GPL (successor piper1-gpl) | ALLOWED WITH CONDITIONS | Prefer the archived MIT release or pre-rendered audio; if the GPL successor is used, keep it a separate program, not linked into the build | 2026-10-12 (R-005) |
| EXT-NEUROKIT2 | NeuroKit2 | MIT | ALLOWED | — | 2026-10-12 (R-007) |
| EXT-DATASKETCH | datasketch | MIT | ALLOWED | — | 2026-10-12 (R-009) |
| — | SDV | Business Source License | REJECTED | Not needed; own copula | 2026-10-12 (R-008) |
| EXT-SCIPY | numpy, scipy | BSD-3-Clause | ALLOWED | — | VERIFY (general knowledge) |
| EXT-FASTAPI | FastAPI | MIT | ALLOWED | — | VERIFY (general knowledge) |
| EXT-CYTOSCAPE | Cytoscape.js | MIT | ALLOWED | Documentation use only | 2026-10-12 (R-006) |
| EXT-MERMAID | Mermaid | MIT | ALLOWED | Documentation use only | VERIFY (general knowledge) |

## Provenance of generated scenarios

Every generated ScenarioPackage records: request, seed, parameter pack versions (each pack cites its profile and dataset ids), block library version, prompt version, model digest, content manifest hash, the ids of every image and strip with its dataset id, and the package SHA-256 (see `docs/architecture/live_generation_spec.md` section 6). A package is therefore traceable to every dataset row in this register.
