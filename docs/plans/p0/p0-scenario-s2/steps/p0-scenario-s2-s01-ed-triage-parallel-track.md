# p0-scenario-s2-s01 — VAR-S2-PC: ED triage, early analgesia, parallel track

Milestone: **M8 S2 complete in VR**

## What
Make these cases fully playable in VR, with their what-ifs and expected end states: VAR-S2-PC Mr Hamza Ali, 19.

## Why
Each case teaches a specific walkthrough lesson; the case is done only when VR and headless agree and the reviewer accepts the video.

## How to implement
1. Run the case headless first (`task sim -- run --case <id> --actions tests/golden/<id>_correct.actions.json`) and fix engine/content gaps there.
2. Play it in VR on the Quest with real rooms/assets; export the session log; `task sim -- replay --log <file>` must match the headless end state.
3. Play the walkthrough's wrong paths (e.g. skipping the key question) and confirm the consequence matches the walkthrough.
4. Record a short playthrough video for the medical reviewer.
5. **VAR-S2-PC Mr Hamza Ali, 19** — classic appendicitis; triage note; early analgesia + IV via request workflow; HR 108 → 96, T 37.9, WBC 13.6; AIR 7 / AAS 19 (guide worked example); assessment, treatment and preparation overlap (WT-I02: use Part 13 duration)

## Verification
For each case: expected end state reached in VR and headless (same hash on replay); wrong-path consequence matches the walkthrough; reviewer video logged.

## Files changed
`tests/golden/VAR-S2-PC_*.actions.json`, `tests/golden/VAR-S2-PC_*_expected.json`, `docs/plans/p0/p0-scenario-s2/results.md` (VR/headless agreement, reviewer notes), fixes in the owning phases

## Dependencies
M7 complete.

## Common issues
A VR-only bug → reproduce headless from the session log first, then fix.

## Sources
Walkthrough Part 13; inventory D463.
