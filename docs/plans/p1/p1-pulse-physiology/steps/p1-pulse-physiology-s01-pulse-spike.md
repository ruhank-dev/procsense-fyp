# p1-pulse-physiology-s01 — Pulse Physiology Engine spike

Milestone: **M11 P1 content — theatre extension, extended cases, composer, Pulse**

## What
Build Pulse (Kitware) and run one S2 scenario through its API; confirm the current licence and Unity/Android support; decide where it can run (Quest ARM64 vs server).

## Why
Pulse could add physiological fidelity, but whether it builds for Quest (ARM64) at all decides the whole approach — find out before investing in an adapter.

## How to implement
1. Spike in `tools/pulse/` (or a blank Unity project for the Quest test per B-R13 Step 6).
2. Record findings in the phase README.
3. Check the official Pulse site/repo for licence, supported platforms, Unity asset availability; record.
4. PC test: build Pulse, run one scenario (e.g. dehydration) and read HR/BP/SpO2 over time.
5. Quest test (B-R13 Step 6): blank Unity 6 project + Pulse Unity asset → ARM64 IL2CPP APK → does it run, and at what cost?

## Verification
Spike notes: runs on PC yes/no, on Quest yes/no, effort estimate for the adapter.

## Files changed
`tools/pulse/README.md` (spike notes), spike project (not in main repo), `docs/plans/p1/p1-pulse-physiology/README.md`.

## Dependencies
p0-physiology-events-behaviour-s05.

## Common issues
Native lib not packaged for ARM64 → server-side only.

## Sources
Implementation Guide B-R13 Step 6; inventory D39.
