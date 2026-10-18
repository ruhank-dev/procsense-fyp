# Licence and provenance register

Every dataset, model, library and voice that ProcSense uses or plans to use, with its licence, what that allows, what we keep in the repository and what we must do. Policy (INSTRUCTIONS.md section 8j): **licensing is not a blanket blocker.** Read the terms, record them here, and use the material in the way the terms allow. Do not upload restricted material to a public repository. The dataset details are in `docs/research/dataset_register.csv`; the sources read are in [research_log.md](research_log.md).

Decision words: **ALLOWED** (use freely), **ALLOWED WITH CONDITIONS** (follow the conditions column), **NOT USED** (not needed or not checked), **REJECTED** (terms conflict with our use), **VERIFY** (terms not re-read yet; read them before the item is installed).

**ProcSense is a final-year project, not a product for sale** (decision of the team, 18 Oct 2026, recorded by Abdul Moiz). So a **non-commercial** (NC) or research-only licence fits our use: it is a condition (academic, non-commercial use only), not a conflict. A **no-derivatives** licence (ND) still conflicts, because cropping, labelling and re-encoding are derivatives. Share-alike (SA) is a condition. If ProcSense is ever offered for sale, every row marked "academic, non-commercial use only" must be read again first.

**Refresh of 16 October 2026 (p1-dataset-analysis-s01):** every licence page below was re-read on that day (R-013 to R-029 in the research log) unless the Checked column says otherwise. `tools/data/licence_register_check.py` (in CI) fails when an id of the pipeline JSON (`datasets`, `model_catalog`, `external_dependencies`) or of the dataset register has no row here, when a decision word is not one of the five, or when a row has no date.

## Datasets

| ID | Item | Licence | Decision | What the repository holds | Conditions and notes | Checked |
|---|---|---|---|---|---|---|
| DS-0001 | VitalDB | CC BY 4.0 for the PhysioNet release (VitalDB 1.0.0); CC BY-NC-SA 4.0 with a data use agreement for the vitaldb.net open dataset and API | ALLOWED WITH CONDITIONS | Aggregates only (`tools/data/derived/vitaldb_noise.json`) | Attribution; academic, non-commercial use only (ProcSense is a final-year project, decision of 18 Oct 2026). Our aggregates were read through the vitaldb.net API (CC BY-NC-SA 4.0, research and development use): that fits a final-year project, and only aggregate statistics are kept, never the data. The PhysioNet release (CC BY 4.0) is the copy to use if the project is ever sold (DT-183, closed as not needed) | 2026-10-16 (R-013, R-014) |
| DS-0002 | PTB-XL 1.0.3 | CC BY 4.0 | ALLOWED WITH CONDITIONS | Templates and selected strips (derived) | Attribution | 2026-10-16 (R-015) |
| DS-0003 | MIMIC-IV Clinical Demo 2.2 | ODbL 1.0 | ALLOWED WITH CONDITIONS | Aggregate percentiles only (derived) | Attribution; share-alike applies to a derived database, so only aggregate statistics are kept; reference and tests only | 2026-10-16 (R-016) |
| DS-0004 | MIMIC-IV-ED Demo 2.2 | ODbL 1.0 | ALLOWED WITH CONDITIONS | Aggregates only (derived) | As DS-0003 | 2026-10-16 (R-016) |
| DS-0005 | UIdataGB v2 (Gallblader Diseases Dataset) | CC BY 4.0 | ALLOWED WITH CONDITIONS | Selected images (raw, selected) | Attribution | 2026-10-16 (R-017) |
| DS-0006 | NIH ChestX-ray14 | NIH Clinical Center terms: "no restrictions on the use", with a link, a citation and an acknowledgement | ALLOWED WITH CONDITIONS | Selected images (raw, selected) | Link to the NIH download site, cite the CVPR 2017 paper, acknowledge the NIH Clinical Center as the data provider. The NIH Box page itself is script-driven; the terms were read in the Google Cloud documentation | 2026-10-16 (R-018) |
| DS-0007 | openFDA drug labels | CC0 1.0 | ALLOWED | Extract (wording, derived) | None required | 2026-10-16 (R-019) |
| DS-0008 | Regensburg Pediatric Appendicitis | CC BY-NC 4.0 | NOT USED | Nothing | Children only, so unsuitable for the adult S2 cases; the non-commercial licence would allow academic use | 2026-10-16 (R-020) |
| DS-0009 | PriMock57 | CC BY 4.0 | NOT USED | Nothing | Allowed with attribution if the speech-recognition evaluation ever needs it (decision of 10 Oct 2026: not needed); its audio would not be committed | 2026-10-16 (R-021) |
| DS-0010 | Mozilla Common Voice | not read: since October 2025 only distributed through Mozilla Data Collective under that site's terms | NOT USED | Nothing | Read the dataset's own terms on Mozilla Data Collective before any use (the page could not be read: script-driven) | 2026-10-16 (R-022) |
| DS-0011 | ProcSense labelled synthetic images | ProcSense generated | ALLOWED | Images | Always labelled synthetic (ADR-0003); the painted CT and MRI findings sit on AMOS slices, so DS-0014's attribution applies to those | 2026-10-16 (own work) |
| DS-0012 | ProcSense EMR distractor identities | ProcSense authored | ALLOWED | CSV | Invented names only | 2026-10-16 (own work) |
| DS-0013 | Indiana University chest X-ray (Open-i) | CC BY-NC-ND 4.0 | REJECTED | Nothing | Non-commercial and no-derivatives (cropping and labelling are derivatives) | 2026-10-10 (licence field of the report files; not re-read) |
| DS-0014 | AMOS 2022 | CC BY 4.0 | ALLOWED WITH CONDITIONS | Selected slices (raw, selected) | Attribution; painted findings labelled synthetic | 2026-10-16 (R-023) |
| — | PriMock57 (Babylon Health): 57 mock primary-care consultations, audio and transcripts | CC BY 4.0 (LICENCE.md of the repository) | ALLOWED WITH CONDITIONS (evaluation only) | No (downloaded to a folder outside the repository; only figures are committed) | Attribution in the report that uses it; not used to train anything; actors, not patients | 2026-10-18 (R-045) |

