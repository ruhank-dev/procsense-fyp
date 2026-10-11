# p0-clinical-logic-s10 — Treatment library, required checks and effects

Milestone: **M4 Engine complete — VAR-S1-A Steps 1–14 headless**

## What
Treatments as content (medicine classes, fluids, oxygen devices, procedures like IV access) with required checks — identity, allergy known, indication, restriction, timing, order, documentation — and effect IDs that physiology applies. No doses unless reviewer-approved display text.

## Why
Step 8 treatments must be safe, checked and visibly effective.

## How to implement
1. `Clinical/Treatments.cs`: `TreatmentDef` from `medicines.csv` + `content/treatments.json` (classes, not doses).
2. `RequiredChecks.cs`: each check is an action or derived state; missing → item (T-TR4).
3. Prerequisites (IV access for IV treatments → reject, T-TR5).
4. Effects referenced by ID in `content/physio/effects.json` (p0-physiology-events-behaviour-s05).
5. Validator rule DOSE_REQUIRES_REVIEWER: any `dose_display` without reviewer → content error (T-TR1).
6. openFDA contraindication/warning text curated for review only (`tools/content/import_labels.py`) — never doses.
7. Each treatment lists its required checks explicitly in content (no implicit checks in code).

## Verification
`dotnet test --filter FullyQualifiedName~Treatments` → T-TR1, T-TR4, T-TR5 green.

## Files changed
`src/ProcSense.Core/Clinical/Treatments.cs`, `RequiredChecks.cs`, `content/treatments.json`, `tools/content/import_labels.py`, tests.

## Dependencies
p0-medical-research-data-s06.

## Common issues
Label data is verbose — curate only needed fields.

## Sources
B-M14 Steps 1–4, T-TR1/4/5; walkthrough Part 8; inventory D24, D234–D267.

## Implementation notes (11 Oct 2026)
- **Content:** `content/treatments.json` is built by `tools/content/build_treatments.py` (CI runs it with `--check`) from the verified `content/medicines.csv` rows. 17 treatments: 15 medicine or fluid groups, oxygen and IV access. Each lists, in the file, its `required_checks`, `requires` (prerequisites), `restricted_by` (conditions, each with a register row), `effects` and `dose_policy`. Nothing is implied in code.
- **Checks:** identity, allergy_known, indication, restriction, timing, order (judged "before") and documentation (judged "after"). Timing is listed only where the medicine row states timing wording (preventive antibiotic, S2 antibiotics, steroid cover, VTE prophylaxis); restriction only where the care column names a condition. Severity is critical for identity, allergy and an applying restriction, major for indication, timing and order, minor for documentation (design values, register row SR-4102).
- **Code:** `Clinical/Treatments.cs` (`TreatmentTables.Parse`, strict like the other tables) and `Clinical/RequiredChecks.cs` (`RequiredChecks.Evaluate(tables, treatment, context, phase)` returns a `CheckReport`: `Allowed` is false only when a prerequisite is missing, with the reject reason such as `IV_ACCESS_REQUIRED`; every failed check is a `CheckItem` in check-list order). `ContentPackage.Treatments` carries the library.
- **Dose policy:** a `dose_display` needs a source and a reviewer or the file is a content error (`DOSE_REQUIRES_REVIEWER`, T-TR1); only EDUCATIONAL_EXAMPLE rows carry one, copied from the medicine row with its source. The Python validator (`validate.py`, `test_content_tools.py` class `TreatmentChecks`) enforces the same rule from the file.
- **Tests:** `TreatmentsTests` (30): T-TR1, T-TR4 (a missing timing check is an item), T-TR5 (an IV treatment without IV access is refused), the severity and order of checks, restrictions, and a loader that refuses unknown checks, effects, prerequisites and registers.
- **Differences from the plan text:** (1) the effect ids are listed in `treatments.json` because `content/physio/effects.json` does not exist yet (physiology s05); that step defines what each effect does. (2) `tools/content/import_labels.py` was not written: `tools/data/extract_openfda.py` already produces `content/drug_label_safety.csv` (wording only, no doses), and each treatment points to its row in `label_row`. (3) "Item logged" (T-TR4) and giving the treatment (T-TR2) need the workflow, so they arrive with s11; this step returns the items. T-TR3 (an allergen given: critical item and the hypersensitivity event) needs the allergy-to-class mapping of s12.
- **Register:** rows SR-4100 to SR-4106 (the block SR-3900 to SR-4039 belongs to the EMR records).

