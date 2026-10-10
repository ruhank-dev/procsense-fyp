# Data first: revised task order and dependencies

**Decided 10 October 2026 by the project owner** (see [ADR-0002](../../decisions/ADR-0002-data-first-order.md)): all data work is done **before** the remaining engine and VR work.
Why: nearly every remaining engine step reads content that does not exist yet (NEWS2 bands, lab ranges, decision tables, flow maps, cases, images), so the engine would be written against placeholders.

Nothing here is marked complete until it is **verified** (checks pass) **and approved by the owner**. "Built" means the files and checks exist; it does not mean approved.

## The order

Stages are in build order. Every stage ends with its checks green and its register rows in place.

| Stage | Plan step | What it produces | Needs | Unlocks later (engine / VR) | Status (10 Oct 2026) |
|---|---|---|---|---|---|
| D0 | [medical-research-data s02](p0-medical-research-data/steps/p0-medical-research-data-s02-extractor-traceability-ci.md) | Walkthrough extractor, traceability report, CSV reference check | s01 ✅ | everything below | Built, waiting for approval |
| D1 | [s03 vitals, NEWS2, sepsis tables](p0-medical-research-data/steps/p0-medical-research-data-s03-vitals-news2-sepsis-tables.md) | Vital-sign bands, NEWS2 scoring table, sepsis tiers | D0 | clinical-logic s03, s04; physiology s01 to s06; workflow s13 | Not started |
| D2 | [s04 laboratory tables](p0-medical-research-data/steps/p0-medical-research-data-s04-laboratory-tables.md) | Lab bands, critical results | D0 | clinical-logic s06 to s08 | Not started |
| D3 | [s05 decision tables](p0-medical-research-data/steps/p0-medical-research-data-s05-decision-tables.md) | Five outcomes, end states, fasting rules, key rules, priority ladder, clash tables | D0 | clinical-logic s11 to s15; workflow s10, s13, s17 | Not started |
| D4 | [s07 scores and calculators](p0-medical-research-data/steps/p0-medical-research-data-s07-scores-calculators-config.md) | Score thresholds and weights (appendicitis scores, TG18, ASGE, ASA, STOP-BANG, VTE) | D0 | clinical-logic s05 | Not started |
| D5 | [s06 medicines as plans](p0-medical-research-data/steps/p0-medical-research-data-s06-medicines-plans-dose-policy.md) | Medicine groups, plans, clash table, dose policy (no trainee doses) | D0 | clinical-logic s10 to s12 | Not started |
| D6 | [s08 WHO checklist, steps, flow maps](p0-medical-research-data/steps/p0-medical-research-data-s08-who-checklist-steps-player-view.md) | WHO checklist, step answers, the two 20-stage flow maps, player view, VR cards, glossary | D0 | workflow s06, s14, s16; UI | Not started |
| D7 | [s13 what-if library](p0-medical-research-data/steps/p0-medical-research-data-s13-what-if-library.md) | The Part 14 what-ifs as data | D1 to D5 | physiology s08, s09; case-library s03 | Not started |
| D8 | [s09 equipment reference sheets](p0-medical-research-data/steps/p0-medical-research-data-s09-equipment-reference-sheets.md) | Real-world equipment facts for the Blender scripts | D6 | blender-equipment | Built, awaiting your approval (not complete): 53 sheets, 104 of 121 dimension rows sourced, 17 open (no source found) |
| D9 | [s10](p0-medical-research-data/steps/p0-medical-research-data-s10-s1-p0-cases.md), [s11](p0-medical-research-data/steps/p0-medical-research-data-s11-s2-p0-cases.md), [s12](p0-medical-research-data/steps/p0-medical-research-data-s12-stacked-p0-cases.md) P0 cases | The 17 P0 cases as data | D1 to D7, and a case format that can hold labs, imaging, medicines and timelines (p0-case-library s01) | case-library, scenarios | Built, awaiting your approval (not complete): 17 case files in content/case_files (own schema; the Core loader does not read them yet), 16 claims verified (clinical_verification.md) |
| D10 | [s15 vitals and ECG datasets](p0-medical-research-data/steps/p0-medical-research-data-s15-datasets-vitals-ecg.md) | Verified dataset rows, noise parameters, ECG templates, dataset cards | D0 | physiology s02, s06; monitor | Built, awaiting approval (not complete): VitalDB, PTB-XL, MIMIC demo extracted; tables verified; clinician plot review pending |
| D11 | [s16 images](p0-medical-research-data/steps/p0-medical-research-data-s16-datasets-images.md) and the [ultrasound feature](p0-medical-research-data/ultrasound-images-and-reports.md) | Ultrasound (and other) images with matching reports: real images first, labelled synthetic images only where none exist, clinician review before use | D9 (the findings each case needs) | clinical-logic s09; EMR; diagnostic transitions | Built, awaiting your approval of the stage (not complete): 204 picture/report pairs over 11 picture tests plus 3 written-only tests (60 results, at least 4 pictures each) with clean and problem results, mapped to every case, scenario card and what-if; picture approvals recorded on the owner's statement |
| D12 | [s14 EMR history](p0-medical-research-data/steps/p0-medical-research-data-s14-emr-historical-records.md) and synthetic text data | Previous records per case; AI-assisted wording sets with dataset cards | D9 | p0-patient-records-emr | Built, awaiting approval (not complete): 4 derived records + 6 distractors; the rest needs clinician-written content |
| D13 | [s17 conflicts and review pack](p0-medical-research-data/steps/p0-medical-research-data-s17-conflicts-review-pack.md), then validation and integration | Review pack for the clinician, content manifest and hash, register and traceability complete, all cards present | D0 to D12 | the engine and VR work resumes | Not started |

## What stays as it was

- **Every earlier decision stays** unless a row in [ADR-0002](../../decisions/ADR-0002-data-first-order.md) or [ADR-0003](../../decisions/ADR-0003-synthetic-medical-images.md) says otherwise. The two that change are the **work order** (data first) and the **no AI-generated images** rule (now: real first, labelled synthetic as a fallback, clinician review). The old wording is kept as history in each place.
- The Unity project, the walking-skeleton work (steps 1 to 14) and everything built in Core so far stay as they are and keep passing.
- Step 15 (Quest replay check) still waits for the headset.
- Engine steps that need no new data are paused, not cancelled: p0-core-engine is finished apart from the checks already done, and the rest resume at D13.

## Rules that apply to every stage

- Every clinical value comes from the walkthrough, with a source-register row; the walkthrough is the master source (conflicts are logged in `docs/research/issues.md`, never resolved silently).
- Drafts from the extractor are re-read against the page; a verbatim check confirms each value appears on the page it cites.
- Doses are never invented or shown to the trainee; local lab ranges and institutional values are the owner-approved walkthrough values.
- Real patient rows never ship; datasets enter the app only as aggregates or approved images.
- AI-assisted data is flagged `synthetic`, has a dataset card, and a clinician approves it before it is used in a session.
- Nothing is renamed with the done mark until the owner approves it.
