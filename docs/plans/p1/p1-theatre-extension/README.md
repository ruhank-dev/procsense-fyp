# p1-theatre-extension

**Tier:** P1 · **Builds on:** p0-scenario-s1, p0-scenario-s2, p0-blender-equipment · **Feeds:** p1-evaluation

## Goal
Extend the journey past Time Out: labelled intra-operative training events (ProcSense extension, not surgery itself),
Step 15 WHO Sign Out (counts, specimen labelling), recovery handover, and their scenes + equipment.

## Scope
- **In:** Step 15 engine (checklist data already exists from P0), intra-op event set (walkthrough C.22–C.24, F/G stages 14–17),
  recovery handover in the reused patient bay, P1 equipment scripts (specimen container + label, recovery bay setup — reusing monitor/oxygen/suction).
- **Out:** performing the operation, anaesthetic drugs/doses (out of scope per walkthrough 2.3).

## Decisions
| Decision | Chosen | Alternatives | Why | Revisit if |
|---|---|---|---|---|
| Intra-op content | Communication/teamwork events only, clearly labelled extension | Surgical procedure simulation | Walkthrough scope | — |
| Recovery bay | **Reuse the patient bay layout** (approved phase review P6) | New recovery room; fade-to-black | Handover is a conversation; little physical interaction | — |

## Experiments
| ID | Question | Method | Success metric |
|---|---|---|---|
| EXP-TX-1 | Does Sign Out count-discrepancy branch teach as intended? | Testers + debrief comprehension | Correct escalation recognised |

## Exit
Step 15 + recovery handover playable for S1 and S2; specimen labelling checked.

## Sources
Walkthrough Parts 11 (Step 15), 15, C.22–C.24, F/G 14–17, E; Implementation Guide B-S12, B-R15.
