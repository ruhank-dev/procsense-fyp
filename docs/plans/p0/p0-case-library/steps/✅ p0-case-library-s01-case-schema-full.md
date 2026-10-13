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

## Implementation notes (13 Oct 2026)
- **What was already there (built in earlier steps by the owner):** the full playable case file and its schema (`content/schema/case_file.schema.json`: hidden facts with `disclose_on`, examination findings, results with value, unit, flag, comment, quality and previous value, problems with type and rank, decisions with expected outcomes, expected end state, debrief block, variants and stacked cases, 53 case files), the strict C# loaders with the path in every error, the review-status gate (`ContentGate`, used by the command line) and the content manifest with its integrity check (`content/manifest.json`, `ContentManifestCheck`). This step checked each item of the plan against them and built what was missing.
- **Shared schema:** `content/schema/common.schema.json` holds the lists the schemas share (review status including CLINICAL_REVIEW, evidence type A to D, number kind, register reference); the case-file schema points at it for its status. `checks.schema_registry()` lets one schema point at another.
- **Packager:** `tools/content/package.py --mode dev|practice|assessment --out <folder>` copies the content for one mode and writes a manifest with the same `entries` as `content/manifest.json` plus `mode`, a `content_version` (12 characters of the content hash and the mode: the same files always give the same version) and the case files left out with the reason. Practice leaves out DRAFT and CLINICAL_REVIEW case files; assessment leaves out everything that is not APPROVED_FOR_ASSESSMENT (today that is all 53, because none has had the final approval). `--check` builds into a temporary folder and verifies it; CI runs it for all three modes (`task content-package`).
- **Loader by mode:** `ContentMode.Load(reader, mode, checkManifest, caseIds)` first checks every content file against the manifest and refuses with `CONTENT_HASH_MISMATCH` naming the files (T-CL-5), then loads each requested case, or lists it as excluded with the reason when its status is below the mode (T-CL-2). A card with no case file (an engine test fixture) is not gated. `validate.py --mode <mode>` says how many case files that mode may play.
- **Tests:** Core `ContentModeTests` (7: T-CL-2, T-CL-4, T-CL-5), Python `ContentLibraryChecks` (8: T-CL-1, T-CL-4, T-CL-5, the packages per mode). T-CL-3 (load on Quest) is for the headset (p0-vr-platform-s02).
- **Left as it was, and why:** the plan asks for `evidence_type`, `number_kind`, `wt_ref` and `review_status` on every content item. Those already live in the source register (evidence type, walkthrough reference and verification state per value) and each content file points at its rows with `refs`, which CI checks; copying them into every file would duplicate the register. What is missing is the number kind, which the register does not record (DT-119).
- **Not in this step:** the command line and Unity still gate by themselves (`ContentGate`); moving them to `ContentMode.Load` is DT-120.

