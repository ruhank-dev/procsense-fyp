# Walking skeleton 1 — patient bay, VAR-S1-A, Steps 1–3

**Goal:** the thinnest end-to-end slice of the real product, in greybox:
Mrs Ayesha Khan (VAR-S1-A) sits in a greybox patient bay on the Quest; the trainee introduces themselves, checks identity
(spoken answer vs wristband vs notes), asks about allergies and the reason for admission, asks a few history questions,
attaches SpO2 + BP cuff and reads her vitals on the monitor — all driven by the **real Core engine**, with the headset session
saved and **replayed headless with an identical result**.

**Not in skeleton 1:** NEWS2 calculation, physiology over time, scoring, examination, tests, EMR, final art, voice.

## Patient data used (all from the walkthrough — proof of truth in `docs/research/source_register.csv`)
Walkthrough Part 12, Example Patient A, page 104:
- Mrs Ayesha Khan, 42, teacher, born 14 March 1984, wristband `AYESHA KHAN, 14/03/1984, MR 102938`; allergies: none;
  reason: "To have my gallbladder taken out with keyhole"; husband takes her home and stays overnight; surgery 09:00, time now 08:00.
- History answers (pain, red flags, other illnesses, medicines, previous anaesthetic, smoking/alcohol, last food 21:00, last drink water 06:30, pregnancy).
- Vitals: HR 76 regular · BP 124/78 · RR 14 · SpO2 98% room air · Temp 36.7 °C · Alert · Pain 0/10.

## Step order (across phases)

| # | Step file | Phase | Needs | Can start now? |
|---|---|---|---|---|
| 1 | [✅ p0-medical-research-data-s01-var-s1-a-steps-1-3-content](p0-medical-research-data/steps/✅%20p0-medical-research-data-s01-var-s1-a-steps-1-3-content.md) | medical-research-data | — | ✅ |
| 2 | [✅ p0-core-engine-s01-sim-clock-and-rng](p0-core-engine/steps/✅%20p0-core-engine-s01-sim-clock-and-rng.md) | core-engine | — | ✅ |
| 3 | [✅ p0-core-engine-s02-actions-session-log](p0-core-engine/steps/✅%20p0-core-engine-s02-actions-session-log.md) | core-engine | 2 | ✅ |
| 4 | [✅ p0-core-engine-s03-content-loader-patient-state](p0-core-engine/steps/✅%20p0-core-engine-s03-content-loader-patient-state.md) | core-engine | 1, 3 | ✅ |
| 5 | [p0-core-engine-s04-observable-state-cli-replay](p0-core-engine/steps/p0-core-engine-s04-observable-state-cli-replay.md) | core-engine | 4 | ✅ |
| 6 | [p0-simulation-workflow-s01-step1-identity-allergy](p0-simulation-workflow/steps/p0-simulation-workflow-s01-step1-identity-allergy.md) | simulation-workflow | 5 | ✅ |
| 7 | [p0-simulation-workflow-s02-step2-history-disclosure](p0-simulation-workflow/steps/p0-simulation-workflow-s02-step2-history-disclosure.md) | simulation-workflow | 6 | ✅ |
| 8 | [p0-simulation-workflow-s03-step3-sensors-vitals](p0-simulation-workflow/steps/p0-simulation-workflow-s03-step3-sensors-vitals.md) | simulation-workflow | 6 | ✅ |
| 9 | [p0-simulation-workflow-s04-golden-run-skeleton1](p0-simulation-workflow/steps/p0-simulation-workflow-s04-golden-run-skeleton1.md) | simulation-workflow | 6–8 | ✅ |
| 10 | [p0-vr-platform-s01-unity-project-quest-setup](p0-vr-platform/steps/p0-vr-platform-s01-unity-project-quest-setup.md) | vr-platform | Unity + Quest installed | ⏳ after installs |
| 11 | [p0-vr-platform-s02-core-as-unity-package](p0-vr-platform/steps/p0-vr-platform-s02-core-as-unity-package.md) | vr-platform | 5, 10 | ⏳ |
| 12 | [p0-vr-platform-s03-simulation-host-emitters-views](p0-vr-platform/steps/p0-vr-platform-s03-simulation-host-emitters-views.md) | vr-platform | 11 | ⏳ |
| 13 | [p0-environment-rooms-s01-greybox-patient-bay](p0-environment-rooms/steps/p0-environment-rooms-s01-greybox-patient-bay.md) | environment-rooms | 10 | ⏳ |
| 14 | [p0-vr-interaction-ui-s01-skeleton1-interactions](p0-vr-interaction-ui/steps/p0-vr-interaction-ui-s01-skeleton1-interactions.md) | vr-interaction-ui | 9, 12, 13 | ⏳ |
| 15 | [p0-evaluation-performance-s01-skeleton1-quest-replay-check](p0-evaluation-performance/steps/p0-evaluation-performance-s01-skeleton1-quest-replay-check.md) | evaluation-performance | 14 | ⏳ |

Steps 1–9 are fully headless (no Unity needed) and can be built and tested from the terminal now.
Steps 10–15 wait for the Unity/Quest installs.

## Skeleton 1 is done when
The golden run (step 9) passes headless; the same actions done by hand on the Quest produce a session log that replays headless to the
**same final-state hash**; and no clinical number exists in code (all from `content/`).
