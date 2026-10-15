# p0-diagnostic-transitions

**Tier:** P0 · **Builds on:** p0-clinical-logic (investigations, turnaround), p0-simulation-workflow (transport), p0-characters (walking), p0-environment-rooms (doors) · **Feeds:** p0-patient-records-emr, scenarios

## Goal
Category-B locations work as a believable loop instead of modelled rooms:

```
Trainee orders test (tablet/EMR) → chooses how the patient goes (walk / wheelchair / trolley + escort + monitor)
→ patient (or porter with samples) goes to the signed door (CT Scan / Imaging / X-ray / Laboratory / MRCP)
→ enters → wait (~10 s on screen = test turnaround on the sim clock) → comes back out
→ carries the physical report → places it on the desk / in the folder
→ the EMR computer updates automatically (report, values, images)
→ critical result? → lab phone call + read-back + acknowledgement (blocker until done)
```

## Scope
- **In:** door + sign-plate assets (one per B location), navigation paths (NavMesh) from bay to doors, patient walk-in/walk-out
  animation, porter carrying samples, wheelchair/trolley transport with escort NPC, wait/processing overlay (time-skip on the sim clock),
  report hand-back, automatic EMR update event, corridor deterioration event hook, result arrival notifications.
- **Out:** modelling CT/lab/imaging interiors (not needed), the EMR UI itself (p0-patient-records-emr).

## Deliverables
Transition controller (Unity) driven by Core events (`InvestigationRequested` → `PatientDeparted` → `ResultReady` → `PatientReturned`);
door prefabs with sign plates; NavMesh setup; wait overlay; report hand-off interaction; tests that replay the loop headless.

## Decisions
| Decision | Chosen | Alternatives | Why | Revisit if |
|---|---|---|---|---|
| Wait length on screen | ~10 s (prototype), sim clock jumps by the test's turnaround | Real-time turnaround; instant | User direction; keeps S2 deterioration honest | Testers find it too short/long |
| Who goes through the lab door | Porter / tube station with labelled samples | Patient walks | Bedside labelling lesson (walkthrough Step 6) | — |
| How patient travels | Trainee chooses walk / wheelchair / trolley + escort; choice scored per stability | Always walks | Walkthrough transfer rules (unstable → monitored) | — |
| What the trainee sees during the wait | Patient off-screen; trainee can keep working (other tasks, S2 parallel tracks) | Freeze game | S2 overlap is core teaching | — |
| Report delivery | Patient/porter returns with printed report + EMR auto-update | EMR only | User direction: both physical and computer | — |
| Trainee at the door | Can follow the patient along the corridor to the door, **can't go through**; message explains why | Trainee enters the room | User decision: B rooms are not modelled | — |

## Experiments
| ID | Question | Method | Success metric | Status |
|---|---|---|---|---|
| EXP-DT-1 | Do trainees understand where the patient went and when results are back? | Testers, no instructions | ≥ 4/5 understand | planned |
| EXP-DT-2 | Does an untreated S2 patient deteriorate correctly during a CT trip? | Headless replay with time-skip | Matches deterioration clock | planned |
| EXP-DT-3 | NavMesh walking on Quest within budget? | Profile | Within EXP-VR-1 budget | planned |

## Exit
Every investigation in the P0 cases has a working transition; transport choice is scored; EMR updates on return; critical calls still block.

## Who does what
Developer: Core events, transition controller, NavMesh/animation scripts, door/sign Blender assets, tests. Team: headset feel, door placement in the room model.

## Sources
Walkthrough Part E (#16–#24, #38), Step 6, Step 12, Part 5 critical results; inventory D202 (escort rule), D204 (turnaround).
