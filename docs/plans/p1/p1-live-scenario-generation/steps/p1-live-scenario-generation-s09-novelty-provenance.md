# p1-live-scenario-generation-s09 — Novelty fingerprints and provenance (LG-13)

Milestone: **M14 P1 live scenario generation — dataset analysis and fresh cases**


## What
Structured three-level fingerprints, weighted Jaccard distances to trainee history and library, thresholds, fallback; provenance record and seed reproducibility.

## Why
Fresh means new coherent combinations, measurably.

## How to implement
1. `server/generation/novelty.py`; datasketch MinHash when history grows.
2. Fingerprint schema in the package schema.
3. Simulation over 1,000 synthetic trainees.

## Status (17 October 2026)

- **Built:** `server/generation/novelty.py` (`levels_of`, `distance`, `check`, `pick_novel`) and the seal in `server/generation/package.py` (`seal`, `verify_seal`, `reproducibility_key`). The fingerprint has three sets (structural 0.5, clinical 0.35, surface 0.15); a case is accepted when D is 0.35 or more from each of the last 10 sessions and its structure differs from each of the last 3 (deliberate practice may repeat a structure when the clinical distance is 0.3 or more); after 5 attempts the most novel valid candidate is served and logged LOW_NOVELTY. The package schema gained the fingerprint levels, novelty report, reproducibility key, SHA-256, repairs and novelty attempts.
- **Verification result** (`tools/ci/test_generation_novelty.py`, 13 tests): fingerprints are deterministic; a new name on the same story is not a new story; the structure rule only looks at the last 3; a changed package no longer matches its hash; the same request gives the same package; 25 simulated trainees of 10 sessions meet a novelty rate of 0.95. The 1,000-trainee run is `tools/generation/simulate_novelty.py` (local).
- **Not built:** MinHash (`datasketch`) for fast nearest-neighbour search: a history of 10 sessions needs no index; it is added when histories grow. Block-space coverage per trainee over time is not yet reported.

## Verification
Novelty target met (spec section 6); fingerprints deterministic.

## Files changed
`server/generation/novelty.py`

## Dependencies
p1-live-scenario-generation-s08

## Common issues
Over-novelty hurts learning: thresholds tunable (OQ-04).

## Sources
Spec LG-13; A6, A13.
