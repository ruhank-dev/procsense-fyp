# p0-physiology-events-behaviour

**Tier:** P0 (Pulse engine option in p1-pulse-physiology) · **Builds on:** p0-clinical-logic · **Feeds:** scenarios, vr-interaction-ui, characters (behaviour → animation)

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
| ID | Question | Method | Success metric |
|---|---|---|---|
| EXP-PHY-1 | Is LitePhysio realistic enough? | VAR-S2-PD (Usman) timeline plots reviewed by medical professional | Accepted, or fix list |
| EXP-PHY-2 | Deterioration clock reproduces the Usman timeline? | Headless run vs walkthrough | NEWS2 progression matches |
| EXP-PHY-3 | Tick cost on Quest | Profile | < 0.5 ms |

## Exit
Usman timeline reproduced; all 47 what-ifs load and have a test; trajectories accepted.

## Who does what
Developer: all + plots. Medical professional: trajectory review.

## Sources
Walkthrough Parts 4.11, 12–14, 17; Implementation Guide B-R13, B-R14, B-R6.
