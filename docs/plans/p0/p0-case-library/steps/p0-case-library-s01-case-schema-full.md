# p0-case-library-s01 — Full content schemas and loader modes

Milestone: **M5 Content complete — all 17 P0 cases as data**

## What
Extend the skeleton case schema to everything P0/P1 cases need — hidden facts with `disclose_on`, exam findings, results {test, value, unit, flag, comment, quality, previous}, imaging reports, problems {type, ladder_rank}, what-if bindings, expected outcomes per decision point, expected end state, debrief block, variant `extends`, stacked-case sections — plus the common header on every content item (`evidence_type`, `number_kind`, `wt_ref`, `review_status`) and loader modes (practice ≥ APPROVED_FOR_TRAINING, assessment = APPROVED_FOR_ASSESSMENT).

## Why
All 33 walkthrough cases must fit one schema; assessment mode must never load unreviewed content.

## How to implement
1. Update `content/schema/case.schema.json`; shared `common.schema.json` for the header (B-M11 Step 6).
2. Validator: referential integrity (every referenced ID exists — T-CL-1), clear path in errors (T-CL-4), status vs mode check.
3. Packager `tools/content/package.py` → `content_version` + sha256 manifest; loader refuses hash mismatch (T-CL-5) and below-mode status (T-CL-2).
4. Keep VAR-S1-A valid (backward compatible); add a fixture per new field.
5. YAGNI: add a field only when a real case needs it.

## Verification
`task content-validate` green; T-CL-1, T-CL-2, T-CL-4, T-CL-5 pass.

## Files changed
`content/schema/*.schema.json`, `tools/content/validate.py`, `tools/content/package.py`, Core loader, tests.

## Dependencies
p0-core-engine-s03; p0-medical-research-data-s02.

## Common issues
Android can't read StreamingAssets with File IO — Unity loader uses UnityWebRequest (handled in p0-vr-platform-s02).

## Sources
Implementation Guide B-M11 Steps 1–6, T-CL-1–T-CL-5; B-M19 interface.
