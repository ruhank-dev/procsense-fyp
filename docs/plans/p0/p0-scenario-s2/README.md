# p0-scenario-s2

**Tier:** P0 · **Builds on:** p0-scenario-s1, p0-physiology-events-behaviour · **Feeds:** evaluation

## Goal
S2 (emergency appendectomy) playable: triage, early analgesia, assessment + treatment + preparation overlapping while the clock runs,
sepsis recognition with timers, diagnostic diversion (ectopic → gynaecology), full-stomach anaesthesia (RSI).
Cases: VAR-S2-PC, -PD (Usman deterioration), -B, -D, -F, -G and STK-S2-B.

## Decisions
| Decision | Chosen | Alternatives | Why | Revisit if |
|---|---|---|---|---|
| Time pressure | Real-time clock + time-skip at waits | Turn-based | Realism without boredom | EXP-S2-1 → beginner clock speed |
| Sepsis timers | Shown in debrief, not on screen (hints in beginner mode) | Visible countdown | Trainee must recognise, not be told | — |

## Experiments
| ID | Question | Method | Success metric | Status |
|---|---|---|---|---|
| EXP-S2-1 | Fair clock speed for beginners? | Testers at 3 speeds | Chosen speed recorded (Type D) | planned |
| EXP-S2-2 | Do testers notice deterioration unprompted? | Observation | Recognition rate recorded | planned |

## Exit
All S2 P0 cases reach expected end states in VR + headless; Usman timeline matches walkthrough.

## Who does what
Same as p0-scenario-s1.

## Sources
Walkthrough Parts 13, G, D (STK-S2-B), 4.9–4.10.
