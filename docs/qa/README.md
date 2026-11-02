# Visual QA with ChatGPT (mandatory for every visual task)

Established 1 Nov 2026 (UTC). Every significant visual change goes through this loop and nothing is called finished on the engineer's judgement alone.

**Implement, capture real Unity URP screenshots, strict ChatGPT review, fix, recapture, verify, repeat.** A scene passes when ChatGPT returns `VISUALLY_APPROVED` in **two consecutive independent rounds** with no new critical or major issue and every critical/major register entry is `VERIFIED`. ChatGPT's approval is an internal gate, not a replacement for the owner's final decision.

## Who reviews

The project's existing authorised integration: the `chatgpt_plan` adapter of the creative toolkit (`docs/3d-blender-unity-skills`, "Sign in with ChatGPT", the owner's own plan, no paid API; live-tested 27 Oct 2026, used for the hospital bed and the monitor). If ChatGPT is unreachable the tool writes `FAILED.txt` and exits non-zero; no other model may stand in for it and no approval may be claimed.

## Tools (`unity/Tools/QA/`)

| File | Purpose |
|---|---|
| `chatgpt_review.py` | Sends real captures plus the approved PatientBay benchmark with a fixed strict prompt (geometry, materials, lighting, placement, characters, environment, missing details; severity CRITICAL / MAJOR / MINOR / ENHANCEMENT). Returns a structured report: issue id, region, severity, why it looks wrong, correct result, root-cause hypothesis, exact fix, expected improvement, verification evidence; a verdict (FIXED / PARTIAL / STILL_PRESENT / NOT_VISIBLE) on every previously open issue of the scene; an approval decision. The tool itself withholds an approval if any critical or major issue is listed. The prompt is not tuned between rounds. |
| `qa_register.py` | Persistent register `docs/qa/visual_qa_register.json` (rendered to `visual_qa_register.md`): `ingest`, `set`, `list`, `gate`, `render`. Statuses OPEN, FIXING, READY_FOR_REVIEW, VERIFIED, BLOCKED_WITH_EVIDENCE. Issues are never deleted; VERIFIED needs a ChatGPT FIXED verdict or screenshot evidence. |
| `qa_capture.ps1` | Captures the full view sets in `views/` (patientbay, corridor, reception, ot, exterior): wide, eye-level, opposite direction, close-ups, architectural, lighting and ceiling views. |
| `benchmark/` | The approved Enhanced URP PatientBay look used as the reference in every review. |

## One round

1. Fix whatever the last round listed (critical first, then major; group by object; engineering checks stay authoritative for measurable facts, e.g. clearances, collisions, tests).
2. `powershell unity/Tools/QA/qa_capture.ps1 -Tag qa<N>` (one Unity at a time).
3. `python unity/Tools/QA/chatgpt_review.py --scene <Scene> --round <N> --images <captures> --benchmark unity/Tools/QA/benchmark/patientbay_approved_wide.png --focus "<facts only>"`.
4. `python unity/Tools/QA/qa_register.py ingest unity/Logs/QA/<Scene>/round_NN/review.json`.
5. Work the open issues (`qa_register.py list --open`), set `READY_FOR_REVIEW` only with new rendered evidence, repeat from 1.
6. `python unity/Tools/QA/qa_register.py gate` reports pass/fail per scene.

Animation and cloth: capture before/during/after poses (gown during knee bend, leg rotation, lying to standing; nurse clothing; curtain; idle) and also run the Unity physics, animation and collision tests; a visual approval is never proof that a simulation is correct.

A technically unavoidable limitation is recorded as `BLOCKED_WITH_EVIDENCE` and reported honestly.
