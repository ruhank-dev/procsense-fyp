# p2-ai-advanced-simulation-s02 — Patient Digital Twin (E48)

Milestone: **M15 P2 learning analytics and advanced simulation**


## What
Persistent patient state across a session and repeat sessions from the generated profile and physiology.

## Why
Continuity makes consequences real.

## How to implement
1. Twin state object over LitePhysio or PULSE; replayable.

## Status (17 October 2026)

- **Not built (research, DT-215):** a patient twin that persists across repeat sessions. The engine already replays a session exactly from its log; carrying the end state into a new session needs a state snapshot and restore, which is a design decision about which systems to keep (the step's own warning is state explosion).

## Verification
Twin state replays.

## Files changed
`src/ProcSense.Core/Twin/` (research)

## Dependencies
p1-live-scenario-generation-s11

## Common issues
State explosion: keep the twin to the case's systems.

## Sources
E48.
