# p3-06-training-mode — Guided training: bulletin board, live ticks and free hints (TRN)

Status: Planned (P3, 7 November 2026). Nothing in this plan is implemented.

## 1. Objective and final user-facing behaviour

In training, a small glowing board icon sits at the upper right of the player's view edge and on the left wrist. Selecting it opens a floating glass **bulletin board** in front of the player: the current objective at the top, then the preparation stages (identity, history, observations, examination, investigations, consent, fasting and medicines, ward checklist, handover) with their steps, what is done, what is left, and "how to do this" for the selected step in the current input mode (menu or voice). When the engine records a step as done, the board ticks it with a short animated checkmark and a soft chime, and a small toast appears even if the board is closed. Steps that do not apply to this patient disappear with a note ("Not needed: the patient is not diabetic"). Hints are free in training and explain instead of doing: a wrong attempt shows why it was refused and what to check.

## 2. Current implementation status

- Core goals: `content/workflow/workflow.json` holds 16 goals with `step`, `label`, `hint`, `applies_if`, `complete_if`, `after`; the case graph publishes `GoalStatus` (`pending`, `complete`, `missed`) for active goals only, in practice mode (`ObservableState.Goals`). **Verified.**
- Flow stages: `ObservableState.Flow` (`FlowStageStatus`) and the 15-step case graph. **Verified.**
- Practice hint: `Tracker.Hint()` at beginner level only (`ObservableState.cs:457`). **Verified.**
- Ward checklist panel (`WardChecklistPanel`) and WHO board exist; placed in scenes on Sannan's branch. **Not integrated on main.**
- No trainee-facing objectives board, no tick animation or sound, no "how to" text per input mode. **Missing.**

## 3. Existing code, assets, scenes and systems to reuse

`ObservableState.Goals/Flow/Hint`, `CaseGraph` goal hints, `HintTexts` (refusal wording), `WardChecklistPanel`, `ModeGate`/`PracticeOnly`, `SubtitleView` (toast placement rules), `Readability.cs`.

## 4. Missing functionality

Objective structure finer than the 16 goals (stages and their steps); "how to" text per step and input mode; the board view and its summon; tick feedback; hint request in training; retry comparison.

## 5. Architecture and integrations

- **Data:** `content/ui/training_guide.json` keyed by goal id, flow stage id and ward checklist item id: section, order, how-to (menu), how-to (voice, with example sentence), short explanation, source register id. Generated or authored by Ruhan; validated (every active goal and stage has an entry; no clinical value that is not already in content).
- **State:** the board reads only the engine's observable goals, flow stages and checklist evidence through `SimFeedback`; it never decides completion. Conditional goals that become inactive are shown as "not needed" from the change in the active list.
- **Hints:** a hint in training is the same engine request as in the examination (P3-EXM-01) with zero cost in practice, so both modes share one hint source.

## 6. Implementation tasks

| ID | Task | Owner | Depends on | Done when | Status |
|---|---|---|---|---|---|
| P3-TRN-01 | `content/ui/training_guide.json` (sections, order, how-to per input mode, explanation, source ids) and its validator in CI | Ruhan | — | Every goal, stage and ward item of S1 and S2 covered; validator green | Planned |
| P3-TRN-02 | Core: publish goals that became inactive (not needed) with the reason id, and the goal's stage, in practice mode only | Ruhan | — | xUnit: a non-diabetic case lists the glucose step as not needed | Planned |
| P3-TRN-03 | Bulletin board view: current objective, sections with steps (done, open, not needed, missed), how-to expander for the selected step in the active input mode | Moiz | P3-TRN-01, P3-UIX-05 | Board renders S1 and S2 from data only | Planned |
| P3-TRN-04 | Summon: upper-right peripheral icon (head-locked at 35° right, 20° up, small) and left-wrist button; board opens world-locked 0.8 m ahead, closes with the same control | Moiz | P3-TRN-03 | Comfort review: no permanent overlay in the centre of view | Planned |
| P3-TRN-05 | Completion feedback: tick animation, chime, toast when the board is closed, driven by `SimFeedback` goal events | Moiz | P3-SES-10, P3-SND-05, P3-UIX-07 | One tick per engine completion, never more | Planned |
| P3-TRN-06 | Training hints: board "hint" button and voice "hint" use the hint request (free in practice); wrong attempts show the refusal reason and the how-to | Moiz | P3-EXM-01 | Hint never applies an action (test) | Planned |
| P3-TRN-07 | Retry comparison: after a retry, results show this attempt beside the previous one (time, missed steps, verdict) | Moiz | P3-RES-06 | Comparison uses saved summaries only | Planned |
| P3-TRN-08 | Tests: board from data, conditional steps, tick-once, hint-no-action, mode gate (board absent in the examination) | Moiz | P3-TRN-05, P3-TRN-06 | Green in CI | Planned |

## 7. Dependencies and execution order

Data and Core (TRN-01, TRN-02) first, in parallel; the board after the UI kit; feedback after the event stream; hints after the engine hint request.

## 8. Frontend components

Peripheral board icon, wrist button, bulletin board panel, section list, step row (state icon, label), how-to expander, toast, hint button.

## 9. Backend and API changes

None.

## 10. AI and voice integration

Voice "hint" and "show my checklist" open the same views. How-to texts for voice give example sentences that the interpreter accepts (checked by P3-VOX-12's sentence list).

## 11. Unity objects, prefabs, animations and assets

Board prefab, tick animation (P3-ANI-09), chime (P3-SND-05), board icon (P3-UIX-06).

## 12. Performance

Board updates on events only; one canvas; text meshes rebuilt only on change.

## 13. Error handling and edge cases

Goal completed while the board is closed; alternative valid workflows (several actions complete one goal); a missed goal (shown as missed with a short why); patient away; mode switched while the board is open.

## 14. Testing

Validator tests, Core xUnit for not-needed goals, PlayMode for ticks and hints, captures for review.

## 15. Acceptance criteria

1. A beginner can finish S1 using only the board's guidance.
2. Every tick corresponds to an engine completion event (log comparison).
3. The board never appears in the examination.

## 16. Definition of Done

All TRN tasks Done; usability notes from P3-QA-07 addressed; headset comfort in DC-14.
