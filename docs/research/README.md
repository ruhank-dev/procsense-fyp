# Medical research — proof of truth

ProcSense is a teaching simulator, not a medical tool, but every clinical number and rule it shows must be
traceable and honest. Nothing clinical enters `content/` or code without a row here.

## Files

| File | What it holds |
|---|---|
| `source_register.csv` | One row per clinical value/rule: where it comes from and how it was verified |
| `dataset_register.csv` | One row per external or synthetic dataset: licence, contents, verification |
| `issues.md` | Conflicts between sources (or source vs walkthrough) waiting for a clinician/supervisor decision |

## Approval status (owner decision, 09 October 2026)

Existing research and clinical workflows are treated as **clinician-approved**; the `clinician_approval` column therefore reads "Approved … per project owner/user" for existing rows. This is the owner's statement, not an independent review.
On the same day the owner confirmed the statement also closes the 15 institutional decisions and the source conflicts ISS-0001 / ISS-0002 (walkthrough values used). New clinical values still need a register row.

## Proof-of-truth protocol (every clinical value)

1. **Find it in the walkthrough** (`docs/context/ProcSense_Medical_Walkthrough_v5_1.md`) — record part/section/page.
2. **Find the original source** the walkthrough cites (guideline, paper). Open it. Record the exact location
   (section, table, recommendation number) and the date checked.
3. **Classify** the evidence type:
   - **A** source-backed · **B** clinical interpretation · **C** institutional (hospital decides) · **D** simulator design value.
4. **Set the status**: `VERIFIED` (source opened, value matches) · `UNVERIFIED` (cited but not opened/confirmed) ·
   `CONFLICT` (sources disagree → add to `issues.md`) · `UNRESOLVED` (no source exists).
5. **Clinician approval** is a separate column — research prepares, the supervisor/clinician signs off.

**Never** invent a value, citation or URL. A link nobody opened is `UNVERIFIED`, not a source.

## Dataset protocol

Before recommending a dataset: confirm it exists at the URL, read its licence and access terms, check what it
really contains (rows, columns, population), and note any restriction (registration, credentialed access,
non-commercial, share-alike). Never ship real patient rows in the app. Synthetic datasets record their
generator, the source of every value range, and are flagged `synthetic=true`.
