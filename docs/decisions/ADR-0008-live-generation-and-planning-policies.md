# ADR-0008: Dataset-driven live scenario generation, with deterministic control and no clinician sign-off gate

**Status:** Accepted 12 October 2026, amended the same day (second planning pass; see Amendment) (owner's planning instruction; planning only, no implementation yet) · **Related:** ADR-0002, ADR-0003, ADR-0004, ADR-0005, ADR-0006, `docs/architecture/`

## Context
Trainees will meet the same 53 cases again and again. The owner wants every practice session to be able to start on a **fresh** scenario that is still realistic and safe. The repository already holds the pieces a generator needs (Stage B validator and mutations, band sampling, derived dataset aggregates, the imaging library, the Elo selector, EventDirector, the grounding validator) but no generator. Earlier plans also required a clinician to approve every composed case before use, and treated an unclear licence as a reason to stop.

## Decision
1. **Live generation is a 21-stage pipeline** (LG-01 to LG-21, `docs/architecture/live_generation_spec.md`). Existing cases and datasets are templates, evidence and evaluation material; a fresh scenario is a coherent, evidence-informed combination the trainee has not met, measured by a structured fingerprint. Randomness is not realism.
2. **Datasets first.** Each modality is profiled (normal, abnormal, borderline, missing, artefact, correlation) before its generator is designed (`p1-dataset-analysis`).
3. **Offline preparation is separate from live generation.** Profiles, parameter packs, indices, prompts and models are prepared offline; a session only reads them and never retrains, refits or downloads.
4. **Method per data type, LLM-first but not LLM-only.** A served LLM is considered first for every responsibility and used where language is the product (narrative, persona, dialogue, debrief wording, judging coherence). Numbers come from seeded statistical draws on profiled packs; images and ECG strips from a tagged, licensed library with parametric ECG variation; selection, validation, novelty and scoring are deterministic. Pretrained models are preferred; training is limited to the P2 classifier, and only if it beats the P1 LLM classifier. Image-generating models and LLM fine-tuning are rejected for now (E64, E65).
5. **Rules decide** (ADR-0005 unchanged): state changes, critical scoring and action acceptance are deterministic. A complete valid case can be generated with no model (LG-06 falls back to a template narrative).
6. **Practice only.** Generated packages carry review status GENERATED_PRACTICE_ONLY, an extension of the ADR-0006 gate that never loosens it: they are never used in assessment.
7. **No additional clinician sign-off as a development gate.** Automated validation (Stage B, cross-modal rules, headless correct path, quality gate, grounding validator) is the gate for generated practice content. Clinician answers (`docs/research/clinician_signoff.md`) remain welcome research input that can change content, but no step waits for them. This replaces the composer's earlier rule that every composed case needs medical approval before the practice pool.
8. **Licensing is not a blanket blocker.** Terms are read, recorded in `docs/research/licence_provenance_register.md`, and material is used as its terms allow; restricted raw data never enters the repository.
9. **One canonical plan.** `docs/architecture/procsense_full_pipeline.json` is the source; the registry, spec, matrix, model register and roadmap are generated from it and checked by `tools/plans/validate_pipeline.py` and `pipeline_docs.py --check`.

## Amendment (12 October 2026, second planning pass)
10. **The composer is real AI.** Stage LG-04 is an LLM with retrieval over a scenario knowledge base (A66) and exemplar cases, constrained to a schema of approved ids, proposing a whole scenario plan with a rationale per choice; the solver and validators enforce it. Solver-only composition is the measured baseline and the fallback, not the design.
11. **Personalisation from recorded evidence** (D67, D68): a Bayesian learner model, an LLM interpreter whose output is a hypothesis, and a targeting policy (rules, then a contextual bandit adopted only after off-policy evaluation) set the objectives that change the next case's content.
12. **Every feature has a complete plan.** Statuses (blocked, deferred, proposed, rejected) are not reasons to omit planning; "not available yet" is distinguished from "technically unsuitable". The two features rejected in the first pass (E64 generative images, E65 LLM fine-tuning) are kept with their history and revised into staged offline research tracks (Deferred, PH-24).
13. **Hardware is planned, not used as an exclusion**: both 6 GB GPUs are benchmarked with Unity running (p1-ai-infrastructure-s02) and every capability has a staged deployment.

## Consequences
The first fresh scenario depends on data work, not on the GPU or a language model, so the critical path avoids the current blockers (Ollama not installed, no labelled set). Every package is reproducible and traceable to its datasets. The cost: more validation code, and practice cases that no clinician has read; this is accepted because they are practice-only, labelled, built only from validated blocks and approved values, and checked automatically.

## Revisit if
A generated case teaches something wrong that the validators missed (add the rule, then reconsider the gate), assessment needs generated cases, or a licence term changes.
