# p0-evaluation-performance-s08 — P0 release (EXP-REL-1, EXP-REL-2)

Milestone: **M9 P0 release**

## What
Clean-machine build following only the README (EXP-REL-1, T-Q2 ≤ 2 h); 10 full S1 + S2 runs on device without crash and within the perf gate (EXP-REL-2); release checklist; tag v1.0 (you push).

## Why
P0 is only finished when someone else can rebuild it from the README on a clean machine and it runs stable on the headset for full sessions — that's the definition of a reproducible release.

## How to implement
1. Fresh machine test; stability runs; checklist; tag.
2. Clean machine: fresh Windows user/VM; follow README top to bottom (tools, clone + LFS, `task build`, `task test`, Unity open, `task unity-build -- Demo`); time it (T-Q2 ≤ 2 h); note every missing instruction and fix the README.
3. Stability: 10 full runs (S1 + S2 cases) on Quest 2 with OVR Metrics recording; 0 crashes, perf gate held, session logs replay headless with matching hashes.
4. Release checklist `docs/RELEASE.md`: version bump, content_version stamped, assessment content approved, attributions regenerated, APK + SHA-256, release notes; you create and push the tag.

## Verification
EXP-REL-1 and EXP-REL-2 recorded as passed in the evaluation README; APK checksum in the release notes.

## Files changed
`docs/RELEASE.md`, release notes.

## Dependencies
p0-evaluation-performance-s02–s07.

## Common issues
Clean-machine test reveals undocumented steps — fix the README immediately, then repeat until it passes without help.

## Sources
Implementation Guide B-S11 Step 4, T-Q2; p0 README done criteria; evaluation README EXP-REL-1/2.
