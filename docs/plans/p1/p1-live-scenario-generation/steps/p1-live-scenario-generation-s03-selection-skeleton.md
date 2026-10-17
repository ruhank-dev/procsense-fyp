# p1-live-scenario-generation-s03 — Component selection (solver) and scenario skeleton (LG-04 baseline, LG-05)

Milestone: **M14 P1 live scenario generation — dataset analysis and fresh cases**


## What
Use the composer solver live with difficulty bounds and a novelty pre-filter. This is the deterministic baseline and fallback of LG-04; the AI plan composer that proposes the plan arrives in p1-live-scenario-generation-s13; assemble a case-file-shaped skeleton with typed slots and discovery rules.

## Why
This is the deterministic heart: a fresh case exists before any LLM runs.

## How to implement
1. Call `tools/composer/solver.py` as a library.
2. `server/generation/skeleton.py`.
3. Property tests over 10,000 seeds.

## Status (17 October 2026)

- **Built:** `server/generation/skeleton.py` (`build_skeleton`): ranks the approved spines of the family by BM25 relevance to the objectives and by how near their tier rating is to the difficulty target, leaves out recent spines, picks one of the best five by the request's seed, varies the patient with the composer (`tools/composer/solve.py`, used as a library; optional slots are given up in the fixed order ALG, PSH, SOC if a spine will not fit; other blocks are tried if the story's fingerprint is one the trainee has met), draws the free numbers (s05) and the pictures (s06), and returns the skeleton: the composed case file, its typed slots, the spine's discovery rules, the numeric and media logs and the story fingerprint. The composer gained `build_card` and `fingerprint`.
- **Verification result:** the property test (`tools/ci/test_generation_skeleton.py`) found zero constraint violations and every problem of a P0 spine discoverable over 10,000 seeds (a local run of 308 s on the composer-level skeleton) and over 200 seeds in CI; composed cases pass Stage B and the package schema; the same objectives give the same skeleton.
- **Not built:** the AI plan composer that proposes the plan before the solver completes it (s13); the discovery rules are carried in the skeleton but the engine must resolve a composed case through its spine (s10).

## Verification
Zero constraint violations; every problem discoverable.

## Files changed
`server/generation/skeleton.py`

## Dependencies
p1-scenario-composer-difficulty-s02, p1-live-scenario-generation-s02

## Common issues
Unsatisfiable templates: relax optional slots in fixed order.

## Sources
Spec LG-04, LG-05; A4, A11.
