# p0-evaluation-performance

**Tier:** P0 (runs alongside every P0 phase) · **Feeds:** P1, FYP report

## Goal
Prove P0 works, is correct, usable and fast: golden runs in CI, performance budgets enforced on device, medical playthrough
review, usability study, and a reproducible v1 release.

## Scope
- **In:** golden playthrough suite (correct / novice / dangerous / edge per case), regression CI, weekly Quest profiling,
  medical playthrough review against walkthrough Part I checklist A–G, usability study (SUS, NASA-TLX, task success),
  clean-machine build test, assessment-mode lock (only approved content), attributions page (assets, voices, datasets, guidelines), report material.
- **Out:** fixing issues (the owning phase fixes them).

## Decisions
| Decision | Chosen | Alternatives | Why | Revisit if |
|---|---|---|---|---|
| Usability measures | SUS + NASA-TLX + task success/time | Custom questionnaire | Standard, comparable | Supervisor asks for others |
| Participants | Medical students / junior doctors (ethics approved) | Team only | Real target users | — |
| Profiling | Weekly on device from the first Quest build | At the end | Catch regressions early | — |
| Report | Written continuously from phase READMEs + experiment logs | At the end | Results already recorded | — |

## Experiments
| ID | Question | Method | Success metric | Status |
|---|---|---|---|---|
| EXP-EVAL-1 | Usability of S1 and S2 | Target users, SUS + tasks | Scores reported honestly | planned |
| EXP-EVAL-2 | Perceived clinical realism | Part I checklist section G | All OK or fix list | planned |
| EXP-REL-1 | Clean-machine reproducibility | Fresh machine, README only | Build + test + APK without help | planned |
| EXP-REL-2 | Stability | 10 full S1 + S2 runs on device | 0 crashes, budget held | planned |

## Exit
All P0 golden runs green; usability + realism results recorded; v1 APK reproducible from a clean machine.

## Who does what
Developer: suites, CI, protocols, analysis scripts, report drafts, attributions. Team: run sessions with participants, device testing.

## Sources
Walkthrough Part I; Master Plan §41, §44; inventory D83–D85.
