# p1-dataset-analysis-s01 — Dataset inventory and licence refresh

Milestone: **M14 P1 live scenario generation — dataset analysis and fresh cases**

> **Planning only (12 October 2026).** Written in the planning update; do not implement until the owner says so. Spec: `docs/architecture/live_generation_spec.md`; canonical graph: `docs/architecture/procsense_full_pipeline.json`.

## What
Refresh the dataset register (DS-0001 to DS-0014) and write the licence and provenance register: for every dataset, model, library and voice the licence, what it allows, attribution text, what is committed (raw, derived, none) and the access date.

## Why
Generators must only draw on material whose licence allows the use; the register makes that checkable instead of a blanket block.

## How to implement
1. Re-read each licence page; record URL and access date in `docs/research/research_log.md`.
2. Mark each item ALLOWED, ALLOWED WITH CONDITIONS (attribution, share-alike), NOT USED or REJECTED with the reason.
3. Add the model and library rows (MC-01 to MC-13, EXT-*) from the pipeline JSON.

## Verification
Every DS, MC and EXT id in the pipeline JSON has a register row; `tools/data/dataset_cards.py --check` passes.

## Files changed
`docs/research/licence_provenance_register.md`, `docs/research/dataset_register.csv`

## Dependencies
p0-medical-research-data (existing registers)

## Common issues
Licence pages change; always record the access date.

## Sources
Pipeline JSON `datasets`, `external_dependencies`.

## Notes (16 Oct 2026)
- **Go-ahead:** Abdul Moiz, the project owner (ownership is with Ruhan Kamran for a while), told the team to implement this phase on 16 Oct 2026, lifting the "planning only" note for this step.
- **Built (branch myt1x54/p1-dataset-analysis-s01):** every licence page re-read and logged with its URL and date (R-013 to R-029 in `docs/research/research_log.md`); `docs/research/licence_provenance_register.md` refreshed: every dataset, model and library row dated 16 Oct 2026 (Open-i and SDV say they were not re-read), rows added for MC-08 to MC-11, EXT-QWEN3, EXT-UNITY and EXT-SUPABASE, a new **Voices** table (the nine British Piper voices) and an **Attribution texts** table; `docs/research/dataset_register.csv` (through `tools/data/dataset_cards.py`) with the new licences and a `licence_checked` column; the pipeline JSON's licence fields of DS-0001, DS-0008, DS-0009, DS-0010 and EXT-PIPER, and EXT-SUPABASE's status (it is in use); `tools/data/licence_register_check.py` (in CI) and `tools/ci/test_licence_register.py` (6 tests). Run against the register as it was, the check reports the 7 missing ids and the 4 rows with no date.
- **What the refresh found:**
  - **VitalDB (DS-0001):** the vitaldb.net open dataset and API, which our extract read, are CC BY-NC-SA 4.0 under a data use agreement for research and development use, not CC BY 4.0 as the register said; the same data is on PhysioNet under CC BY 4.0. Decision: allowed through the PhysioNet release; the aggregates are to be re-derived from it (DT-183).
  - **Regensburg (DS-0008):** CC BY-NC 4.0, so rejected (and children only).
  - **PriMock57 (DS-0009):** CC BY 4.0; still not needed.
  - **Common Voice (DS-0010):** since October 2025 only on Mozilla Data Collective, whose dataset page could not be read; still not used.
  - **Piper voices:** only en_GB-cori-high (public-domain LibriVox, trained from scratch) is allowed; en_GB-semaine-medium is rejected (non-commercial recordings); the other seven were fine-tuned from a US voice trained on research-only or non-commercial data, so they wait for a qualified person's answer (DT-184).
  - Ollama, numpy, scipy, FastAPI and Mermaid, recorded from general knowledge on 12 Oct, are now read from their licence files (all as recorded).
- **Decisions that are the project team's, not the book's:** ProcSense is treated as a product that is sold, so a non-commercial or no-derivatives licence is a conflict; a fine-tuned voice counts as carrying its base voice's data terms until a qualified person says otherwise; a VERIFY row needs no date, every other decision does.
- **Not done:** re-deriving the VitalDB aggregates (DT-183); the legal question about fine-tuned voices (DT-184); the terms of Llama 3.2, Unity in full and Supabase's hosted service were not read (the register says so).
