# p1-pulse-physiology-s02 — IPhysiologyEngine adapter for Pulse

Milestone: **M11 P1 content — theatre extension, extended cases, composer, Pulse**

## What
Adapter mapping Core effects/drivers to Pulse actions and Pulse outputs back to Core vitals, behind `IPhysiologyEngine` with a config switch; runs on a worker thread (Quest) or server-side bridge; determinism handled (server-side results recorded in the log for replay).

## Why
Pulse must plug in behind the same `IPhysiologyEngine` interface so no rule, case or scoring code changes when the engine changes — that's what makes the parity experiment fair.

## How to implement
1. Adapter class + mapping table; config switch per case.
2. Mapping table: our effects (fluids, oxygen, analgesia, anaphylaxis…) → Pulse actions; Pulse outputs → our variables.
3. If Pulse runs server-side: results streamed and logged so replays use the logged values.

## Status (18 October 2026)

- **Built:** `PulseTraceEngine` behind `IPhysiologyEngine` (`src/ProcSense.Core/Physiology/PulseTraceEngine.cs`), `content/physio/pulse_mapping.json`, the bridge that records Pulse runs (`tools/pulse/pulse_bridge.py`) and a config switch per session (`SessionOptions.PulseTrace`, header field `pulse_trace`, CLI `--pulse-trace`). Server-side results are logged data: a session replays a recorded run, so it verifies on any machine. 11 tests (`PulseTraceTests`); all 142 golden runs complete with a trace selected, with the same end state, and are identical unless the effect that starts the recording was given.
- **Limit:** it is a recorded scenario aligned by its first action, not an interactive patient; an interactive bridge (Pulse beside the session, values logged) is not built.

## Verification
P0 golden runs complete with Pulse selected; logged values replay identically.

## Files changed
`src/ProcSense.Core/Physiology/PulseAdapter.cs` (or `server/physio/pulse_bridge.py`), `content/physio/pulse_mapping.json`, tests.

## Dependencies
p1-pulse-physiology-s01.

## Common issues
Server-side Pulse breaks determinism unless its outputs are logged and replayed from the log.

## Sources
Implementation Guide B-R13 (IPhysiologyEngine interface, Pulse gate).