## Models

| ID | Item | Licence | Decision | Held in repository | Conditions and notes | Checked |
|---|---|---|---|---|---|---|
| MC-01, MC-02, MC-04, MC-12 | Qwen3 (1.7B, 4B, 8B) | Apache-2.0 | ALLOWED WITH CONDITIONS | No (downloaded by the runtime) | Keep the licence notice; record the model digest in every package | 2026-10-16 (R-025) |
| MC-01 to MC-04, MC-12 | Qwen3.5 4B (`qwen3.5:4b-q4_K_M`, digest d8b0f5e9760c) and 9B (`qwen3.5:9b-q4_K_M`, digest 56671c2ab938) | Apache-2.0 | ALLOWED WITH CONDITIONS | No (downloaded by the runtime) | Keep the licence notice; record the model digest in every package; installed on the desktop 16 Oct 2026 | 2026-10-16 (R-030, R-031) |
| MC-02 (baseline only) | Qwen3 4B (`qwen3:4b`, digest 359d7dd4bcda; a thinking-2507 build) | Apache-2.0 | ALLOWED WITH CONDITIONS | No | Kept only as the benchmark comparison; **removed from the desktop on 17 October 2026** at the owner's request | 2026-10-16 (R-003, R-025) |
| MC-03, MC-07 | Sentence embedding model `all-minilm:22m` (digest 1b226e2802db; sentence-transformers/all-MiniLM-L6-v2 converted by Ollama, F16, 22.6M parameters, 46 MB, 384 dimensions, 256 word pieces) | Apache-2.0 | ALLOWED WITH CONDITIONS | No (downloaded by the runtime) | Keep the licence notice; the classifier head `content/assessment/act_head.json` records the model name and is only valid for it | 2026-10-17 (R-039) |
| MC-05 | Whisper weights via faster-whisper | MIT (weights and code) | ALLOWED | No | Keep the licence notice | 2026-10-16 (R-024) |
| MC-06 | Piper voices | per voice: see Voices below | ALLOWED WITH CONDITIONS | No | Only a voice marked ALLOWED or ALLOWED WITH CONDITIONS below may be bundled or used to render lines, with its conditions | 2026-10-16 (R-026) |
| MC-07 | Embedding model (only if needed) | to choose | VERIFY | No | Lexical retrieval (BM25, own code) first; read the model's licence if one is added | — (not chosen) |
| MC-08 | Numeric generation (seeded distributions and a Gaussian copula) | own code on numpy and scipy (BSD-3-Clause) | ALLOWED | Code; parameter packs cite their profiles and dataset ids | The parameters come from aggregates only (DS-0001, DS-0003, DS-0004), so their conditions apply to the packs | 2026-10-16 (R-024) |
| MC-09 | ECG traces and strips | own code; NeuroKit2 (MIT); PTB-XL strips (CC BY 4.0) | ALLOWED WITH CONDITIONS | Selected strips and templates | PTB-XL attribution (DS-0002) | 2026-10-16 (R-015, R-024) |
| MC-10 | Medical image selection (tag retrieval) | own code; each image keeps its dataset's licence | ALLOWED WITH CONDITIONS | Image library | The licence filter keeps only images of an ALLOWED or ALLOWED WITH CONDITIONS dataset; each image carries its dataset id for attribution | 2026-10-16 (dataset rows above) |
| MC-11 | Novelty fingerprints | own code (weighted Jaccard); datasketch (MIT) later | ALLOWED | Code | Keep the licence notice if datasketch is added | 2026-10-16 (R-024) |
| MC-13 | MedGemma 4B | Health AI Developer Foundations terms (gated; Hugging Face lists the licence as "other") | ALLOWED WITH CONDITIONS (deferred) | No | The account holder accepts the terms before download, after confirming they are entitled to (the pages read do not state the eligibility rule; nothing is accepted on anyone's behalf); outputs never used for clinical decisions; the registry content/ai/model_candidates.json records the acceptance | 2026-10-18 (R-010, R-028, R-041) |
| — | Pulse Physiology Engine 4.4.0 (Kitware; built from source, commit 5e29e5b) | Apache-2.0 (NOTICE file: keep the Kitware copyright notice) | ALLOWED WITH CONDITIONS | No: only recordings of its output (content/physio/pulse_traces) | Keep the notice with any copy of the engine or the traces; the traces are outputs of a simulation, labelled as such | 2026-10-18 (R-043) |
| — | sherpa-onnx (k2-fsa) with Whisper tiny.en int8 and the 20M streaming Zipformer (mobile) model files | sherpa-onnx code Apache-2.0; the model files' own licences were NOT read in this pass | VERIFY | No (downloaded to a folder outside the repository for the proxy measurement) | Read each model's licence before any file is shipped on the headset (ADR-0014) | 2026-10-18 (R-044) |
| — | Llama 3.2 3B | Llama 3.2 Community License (Hugging Face licence tag `llama3.2`; gated, approved by hand) | VERIFY | No | The terms themselves were not read; read them before choosing it (OQ-08) | 2026-10-16 (R-027, licence name only) |

## Voices

The Piper voices that speak British English (the cases are set in a UK hospital). A voice's model card names the recordings it was trained on, and most were fine-tuned from a US voice, whose own recordings then matter too: the US `lessac` voice is trained on the Blizzard 2013 Lessac data, licensed for research only with every commercial use excluded, and the US `ryan` voice on RyanSpeech, CC BY-NC-SA 4.0. Because ProcSense is a final-year project (decision of 18 Oct 2026), research-only and non-commercial terms behind a voice are met, so these voices are allowed for academic, non-commercial use, with each set of recordings' own conditions (DT-184, closed by that decision; it reopens if the project is ever sold).

| Voice (Piper id) | Recordings and their licence | Fine-tuned from | Decision | Conditions and notes | Checked |
|---|---|---|---|---|---|
| en_GB-cori-high | LibriVox, public domain | none (trained from scratch) | ALLOWED | The only British voice with no restricted data behind it; credit "Cori (Bryce Beattie), LibriVox" as a courtesy | 2026-10-16 (R-026) |
| en_GB-alba-medium | Edinburgh DataShare 10283/3270, CC BY 4.0 | lessac (research only) | ALLOWED WITH CONDITIONS | Attribution; academic, non-commercial use only (ProcSense is a final-year project, decision of 18 Oct 2026) | 2026-10-16 (R-026) |
| en_GB-vctk-medium | VCTK, Edinburgh DataShare 10283/3443, CC BY 4.0 | lessac (research only) | ALLOWED WITH CONDITIONS | As alba | 2026-10-16 (R-026) |
| en_GB-aru-medium | University of Liverpool ARU speech corpus, CC BY 4.0 | lessac (research only) | ALLOWED WITH CONDITIONS | As alba | 2026-10-16 (R-026) |
| en_GB-jenny_dioco-medium | Jenny (Dioco) dataset: commercial use permitted; the voice must be credited as "Jenny (Dioco)" where practical | lessac (research only) | ALLOWED WITH CONDITIONS | As alba, and credit the voice as "Jenny (Dioco)" | 2026-10-16 (R-026) |
| en_GB-northern_english_male-medium | OpenSLR 83, CC BY-SA 4.0 | lessac (research only) | ALLOWED WITH CONDITIONS | As alba; share-alike applies to the voice and what is made from it | 2026-10-16 (R-026) |
| en_GB-southern_english_female-low | OpenSLR 83, CC BY-SA 4.0 | ryan (CC BY-NC-SA 4.0) | ALLOWED WITH CONDITIONS | Attribution; share-alike; academic, non-commercial use only (ProcSense is a final-year project, decision of 18 Oct 2026) | 2026-10-16 (R-026) |
| en_GB-semaine-medium | DFKI SEMAINE data, CC BY-NC-SA 4.0 | — | ALLOWED WITH CONDITIONS | Attribution; share-alike; academic, non-commercial use only (ProcSense is a final-year project, decision of 18 Oct 2026) | 2026-10-16 (R-026) |
| en_GB-alan-medium | Mimic 3 voice `apope` ("see URL", not read) | lessac (research only) | VERIFY | The recordings' licence was not read: read it before using the voice | 2026-10-16 (R-026) |

## Libraries and runtimes

| ID | Item | Licence | Decision | Conditions and notes | Checked |
|---|---|---|---|---|---|
| EXT-OLLAMA | Ollama | MIT | ALLOWED | Local runtime; keep the licence notice if it is shipped | 2026-10-16 (R-024) |
| EXT-QWEN3 | Qwen3 model weights | Apache-2.0 | ALLOWED WITH CONDITIONS | As MC-01 | 2026-10-16 (R-025) |
| EXT-FWHISPER | faster-whisper | MIT | ALLOWED | — | 2026-10-16 (R-024) |
| EXT-FFMPEG | ffmpeg (Gyan.dev "full" Windows build) | LGPL 2.1 or later; this build is GPLv3 | ALLOWED WITH CONDITIONS | Developer tool only: shapes and encodes rendered voice lines; never linked into or shipped with the app. If it is ever bundled, use an LGPL build and offer the source | 2026-10-17 (R-037) |
| EXT-PIPER | Piper | MIT (archived rhasspy/piper) or GPL-3.0 (successor OHF-Voice/piper1-gpl) | ALLOWED WITH CONDITIONS | Prefer the archived MIT release or pre-rendered audio; if the GPL successor is used, keep it a separate program, not linked into the build; each voice under Voices | 2026-10-16 (R-024) |
| EXT-NEUROKIT2 | NeuroKit2 | MIT | ALLOWED | — | 2026-10-16 (R-024) |
| EXT-DATASKETCH | datasketch | MIT | ALLOWED | — | 2026-10-16 (R-024) |
| — | SDV | Business Source License | REJECTED | Not needed; own copula | 2026-10-12 (R-008; not re-read) |
| EXT-SCIPY | numpy, scipy | BSD-3-Clause | ALLOWED | Keep the licence notices if shipped | 2026-10-16 (R-024) |
| EXT-FASTAPI | FastAPI | MIT | ALLOWED | — | 2026-10-16 (R-024) |
| EXT-UNITY | Unity (Personal) | Unity Terms of Service | ALLOWED WITH CONDITIONS | Unity Personal is for individuals and small organisations with under USD 200,000 of revenue and funds raised in the last 12 months; above that a paid plan is needed. Unity's full terms were not re-read | 2026-10-16 (R-029) |
| EXT-SUPABASE | Supabase (PostgreSQL and Auth) | Apache-2.0 (the open-source code); the hosted service under Supabase's terms and plan limits | ALLOWED WITH CONDITIONS | Free-plan limits and operations in `server/RUNBOOK.md`; the hosted terms were not re-read | 2026-10-16 (R-024, code licence) |
| EXT-CYTOSCAPE | Cytoscape.js | MIT | ALLOWED | Documentation use only | 2026-10-16 (R-024) |
| EXT-MERMAID | Mermaid | MIT | ALLOWED | Documentation use only | 2026-10-16 (R-024) |

The packages the application itself is built with (Python, .NET and Unity packages) are listed with their licences in `docs/research/package_register.csv` and `docs/ATTRIBUTIONS.md` (p0-evaluation-performance-s07).

## Attribution texts

The words to show where an item is used (the credits screen and `docs/ATTRIBUTIONS.md` take them from here and from the dataset register).

| ID | Attribution text |
|---|---|
| DS-0001 | Vital-sign variability derived from VitalDB (Lee H-C et al., Sci Data 2022), PhysioNet release 1.0.0, CC BY 4.0. |
| DS-0002 | ECG strips and templates from PTB-XL 1.0.3 (Wagner P et al., Sci Data 2020), PhysioNet, CC BY 4.0. |
| DS-0003, DS-0004 | Reference ranges checked against the MIMIC-IV Clinical Database Demo 2.2 and MIMIC-IV-ED Demo 2.2, PhysioNet (doi:10.13026/dp1f-ex47 and doi:10.13026/jzz5-vs76), ODbL 1.0; aggregate statistics only. |
| DS-0005 | Gallbladder ultrasound images from the Gallblader Diseases Dataset v2 (Turki A et al., Mendeley Data, doi:10.17632/r6h24d2d3y.2), CC BY 4.0. |
| DS-0006 | Chest X-rays from the NIH Chest X-ray dataset (https://nihcc.app.box.com/v/ChestXray-NIHCC); data provided by the NIH Clinical Center; Wang X et al., ChestX-ray8, CVPR 2017. |
| DS-0007 | Medicine safety wording from openFDA drug labels (CC0 1.0). |
| DS-0014 | CT and MRI slices from AMOS 2022 (Ji Y et al., NeurIPS 2022, doi:10.5281/zenodo.7155725), CC BY 4.0; synthetic findings painted by ProcSense. |
| MC-01, MC-02, MC-04, MC-12 | Qwen3 by the Qwen team, Alibaba Cloud, Apache-2.0. |
| MC-05 | Speech recognition: Whisper (OpenAI, MIT) through faster-whisper (SYSTRAN, MIT). |
| MC-06 | Voice: Piper (Michael Hansen, MIT) with the Cori voice (Bryce Beattie; LibriVox recordings, public domain). |

## Provenance of generated scenarios

Every generated ScenarioPackage records: request, seed, parameter pack versions (each pack cites its profile and dataset ids), block library version, prompt version, model digest, content manifest hash, the ids of every image and strip with its dataset id, and the package SHA-256 (see `docs/architecture/live_generation_spec.md` section 6). A package is therefore traceable to every dataset row in this register.
