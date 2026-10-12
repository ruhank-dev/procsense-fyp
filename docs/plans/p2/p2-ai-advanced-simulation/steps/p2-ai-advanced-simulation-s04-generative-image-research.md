# p2-ai-advanced-simulation-s04 — Generative image and waveform synthesis research track (E64, B15.c2)

Milestone: **M15 P2 learning analytics and advanced simulation**

> **Planning only (12 October 2026).** Written in the planning update; do not implement until the owner says so. Spec: `docs/architecture/live_generation_spec.md`; canonical graph: `docs/architecture/procsense_full_pipeline.json`.

## What
Offline research: train a small mask-conditioned image generator (and, if needed, a conditional ECG generator) on licensed data; generate labelled synthetic candidates for library gaps; evaluate with finding-classifier agreement, rater discrimination and distribution distance; ingest only passing assets, labelled synthetic.

## Why
Revives the rejected feature as a staged track: the library has gaps and generation may fill them safely offline.

## How to implement
1. Gap list from p1-dataset-analysis-s04.
2. Data preparation with masks; licence check per dataset.
3. Train locally at 128 to 256 px or on a rented GPU; record cost.
4. Evaluation protocol and review.
5. Ingest via the imaging library builder with `synthetic=true`.

## Verification
Finding agreement at least 90 percent and rater accuracy under 70 percent, or a documented negative result.

## Files changed
`tools/research/gen_images/`

## Dependencies
p1-dataset-analysis-s04, p1-ai-infrastructure-s02

## Common issues
Generated false findings: nothing reaches the library without passing the protocol.

## Sources
E64, B16, B17, B15; ADR-0003.
