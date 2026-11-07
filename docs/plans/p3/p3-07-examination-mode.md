# p3-07-examination-mode — Test mode: no guidance, lightbulb hints with a cost, Submit Patient to OT (EXM)

Status: Planned (P3, 7 November 2026). Nothing in this plan is implemented. Needs owner decisions D1 (pool), D5 (hint cost) and D6 (where the examination ends).

## 1. Objective and final user-facing behaviour

In the examination there is no checklist, no next-step guidance and no automatic hint. A small glass panel on the left wrist (and a world panel near the bay entrance) shows a large **lightbulb**. Pressing it, or saying "I'd like a hint", asks "Use a hint? It costs N points." and then shows one controlled hint (a nudge, not the answer). The panel shows how many hints were used. When the player believes the patient is ready, they select the patient and press the large **Submit Patient to OT** button, or say "Submit the patient to the operating theatre". A confirmation summarises what happens ("The examination will end and your preparation will be assessed."). The examination then ends and the results transition begins. Submitting never makes a session successful by itself: everything incomplete, wrong or unsafe is assessed.

## 2. Current implementation status

- `SessionMode.Assessment` exists; `ModeGate` hides practice aids; Core hides goals and the hint in assessment (`ObservableState.cs:455-457`). **Verified.**
- Assessment cannot start any case (D1, see [p3-audit.md](p3-audit.md)). **Blocked.**
- No hint-on-request with a cost. **Missing.**
- Session end in Core: the end-state resolver ends a case when the trainee declares the outcome (`declare_outcome`), when the WHO Time Out makes the patient ready for incision, or when the case is stopped (`EndStateResolver.cs:117-120`); transfer to theatre exists (`transfer_to_theatre`); theatre readiness is `declare_or_ready` (OR stations, not the patient). There is no single "submit patient" action. **Partial.**
- The current finish is the tablet's "Finish session and upload" (`SessionTabView`). **Verified.**

## 3. Existing code, assets, scenes and systems to reuse

Core `DecisionSubsystem` (`declare_outcome`), `TransportSubsystem` (`transfer_to_theatre`), `HandoverSubsystem` (`finish_handover`), `EndStateResolver`, readiness blockers in `content/workflow/workflow.json`, `CaseGraph` goal hints, `Scorer`, `content/score_items.csv`, `ModeGate`, `SessionTabView.FinishSession`.

## 4. Missing functionality

Engine hint request with deterministic hint selection and a scored cost; hint counter; the submit action with confirmation; ending the examination at submission and assessing incomplete preparation; abandon handling; an audit that no practice aid leaks into the examination.

## 5. Architecture and integrations

- **Hint (engine verb `request_hint`):** logged like any action, so replay and server verification see it. The engine chooses the hint deterministically: the earliest pending, applicable goal whose prerequisites are complete; tier 1 is the goal's label as a question ("Have you confirmed who the patient is?"), tier 2 (a second request on the same goal) is the goal's `hint` text. In assessment each request adds a score item `hint.used` (penalty from `content/scoring`, value set by D5); in practice the cost is 0. The hint never applies an action and never names a hidden finding.
- **Submit (engine verb `submit_patient`, D6):** the trainee's declaration that preparation is complete and the patient goes to theatre. The engine records it; if no outcome has been declared it is taken as "proceed"; it requests the theatre transfer; then the end-state resolver closes the case and every open readiness blocker, missed goal and unsafe state becomes a score item exactly as the existing rules define. Further actions are refused with `CASE_ENDED`.
- **Abandon:** returning to the menu or quitting during an examination applies `submit_patient` with `abandoned = true`: end state recorded, result FAIL, shown as "Examination not completed".
- Menu, voice and the patient panel all send the same two requests through the gateway (P3-SES-03).

## 6. Implementation tasks

