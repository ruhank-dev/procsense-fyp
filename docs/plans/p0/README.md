# P0 — The complete core simulator

Everything needed for a trainee to play **S1 (planned laparoscopic cholecystectomy)** and **S2 (emergency appendectomy)**
from patient arrival to WHO Time Out (Steps 1–14), across the **17 P0 cases**, in a fully equipped VR hospital,
with deterministic scoring and a debrief.

P0 cases: VAR-S1-A · VAR-S1-PB · VAR-S1-B (+B2) · VAR-S1-C (+C2) · VAR-S1-D (a/b) · VAR-S1-E · VAR-S1-G · VAR-S1-H · VAR-S1-J ·
VAR-S2-PC · VAR-S2-PD · VAR-S2-B · VAR-S2-D · VAR-S2-F · VAR-S2-G · STK-S1-D · STK-S2-B

## Phases and how they connect

```
p0-foundation ─┬─ p0-core-engine ─┬─ p0-simulation-workflow ─┐
               │                  ├─ p0-clinical-logic ──────┼─ p0-physiology-events-behaviour
               │                  │                          ├─ p0-scoring-debrief
               │                  │                          └─ p0-case-library
               │                  └─ p0-vr-platform ─┬─ p0-environment-rooms (OT + patient bay; A/B/C) ─ p0-blender-equipment ─┐
               │                                     ├─ p0-characters                                                         │
               │                                     ├─ p0-voice-audio                                                        │
               │                                     ├─ p0-vr-interaction-ui ◄─────────────────────────────────────────────────┘
               │                                     ├─ p0-diagnostic-transitions (doors → wait → report → EMR update)
               │                                     └─ p0-patient-records-emr (physical folder + EMR computer)
               └─ p0-medical-research-data (proof of truth + datasets)
                                       ▼ (all meet)
                             p0-scenario-s1 → p0-scenario-s2
                  p0-evaluation-performance runs alongside all of them
```

Two tracks run in parallel: the **engine track** (headless, fully testable) and the **VR/3D track**. They meet at p0-scenario-s1.
Medical research runs underneath both. Environment rule: **fully model only what the trainee interacts with** (OT + patient bay);
diagnostics are doors (B); history and results live in the EMR (C).

## Work order since 10 Oct 2026: data first

The owner decided that **all data work comes before the remaining engine and VR work**. The stage order, dependencies and status are in [data-first-order.md](data-first-order.md); the decision is [ADR-0002](../../decisions/ADR-0002-data-first-order.md). The walking-skeleton order below still describes what is built and how it was built; its remaining engine steps resume after the data stages.

## Build order — walking skeleton first (approved, phase review P3)

**All steps by milestone:** [../milestones.md](../milestones.md) · **Current:** [walking-skeleton-1.md](walking-skeleton-1.md) — 15 steps across 6 phases (steps 1–9 headless, 10–15 after Unity/Quest installs).

Before any final art, prove the whole loop end to end with greybox (simple shapes + labels), then widen:

1. **Skeleton 1 — patient bay:** VAR-S1-A Steps 1–3 (identity + allergy band, history question, vitals on the monitor) in a greybox bay on Quest,
   driven by the real Core engine, with session log replayed headless.
2. **Skeleton 2 — records + diagnostics:** order a test → patient walks the greybox corridor to a signed door → wait → returns with report →
   physical folder + EMR show it.
3. **Skeleton 3 — OT:** greybox OT with WHO Sign In + Time Out board for VAR-S1-A.
4. Then each phase deepens its part (rules, cases, equipment, characters, UI); real Blender/free assets replace greybox as they're ready.

Art never blocks logic; every skeleton stays playable after each change.

## P0 is done when
All 17 P0 cases reach their expected end state both headless and in VR on Quest, every clinical value is in the source register,
every P0 equipment item exists as our Blender Python asset in the right A room and works per the interaction spec, every B location works as a door transition, and the performance budget holds.
