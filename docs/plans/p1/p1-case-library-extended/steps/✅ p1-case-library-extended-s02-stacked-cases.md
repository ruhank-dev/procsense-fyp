# p1-case-library-extended-s02 — Transcribe the 6 remaining stacked cases

Milestone: **M11 P1 content — theatre extension, extended cases, composer, Pulse**

## What
STK-S1-A, -B, -C and STK-S2-A, -C, -D with all 20 Part D sections (e.g. STK-S1-A: hypoglycaemia P1, SGLT2 taken today P2, HbA1c 76 P3, BP 172/100 P4 per guide example).

## Why
The six remaining stacked cases complete the walkthrough's full playable stacked-case set (Part D) and give advanced trainees prioritisation practice beyond the two P0 stacked cases.

## How to implement
1. Same as P0 stacked cases; ranks verified against Part D.
2. For each case: Part D §4 'immediately available' vs 'only revealed if asked'; §8 priorities; §9 decisions as checkpoints; §17 incorrect paths as branches; §20 debrief.
3. Verbatim check + register rows.

## Verification
Validator green; ladder ranks match Part D (T-DE1-style test per case).

## Files changed
`content/cases/STK-S1-A.json`, `STK-S1-B.json`, `STK-S1-C.json`, `STK-S2-A.json`, `STK-S2-C.json`, `STK-S2-D.json`, register rows, `tests/golden/STK-*`.

## Dependencies
p1-case-library-extended-s01.

## Common issues
Part D reuses variant letters (WT-I01) — namespaced STK- IDs avoid collisions with VAR- cases.

## Sources
Walkthrough Part D; inventory D475–D477, D479, D481–D482; Implementation Guide B-M19 Step 2, B-R17 worked example.

## Notes (17 Oct 2026)
- **What was there:** the six case files were written by Ruhan Kamran on 10 Oct 2026 (`tools/content/cases/p1stk.py`); five were APPROVED_FOR_TRAINING and STK-S1-A was already waiting for sign-off (12 Oct evidence review). Compared with Part D section by section they were shorter and in places different: 9 of the book's decision steps (section 9) were missing, STK-S2-D's problems did not follow the priority table (section 8), STK-S2-C had one priority fewer, and several values differed from the book's tables (sections 3, 4 and 12), for example STK-S1-A ketones 0.4 for the book's 0.9, STK-S1-C creatinine 168 for 178, STK-S1-B creatinine 98 (normal) for 118 (CKD G3a), STK-S2-A lactate 2.4 (high) for 1.8 (normal), STK-S2-C a chest X-ray with shadowing where the book says the chest is clear. Abdul Moiz (owner) chose to complete them on 17 Oct 2026.
- **Built (branch myt1x54/p1-case-library-extended-s02):** the six cases re-transcribed from Part D in `tools/content/cases/p1stk.py`: observations and history from sections 3 and 4 (the book's words where it gives them, marked `book`), problems in the order of section 8 (types from the 12 Oct evidence review kept; STK-S2-C gains "opioid sedation", STK-S2-D's four problems are the table's four rows), one decision per decision of section 9 with the wrong options taken from the incorrect paths of section 17, results from section 12, the correct, incorrect, missed and recovery paths from sections 16 to 19 and the debrief from section 20 (a new `partd()` helper sets the debrief in the book's words). No dose is copied. `tools/content/stacked_partd.py` (in CI with `--check`, writes `docs/research/stacked_partd.md`) reads each case's sections from the walkthrough and checks the section 8 order against the problem ranks, the number and the noticed values of the section 9 decisions, and the section 12 results, for all eight stacked cases (the two P0 ones pass unchanged); `tools/ci/test_stacked_partd.py` (5 tests). Run against the case files as they were, the check reports 32 differences; now none.
- **Approval:** a changed case loses its approval (the approval is tied to the file's hash), so the six are DRAFT and listed with what changed and the question in `content/case_signoff_pending.json` (`CaseFileTests` pins that list); practice and assessment refuse them until the owner signs them off (`build_case_files.py --grant`).
- **Decisions that are the project team's, not the book's:** the problem types of the 12 Oct 2026 evidence review stay as they are, only the order and the rows follow section 8; a book row that names two problems is kept as two (STK-S2-A: kidney injury and low potassium); STK-S2-D follows section 8 although its own debrief disagrees (DT-185); STK-S2-C's venous CO2 is not transcribed because no blood-gas test exists yet (DT-186); a wrong option the book does not give (four decisions) is written plainly and marked by its consequence only.
- **Approved by Abdul Moiz (owner), 17 Oct 2026**, with the decisions above; the six cases were signed off with `build_case_files.py --grant-cases` (new: approves only the named cases, records who and when, and takes them off the sign-off list; `--grant` would have re-approved all 53 under the 10 Oct attestation), so they are APPROVED_FOR_TRAINING again and 51 of the 53 cases are approved (SC-4 and VAR-S1-B still wait for the clinician). STK-S2-D's top priority stays open for the clinician (DT-185).
- **Not done here:** golden runs of the six cases and VR play (p1-case-library-extended-s04: the step's file list names `tests/golden/STK-*`, but a golden run needs the engine additions of s03 first: the P1 cases have no discovery rules yet); the blood-gas test (DT-186).
