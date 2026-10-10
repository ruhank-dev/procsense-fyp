# ADR-0002 — All data work comes before the remaining engine and VR work

**Date:** 10 October 2026 · **Status:** Accepted (project owner's instruction, 10 October 2026)

## Context
The plan ran the engine track and the content track in parallel. Building the engine showed that most remaining steps
read content that does not exist yet: NEWS2 and vital bands (clinical-logic s03, s04), lab ranges (s06 to s08), decision tables
(s11 to s15), flow maps (workflow s06), the WHO checklist (workflow s16), cases, images. Only `p0-medical-research-data` s01 was done,
and `dataset_register.csv` was empty. Writing engine code against missing data means rewriting it later.

## Decision
Finish **all data work first**, in the order in [data-first-order.md](../plans/p0/data-first-order.md): the walkthrough extraction and traceability (s02),
then the clinical tables, decision rules, flow maps and cases, then the external-dataset research and acquisition, the synthetic data, the validation
and the review pack. Only then resume the remaining engine steps and the VR integration.

## What stays the same
- Every earlier decision stays (see the table in the order document). Source-of-truth rules, the proof-of-truth protocol, the owner's approval statement of 09 October 2026 and "no deadlines" are unchanged.
- Nothing is marked complete until it is verified and the owner approves it.
- The Unity project and walking-skeleton steps 1 to 14 stay as built; step 15 still waits for the headset.

## Consequences
- The engine steps that need data pause at their current state; they resume at D13 with real content to read.
- Several engine steps become smaller, because their data and tests arrive first.
- The case format must be able to hold labs, imaging, medicines and timelines before the 17 P0 cases can be written as data (p0-case-library s01 defines it); that is a dependency of D9.
- A review pack for the clinician (D13) is now a gate before any new clinical content is treated as approved for assessment use.
