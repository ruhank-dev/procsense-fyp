# ADR-0006: The review-status gate and the content manifest check run where a session is started

**Status:** Accepted 12 October 2026 (for the owner's approval; implements DT-055) · **Related:** ADR-0001, INSTRUCTIONS section 8i

## Context
Cases carry a review status (DRAFT, CLINICAL_REVIEW, APPROVED_FOR_TRAINING, APPROVED_FOR_ASSESSMENT). Three cases were retyped after the owner's attestation and are DRAFT until a clinician answers Q1. Nothing stopped a draft being played, and edited content could be played unnoticed.

## Decision
`ContentGate` refuses a case below the mode's minimum (practice needs APPROVED_FOR_TRAINING, assessment needs APPROVED_FOR_ASSESSMENT, a DRAFT is never played) and `ContentManifestCheck` verifies every text file against `content/manifest.json`. Both are applied at the **host entry points** (the command-line host now; the Unity host must call the same two functions), not inside the engine, so tests can still build a session from any case. `--allow-draft` and `--skip-integrity` are developer overrides that print a warning. A CI step proves the command line refuses an assessment of a training-only case.

## Consequences
Content that is not approved cannot be played by accident, and a tampered file is named. Because the manifest is checked on every run and replay, regenerating it after any content change is part of the workflow (CI checks it is current).

## Amendment, 17 October 2026 (p1-live-scenario-generation-s10): a fifth status for generated cases
The generator composes practice cases from an approved case and story-neutral blocks (decision 17 Oct 2026). Such a case is not reviewed by a person and must not look as if it were, so the status set is **extended, not loosened**: `GENERATED_PRACTICE_ONLY`. `ContentGate` lets it into practice and never into assessment; the case-file loader accepts it only with `created_by: composer` and a `composition.spine`, and refuses a composed case at any APPROVED status (a person's approval is a different act on a different file). The four existing statuses behave as before. A scenario package whose status, or whose case's status, is anything else is refused at load. The tables keyed by case (problem discovery, imaging report, infection, deterioration, end-state surfacing) resolve through the spine (`CaseFile.TableKey`), so a composed patient is found and treated by the same rules as the story it keeps.
