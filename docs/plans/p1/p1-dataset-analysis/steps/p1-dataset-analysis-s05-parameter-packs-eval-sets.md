# p1-dataset-analysis-s05 — Generator parameter packs and evaluation sets

Milestone: **M14 P1 live scenario generation — dataset analysis and fresh cases**


## What
Turn the profiles into small versioned packs that live generators read (per analyte and condition distributions, correlation matrices, trajectories, artefact rates, ECG parameters) and build the evaluation sets (gold scenarios, cross-modal mutations, retrieval queries).

## Why
Separates offline preparation from live generation: live sessions only read packs and never refit.

## How to implement
1. `tools/generation/build_packs.py` writes `content/generation/packs/*.json` with a version and SHA-256 in the content manifest; `--check` in CI.
2. Evaluation sets under `tests/generation/` with data cards.
3. Every pack row cites its profile and dataset id.

## Status (17 October 2026)

- **Built:** `tools/generation/build_packs.py` (`--check` in CI) writes `content/generation/packs/{labs,correlations,vitals,ecg,manifest}.json` (version 1, SHA-256 per pack, byte-identical on every run) and the evaluation sets `tests/generation/{gold_scenarios,mutations,retrieval_queries}.json` with a data card; `tools/generation/packs.py` reads packs and draws values (`LabPack.draw`, `draw_pair`): inside a zone from the dataset's own quantile curve where the zone holds at least 2 % of the data, otherwise uniformly inside the walkthrough's band and marked `band_only`; linked analytes through a Gaussian copula with the pack's Spearman correlation (weak pairs are drawn independently).
- **Verification result:** packs rebuild byte-identically; 10,000 draws reproduce p5 to p95 of six analytes within 5 % of the span; drawn values lie in their zone; the copula reproduces a pair's Spearman within 0.06 (`tools/ci/test_generation_packs.py`); no pack holds a list of raw values.
- **Not built:** per-condition distributions (the demo is hospital patients, not condition groups, so a zone is the unit); vital-sign trajectories and artefact rates beyond what the profiles hold; retrieval queries are derived from the cases (a paraphrase set needs a human, p1-ai-infrastructure-s04).

## Verification
Packs rebuild byte-identically; sampling 10,000 values from a pack reproduces its profile within tolerance.

## Files changed
`tools/generation/build_packs.py`, `content/generation/packs/`, `tests/generation/`

## Dependencies
p1-dataset-analysis-s02, p1-dataset-analysis-s03, p1-dataset-analysis-s04

## Common issues
Packs must never contain raw rows.

## Sources
Live generation spec section 3.
