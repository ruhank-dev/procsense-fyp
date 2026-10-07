# p0-evaluation-performance-s06 — Clinical realism review (walkthrough Part I, EXP-EVAL-2)

Milestone: **M9 P0 release**

## What
The medical professional plays or watches all P0 cases and completes the walkthrough Part I checklist A–G (medical facts, numbers, surgery, anaesthesia, consent, local policy, VR realism); outcomes recorded; fixes planned.

## Why
The walkthrough itself asks for clinician validation (Part I); the realism review is the clinical-validity evidence for the FYP and catches errors the team can't see.

## How to implement
1. Review pack from the checklist + videos; session; import decisions.
2. Pack: walkthrough Part I checklist A–G as rows (Item / OK / Correction / Comment) + case videos + debrief samples + open issues list; ≤ 45 min per session, split by section if longer.
3. Session: reviewer marks each item; corrections become issues with owners.
4. `tools/content/import_decisions.py` updates register/issues; content fixes re-validated; affected golden runs re-run.

## Verification
Checklist A–G completed in `results/clinical-review.md`; every correction has an issue ID and status.

## Files changed
`docs/research/review-packs/<date>-realism.md`, `docs/plans/p0/p0-evaluation-performance/results/clinical-review.md`, updated `docs/research/issues.md` + register.

## Dependencies
M8.

## Common issues
Reviewer time is the bottleneck — prioritise items affecting P0 cases and split packs above 45 minutes.

## Sources
Walkthrough Part I (I.3 checklist A–G), Part H; Implementation Guide B-M17 Steps 3–5; inventory D85.
