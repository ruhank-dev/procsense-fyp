# p1-live-scenario-generation-s05 — Numeric generation: labs, vitals, trends, anomalies (LG-07)

Milestone: **M14 P1 live scenario generation — dataset analysis and fresh cases**


## What
Seeded sampling from parameter packs: truncated distributions per band and condition, copula for linked analytes, trends, repeats, artefacts.

## Why
Realistic, coherent numbers from data, not random draws.

## How to implement
1. `server/generation/numeric.py`; one seed stream per slot.
2. Extend EXP-DATA-2 to packs.
3. Plausibility and critical-limit checks inline.

## Status (17 October 2026)

- **Built:** `server/generation/numeric.py` (`vary_numbers`) and the sampler `server/generation/packs.py` (moved from `tools/generation`; the tools import it): a composed case keeps the numbers its story is built on (abnormal results, values named in the text, walkthrough rows, faulty samples, results with a previous value, variant results, observations that are not the starting one) and draws the free ones again, one random stream per slot: normal laboratory results from the normal zone of the band shaped by the dataset's curve and kept only if inside the case's own range (linked analytes with a solid correlation together, weak pairs independently), the starting vital signs moved a little around the patient's own reading by a share of the real spread, inside the range that scores 0 on NEWS2 and only if the total stays. Every decision is returned in a log that goes into the package.
- **Verification result** (`tools/ci/test_generation_numeric.py`, 12 tests): 100 percent of normal draws inside their band over 10,000 seeds (12 analytes, both sexes); KS distance under 0.03 against the profile for three analytes; the same seed gives identical values; the numbers the story names are never touched; composed skeletons with drawn numbers pass Stage B.
- **Not built:** per-condition distributions (the demo is not split by condition, so the zone is the unit); trends and repeats are the case's own; plausibility against `lab_plausibility.csv` is by the band and the case's range only; the learned conditional generator is s14.

## Verification
Band compliance 100 percent over 10,000 seeds; KS and correlation within tolerance.

## Files changed
`server/generation/numeric.py`

## Dependencies
p1-dataset-analysis-s05, p1-live-scenario-generation-s03

## Common issues
Copula with weak correlation estimates: fall back to independent draws and log.

## Sources
Spec LG-07; B14, B20, B21.
