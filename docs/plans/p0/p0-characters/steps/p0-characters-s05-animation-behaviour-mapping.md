# p0-characters-s05 — Animation set and behaviour mapping

Milestone: **M6 Environment & assets — rooms, equipment, characters, voice**

## What
Clip set: idle, breathing (speed = RR / 15 so visible breaths match the monitor), talk loop, nod, shake head, point, hand-over, wince, guard abdomen, fidget, slump, walk, sit, lie; Animator params Mode (int), Intensity (float), EventCue (trigger); `BehaviourView` maps Core behaviour mode + intensity to Animator, shapes and shader params.

## Why
Behaviour must be visible and consistent with the monitor (EXP-CHR-3).

## How to implement
1. Mixamo (or chosen set) clips retargeted to Humanoid; avatar masks for upper-body gestures.
2. `Scripts/Characters/BehaviourView.cs` subscribes to Observable behaviour; interpolates weights (no popping).
3. Breathing layer speed from Core RR; EXP-CHR-3: count breaths per minute vs monitor RR.

## Verification
T-AV1 each mode distinct; breaths counted = RR; T-ST-A1 every clip plays.

## Files changed
Animator controllers `unity/Assets/_Project/Art/Characters/Animation/*`, `Scripts/Characters/BehaviourView.cs`, retargeted clips.

## Dependencies
p0-characters-s04; p0-physiology-events-behaviour-s10.

## Common issues
Breathing animation too subtle to count in VR → exaggerate chest/abdomen motion slightly while keeping the rate exact.

## Sources
Implementation Guide B-S2 Steps 3–4 (breathing speed = RR/15), B-S3 Step 3; walkthrough Part 16 (reactions).
