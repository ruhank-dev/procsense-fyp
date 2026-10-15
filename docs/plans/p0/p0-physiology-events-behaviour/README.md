# p0-physiology-events-behaviour

**Tier:** P0 (Pulse engine option in p1-pulse-physiology) · **Builds on:** p0-clinical-logic · **Feeds:** scenarios, vr-interaction-ui, characters (behaviour → animation)

> **Progress, 12 Oct 2026:** s01 to s10 are built and tested (s01, s02, s04 and s10 approved; s03, s05 to s09 approved 12 Oct 2026): effects and patterns, display zones and the probe artefact, the plots for the review pack, the Event Director and the what-if coverage list. Magnitudes are provisional design values (Q4, Q7).

## Goal
A living patient: vitals that drift and respond to treatment, deterioration clocks for untreated problems, all 47 what-if branches
running from data, and a bounded behaviour model (pain, anxiety, trust, understanding).

## Scope
- **In:** LitePhysio (curves + noise), treatment effect library, deterioration clock (speed = most dangerous untreated problem),
  Event Director (seeded, approved events only), what-if runtime (all 47; bound to cases via case-library), behaviour model + transition table.
- **Out:** animation (characters), AI phrasing (p1-ai-language).

## Decisions
| Decision | Chosen | Alternatives | Why | Revisit if |
|---|---|---|---|---|
| Physiology | LitePhysio (own, deterministic) | Pulse; BioGears | Fast on Quest, controllable, matches walkthrough patterns | EXP-PHY-1 → also add Pulse (p1) |
| Effect magnitudes | Type D design values reviewed as plots | Literature models | No dataset gives these parameters | Source found |
| Events | Approved data-defined events only | AI-invented | Safety, reproducibility | — |
| Behaviour | Deterministic transition table | AI-driven emotion | Bounded + testable | — |

## Experiments
| ID | Question | Method | Success metric | Status |
|---|---|---|---|---|
| EXP-PHY-1 | Is LitePhysio realistic enough? | VAR-S2-PD (Usman) timeline plots reviewed by medical professional | Accepted, or fix list | running (plots built; the medical review is open, clinician question Q4) |
| EXP-PHY-2 | Deterioration clock reproduces the Usman timeline? | Headless run vs walkthrough | NEWS2 progression matches | planned |
| EXP-PHY-3 | Tick cost on Quest | Profile | < 0.5 ms | planned |

## Exit
Usman timeline reproduced; all 47 what-ifs load and have a test; trajectories accepted.

## Who does what
Developer: all + plots. Medical professional: trajectory review.

## Sources
Walkthrough Parts 4.11, 12–14, 17; Implementation Guide B-R13, B-R14, B-R6.

## Experiment log

### EXP-PHY-2: does the deterioration clock reproduce the Usman timeline? (11 Oct 2026)
- **Method:** headless, `DeteriorationTests` (T-P6, T-P7): the clock's vitals at 14:00, 14:45, 15:15 and 15:45 are scored with the NEWS2 chart (`News2`) and compared with the walkthrough's printed totals.
- **Result:** NEWS2 2, 5, 10 and 17 against the book's 2, 5, 10 and 16: every point is within the plan's one-point limit; the last differs by one because the book's 16 is itself a mis-total (ISS-0016). Treated at 14:45, blood pressure reaches 108/64 and heart rate 112 (the walkthrough's values after the correct action) 30 minutes later, against 92 and 128 untreated.
- **How it works and what is a design value:** a case with a timeline follows it (straight lines between the readings), so the match is by construction at the four reading times and the lines between them are a design choice; every other emergency case with a life-threatening or time-critical problem drifts its heart rate 5 to 10 every 30 minutes until a fluid bolus or an antibiotic is given (SR-0096, provisional). The repeat lactate of 2.4 is not applied yet (DT-074).
- **Decision:** accepted for the walking skeleton; the clinician reviews the plots under EXP-PHY-1.
