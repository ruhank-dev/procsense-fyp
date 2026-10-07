# p0-vr-interaction-ui-s02 — Interaction framework and equipment state machines

Milestone: **M6 Environment & assets — rooms, equipment, characters, voice**

## What
Data-driven `EquipmentStateMachine` (states/transitions from `content/equipment/*.json`): trigger (grab | socket | activate | ui) → emits a SimAction → Core confirms → visual state (child objects / material params); socket library (finger, arm, chest, wrist, ear, face, stand, trolley slots) with interaction layers; poke panels built from `panel` entries; fault visuals from Observable equipment faults.

## Why
Every interactive item behaves per its spec row without bespoke code; Core can reject and visuals revert (T-IN2).

## How to implement
1. `Scripts/Interaction/EquipmentStateMachine.cs`, `SocketLibrary`, `PanelBuilder`, `FaultListener`.
2. Example chain (B-S9): fluid bag SEALED → SPIKED (giving set socket) → HUNG (pole) → CONNECTED (cannula) → RUNNING (pump button), each a Core action.
3. Haptics on snap; hover highlight; one item per socket (T-IN4).
4. Every interaction has a tablet fallback emitting the same action (T-IN5).

## Verification
T-IN1, T-IN2, T-IN4, T-IN5 green with the pilot assets.

## Files changed
`unity/Assets/_Project/Scripts/Interaction/*`, `content/equipment/*.json`.

## Dependencies
p0-vr-interaction-ui-s01; p0-blender-equipment-s05.

## Common issues
Objects fall through → kinematic sockets + collision layers.

## Sources
Implementation Guide B-S9 Steps 1–4, T-IN1–T-IN5.
