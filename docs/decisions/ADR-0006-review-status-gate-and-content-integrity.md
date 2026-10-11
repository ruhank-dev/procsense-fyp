# ADR-0006: The review-status gate and the content manifest check run where a session is started

**Status:** Accepted 12 October 2026 (for the owner's approval; implements DT-055) · **Related:** ADR-0001, INSTRUCTIONS section 8i

## Context
Cases carry a review status (DRAFT, CLINICAL_REVIEW, APPROVED_FOR_TRAINING, APPROVED_FOR_ASSESSMENT). Three cases were retyped after the owner's attestation and are DRAFT until a clinician answers Q1. Nothing stopped a draft being played, and edited content could be played unnoticed.

## Decision
`ContentGate` refuses a case below the mode's minimum (practice needs APPROVED_FOR_TRAINING, assessment needs APPROVED_FOR_ASSESSMENT, a DRAFT is never played) and `ContentManifestCheck` verifies every text file against `content/manifest.json`. Both are applied at the **host entry points** (the command-line host now; the Unity host must call the same two functions), not inside the engine, so tests can still build a session from any case. `--allow-draft` and `--skip-integrity` are developer overrides that print a warning. A CI step proves the command line refuses an assessment of a training-only case.

## Consequences
Content that is not approved cannot be played by accident, and a tampered file is named. Because the manifest is checked on every run and replay, regenerating it after any content change is part of the workflow (CI checks it is current).
