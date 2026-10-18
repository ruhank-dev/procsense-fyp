# ADR-0015 — Pulse is a recorded reference for practice mode; LitePhysio stays the default engine

**Date:** 18 October 2026 · **Status:** Proposed (no reviewer has compared the curves; the Quest cost is Sannan's, DC-05) · **Related:** p1-pulse-physiology-s01 to s03, ADR-0005, DT-207

## Context
Pulse 4.4.0 now builds and runs on the desktop with free tools (`tools/pulse/README.md`). Four scenarios were recorded and compared with LitePhysio (`docs/research/physio-plots/pulse-parity`): the main effect of each action agrees in direction, 9 of the 27 comparisons where either engine moves agree overall. Pulse costs about 1.6 ms for each 20 ms step on the desktop. A live Pulse in a session would break determinism unless its values were logged and replayed.

## Decision
1. LitePhysio stays the engine of every session unless the session names a recorded run (`SessionOptions.PulseTrace`, header field `pulse_trace`, `--pulse-trace`).
2. A recorded Pulse run is replayed by `PulseTraceEngine` behind `IPhysiologyEngine`: until the effect that starts it is applied the patient is at the case's baseline, then the case moves by what Pulse moved. The values are data; a replay is identical on any machine.
3. Scoring does not depend on the engine (it is by actions, ADR-0005): the 142 golden runs end in the same state with a trace selected.
4. Not for assessment mode; no claim that Pulse is more realistic until a reviewer says so.

## Alternatives
Pulse as the default (rejected: 9 of 27 agreement, a design change to every effect table, no headset cost); a live Pulse beside the session with logged values (not built: the recorded form answers replay, and the interactive form needs a server bridge); not using Pulse (rejected: it works and gives a physiological reference the equations cannot).

## Consequences
Practice scenarios can be given a more physiological response (for example bleeding that the fluid bolus only slows). New recordings are made with `tools/pulse/pulse_bridge.py` and listed in `physio/pulse_traces/index.json`.

## Revisit if
A reviewer prefers the Pulse curves; the Quest cost is measured and low; an interactive bridge is wanted.