| ID | Task | Owner | Depends on | Done when | Status |
|---|---|---|---|---|---|
| P3-EXM-02 | Owner decisions D5 (hint cost and maximum, whether hints are allowed in the examination at all) and D6 (examination ends at submission; theatre steps stay a training extension) | Owner | — | Decision rows recorded | Planned |
| P3-EXM-01 | Core verb `request_hint`: deterministic choice, two tiers, logged, `hint.used` score item with the D5 cost in assessment and 0 in practice; engine version raised; new goldens | Ruhan | P3-EXM-02 | xUnit: same log → same hints; hint never changes clinical state; cost applied only in assessment | Planned |
| P3-EXM-05 | Core verb `submit_patient` per D6: outcome defaulting to proceed, transfer request, case closed, open blockers and missed goals scored by existing rules, later actions refused; `abandoned` flag | Ruhan | P3-EXM-02 | Golden runs for all 16 P0 cases: complete → existing verdict; submit at start → FAIL with the missed items listed | Planned |
| P3-EXM-03 | Lightbulb panel: large icon, hint counter, confirmation with the cost, hint text card; wrist and world placement | Moiz | P3-EXM-01, P3-UIX-05 | Works in both input modes | Planned |
| P3-EXM-04 | Submit Patient to OT: large button on the patient panel (menu and voice mode), voice intent (P3-VOX-08), confirmation dialog, then `submit_patient` and the flow's Ending state | Moiz | P3-EXM-05, P3-SES-03, P3-CTX-04 | One submission per session; double confirm impossible | Planned |
| P3-EXM-06 | Abandon path: pause menu Return/Quit during an examination submits with `abandoned = true` after its confirmation | Moiz | P3-EXM-05, P3-MNU-08 | Abandoned session uploads and shows "not completed" | Planned |
| P3-EXM-07 | Examination presentation audit: every view checked for practice-only content (board, goal text, nurse prompts, auto hint, "next step", greyed-action reasons that reveal the order); a PlayMode test fails on any leak | Moiz | P3-TRN-03, P3-CTX-04 | Leak test green | Planned |
| P3-EXM-08 | Timing: record stage durations and total time in the result summary (no time pressure added) | Ruhan | P3-EXM-05 | Times appear in `ResultSummary` | Planned |
| P3-EXM-09 | Tests: hint determinism and cost, submit and abandon goldens, leak test, gateway duplicate protection on submit | Ruhan | P3-EXM-01, P3-EXM-05, P3-EXM-07 | Green in CI | Planned |

## 7. Dependencies and execution order

Decisions → engine verbs (hint, submit) → panels → abandon → leak audit → tests. Examination play also needs D1 (P3-SCN-07).

## 8. Frontend components

Wrist lightbulb panel, world lightbulb panel, hint confirmation, hint card, Submit Patient to OT button, submit confirmation, "examination ended" transition (P3-RES-03).

## 9. Backend and API changes

None for gameplay: the server replays the log and so verifies hints and submission. The session report and dashboard show hints used (read from the log; P3-API-06).

## 10. AI and voice integration

Hint and submit intents in the interpreter catalogue (P3-VOX-08). Hints are engine text, not model text, so no model can reveal an answer.

## 11. Unity objects, prefabs, animations and assets

Lightbulb icon and glow (P3-UIX-06), submit button style (danger-confirm variant), sounds (P3-SND-05).

## 12. Performance

Negligible.

## 13. Error handling and edge cases

Hint when no goal is pending ("You have covered the steps this simulator checks."); hint while the patient is away; hint limit reached (D5); submit while a transport is running; submit while a result is being phoned; voice "submit" misheard (confirmation always required); headset removed during the confirmation.

## 14. Testing

Core xUnit and goldens; PlayMode leak test; replay verification on the server of a session with hints and a submission.

## 15. Acceptance criteria

1. An examination session can be completed and submitted in both input modes.
2. A premature submission gives FAIL with the missed and unsafe items listed.
3. Hints used are counted, costed per D5 and identical on replay.
4. No practice aid appears anywhere in the examination.

## 16. Definition of Done

All EXM tasks Done; decisions recorded; ADR for the two verbs; goldens and CI green.
