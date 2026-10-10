# ADR-0003 — Synthetic medical images are allowed as a labelled fallback (supersedes "never AI-generated medical images")

**Date:** 10 October 2026 · **Status:** Accepted (project owner's instruction, 10 October 2026)

## Context
Until now the plans said "never AI-generated medical images" (medical-research-data README, step s16, p0-patient-records-emr README, clinical-logic s09),
and the Data Source Inventory (D41) said "DO NOT generate medical images with AI; text report only". The alternatives table in the research README
listed "AI images" as rejected because "never generate medical images".
The owner now wants ultrasound images shown next to matching reports, and says: find online datasets first, otherwise generate synthetic images and
matching reports for clinician review.

## Decision
Order of preference for every image the EMR viewer shows:
1. **A real, licensed image** from an open dataset, selected to match the case's report, approved by the clinician.
2. **A synthetic image**, only when no suitable real image exists for that finding, always labelled as synthetic, with its generator and method recorded, and
   **not usable in a session until the clinician approves it**.
3. **A text report only**, if neither exists or neither is approved.

The matching report is always generated from the same finding code as the image, so they cannot disagree.

## Safeguards (all required)
- Every synthetic image carries a visible mark ("SIMULATION: synthetic image, not a real scan") inside the picture, plus `synthetic: true`, generator name, version and seed in `content/images/index.json`.
- A synthetic image is never presented as a real patient's scan; the viewer shows the mark; the debrief and the dataset card say what it is.
- Status per image: `DRAFT` (generated), `REVIEWED`, `APPROVED_FOR_TRAINING`; assessment mode accepts only `APPROVED_FOR_ASSESSMENT` content (DT-055).
- Real third-party images are used only if the licence allows it, with attribution; embedded metadata is stripped; no real patient identifiers ever ship.
- Synthetic images are teaching illustrations. They are not used to train or evaluate any diagnostic model.
- Doses and local lab ranges remain "never AI-generated" (unchanged).

## Other kinds of picture (10 October 2026, same rules)
The same order applies to every picture a doctor looks at. What the walkthrough needs today: **ECG** (what-ifs and bedside tests) and **ultrasound** (Part 6). Chest X-ray appears only as a test that should *not* be ordered (Example Patient A); CT, MRI and MRCP appear only as choices the senior makes, never as a result shown to the trainee.
- **ECG: done with real data.** PTB-XL (CC BY 4.0) gives real recordings with human-validated rhythm labels; `tools/data/ecg_images.py` draws six strips with matching reports (rate measured from the signal). No synthetic image is needed.
- **Chest X-ray: real, from NIH ChestX-ray14.** The Indiana University/Open-i collection was rejected (CC BY-NC-ND: no derivatives, non-commercial; DS-0013). NIH ChestX-ray14 states no restrictions on use with attribution (read in secondary documentation; confirm on the primary page before release). Only one 450 MB shard of the copy is needed. Ten pictures for five finding codes are built; the dataset has labels but no reports, so each report states only what the label supports. See `tools/data/cxr_images.py`.
- **CT, MRI, MRCP: built (10 October 2026, later).** No open set with reports exists, so the library uses real AMOS 2022 slices (CC BY 4.0) as the clean pictures and as the base the findings are painted into (flagged synthetic, marked, base slice named); MRCP is drawn in code. See `docs/research/imaging_scenarios.md` and ADR-0004.
- **Owner policy:** search online first; when nothing suitable is found, generate a labelled fake that looks as realistic as possible; do not stop for it.
- **Approval:** the owner stated that the clinician approves everything. Approvals are recorded in `content/image_approvals.json`, one per picture, tied to the SHA-256 of the exact file and report text, by "Owner (Ruhan Kamran), who states that the clinician approves everything; the clinician is not named", level APPROVED_FOR_TRAINING. An edited picture or report loses its approval automatically. APPROVED_FOR_ASSESSMENT is not granted; reconfirm with the named clinician before assessment use.

## What this does not change
Report wording for categories defined in the walkthrough (Part 6.3 and 6.4) stays the master; AI only varies wording, never the finding.

## Consequences
- The old rule is kept in each document as history, with a pointer here.
- The step s16 acceptance changes from "every image has a licence row" to "every image has a register row, a licence or generator record, and an approval state".
- A clinician must review every synthetic image and report pair; until then they are `DRAFT` and the viewer falls back to the text report.

## Alternatives considered
Keep "report only" for missing images (the earlier choice, lost because the owner wants the images). Generate with no label (rejected: unsafe and misleading).
