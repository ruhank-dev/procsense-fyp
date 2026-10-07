# p0-scenario-s1

**Tier:** P0 · **Builds on:** all P0 engine + VR phases · **Feeds:** p0-scenario-s2, evaluation

## Goal
S1 (planned laparoscopic cholecystectomy) playable on Quest from arrival to WHO Time Out for all S1 P0 cases:
VAR-S1-A, -PB, -B(+B2), -C(+C2), -D(a/b), -E, -G, -H, -J and STK-S1-D, with their what-ifs and every end state incl. appropriate postponement.

## Decisions
| Decision | Chosen | Alternatives | Why | Revisit if |
|---|---|---|---|---|
| Order | Walking skeleton (VAR-S1-A Steps 1–3 greybox → records/diagnostics loop → OT Sign In/Time Out) → full VAR-S1-A → PB (redirect) → other variants → STK-S1-D | All at once | Prove the flow early, then branch (approved P3) | — |
| Art | Greybox allowed until Blender assets land | Wait for art | Flow not blocked by art | — |
| Testing | Every VR session exportable and replayed headless | VR-only | Reproducible bugs | — |

## Experiments
| ID | Question | Method | Success metric |
|---|---|---|---|
| EXP-S1-1 | How long is an S1 run? | Time testers | Recorded; informs session design |
| EXP-S1-2 | Headless golden runs and VR playthroughs agree? | Replay exported VR sessions | Same end state + scores |

## Exit
All S1 P0 cases reach expected end states in VR and headless; playthrough videos reviewed by the medical professional.

## Who does what
Developer: content wiring, golden runs, bug reproduction. Team: headset playthroughs, videos.

## Sources
Walkthrough Parts 12, F, D (STK-S1-D).
