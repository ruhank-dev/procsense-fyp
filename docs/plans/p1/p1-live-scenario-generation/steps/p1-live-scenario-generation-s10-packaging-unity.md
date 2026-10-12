# p1-live-scenario-generation-s10 — Packaging, pre-generated pool and Unity delivery (LG-14, LG-15)

Milestone: **M14 P1 live scenario generation — dataset analysis and fresh cases**

> **Planning only (12 October 2026).** Written in the planning update; do not implement until the owner says so. Spec: `docs/architecture/live_generation_spec.md`; canonical graph: `docs/architecture/procsense_full_pipeline.json`.

## What
Canonical ScenarioPackage with SHA-256 and provenance; batch pre-generation into a local pool; Unity loads a package through the existing bridge and ContentGate with review status GENERATED_PRACTICE_ONLY.

## Why
Offline-first delivery; latency hidden by the pool.

## How to implement
1. `server/generation/package.py`, pool CLI.
2. C# loader extension and ContentGate status (Core), then Unity bridge (Unity work).

## Verification
Byte-identical packages; pooled package loads offline under 2 s.

## Files changed
`server/generation/package.py`, `src/ProcSense.Core/Content/`, Unity bridge

## Dependencies
p1-live-scenario-generation-s09, p1-backend-s06

## Common issues
Review-status enum change touches ADR-0006: extend, do not loosen.

## Sources
Spec LG-14, LG-15; ADR-0006, ADR-0008.
