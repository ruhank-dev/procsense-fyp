# Novelty over simulated trainees

Run: 17 October 2026 (second run, after fingerprints stopped carrying readable names; the first gave 0.9984, 19, 0.756), `python tools/generation/simulate_novelty.py 1000 12` (p1-live-scenario-generation-s09, stage LG-13). Code at the commit that added this file; content hash as in `content/manifest.json` at that commit.

| Measure | Result | Target (live_generation_spec.md section 6) |
|---|---|---|
| Simulated trainees | 1,000, each playing 12 generated cases in a row, the two families alternating | |
| Sessions | 12,000 | |
| Novelty rate (sessions meeting the threshold) | 0.998 | 0.95 or more |
| Low-novelty sessions served after 5 attempts | 24 of 12,000 | logged, not hidden |
| Fallbacks to the pool or library | 0 | |
| Mean distance to the nearest earlier case | 0.762 | at least 0.35 |
| Run time | 1,893 s | |

**What this does and does not show.** The trainees are simulated with empty ratings (the objectives do not adapt), so the same 53 approved cases are the spines every time; the result says the novelty rule and the retry work over about 12 sessions per trainee with the current library, which has 23 approved cases in family S1 and 28 in S2. It does not say a trainee will find case 40 fresh: with 10 spines in the history window and 23 to choose from the space is small, and novelty beyond about 20 sessions rests on more spines being approved (the composer varies the patient, not the story). Over-novelty, the opposite risk, is not measured here; the thresholds are starting values (OQ-04).
