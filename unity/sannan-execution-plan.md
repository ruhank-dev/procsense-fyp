# Sannan's implementation plan and work log

Created: 10 October 2026 (Asia/Karachi). Owner: Sannan (Sannan47). Implementation: the assistant working with Sannan. Scope of this change: planning only.

## 1. Instructions to read before every work session

These are Sannan's explicit instructions from 10 October 2026 and apply to all work carried out using this plan:

1. The assistant implements Sannan's tasks, maintains this plan and records what is done and what remains.
2. After every implementation step, the assistant verifies the result itself. Run meaningful checks for that change, inspect the actual result, fix failures and verify the fixes before handing over.
3. Then Sannan manually verifies the work. Provide exact instructions, expected results and evidence so that review is practical.
4. Do not commit until Sannan has explicitly accepted the implementation after manual review.
5. Before **every commit**, show Sannan the exact proposed commit subject and body and ask for approval. Approval of the work does not automatically approve the message. A changed message requires approval again. This includes documentation, fixes and merge commits.
6. Do not stage unrelated changes or include someone else's work in a commit. Do not push or update main without authorization for that action.
7. The initial request authorized creating this plan only. Later explicit requests authorize the individual work recorded below; do not infer authorization to start the entire roadmap. On 10 October 2026 Sannan authorized reorganizing the Unity application into familiar asset folders and improving the editing workspace. Staging, commit and push still require their respective approvals.

The older repository workflow says no approvals are required. Sannan's newer instructions above take precedence for this work. Preserve repository checks and Git safeguards; add these review gates rather than bypassing them. No number of tests proves the absence of every bug: report the actual checks, known issues and untested conditions honestly.

**Quality requirement (10 October 2026):** Sannan requires polished UI, animations and procedurally authored assets, proper collisions, smooth movement and reliable interactions. Treat the current greybox as foundation work, not the final visual quality bar. Design and profile for Quest 3 against section 7's measured budgets; do not promise zero frame drops from Editor results. Quest 3 is not yet available from the supervisor: continue Editor-verifiable work and leave APK/device replay, comfort and device performance acceptance explicitly pending.

Push communication preference (10 October 2026): always state the destination branch before an authorized push and report it in the handoff. Commit authorization does not authorize pushing.

Commit communication preference (11 October 2026): every implementation handoff must include the exact proposed commit subject and whether it has a body, without waiting for Sannan to ask. Exact-message approval remains required before committing.

Batching preference (11 October 2026): Sannan requested doing several related steps at a time to speed progress. Group cohesive work into a complete reviewable batch, self-verify throughout, then give one manual-review handoff and commit-message proposal for that batch. Do not stop for approval after each small internal fix. Keep scope boundaries, final implementation/message approval and dependency gates. This supersedes the earlier one-small-step-at-a-time handoff cadence, not verification requirements. A request to outline batches does not authorize their implementation.

At the start of a future session, read this section, the progress ledger and blockers, inspect the working tree, then read the original step specification for the next task. If code or dependencies changed since the last verification, reassess the evidence before proceeding.

## 2. Assignment and source of truth

Sannan owns the Unity application and its presentation/input layer. The Implementation Guide explicitly assigns these work packages:

| Package | Sannan's responsibility | Tier |
|---|---|---|
| B-S6 | Unity project, XR rig, locomotion and headset-free setup | P0 |
| B-S7 | SimulationHost bridge, action emitters, state views and SceneDirector | P0 |
| B-S8 | Supervisor environment import, optimisation, anchors and room layout | P0 |
| B-S9 | Interactions and operational equipment state presentation | P0 |
| B-S10 | Tablet, monitor, results viewer and checklist boards | P0 |
| B-S11 | Quest builds, profiling and Unity CI | P0 |
| B-S1 | Procedural Blender equipment and prop assets | P0, with P1 items later |
| B-S2 | Patient signs, blend shapes, shaders and behaviour animation mapping | P0 |
| B-S3 | Staff rigs, roles, animation and navigation | P0 |
| B-S4 | Voice client, push-to-talk and fallback; subtitles and lip sync earlier | P1; subtitles/lip sync P0 |
| B-S5 | Debrief and difficulty-explanation UI | P0 template; P1 AI text/difficulty |
| B-S12 | Theatre extension, Sign Out presentation and recovery scenes | P1 |

Primary references (paths relative to this document):

- [Implementation Guide](../docs/context/ProcSense_Implementation_Guide_from_PDF.md): ownership table and B-S1 through B-S12, interfaces, tests and acceptance criteria.
- [Team Board](../docs/context/ProcSense_Team_Board.md): ownership and walkthrough responsibilities. Weekly dates and old status entries are historical.
- [Current phase plan](../docs/plans/README.md), [milestones](../docs/plans/milestones.md), [working model](../docs/plans/working-model.md).
- [Walking skeleton 1](../docs/plans/p0/walking-skeleton-1.md), [approved data-first order](../docs/plans/p0/data-first-order.md).
- [Equipment inventory](../docs/plans/p0/p0-blender-equipment/equipment-list.md), [interaction specification](../docs/plans/p0/p0-vr-interaction-ui/equipment-interaction-spec.md), [research review pack](../docs/research/review_pack.md).
- [Deferred tasks](../docs/deferred-tasks.md), [CI checks](../docs/CI.md), [Git workflow](../docs/TEAM_GIT_WORKFLOW.md).

This file consolidates execution; it does not replace the original medical source, phase specifications or architecture decisions. P0, P1 and P2 are dependency/build order, not permission to discard later scope. No deadlines are imposed here.

## 3. Starting position: inspect before rebuilding

The following is a read-only inspection baseline, not a fresh passing-test report:

| Area | Evidence already present | Remaining evidence/work |
|---|---|---|
| Unity setup | `unity/`, Editor 6000.5.10f1, URP, OpenXR, XRI, local Core package | Compile and verify on this machine and Quest; audit settings/package requirements |
| Skeleton 1 | Boot, Sys_Core, Skeleton1_PatientBay and DevOnly scenes; bridge, emitters, fallback registry and views | Re-verify existing interactions and finish device/replay gate |
| Engine | Deterministic Core, CLI/replay, identity/history/observations, rule/goal framework | Other owners' clinical systems needed for later UI integration |
| Cases | 53 detailed files in `content/case_files/` and a strict loader | Normal sessions still use the smaller CaseCard path; full case runtime integration is a dependency |
| Data | Clinical tables, historical records, images/reports, 53 equipment reference sheets | Consume through real runtime contracts; preserve approval/provenance and open reference issues |
| Art | Greybox scene generator and room manifest | Supervisor shells, procedural asset pipeline, final equipment and characters |
| Backend | `server/README.md` | P1 endpoints are a dependency, not existing functionality |
| Verification | xUnit tests, golden scripts, Unity EditMode/PlayMode tests and build tools | Run the appropriate checks later; no current pass claimed here |
| Live Editor control | Unity CLI installed; Pipeline package absent from checked manifest | Connect only during authorized implementation; verify actual availability first |

Existing approved step filenames mean **historically approved**, not **re-verified by this assistant** and not **accepted by Sannan in this workflow**. Keep these distinctions in the ledger.

Pre-existing working-tree changes observed while preparing this plan, to leave untouched:

- Modified `unity/Assets/UniversalRenderPipelineGlobalSettings.asset`.
- Untracked `src/ProcSense.Core/Rules.meta`, `Rules/Logic.cs.meta`, `Rules/RuleEngine.cs.meta`, `Workflow/CaseGraph.cs.meta`, `Workflow/CaseGraphSubsystem.cs.meta`.

Re-check this list at the start of implementation. Do not delete generated metadata or assume these files belong to a future task. Shared Core-package `.meta` files may be needed, but inclusion must be explicitly reviewed.

## 4. Boundaries and integration contracts

- Unity is view and input. Clinical truth, readiness, timing, physiology, scores, investigation completion and end states belong to Core/content.
- Send trainee interactions through `SimAction` and `Session.Apply`; render only permitted observable state. Do not change clinical state from a Unity component or infer a clinical outcome from an animation finishing.
- Ruhan supplies Core, physiology/events, checklist and decision services; Moiz supplies content/schema, investigations/treatment, calculators, scoring/cases and offline voice production. These are dependencies and integration responsibilities, not silently reassigned implementation ownership.
- UI that needs an unavailable contract can be developed against explicit test fixtures. Fixture demonstrations must remain visibly development-only and do not satisfy end-to-end acceptance.
- For each handoff, record: action/event IDs, payload, observable fields, rejection reasons, update timing, case/content hash, reference tests and the supplying task. Compare planned names with the real API before writing code.
- No clinical thresholds, doses or patient facts baked into UI, meshes or animation. Patient labels and screens use live data. Medicines follow request -> clinician confirmation -> nurse administration.
- Physical folder contains the current episode; EMR contains current plus historical information. Results cannot appear before Core publishes them. Content and image review status must be enforced by the applicable runtime gate.
- Category A: one bay with S1 ward/S2 ED variants, OT, corridor and desks. Category B: signed diagnostic doors; trainee cannot enter. Category C: records/results/UI. Lab samples travel; the patient stays at bedside for blood sampling.
- Every equipment/prop asset comes from our procedural Blender scripts. Free equipment models are references only. Human base meshes/animation candidates require approval and licence checks before adoption. Supervisor room shells remain the room source.
- Sannan reviews spatial comfort, reach, visual quality and headset behaviour. The assistant supplies implementation, automated checks, inspected previews and reproducible evidence; it cannot personally wear the headset.

## 5. Execution sequence

Each numbered stage below is split into small reviewable steps in the detailed source checklist later in this file. Never implement an entire stage and leave verification until its end. Apply section 6 after each step.

### Stage 0 — Establish a reproducible baseline

1. Inspect Git status, current branch, local instructions and installed tool versions; preserve existing changes.
2. Confirm Unity 6000.5.10f1 and Android modules; check Blender, .NET 9, Task, uv, Git/LFS and device tooling as required. Do not upgrade unrelated packages to satisfy a guessed requirement.
3. Open the existing project using its pinned Editor. Check compile errors and XR validation. If using live Editor access, connect the Unity CLI/Pipeline to this exact project and record the connection test. Fall back to reproducible Editor tooling when unavailable.
4. Establish baseline Core/golden/Unity tests and APK build. Save logs outside tracked source folders. Separate pre-existing failures from regressions.
5. Inventory existing scenes, prefabs, emitters and tablet coverage. Map real files to historically approved skeleton steps rather than recreating working components.
6. Complete the skeleton-1 Quest gate: Sannan plays the original identity/history/vitals path; export the device log, replay on PC, compare hashes and record performance/readability findings.

Exit: verified baseline with explicit remaining failures; device gate either verified or named as blocked by hardware/access. Never label a simulator pass as a Quest pass.

### Stage 1 — Finish scene lifecycle and input foundations

1. Keep a single Session and persistent service ownership across additive room loading. Prevent duplicate hosts, cameras, XR rigs and event systems.
2. Implement room loading/fades and location-anchor registry; create corridor and OT greyboxes without resetting patient/session state.
3. Configure teleport areas/anchors, snap turn and seated play. Check valid/invalid targets, collision bounds and no entry through diagnostic doors.
4. Audit action debouncing, grab/socket/poke/UI emitters and tablet fallbacks; one intent produces one logical action.
5. Test scene unload/reload, subscription cleanup, repeated entry, pause/resume and error handling. Cosmetic movement must not write clinical truth.

Exit: bay -> corridor -> OT -> bay works, interaction routes remain available and equivalent logical action sequences reproduce the expected Core results.

### Stage 2 — Deliver skeleton 2: records and diagnostics

Dependencies: runtime investigation/result/transport contracts, locations/readiness from Core, case bindings from content owners.

1. Build the UI shell and verified order/transport adapters; unavailable runtime features remain development fixtures.
2. Place signed B doors and block trainee entry. Show destination and current patient/sample status.
3. Present stable walking, wheelchair or monitored trolley with escort according to Core. Handle unreachable routes and cancellation/restart safely.
4. Show processing using Core time-skip and attention-stop semantics; preserve deterioration and other work during waits.
5. Deliver report and synchronize folder/EMR; critical calls require separate acknowledgement and cannot be bypassed by reading a report.
6. Test normal arrival, wrong/blocked order, unsuitable transport, duplicate events, interrupted wait and an untreated deteriorating patient.

Exit: genuine order -> transport/sample -> wait -> result -> record update loop, reproduced headlessly from the emitted actions.

### Stage 3 — Deliver skeleton 3: OT and safety boards

Dependencies: readiness, consent/preparation, WHO Sign In/Time Out and transport runtime services.

1. Build a greybox OT with stations, anchor positions and staff stand-ins.
2. Wire consent/preparation/transfer UI to real blockers and rejection feedback.
3. Display exact checklist wording from content. Confirmed ticks require Core evidence; scene-local ticks cannot unlock progression.
4. Present team introductions, staff responses and Time Out progression without simulating the operation itself in P0.
5. Verify correct progression, missing-item stop, repeated skips, unsafe transfer and appropriate postponement/redirect outcomes.

Exit: the initial S1 case reaches its expected P0 end state in a reproducible greybox path. Unavailable engine components stay recorded as blockers.

### Stage 4 — Establish the Blender pipeline and quality bar

1. Pin Blender; build headless and interactive runners with factory-startup isolation, shared helpers and per-asset scripts.
2. Implement naming, geometry, materials, UVs, LODs, interaction empties and FBX export in metre scale for Unity Y-up.
3. Build validators for budget, body dimensions, transforms, topology, UV/materials and required attachment points. Test deliberate invalid assets so the validator demonstrably catches errors.
4. Render front/side/top/three-quarter views plus a 1.7 m scale reference. Inspect every render before handoff.
5. Pilot monitor, IV pole with pump and wristband. Open dimension rows remain explicit placeholders until sourced; do not claim their dimensions verified.
6. Import into URP, bind named points/materials, create prefabs and verify grab/socket/screen behaviour. Obtain Sannan's acceptance of the pilots before mass production.

Exit: repeatable build/export/import, inspected previews, validated pilot assets and a manually accepted quality bar.

### Stage 5 — Generate and integrate every equipment family

Use the source inventory and per-item interaction specification as the complete roster, including room dressing and P1 items. The number of research sheets is not the number of required 3D meshes: some entries are UI or door/report representations.

1. Identity/documents: wristband, allergy band, folder/pages, forms, marker and readable live labels.
2. Bedside monitoring: monitor, cuff, probe, thermometer, stethoscope, ECG unit/electrodes and glucose/ketone meter.
3. Sampling/testing: blood kit/tubes/labels, culture bottles, urine items and disposal. Preserve bedside identity/labelling teaching.
4. IV/oxygen/airway: cannula, lines/bags/pole/pump, supply/flowmeter/masks, suction, NG/catheter, stockings/IPC and warming items.
5. Furniture/transport: bed, wheelchair/trolley, portable monitoring, desks and workstation.
6. OT machines: table/positioning, anaesthesia unit, lights, capnography, lap tower/cylinder and electrosurgery.
7. OT storage/sterility/hygiene: instrument and airway/resuscitation trolleys, trays/drapes, cupboards, sink/PPE and waste handling.
8. Boards/doors/signs and other listed props. For every item map: source row -> script -> exported prefab -> placement anchor -> interaction -> test -> review status.
9. Validate and visually inspect each asset, then repeat the actual interaction with the greybox replaced. Profile complete rooms; individual asset budgets do not prove scene performance.

Exit: every P0 inventory row has its required representation and working interaction, with no untracked decorative stand-ins counted as complete.

### Stage 6 — Integrate supervisor shells and final layouts

1. Inventory provided model files and permissions; keep originals intact. If unavailable, retain greybox and name this dependency.
2. Convert/import materials, scale and axes; classify shells A/unused and preserve the A/B/C scope.
3. Generate named anchors/manifests, one bay dressed as ward/ED and a fully equipped OT; diagnostic doors remain corridor endpoints.
4. Validate all required equipment placements, staff clearance, seating/reach, teleport targets and NavMesh routes.
5. Bake lighting/probes, apply colliders/occlusion and texture compression; compare measurements before/after optimisation.

Exit: reproducible placement, readable signs/screens, reachable equipment and measured room budgets, accepted in-headset by Sannan.

### Stage 7 — Characters, clinical signs and nurse workflow

1. Evaluate approved base-human candidates and animations; record licensing and version before import.
2. Prepare patient variations and staff roles; remote report/phone-only roles do not need full avatars.
3. Implement clinical signs and countable breathing driven by the authorized observable presentation contract, with reset between cases.
4. Map behaviour/intensity to layered idle/talk/pain/confusion/sedation/gestures; avoid facial cues that leak undisclosed diagnostic facts.
5. Build walk/escort, wheelchair/trolley poses, door paths and obstruction recovery.
6. Queue the nurse wristband sequence from the immediate Core events: enter, check notes, replace, finish and leave. Visual animation must not delay or modify the already-resolved Core state.
7. Verify normal/mismatch/missing/allergy-band cases, re-read/re-confirm, repeated requests and tablet equivalence. Test blocked doorway and scene exit during a sequence.
8. Validate skinning, animation transitions, two-speaker lip sync and patient-plus-staff performance; Sannan reviews appearance and recognition.

Exit: correct roles/signs/movement and wristband visuals without state divergence or replay regressions.

### Stage 8 — Complete all clinical UI and interactions

1. Tablet: Admission, history/Known, observations, examination, assessment/calculators, orders/results, treatment, consent/preparation, transfer/SBAR, decisions/goals and debrief.
2. Sensors/monitor: values gated by actual attachment; cuff cycle, signal-quality/artefacts, measured values/trends, waveform sampling and alarm presentation. Never generate clinical values in Unity.
3. Examination: abdominal regions, patient responses, stethoscope/airway/curtain interactions and privacy presentation.
4. Results: units/flags/comments, images/reports, old vs new results and critical-call read-back; no unapproved image silently substituted.
5. Treatments: request/confirmation/administration presentation and faults; consent/form/signing; preparation/gown/jewellery/site-marker/PPE states.
6. Theatre stations: working lights/table/drawers/screens, suction/camera/cylinder/pad/sterility checks and faults using the per-item spec.
7. WHO boards and SBAR cards: exact content and state-derived completeness; practice-only feedback and assessment behaviour follow Core.
8. Accessibility: seated reach, minimum readable text established on device, contrast, subtitles, controller/tablet alternative for every required interaction.
9. Test rejected actions, rapid double selection, switching tabs/patients, stale views, disabled-state correctness and subscriptions after reload.

Exit: every P0 walkthrough step has a usable controller route and specified fallback, with UI reflecting real Core acceptance/rejection.

### Stage 9 — Physical records and EMR

1. Bind records to identity/current episode and historical content; search name/MRN/DOB with valid empty/no-match outcomes and distractors.
2. Build folder pickup/open/page flip/add-report interactions; ensure readability without granting future knowledge.
3. Build workstation tabs, virtual keyboard/quick patient list, current/previous labs, imaging viewer, medicines and histories.
4. Synchronize new-result notifications/badges and physical reports; log record opening through the real action contract where required.
5. Test wrong patient selection, duplicate updates, missing/unsupported/unapproved image, scene reload and concurrent arrivals.

Exit: folder/current vs EMR/full-history separation preserved; results appear once at the correct time and viewed-record evidence reaches the scorer.

### Stage 10 — Offline audio, subtitles and lip sync

1. Consume the approved line catalogue/offline production handoff; add Unity playback, speaker mapping and missing-line validation.
2. Subtitle speaker/text must match audio; missing audio shows text and a recorded warning.
3. Implement priority/interrupt queue, spatial audio, pooled sources, mute/volume settings and nurse workflow lines.
4. Calibrate lip sync per voice, disable expensive distant updates where appropriate, and profile simultaneous speakers.
5. Tie equipment sounds/alarms to authoritative state; silence/acknowledge is logged and does not resolve the clinical issue.

Exit: all required P0 lines have accessible playback/fallback, correct alarm behaviour and recorded sound/voice attributions.

### Stage 11 — Debrief, case integration and P0 release

1. Render scores, end state, timeline and evidence from the scoring/debrief handoff; appropriate postponement and redirect count as defined success outcomes.
2. Integrate S1 then S2. Use all 17 P0 cases and correct/novice/dangerous/edge paths supplied by the case/scoring owners.
3. Test S2 overlap, delayed results, deterioration during waits, corridor events and equipment faults. Confirm presentation never masks a blocker.
4. Produce Dev/Demo/Study builds; ensure Study never uses unapproved assessment content and development-only fixtures/debug UI do not ship.
5. Profile fixed routes/worst views on the supported devices, audit licences/attributions and reproduce a clean-machine build.
6. Conduct manual usability/clinical review using existing protocols and record observations/fixes honestly. Repeat impacted tests after fixes.

Exit: P0 checklist in section 8 is satisfied and accepted by Sannan; other owners' clinical/content acceptance remains explicit.

### Stage 12 — P1 client extensions

Dependencies: P0 baseline, backend/ASR/dialogue/TTS contracts, confirmed-order parser, extended scoring and content.

1. Push-to-talk, Android runtime microphone permission, recording indicator and bounded capture; nonblocking upload with cancellation.
2. Transcript/clarification, read-back and explicit confirmation. No Core order before confirmation; cancelling/stale responses cannot issue an action.
3. Offline/timeout/permission-denied paths return to choice menus and pre-rendered voices. Test connection loss mid-request and recovery without duplicate execution.
4. Live sentence/audio playback with lip sync, subtitle-only failure path and consistent voice mapping.
5. AI debrief/difficulty UI uses supplied grounded facts/explanation; errors preserve template fallback.
6. Theatre educational extension, Sign Out board, labelled specimen asset and read-aloud interaction, time-lapse event cards and recovery bay variant. Reuse recovery-specific handover requirements, not theatre-transfer wording.
7. Integrate remaining 16 P1 case files when runtime/content gates permit; expose Pulse/composer controls only through supported contracts.

Exit: real P1 endpoints verified and offline P0 path remains playable; every extension has self-check evidence and Sannan acceptance.

### Stage 13 — P2 presentation and final maintenance

1. Integrate SC-1 through SC-20 into selection/records/presentation after their runtime support exists.
2. Add UI/voice-client support for approved multilingual/on-device features; verify font coverage, layout, language switching and spoken identity handoff.
3. Repeat fallback, accessibility, device budget and end-to-end regressions for affected paths.
4. Keep builds, dependency pins, licences, experiments and this ledger current. P2 engine/AI research remains with its owners.

Exit: every promised Sannan-facing integration is either accepted or has a named unresolved dependency; no later-tier scope disappears from the log.

## 6. Mandatory verification and handoff loop for each step

1. **Before:** record task ID, source step, exact scope, dependencies, baseline status and affected files. Define positive, rejection and edge cases before implementation.
2. **Implement:** make the smallest useful change; use the live Editor or reproducible generation where suitable. Inspect generated scene/prefab wiring as well as source code.
3. **Self-verify:** compile the affected assemblies; run relevant meaningful tests; inspect Play Mode or rendered asset/UI evidence; exercise at least the relevant normal and failure paths. Fix and re-check any regression.
4. **Integration verify:** when the action/UI boundary changes, compare equivalent logical action sequences headless vs Unity using the same content, seed, mode and simulated timing. Account for intentional channel metadata when comparing raw log hashes; do not remove meaningful action metadata merely to force equality.
5. **Record:** exact commands, tool/Editor version, case/seed/mode, content hash where relevant, results/artifact location and untested device cases. Set status to Self-verified only if all mandatory checks available for this step passed; otherwise record Partial verification/Blocked.
6. **Manual handoff:** provide scene/build to open, setup, numbered interactions, expected visible state, failure-path checks and how to export evidence. Mark Awaiting Sannan review.
7. **Sannan review:** record feedback and acceptance explicitly. If changes are requested, return to implementation and repeat affected checks and review.
8. **Pre-commit:** inspect exact diff and existing changes, run required repository checks, propose the exact subject and 3-8 body bullets, then ask Sannan for message approval. Never approve a message on Sannan's behalf.
9. **Commit only after both approvals:** stage the agreed files, inspect staged diff, use the approved message and record the SHA. Authorization to commit is not automatic authorization to push.
10. **After:** update status/evidence and relevant deferred-task/source-plan records in the same reviewed change where applicable. If later edits invalidate evidence, reopen the task.

### Commands available now (run only when implementing or validating an authorized change)

From the repository root:

```powershell
git status --short
git diff --check
task build
task test
task golden
task content-validate
task check
task plans-check
task unity-test
task unity-build
task sim -- replay --log <absolute-path-to-exported-session.jsonl>
task ci
```

Use targeted tests during each small change and the full required CI before a code commit. For a documentation-only change, verify content, links, formatting and scope; run repository-required checks before its eventual commit without inventing application test results. Unity commands need the pinned Editor; builds need Android modules. A failed command due to unavailable tooling is a blocker, not a pass. Some tools write files; keep generated outputs in their designated ignored/artifact locations.

The Blender Task commands and Quest pull command mentioned in the source plans are **future deliverables** unless verified in Taskfile; do not assume they already exist. Add new capability checks to the relevant tool/CI registry and documentation as required by repository policy. Updating existing prose does not require regenerating milestones; adding/renaming canonical step files does.

## 7. Performance and asset acceptance targets

These are existing plan targets, to be measured and revisited through the recorded experiments rather than claimed from Editor performance:

| Measure | Starting target | Source |
|---|---|---|
| Device frame rate | At least 72 fps average; inspect sustained frame timing | vr-platform s07 / B-S11 |
| Loading/play hitches | No hitch over 50 ms on specified route | vr-platform s04/s07 |
| Memory | At most 2.5 GB under specified device workload | vr-platform s07 |
| Worst room view | At most 150 batches and 750k triangles | B-S8 / equipment step targets |
| Small props / large machines | At most 5k / 20k triangles initially | Blender s06-s14; refine with EXP-BL-3 |
| Textures | Normally 1K; up to 2K towers/monitors; ASTC as appropriate | Blender/environment plans |
| Asset dimensions | Main body within 5% of sourced reference | Blender s03; open dimensions explicitly excluded from verified claims |
| Asset previews | Front, side, top, three-quarter and human-scale reference | Blender s03 |

Measure full-room equipment/characters/audio together, not only each isolated asset. Record device model, refresh target, build hash, route, CPU/GPU/memory metrics and capture duration. The installed Unity version is the project's pin; older source text calling it LTS is not authority to upgrade or relabel it.

## 8. Release completion checklist

- [ ] Existing skeleton successfully replayed from an actual Quest log.
- [ ] All 17 P0 cases have their expected accepted outcome on supported runtime/headset routes.
- [ ] All P0 required controller interactions and tablet fallbacks work; rejected actions leave authoritative state correct.
- [ ] Bay/corridor/OT lifecycle preserves Session and prevents duplicate actions/services.
- [ ] Every inventory item has its prescribed model/UI/door representation, validator evidence and manual review.
- [ ] Patients/staff, clinical cues, navigation and nurse wristband sequences work and reset safely.
- [ ] Folder/EMR/search/results and critical acknowledgement remain synchronized with Core.
- [ ] Audio/subtitles/lip sync and alarms have verified fallbacks and attribution.
- [ ] WHO progression, consent/preparation/SBAR and debrief are bound to the real engine services.
- [ ] Device performance targets measured; no unresolved crash or known release-blocking defect.
- [ ] Study review-status gate verified; training approval is not treated as assessment approval.
- [ ] Reproducible build, dependency versions, licences, checksums and manual review evidence recorded.
- [ ] P1/P2 client extensions each tracked and verified when their dependencies become available.
- [ ] Every eventual commit has separate recorded implementation acceptance and exact message approval from Sannan.

## 9. Progress, blockers and review log

Status vocabulary: Planned; Historically approved / needs baseline verification; In progress; Partial verification; Self-verified; Awaiting Sannan review; Changes requested; Accepted by Sannan; Awaiting commit-message approval; Committed; Blocked. Never equate code presence with completion. Record dates in Asia/Karachi with timezone.

| Local ID | Work | Status | Evidence / next action |
|---|---|---|---|
| SAN-PLAN | Read assignment and write this plan | Awaiting Sannan review | Documentation self-check passed: 109 relative links resolved, 96 source steps and 12 B-S work packages present, code fences balanced, Git whitespace check passed. No implementation or commit. |
| SAN-LAYOUT | Conventional Unity folders and editable patient-bay workspace | Accepted by Sannan; commit approved | See section 12. 64 moved GUIDs preserved; EditMode 3/3, PlayMode 12/12 and full repository CI 27/27 passed; scene/screenshots checked. Included in the approved layout commit on sannan47/unity-layout; identify its SHA in Git history. Quest checks remain pending. |
| SAN-BASE | Baseline and skeleton-1 device replay | Planned | First implementation stage; requires implementation authorization and available Editor/headset |
| SAN-SCENES | Persistent scene lifecycle/input | Editor foundations accepted and committed | Closeout 3cbea3f; 53/53 PlayMode, 3/3 EditMode and 27/27 CI passed. Earlier foundation/room/comfort/input work is committed. Physical headset acceptance and final equipment integration remain separate. |
| SAN-DIAG | Records/diagnostics skeleton | Presentation preview in progress; runtime integration blocked | Stage 2 Editor-only fixture and contract handoff in section 20. Investigation/result/transport runtime contracts and case integration still required. |
| SAN-OT | Greybox theatre/WHO skeleton | Blocked | Readiness/checklist/consent/transport runtime contracts required |
| SAN-BLENDER | Procedural pipeline and pilot assets | Pilot batch committed/pushed | b4aef49; see section 21. Pinned Blender and import pipeline verified; fitted wristband and Quest quality/performance gates remain open. |
| SAN-ASSETS | Full equipment roster | Incremental implementation | Bedside sensors committed/pushed in 4fc045a (section 22); physical notes folder in review on sannan47/physical-notes (section 23). Full roster and Quest quality bar remain open. |
| SAN-ROOMS | Final shells/layout | Evaluation finishes awaiting review | Section 32: bay/corridor/OT surfaces, ceilings and bay window added to existing shells. Supervisor final-shell dependency and Quest acceptance remain open. |
| SAN-CHAR | Characters and nurse sequence | MPFB promoted to main bay; awaiting review | Permanent PatientBay promotion and CGTrader graphic retirement self-verified in section 30. Clinical animation, staff and headset acceptance remain open. |
| SAN-UI | Complete clinical interactions/UI | Login enabled by default; main MPFB launch awaiting review | Login and wristband correction in section 29 accepted for further work; normal sign-in now enters the permanent MPFB PatientBay, section 30. Backend authentication remains a teammate TODO. |
| SAN-EMR | Folder and complete EMR | Blocked | Record/result runtime binding required for full acceptance |
| SAN-AUDIO | Offline client audio/subtitles/lip sync | Planned | Voice catalogue/production and licensing handoff required |
| SAN-P0 | Full P0 integration/debrief/release | Blocked | Clinical/case/scoring runtime and earlier stages |
| SAN-P1 | Voice/AI client/theatre/recovery | Blocked | Backend and P1 engine/content contracts |
| SAN-P2 | Advanced cases/multilingual client | Blocked | P2 engine/AI/content contracts |

### Evidence entry template (copy for every implementation step)

```text
Task ID / original step:
Started / updated (Asia/Karachi):
Status:
Scope and changed files:
Dependencies / contract version:
Baseline failures and unrelated changes:
Implementation summary:
Self-verification commands and exact results:
Visual inspection / artifact locations:
Case / seed / mode / content hash / build hash:
Negative and edge cases checked:
Device verification and device/version:
Known issues / unavailable checks:
Sannan manual test steps and expected outcomes:
Sannan feedback:
Explicit implementation acceptance (date / reference):
Exact proposed commit subject and body:
Explicit message approval (date / reference):
Commit SHA / files included (only after approvals):
Push authorization / result (if separately authorized):
Follow-up tasks / evidence invalidated by later changes:
```

### Current decision and blocker register

| ID | Item | Resolution / next action |
|---|---|---|
| SAN-B01 | Quest 3 not yet supplied by supervisor (Sannan, 10 October 2026) | Continue Editor-verifiable Stage 1 work; device replay, comfort and device profiling remain pending. |
| SAN-B02 | Supervisor shell files not located in this inspection | Inventory during authorized room work; greybox does not block logic |
| SAN-B03 | Detailed case files not driving normal sessions yet | Obtain/integrate real runtime contract from Core/content work; do not count files as playable cases |
| SAN-B04 | Investigation, treatment, scoring and complete checklist services missing from inspected default pipeline | Record exact owner handoff per adapter; fixtures do not close this blocker |
| SAN-B05 | 13 open equipment dimension rows in research review pack | Preserve explicit uncertainty; source and approve before verified dimensional claims |
| SAN-B06 | Historical plans/status notes inconsistent | Use actual files and evidence; resolve applicable discrepancies in future reviewed changes |
| SAN-B07 | Initial missing live Editor Pipeline | Resolved: package/setup committed in 54d152a; Unity 6000.5.10f1 connected and used for live compilation/tests in sections 15-18. |
| SAN-B08 | Existing Unity-generated working-tree changes | Preserve; compare baseline and agree exact inclusion before any commit |

### Session history

- 10 October 2026: Sannan identified himself and requested a detailed plan only. Assignment recovered from Implementation Guide/Team Board and current phase plans. His self-verification, manual-review and exact commit-message approval rules recorded above. No implementation, installation, tests, staging, commit or push performed as part of planning.
- 10 October 2026: Documentation verification passed: all 109 Markdown links resolve; appendix contains 96 canonical phase steps and all 12 assigned B-S packages; four code-fence boundaries are balanced; `git diff --check` passed for tracked changes. The new untracked document was inspected separately for structure and trailing whitespace. Working-tree status shows only this document added by the assistant; pre-existing Unity/metadata changes remain. Application/build/device checks were not run for this planning-only request. Sannan manual review and any eventual commit-message approval remain pending.

## 10. Detailed original-step checklist

**Path migration, 10 October 2026:** the original specifications below retain historical `Assets/_Project/...` paths. Current application folders are directly under `Assets`: `Scenes`, `Scripts`, `Editor`, `Settings`, `Tests`; old `Content` is now `Data`, except materials in `Materials/Prototype`. `Skeleton1_PatientBay` is now `PatientBay`. The application's XR rig variant is `Assets/Prefabs/XR/PlayerRig.prefab`. Do not recreate `_Project` when following an old specification. See the Unity README for the current editing workflow.

The following appendix is a planning snapshot assembled from the repository's canonical step files. It preserves their implementation, verification and dependency detail and provides a checklist per step. Read the linked current file before implementation, since this snapshot can become stale. Older prerequisites, package choices and proposed paths are planning inputs: compare against actual installed versions/APIs and existing files, preserve working implementations and log deviations.

Each checkbox means **verified and explicitly accepted by Sannan under this workflow**, not merely implemented. All begin unchecked. Each step also requires the evidence entry and commit gates in section 6. Shared clinical/evaluation/voice-production steps are handoffs; their presence in the appendix does not assign their entire implementation to Sannan.

### p0/p0-vr-platform


#### p0-vr-platform-s01 — Unity project and Quest setup

- [ ] Self-verification and Sannan manual acceptance recorded.

Source: [Original step](../docs/plans/p0/p0-vr-platform/steps/%E2%9C%85%20p0-vr-platform-s01-unity-project-quest-setup.md). Initial status: Historically approved; re-verification and Sannan review pending.

**What**

Create the Unity 6 LTS (URP) project in `unity/`, configured for Meta Quest, with an XR rig (teleport + snap turn), the XR Device
Simulator for headset-free testing, and a first build running on the Quest.

**Prerequisites (you)**

Unity Hub + Unity 6 LTS with **Android Build Support** (OpenJDK + Android SDK/NDK); Meta Quest in **developer mode** (Meta Horizon app);
USB cable or Air Link; Git LFS installed.

**How to implement (Implementation Guide B-S6)**

1. New project from the URP template into `unity/`; Editor settings: Force Text serialization, visible meta files.
2. Packages: OpenXR + Meta Quest feature group, Meta XR Core SDK (platform), XR Interaction Toolkit 3.x (+ Starter Assets, XR Device Simulator), TextMeshPro, Newtonsoft JSON.
3. Android: IL2CPP, ARM64, Vulkan, ASTC; minimum API level per current Meta requirement (check Project Validation).
4. XR Plug-in Management → OpenXR (Android) → Meta Quest feature group + Oculus Touch profile; fix all Project Validation warnings.
5. Rig: XRI Starter Assets XR Origin; teleport anchors only; snap turn 45°; fade on teleport.
6. `DevOnly` scene with the XR Device Simulator.
7. `Boot` scene + `Sys_Core` scene (empty for now; SimulationHost lands in s03).
8. Build & Run to Quest; record a 60 s idle OVR Metrics baseline (start of EXP-VR-1).
9. Taskfile: `task unity-build` (batchmode build script, `Editor/BuildScript.cs`).

**Verification**

- In the Editor with the device simulator: you can look around and teleport.
- On the Quest: app launches, both controllers tracked, teleport works, stable frame rate in an empty scene (T-U1…T-U4).

**Files changed**

`unity/` (ProjectSettings, Packages/manifest.json, Assets/_Project/Scenes, Assets/_Project/Editor/BuildScript.cs), `.gitattributes` (LFS patterns), `Taskfile.yml`, README.

**Dependencies**

p0-foundation; installs above.

**Common issues**

Black screen → Meta feature group / Vulkan; controllers not tracked → interaction profile missing; build fails stripping → `link.xml` (B-S6 troubleshooting).


#### p0-vr-platform-s02 — Core engine as a Unity package

- [ ] Self-verification and Sannan manual acceptance recorded.

Source: [Original step](../docs/plans/p0/p0-vr-platform/steps/%E2%9C%85%20p0-vr-platform-s02-core-as-unity-package.md). Initial status: Historically approved; re-verification and Sannan review pending.

**What**

Make `src/ProcSense.Core` loadable by Unity as a local package (ADR-0001), so the exact same engine runs headless and on the Quest.

**How to implement**

1. `src/ProcSense.Core/package.json` (`com.procsense.core`) + `ProcSense.Core.asmdef` with `noEngineReferences: true`.
2. `unity/Packages/manifest.json`: `"com.procsense.core": "file:../../src/ProcSense.Core"`.
3. Exclude `bin/` and `obj/` from Unity import (asmdef folder layout or `.gitignore`d build output outside the package folder — move .NET build output via `Directory.Build.props`).
4. Unity-side JSON reader implementing Core's `IContentReader` (Newtonsoft) + `link.xml` preserving Core and Newtonsoft types.
5. Copy/stream `content/` into `StreamingAssets` at build time (build script), so the Quest reads the same files.
6. Commit the `.meta` files Unity generates inside `src/ProcSense.Core`.

**Verification**

Unity compiles with Core referenced; an Editor test runs the skeleton-1 golden script inside Unity and gets the same log hash as `task golden`.

**Files changed**

`src/ProcSense.Core/package.json`, `ProcSense.Core.asmdef`, `Directory.Build.props`, `unity/Packages/manifest.json`, `unity/Assets/_Project/Scripts/Content/NewtonsoftContentReader.cs`, `unity/Assets/link.xml`, build script, ADR-0001 status → Accepted.

**Dependencies**

p0-core-engine-s04, p0-vr-platform-s01.

**Common issues**

`dotnet build` output inside the package folder confuses Unity → redirect output; C# language version mismatch → stay on C# 9 features.


#### p0-vr-platform-s03 — SimulationHost, action emitters, state views

- [ ] Self-verification and Sannan manual acceptance recorded.

Source: [Original step](../docs/plans/p0/p0-vr-platform/steps/%E2%9C%85%20p0-vr-platform-s03-simulation-host-emitters-views.md). Initial status: Historically approved; re-verification and Sannan review pending.

**What**

The bridge between Unity and Core (Implementation Guide B-S7):
- **SimulationHost** (in `Sys_Core`) owns one Core `Session`, ticks it every frame with `Time.deltaTime`, publishes the `ObservableState`,
  pauses + logs on any Core exception, and **saves the session log** to the Quest's persistent storage.
- **Emitters** — generic components that turn XR events into `SimAction`s: GrabEmitter, SocketEmitter, PokeEmitter, UIButtonEmitter (debounced).
- **StateView** base — `Render(ObservableState)` with diff caching; text panels throttled to 10 Hz.
- **Tablet fallback registry** — every emitter's action also available as a tablet button.

**How to implement**

1. `Scripts/Bridge/SimulationHost.cs`, `ActionEmitters/*.cs`, `Views/StateView.cs`, `Fallback/TabletFallbackRegistry.cs`.
2. Session config (case ID, seed, mode) from a ScriptableObject for now.
3. Session log written as JSONL to `Application.persistentDataPath/sessions/`; a menu button to export.
4. Edit Mode / Play Mode tests: emitter fires once per select; rejected action shows a hint and changes nothing visually (T-B2); exception → paused, no crash (T-B5).

**Verification**

Play Mode test passes; in the Editor, a debug button emitting `introduce_self` produces a log line identical in format to the CLI's.

**Files changed**

`unity/Assets/_Project/Scripts/Bridge/*`, `ActionEmitters/*`, `Views/*`, `Fallback/*`, tests, `Sys_Core` scene.

**Dependencies**

p0-vr-platform-s02.

**Common issues**

Double actions → debounce select events; Observable null on first frame → create Session in `Awake` before views (B-S7 troubleshooting).


#### p0-vr-platform-s04 — Scene structure and room loading

- [ ] Self-verification and Sannan manual acceptance recorded.

Source: [Original step](../docs/plans/p0/p0-vr-platform/steps/p0-vr-platform-s04-scene-structure-loading.md). Initial status: Planned; no implementation acceptance claimed.

**What**

Scene layout: `Boot` (loads content, shows progress) → `Sys_Core` (persistent: SimulationHost, XR rig, UI root, audio) → room scenes loaded additively (`Room_PatientBay`, `Room_Corridor`, `Room_OT`). A `LocationAnchorRegistry` maps Core location IDs (LOC-BAY, LOC-CORRIDOR, LOC-OT, B-door IDs) to scene anchors.

**How to implement**

1. `Scripts/Scenes/SceneLoader.cs`: additive load/unload with 0.3 s fade; keep `Sys_Core` (DontDestroyOnLoad).
2. `LocationAnchorRegistry` component in each room scene registers its anchors (`Anchor_<LOC>`); SceneDirector queries it.
3. Content loading on Android via UnityWebRequest from StreamingAssets (File IO fails on Android — B-M11 troubleshooting).
4. Play Mode test: load Boot → teleport across all three rooms → Core state hash unchanged.

**Verification**

Device simulator: bay → corridor → OT → bay with no hitch > 50 ms; Play Mode test green.

**Files changed**

`unity/Assets/_Project/Scripts/Scenes/*`, scenes `Boot`, `Sys_Core`, `Room_*`.

**Dependencies**

p0-vr-platform-s03.

**Common issues**

Observable null on first frame → create the Session in `Awake` before views (B-S7).


#### p0-vr-platform-s05 — Locomotion and comfort (EXP-VR-3)

- [ ] Self-verification and Sannan manual acceptance recorded.

Source: [Original step](../docs/plans/p0/p0-vr-platform/steps/p0-vr-platform-s05-locomotion-comfort.md). Initial status: Planned; no implementation acceptance claimed.

**What**

Teleport anchors at bedside/OT stations, teleport areas in the corridor (free roaming), 45° snap turn, fade on teleport, seated mode; comfort experiment teleport vs smooth locomotion.

**How to implement**

1. XRI Teleportation Area on corridor floors; Teleportation Anchors at stations (`TELEPORT_*` from room manifests).
2. Snap turn 45°, vignette optional, seated height offset.
3. EXP-VR-3: 3+ testers, short comfort questionnaire; record result; smooth locomotion only if testers ask.
4. Teleport arc colour indicates valid/invalid targets; fade 0.2–0.3 s.
5. Comfort questionnaire: 3 questions (nausea, disorientation, eye strain) after 10 minutes.

**Verification**

EXP-VR-3 recorded; T-U4 all anchors reachable; seated mode works.

**Files changed**

XR rig prefab `unity/Assets/_Project/Prefabs/XR/XROrigin.prefab` (teleport/turn settings), teleport areas in room scenes, `docs/plans/p0/p0-vr-platform/README.md` (EXP-VR-3 result).

**Dependencies**

p0-vr-platform-s04.

**Common issues**

Teleport targets on uneven floors or stairs fail → use flat teleport areas only. Snap-turn angle too large disorients — 45° is the default; let users pick 30°.


#### p0-vr-platform-s06 — SceneDirector: moving the patient

- [ ] Self-verification and Sannan manual acceptance recorded.

Source: [Original step](../docs/plans/p0/p0-vr-platform/steps/p0-vr-platform-s06-scene-director.md). Initial status: Planned; no implementation acceptance claimed.

**What**

Unity reacts to Core transport events: the patient walks (NavMesh), sits in a wheelchair pushed by the porter, or lies on a trolley pushed by the porter with an escort; at a B door the patient enters and is hidden until `PatientReturned`; the trainee teleports with the patient when accompanying.

**How to implement**

1. `SceneDirector.MovePatient(from, to, mode)` subscribed to Core transport states (REQUESTED…BACK).
2. NavMesh baked for bay, corridor, OT; door areas link to hidden 'inside' anchors.
3. Visual travel time is cosmetic; arrival waits for Core state (T-B4).
4. Accompany: trainee rig follows/teleports with the patient.
5. Abstract B-room 'inside' anchors are hidden positions behind doors (no room built).

**Verification**

Device simulator: patient goes bay → CT door → hidden → returns on Core events; T-B4 green.

**Files changed**

`Scripts/Scenes/SceneDirector.cs`, NavMesh data.

**Dependencies**

p0-simulation-workflow-s13; p0-characters-s06.

**Common issues**

NPC walks through beds → re-bake NavMesh / add obstacles (B-S3 troubleshooting).


#### p0-vr-platform-s07 — Quest builds, profiling and Unity CI

- [ ] Self-verification and Sannan manual acceptance recorded.

Source: [Original step](../docs/plans/p0/p0-vr-platform/steps/p0-vr-platform-s07-quest-builds-profiling-ci.md). Initial status: Planned; no implementation acceptance claimed.

**What**

Build variants Dev / Demo / Study via scripting defines from an editor menu and batchmode; profiler markers around Core Tick and render views; weekly OVR Metrics captures on a fixed route; Unity Play Mode tests in CI (GameCI or a local runner); release checklist.

**How to implement**

1. `Editor/BuildScript.cs` (ProcSense → Build → Dev/Demo/Study); `task unity-build -- <variant>`.
2. ProfilerMarkers in SimulationHost; OVR Metrics CSV per capture stored in `docs/plans/p0/p0-vr-platform/perf/`.
3. `task unity-test` runs `Unity -runTests -batchmode`; add to CI if licence activation works (verify GameCI personal-licence flow).
4. `docs/RELEASE.md` checklist (APK + checksum per tag).

**Verification**

T-Q1 (demo path passes perf gate), T-Q3 (each variant builds), T-Q4 (memory) recorded.

**Files changed**

`unity/Assets/_Project/Editor/BuildScript.cs`, CI workflow, `docs/RELEASE.md`, perf captures.

**Dependencies**

p0-vr-platform-s01.

**Common issues**

Hitches → GC/texture uploads: pre-warm; build too large → texture sizes/audio compression.


#### p0-vr-platform-s08 — Experiment EXP-VR-4: Unity MCP bridge for the developer

- [ ] Self-verification and Sannan manual acceptance recorded.

Source: [Original step](../docs/plans/p0/p0-vr-platform/steps/p0-vr-platform-s08-unity-mcp-experiment.md). Initial status: Planned; no implementation acceptance claimed.

**What**

Evaluate a community Unity MCP server (e.g. CoplayDev `unity-mcp` — not yet verified) that would let the developer inspect and edit the open Unity Editor: list scene objects, read/set component values, create prefabs, run tests, capture the Game view.

**How to implement**

1. Check the tool first: repo, maintenance, licence, what it can modify; read its install instructions fully.
2. Commit before trying (any scene change must be revertible).
3. Run 5 real tasks from the skeleton work; record success/failure per task.
4. Adopt if ≥ 4/5 succeed; otherwise keep editor scripts + screenshots.

**Verification**

Decision recorded in the vr-platform README (EXP-VR-4) with the 5 task results.

**Files changed**

`docs/plans/p0/p0-vr-platform/README.md` (EXP-VR-4 results table), MCP server config (only if adopted), `docs/decisions/ADR-xxxx-unity-mcp.md`.

**Dependencies**

p0-vr-platform-s01.

**Common issues**

Never let a tool change scenes without a clean git state first.


### p0/p0-environment-rooms


#### p0-environment-rooms-s01 — Greybox patient bay

- [ ] Self-verification and Sannan manual acceptance recorded.

Source: [Original step](../docs/plans/p0/p0-environment-rooms/steps/%E2%9C%85%20p0-environment-rooms-s01-greybox-patient-bay.md). Initial status: Historically approved; re-verification and Sannan review pending.

**What**

A simple placeholder patient bay (until the supervisor's model is integrated): floor, walls, bed/trolley, patient stand-in (capsule or
basic mannequin seated on the bed), wristband on the wrist, notes folder on the bed table, monitor on a stand, a nurse stand-in,
tablet holder, teleport anchor at the bedside. Every object has a **text label** and the named anchor it will keep when real assets replace it.

**How to implement**

1. The developer writes an **editor script** `Editor/Greybox/BuildPatientBay.cs` that generates the greybox scene from a small data file
   (`room_manifest_bay.json`: anchor name → position/rotation → placeholder shape → label). Re-runnable.
2. Anchors: `ANCHOR_Bed`, `ANCHOR_PatientSeat`, `ANCHOR_Wristband` (child of patient wrist), `ANCHOR_NotesFolder`, `ANCHOR_Monitor`,
   `ANCHOR_Nurse`, `ANCHOR_TabletHolder`, `TELEPORT_Bedside`.
3. Scale: real-world metres (bed height ~0.6 m, etc.); a 1.7 m reference capsule to check scale in the headset.
4. Baked simple lighting.

**Verification**

You open the scene in the headset: bed/patient/monitor are at believable sizes and reachable from the bedside teleport point; labels readable.
Send the developer a screenshot from the Editor for review.

**Files changed**

`unity/Assets/_Project/Scenes/Skeleton1_PatientBay.unity`, `unity/Assets/_Project/Editor/Greybox/BuildPatientBay.cs`, `unity/Assets/_Project/Data/room_manifest_bay.json`.

**Dependencies**

p0-vr-platform-s01.

**Common issues**

Things feel too big/small in VR → adjust the manifest, re-run the script (never hand-move objects that the script owns).

**Carried-over requirements (added 09 October 2026)**

- The bay needs a **doorway/corridor side** and anchors `ANCHOR_BayDoor`, `ANCHOR_NurseBedside`, `ANCHOR_NurseExit` so a nurse can visibly enter from outside and replace the wristband (p0-characters-s08; DT-007).


#### p0-environment-rooms-s02 — Import the supervisor's hospital model (EXP-ENV-1, EXP-ENV-3)

- [ ] Self-verification and Sannan manual acceptance recorded.

Source: [Original step](../docs/plans/p0/p0-environment-rooms/steps/p0-environment-rooms-s02-import-supervisor-model.md). Initial status: Planned; no implementation acceptance claimed.

**What**

Import the base hospital model into the URP project, fix materials (Render Pipeline Converter), scale and axes, split into the three category-A spaces (OT, patient bay, corridor), map every shell to an A location or 'unused', and profile each room on the Quest.

**How to implement**

1. Keep an untouched copy of the original package (outside the repo or LFS) — we only work on imported copies.
2. Run Render Pipeline Converter (pink materials); verify 1 unit = 1 m with the 1.7 m reference capsule.
3. Remove unused wings/rooms from the room scenes (not from the original).
4. Room mapping table in the phase README: shell → A location / unused (EXP-ENV-3).
5. OVR Metrics per room (EXP-ENV-1): T-EN1 ≥ 72 fps, T-EN4 memory ≤ 2.5 GB.

**Verification**

All three rooms load on the Quest within budget; mapping table complete.

**Files changed**

`unity/Assets/_Project/Art/Rooms/*`, room scenes, README.

**Dependencies**

p0-vr-platform-s04.

**Common issues**

Pink materials → converter; wrong scale → fix import scale, not by scaling objects.


#### p0-environment-rooms-s03 — Optimise and light the rooms (EXP-ENV-2)

- [ ] Self-verification and Sannan manual acceptance recorded.

Source: [Original step](../docs/plans/p0/p0-environment-rooms/steps/p0-environment-rooms-s03-optimise-lighting.md). Initial status: Planned; no implementation acceptance claimed.

**What**

Static batching and mesh combining, hidden geometry removed, ASTC textures ≤ 2K, colliders, occlusion culling, baked lighting (lightmap 10–20 texels/m) with light probes around patient and NPC areas; compare 2–3 bake settings.

**How to implement**

1. Mesh Combine static props; delete invisible faces.
2. Lightmap resolution trial 10 / 15 / 20 texels/m (EXP-ENV-2); light probes near bed, OT table, corridor.
3. Occlusion culling bake per room.
4. Re-bake after equipment placement (s04).
5. ASTC texture compression ≤ 2K; mipmaps on; remove unused materials.
6. Measure before/after each optimisation (OVR Metrics) so effort goes where it helps.

**Verification**

T-EN3: worst OT view ≤ 150 batches; bake setting chosen and recorded.

**Files changed**

Lighting settings assets + lightmaps (LFS) under `unity/Assets/_Project/Art/Rooms/Lighting/`, occlusion data per room scene, `docs/plans/p0/p0-environment-rooms/README.md` (EXP-ENV-2 result).

**Dependencies**

p0-environment-rooms-s02.

**Common issues**

Bake takes hours → GPU lightmapper on the RTX 4050 laptop or lower resolution.


#### p0-environment-rooms-s04 — Anchors, room manifests and placement script

- [ ] Self-verification and Sannan manual acceptance recorded.

Source: [Original step](../docs/plans/p0/p0-environment-rooms/steps/p0-environment-rooms-s04-anchors-manifest-placement.md). Initial status: Planned; no implementation acceptance claimed.

**What**

Room manifests (data) listing every anchor — equipment slots, patient positions (bed, chair, trolley), NPC positions, teleport points, door anchors — and which asset goes where; an editor script places/updates assets from manifests so greybox and Blender assets swap by changing an ID.

**How to implement**

1. Reuse the `room_manifest_bay.json` format from s01; write `room_manifest_ot.json`, `room_manifest_corridor.json`.
2. `Editor/Rooms/PlaceFromManifest.cs`: idempotent (re-running gives identical scenes); objects it owns are tagged `ManifestOwned`.
3. Anchor naming: `Anchor_<LOC>`, `SLOT_<AssetId>_<n>`, `NPC_<Role>`, `TELEPORT_<Name>`, `DOOR_<Dest>` (T-EN2).
4. Manifest validator (in `validate.py` or a C# editor test): every anchor referenced by an equipment item exists; no two items share a slot.

**Verification**

Re-running placement produces no diff; swapping an asset ID in a manifest updates the room.

**Files changed**

`unity/Assets/_Project/Data/room_manifest_*.json`, editor script.

**Dependencies**

p0-environment-rooms-s02.

**Common issues**

Never hand-move a script-owned object — edit the manifest.


#### p0-environment-rooms-s05 — Patient bay: S1 ward / S2 ED dressing

- [ ] Self-verification and Sannan manual acceptance recorded.

Source: [Original step](../docs/plans/p0/p0-environment-rooms/steps/p0-environment-rooms-s05-patient-bay-variants.md). Initial status: Planned; no implementation acceptance claimed.

**What**

One pre-op patient bay shell dressed from data as a day-surgery ward bay (S1: patient seated on bed in own clothes, folder, nurse) or an ED cubicle (S2: patient on trolley, wall monitor, triage clipboard, relative) — interaction-relevant objects only.

**How to implement**

1. `room_manifest_bay_S1.json`, `room_manifest_bay_S2.json` sharing the shell; the case's scenario selects one.
2. Objects from equipment-list.md (bay rooms) + interaction-relevant room objects only.
3. S1 dressing: day-surgery bay, bed, bedside locker/table only if interacted with, folder on the bed table, nurse position; S2 dressing: ED cubicle, trolley, wall monitor, triage clipboard, relative position.
4. Shared anchors for everything common (monitor, outlets, sink) so interactions are written once.

**Verification**

Loading VAR-S1-A shows the ward dressing and VAR-S2-PC the ED dressing; all spec interactions work in both.

**Files changed**

`unity/Assets/_Project/Data/room_manifest_bay_base.json`, `room_manifest_bay_S1.json`, `room_manifest_bay_S2.json`, `Room_PatientBay` scene.

**Dependencies**

p0-environment-rooms-s04.

**Common issues**

Two dressings drifting apart → keep shared anchors in one base manifest that both variants extend.


#### p0-environment-rooms-s06 — Operation Theatre layout (main environment)

- [ ] Self-verification and Sannan manual acceptance recorded.

Source: [Original step](../docs/plans/p0/p0-environment-rooms/steps/p0-environment-rooms-s06-ot-layout.md). Initial status: Planned; no implementation acceptance claimed.

**What**

Full OT layout: anaesthetic / Sign In area at the head end, operating table centre under the lights, anaesthesia machine + monitor at the head, laparoscopic tower and diathermy at the side, scrub area, storage cupboards, instrument trolley and back table, WHO board on the wall, computer workstation, doors; walkway space for teleport and NPCs.

**How to implement**

1. `room_manifest_ot.json` with anchors for every OT item in equipment-list.md (#40–#51 + OT room objects).
2. Use the Charité OR model as a visual layout reference only.
3. Check reach and clearance in the headset with you; adjust the manifest.
4. Mark sterile-field zones (around the table) so NPC paths avoid them.

**Verification**

You approve the layout in the headset; all OT anchors defined.

**Files changed**

`unity/Assets/_Project/Data/room_manifest_ot.json`, `Room_OT` scene, headset review notes in the phase README.

**Dependencies**

p0-environment-rooms-s04.

**Common issues**

Leave clear paths between table, machines and door for NPCs.


#### p0-environment-rooms-s07 — Walkable corridor and diagnostic doors

- [ ] Self-verification and Sannan manual acceptance recorded.

Source: [Original step](../docs/plans/p0/p0-environment-rooms/steps/p0-environment-rooms-s07-corridor-doors.md). Initial status: Planned; no implementation acceptance claimed.

**What**

A roamable corridor connecting the patient bay, the OT and the B doors (CT Scan, Imaging, X-ray, Laboratory, MRCP) plus the specimen drop / tube station; B doors physically block the trainee (short message) while the patient/porter layer passes.

**How to implement**

1. Greybox corridor from a manifest first (M2); real corridor from the supervisor model later (s02).
2. Door anchors `DOOR_CT`, `DOOR_IMAGING`, `DOOR_XRAY`, `DOOR_LAB`, `DOOR_MRCP` with sign-plate slots.
3. Trainee-blocking collider on each B door + proximity message; separate physics layer for patient/porter.
4. NavMesh areas through doors for patient/porter only.

**Verification**

In the headset: you can roam the corridor and can't pass any B door; the patient walks through.

**Files changed**

`unity/Assets/_Project/Data/room_manifest_corridor.json`, `Room_Corridor` scene, door prefabs, NavMesh data.

**Dependencies**

p0-environment-rooms-s01.

**Common issues**

NavMesh through doors must exist for patient/porter but the trainee's teleport areas must stop at the door.


#### p0-environment-rooms-s08 — Greybox Operation Theatre

- [ ] Self-verification and Sannan manual acceptance recorded.

Source: [Original step](../docs/plans/p0/p0-environment-rooms/steps/p0-environment-rooms-s08-greybox-ot.md). Initial status: Planned; no implementation acceptance claimed.

**What**

Placeholder OT from a manifest, like the greybox bay: labelled shapes for the operating table, anaesthesia machine, patient monitor, WHO board, surgical lights, lap tower, doors — at the final anchor names.

**How to implement**

1. Generalise `BuildPatientBay.cs` only as far as needed → `BuildFromManifest.cs`; write `room_manifest_ot.json` (greybox shapes).
2. Anchor names identical to the final OT layout (s06) so real assets drop in.
3. Placeholder NPC capsules for anaesthetist, surgeon and scrub nurse at `NPC_<Role>` anchors so checklist answers have a visible speaker.

**Verification**

VAR-S1-A Sign In + Time Out playable in the greybox OT on the Quest.

**Files changed**

`unity/Assets/_Project/Editor/Greybox/BuildFromManifest.cs`, `unity/Assets/_Project/Data/room_manifest_ot.json` (greybox shapes), `Room_OT` scene.

**Dependencies**

p0-environment-rooms-s01.

**Common issues**

Keep anchor names stable — s06 depends on them.


### p0/p0-blender-equipment


#### p0-blender-equipment-s01 — Blender pipeline setup

- [ ] Self-verification and Sannan manual acceptance recorded.

Source: [Original step](../docs/plans/p0/p0-blender-equipment/steps/p0-blender-equipment-s01-blender-pipeline-setup.md). Initial status: Planned; no implementation acceptance claimed.

**What**

Install and pin Blender, create the `tools/blender/` layout (runner, helper library folder, assets per category, budgets/requirements files), and add Taskfile commands `blender-build -- <asset|all>`, `blender-validate`, `blender-preview`, `blender-build-all`, all running Blender headless with `--factory-startup` so user add-ons/settings can't change the output.

**How to implement**

1. Install Blender (LTS); record exact version in `tools/blender/VERSION`; `task doctor` checks it.
2. `tools/blender/run.py` wrapper: finds Blender, runs `blender -b --factory-startup -P <script> -- <args>`, returns exit code.
3. Taskfile entries; output folder `build/blender/` (gitignored) + export target `unity/Assets/_Project/Art/Equipment/`.
4. Smoke test: `task blender-build -- hello_cube` creates and exports a bevelled cube.
5. `.gitignore`: `build/blender/`; exports go to `unity/Assets/_Project/Art/Equipment/` (LFS or rebuilt — EXP-FND-1).

**Verification**

`task blender-build -- hello_cube` produces an FBX that imports into Unity at 1 m.

**Files changed**

`tools/blender/*`, `Taskfile.yml`, `.gitignore`.

**Dependencies**

p0-foundation-s04.

**Common issues**

Blender's Python API changes between versions — pin it.


#### p0-blender-equipment-s02 — Helper library `procsense_bpy`

- [ ] Self-verification and Sannan manual acceptance recorded.

Source: [Original step](../docs/plans/p0/p0-blender-equipment/steps/p0-blender-equipment-s02-helper-library.md). Initial status: Planned; no implementation acceptance claimed.

**What**

Shared Blender Python helpers so each asset script is short and consistent: naming (`EQ_`/`M_`/empties), geometry (bevelled boxes, rounded panels, tubes along curves, cables, castor wheels, handles, screws, buttons, screen bezels), PBR material library (`M_PlasticWhite`, `M_PlasticGrey`, `M_SteelBrushed`, `M_Chrome`, `M_RubberBlack`, `M_Glass`, `M_ScreenOff`, `M_FabricBlue`…), label/decal baking, UV unwrap, LOD generation (decimate), interaction empties, FBX export with Unity axes (Y-up, 1 unit = 1 m, apply transforms).

**How to implement**

1. Build only the helpers the 3 pilot assets need first; grow them as real assets need (Ponytail/YAGNI).
2. Helpers are plain functions in a few modules (`naming.py`, `geometry.py`, `materials.py`, `uv.py`, `lod.py`, `points.py`, `export.py`).
3. Self-check script `tools/blender/selftest.py` builds one of each helper headless and asserts names/scale.
4. Helpers documented with a one-line docstring and a usage example in `selftest.py`.

**Verification**

`task blender-build -- selftest` passes headless.

**Files changed**

`tools/blender/procsense_bpy/*.py`, `tools/blender/selftest.py`.

**Dependencies**

p0-blender-equipment-s01.

**Common issues**

Normals inverted after boolean ops → recalculate outside in the helper.


#### p0-blender-equipment-s03 — Validator and preview renders

- [ ] Self-verification and Sannan manual acceptance recorded.

Source: [Original step](../docs/plans/p0/p0-blender-equipment/steps/p0-blender-equipment-s03-validate-preview.md). Initial status: Planned; no implementation acceptance claimed.

**What**

`validate.py`: triangle count vs the asset's budget class, naming rules, required empties per the interaction spec, scale (bounding box vs reference sheet dimensions ±5 %), applied transforms, UVs present, no non-manifold edges, material count. `preview.py`: renders front/side/top/three-quarter views with a 1.7 m human reference and a scale bar into `build/previews/`.

**How to implement**

1. Budget classes in `tools/blender/budgets.json` (starting values from B-S1; EXP-BL-3 tunes them).
2. Required empties per asset read from `tools/blender/requirements.json` (generated from the interaction spec).
3. Preview: Eevee, neutral studio lighting, 1024 px, consistent camera framing.
4. Validator output is machine-readable (JSON) so CI and the import pipeline can use it.

**Verification**

Validator fails a deliberately bad cube (wrong name, unapplied scale, too many tris); previews render for `hello_cube`.

**Files changed**

`tools/blender/validate.py`, `preview.py`, `budgets.json`, `requirements.json`.

**Dependencies**

p0-blender-equipment-s02.

**Common issues**

Bounding-box scale checks fail for items with cables/tubing — measure the main body object only (`EQ_<Asset>_Body`).


#### p0-blender-equipment-s04 — Pilot assets and quality bar (EXP-BL-1)

- [ ] Self-verification and Sannan manual acceptance recorded.

Source: [Original step](../docs/plans/p0/p0-blender-equipment/steps/p0-blender-equipment-s04-pilot-assets-quality-bar.md). Initial status: Planned; no implementation acceptance claimed.

**What**

Three pilot assets built to the full quality bar — vital-sign monitor on a roll stand (screen as `SCREEN_Main`, cable ports, handle, basket, 5-castor base), IV pole with infusion pump (hooks `SOCKET_Bag_1..4`, pole clamp, pump display `SCREEN_Pump`, door, buttons), patient wristband (strap, clasp, label area `LABEL_Identity`, `SOCKET_Wrist` fit) — iterated until you approve them in Blender and the headset. Lessons become the written quality bar.

**How to implement**

1. Write/confirm the reference sheet for each item (p0-medical-research-data-s09); free-model references are visual only.
2. One script per asset: `tools/blender/assets/<category>/<asset_id>.py` using `procsense_bpy` helpers; runs from Blender's Scripting tab or headless (`blender -b -P …`).
3. Organisation: collection per asset, objects `EQ_<AssetId>_<Part>`, shared `M_*` materials, applied transforms, origin at floor contact / natural grip; separate parts for anything that moves or changes state.
4. `task blender-build -- <asset>` → `task blender-validate` → `task blender-preview`; the developer reviews the 4-view previews with the 1.7 m reference, then you review in Blender and in the headset.
5. Iterate: the developer self-reviews previews against the reference photos, you review in Blender + headset, the developer revises the script.
6. Write the agreed quality bar (detail level, bevel sizes, material look, tri ranges) into the blender-equipment README.

**Verification**

You approve all three; quality-bar section written (EXP-BL-1).

**Files changed**

`tools/blender/assets/monitoring/vital_signs_monitor.py`, `tools/blender/assets/iv/iv_pole_pump.py`, `tools/blender/assets/identity/wristband.py`, `docs/research/equipment/{vital_signs_monitor,iv_pole_pump,wristband}.md`, `docs/plans/p0/p0-blender-equipment/README.md` (quality bar section).

**Dependencies**

p0-blender-equipment-s03; p0-medical-research-data-s09 (pilot sheets).

**Common issues**

Don't start other assets until the bar is approved.


#### p0-blender-equipment-s05 — Unity import pipeline (EXP-BL-2)

- [ ] Self-verification and Sannan manual acceptance recorded.

Source: [Original step](../docs/plans/p0/p0-blender-equipment/steps/p0-blender-equipment-s05-unity-import-pipeline.md). Initial status: Planned; no implementation acceptance claimed.

**What**

FBX import preset; URP material library matched by `M_*` names; an asset postprocessor that converts named empties into components — `GRAB_*` → XRGrabInteractable attach point, `SOCKET_*` → XRSocketInteractor, `ATTACH_*` → cable/tube endpoint, `SCREEN_*` → world-space canvas anchor, `PIVOT_*` → hinge/slider config, `LABEL_*` → TextMeshPro surface; prefab variants generated automatically. Experiment: URP shared materials vs baked textures on Quest.

**How to implement**

1. `Editor/Import/EquipmentPostprocessor.cs` (AssetPostprocessor) + `EquipmentPrefabBuilder.cs`.
2. Material library `Art/Materials/M_*.mat`; missing name → import warning.
3. Colliders: convex mesh or primitives from `COL_*` helper objects.
4. EXP-BL-2 on the pilot monitor: memory + visual comparison; record decision.

**Verification**

The three pilot assets become working prefabs (grab, socket, screen) without manual setup; re-import keeps component settings.

**Files changed**

`unity/Assets/_Project/Editor/Import/*`, `unity/Assets/_Project/Art/Materials/*`.

**Dependencies**

p0-blender-equipment-s04; p0-vr-platform-s03.

**Common issues**

Socket steals items → interaction layer masks (B-S9 troubleshooting).


#### p0-blender-equipment-s06 — Identity bands and documents

- [ ] Self-verification and Sannan manual acceptance recorded.

Source: [Original step](../docs/plans/p0/p0-blender-equipment/steps/p0-blender-equipment-s06-identity-documents.md). Initial status: Planned; no implementation acceptance claimed.

**What**

Our own procedural Blender Python assets for: Patient wristband (#1), Allergy band (#2), Patient folder (#5), Printed report sheet + imaging printout, Consent form (#7), Clipboard + observation chart (#6). Each is clinically recognisable, built from its reference sheet, VR-ready, and carries the interaction points its row in `equipment-interaction-spec.md` needs.

**How to implement**

1. Write/confirm the reference sheet for each item (p0-medical-research-data-s09); free-model references are visual only.
2. One script per asset: `tools/blender/assets/<category>/<asset_id>.py` using `procsense_bpy` helpers; runs from Blender's Scripting tab or headless (`blender -b -P …`).
3. Organisation: collection per asset, objects `EQ_<AssetId>_<Part>`, shared `M_*` materials, applied transforms, origin at floor contact / natural grip; separate parts for anything that moves or changes state.
4. `task blender-build -- <asset>` → `task blender-validate` → `task blender-preview`; the developer reviews the 4-view previews with the 1.7 m reference, then you review in Blender and in the headset.
5. **Patient wristband (#1)** — final version of the pilot; white band; `LABEL_Identity` (name, DOB, MR number rendered live); fits `SOCKET_Wrist`; rotate-to-read.
6. **Allergy band (#2)** — same strap mesh, colour material per institutional choice (red by default per walkthrough, Type C); no text.
7. **Patient folder (#5)** — ring-binder folder with cover hinge `PIVOT_Cover`, 6–10 page meshes with a page-turn bone/shape-key rig (`PIVOT_Page_n`), divider tabs, pocket for printed reports `SOCKET_Report`; page faces are `SCREEN_Page_n` surfaces.
8. **Printed report sheet + imaging printout** — A4 sheet with slight curl, `SCREEN_Sheet` surface, `GRAB_Corner`; imaging printout variant with glossy material.
9. **Consent form (#7)** — A4 multi-page stapled form, `SCREEN_Form`, pen with `GRAB_Pen` for signing.
10. **Clipboard + observation chart (#6)** — clipboard with clip `PIVOT_Clip`, chart surface `SCREEN_Chart`.

**Verification**

Per asset: validator green (triangles within class budget, naming, required empties, scale in metres, UVs, no non-manifold), previews approved by you, imports into Unity as a working prefab via the import pipeline, and the interaction-spec row works with the greybox stand-in replaced.

**Files changed**

`tools/blender/assets/<category>/*.py`, `docs/research/equipment/*.md`, exported FBX in `unity/Assets/_Project/Art/Equipment/` (rebuildable). Starting budgets (B-S1/B-S8, tunable by EXP-BL-3): small props ≤ 5k tris, large machines ≤ 20k, textures ≤ 1K (≤ 2K for towers/monitors), ~512 px/m texel density; per view ≤ 150 batches, ≤ 750k tris; ≥ 72 fps.

**Dependencies**

p0-blender-equipment-s04 (quality bar locked), s05 (import pipeline)

**Common issues**

Soft/organic shapes (bags, tubing, cloth, gloves) → cloth/curve simulation baked inside the script; readable text (labels, screens, gauges where numbers matter) → live `LABEL_`/`SCREEN_` surfaces in Unity, not baked geometry; inverted normals → recalculate outside before export; FBX unit 1.0 with applied scale.


#### p0-blender-equipment-s07 — Bedside monitoring and examination

- [ ] Self-verification and Sannan manual acceptance recorded.

Source: [Original step](../docs/plans/p0/p0-blender-equipment/steps/p0-blender-equipment-s07-bedside-monitoring.md). Initial status: Planned; no implementation acceptance claimed.

**What**

Our own procedural Blender Python assets for: Spot-check monitor on stand (#9), BP cuff + hose (#10), SpO2 finger probe + cable (#11), Tympanic thermometer (#12), 12-lead ECG machine + electrodes (#13), Stethoscope (#14), Glucose/ketone meter (#15). Each is clinically recognisable, built from its reference sheet, VR-ready, and carries the interaction points its row in `equipment-interaction-spec.md` needs.

**How to implement**

1. Write/confirm the reference sheet for each item (p0-medical-research-data-s09); free-model references are visual only.
2. One script per asset: `tools/blender/assets/<category>/<asset_id>.py` using `procsense_bpy` helpers; runs from Blender's Scripting tab or headless (`blender -b -P …`).
3. Organisation: collection per asset, objects `EQ_<AssetId>_<Part>`, shared `M_*` materials, applied transforms, origin at floor contact / natural grip; separate parts for anything that moves or changes state.
4. `task blender-build -- <asset>` → `task blender-validate` → `task blender-preview`; the developer reviews the 4-view previews with the 1.7 m reference, then you review in Blender and in the headset.
5. **Spot-check monitor on stand (#9)** — final pilot; `SCREEN_Main`; `ATTACH_SpO2`, `ATTACH_NIBP`, `ATTACH_ECG` ports; alarm light emissive.
6. **BP cuff + hose (#10)** — cuff that wraps (cloth sim baked to two states: open / wrapped), hose curve to `ATTACH_NIBP`; adult size variants (wrong size → false reading per walkthrough).
7. **SpO2 finger probe + cable (#11)** — clip probe with `PIVOT_Jaw`, `SOCKET_Finger` fit, cable to monitor.
8. **Tympanic thermometer (#12)** — handheld body, probe cover, `SCREEN_Temp` display, `GRAB_Handle`, probe tip `ATTACH_Ear`.
9. **12-lead ECG machine + electrodes (#13)** — cart unit with screen `SCREEN_ECG`, printer slot with printout prop, 10 lead wires ending in `ATTACH_Lead_n`; electrode pads with `SOCKET_Chest/Limb_n` fits.
10. **Stethoscope (#14)** — binaural headset, tubing curve, chest piece `GRAB_ChestPiece`; hangs on neck socket.
11. **Glucose/ketone meter (#15)** — meter with `SCREEN_Meter`, strip port, lancet device, test strip.

**Verification**

Per asset: validator green (triangles within class budget, naming, required empties, scale in metres, UVs, no non-manifold), previews approved by you, imports into Unity as a working prefab via the import pipeline, and the interaction-spec row works with the greybox stand-in replaced.

**Files changed**

`tools/blender/assets/<category>/*.py`, `docs/research/equipment/*.md`, exported FBX in `unity/Assets/_Project/Art/Equipment/` (rebuildable). Starting budgets (B-S1/B-S8, tunable by EXP-BL-3): small props ≤ 5k tris, large machines ≤ 20k, textures ≤ 1K (≤ 2K for towers/monitors), ~512 px/m texel density; per view ≤ 150 batches, ≤ 750k tris; ≥ 72 fps.

**Dependencies**

p0-blender-equipment-s04 (quality bar locked), s05 (import pipeline)

**Common issues**

Soft/organic shapes (bags, tubing, cloth, gloves) → cloth/curve simulation baked inside the script; readable text (labels, screens, gauges where numbers matter) → live `LABEL_`/`SCREEN_` surfaces in Unity, not baked geometry; inverted normals → recalculate outside before export; FBX unit 1.0 with applied scale.


#### p0-blender-equipment-s08 — Sampling and bedside tests

- [ ] Self-verification and Sannan manual acceptance recorded.

Source: [Original step](../docs/plans/p0/p0-blender-equipment/steps/p0-blender-equipment-s08-sampling-tests.md). Initial status: Planned; no implementation acceptance claimed.

**What**

Our own procedural Blender Python assets for: Blood collection set + tourniquet (#16), Specimen tubes + labels (#17), Blood culture bottles (#18), Urine pot, dipstick, pregnancy test (#19), Sharps bin (#33), Specimen drop / pneumatic-tube station. Each is clinically recognisable, built from its reference sheet, VR-ready, and carries the interaction points its row in `equipment-interaction-spec.md` needs.

**How to implement**

1. Write/confirm the reference sheet for each item (p0-medical-research-data-s09); free-model references are visual only.
2. One script per asset: `tools/blender/assets/<category>/<asset_id>.py` using `procsense_bpy` helpers; runs from Blender's Scripting tab or headless (`blender -b -P …`).
3. Organisation: collection per asset, objects `EQ_<AssetId>_<Part>`, shared `M_*` materials, applied transforms, origin at floor contact / natural grip; separate parts for anything that moves or changes state.
4. `task blender-build -- <asset>` → `task blender-validate` → `task blender-preview`; the developer reviews the 4-view previews with the 1.7 m reference, then you review in Blender and in the headset.
5. **Blood collection set + tourniquet (#16)** — needle holder, butterfly with tubing, tourniquet strap (two states: loose / applied).
6. **Specimen tubes + labels (#17)** — tube family with colour-coded caps per tube type (types per content), `LABEL_Tube` surface, fill-level shape key, tube rack.
7. **Blood culture bottles (#18)** — aerobic/anaerobic bottle pair with cap colours, `LABEL_Bottle`, fill state.
8. **Urine pot, dipstick, pregnancy test (#19)** — pot with lid `PIVOT_Lid`; dipstick with pad colour material slots (driven by result); cassette test with result window material.
9. **Sharps bin (#33)** — yellow bin with lid flap `PIVOT_Lid`, opening `SOCKET_Sharps`, fill line.
10. **Specimen drop / pneumatic-tube station** — wall station with carrier, door `PIVOT_Door`, `SOCKET_Carrier`, send button `SCREEN_Status`.

**Verification**

Per asset: validator green (triangles within class budget, naming, required empties, scale in metres, UVs, no non-manifold), previews approved by you, imports into Unity as a working prefab via the import pipeline, and the interaction-spec row works with the greybox stand-in replaced.

**Files changed**

`tools/blender/assets/<category>/*.py`, `docs/research/equipment/*.md`, exported FBX in `unity/Assets/_Project/Art/Equipment/` (rebuildable). Starting budgets (B-S1/B-S8, tunable by EXP-BL-3): small props ≤ 5k tris, large machines ≤ 20k, textures ≤ 1K (≤ 2K for towers/monitors), ~512 px/m texel density; per view ≤ 150 batches, ≤ 750k tris; ≥ 72 fps.

**Dependencies**

p0-blender-equipment-s04 (quality bar locked), s05 (import pipeline)

**Common issues**

Soft/organic shapes (bags, tubing, cloth, gloves) → cloth/curve simulation baked inside the script; readable text (labels, screens, gauges where numbers matter) → live `LABEL_`/`SCREEN_` surfaces in Unity, not baked geometry; inverted normals → recalculate outside before export; FBX unit 1.0 with applied scale.


#### p0-blender-equipment-s09 — IV, oxygen and bedside airway items

- [ ] Self-verification and Sannan manual acceptance recorded.

Source: [Original step](../docs/plans/p0/p0-blender-equipment/steps/p0-blender-equipment-s09-iv-oxygen-airway-bedside.md). Initial status: Planned; no implementation acceptance claimed.

**What**

Our own procedural Blender Python assets for: IV cannula kit (#25), Giving set + fluid bags (#26), IV pole + infusion pump (#26/#27), Wall oxygen outlet + flowmeter (#28), Nasal cannula, simple mask, non-rebreather mask (#28), Ward suction (#29), Nasogastric tube (#30) and urinary catheter + urometer (#31). Each is clinically recognisable, built from its reference sheet, VR-ready, and carries the interaction points its row in `equipment-interaction-spec.md` needs.

**How to implement**

1. Write/confirm the reference sheet for each item (p0-medical-research-data-s09); free-model references are visual only.
2. One script per asset: `tools/blender/assets/<category>/<asset_id>.py` using `procsense_bpy` helpers; runs from Blender's Scripting tab or headless (`blender -b -P …`).
3. Organisation: collection per asset, objects `EQ_<AssetId>_<Part>`, shared `M_*` materials, applied transforms, origin at floor contact / natural grip; separate parts for anything that moves or changes state.
4. `task blender-build -- <asset>` → `task blender-validate` → `task blender-preview`; the developer reviews the 4-view previews with the 1.7 m reference, then you review in Blender and in the headset.
5. **IV cannula kit (#25)** — cannula with wings and cap, dressing, `SOCKET_Arm` fit (insertion abstracted).
6. **Giving set + fluid bags (#26)** — bags (500/1000 mL variants) with `LABEL_Bag`, fill shape key, port; giving set tubing curve with drip chamber, roller clamp `PIVOT_Clamp`.
7. **IV pole + infusion pump (#26/#27)** — final pilot versions.
8. **Wall oxygen outlet + flowmeter (#28)** — outlet plate, flowmeter tube with ball float (shape key), knob `PIVOT_Knob`, `ATTACH_O2`.
9. **Nasal cannula, simple mask, non-rebreather mask (#28)** — tubing curves, elastic strap, reservoir bag (shape key inflate), `SOCKET_Face` fit.
10. **Ward suction (#29)** — wall regulator with gauge needle `PIVOT_Needle`, canister, tubing, Yankauer tip.
11. **Nasogastric tube (#30) and urinary catheter + urometer (#31)** — packaged + deployed states; urometer chamber with fill shape key.

**Verification**

Per asset: validator green (triangles within class budget, naming, required empties, scale in metres, UVs, no non-manifold), previews approved by you, imports into Unity as a working prefab via the import pipeline, and the interaction-spec row works with the greybox stand-in replaced.

**Files changed**

`tools/blender/assets/<category>/*.py`, `docs/research/equipment/*.md`, exported FBX in `unity/Assets/_Project/Art/Equipment/` (rebuildable). Starting budgets (B-S1/B-S8, tunable by EXP-BL-3): small props ≤ 5k tris, large machines ≤ 20k, textures ≤ 1K (≤ 2K for towers/monitors), ~512 px/m texel density; per view ≤ 150 batches, ≤ 750k tris; ≥ 72 fps.

**Dependencies**

p0-blender-equipment-s04 (quality bar locked), s05 (import pipeline)

**Common issues**

Soft/organic shapes (bags, tubing, cloth, gloves) → cloth/curve simulation baked inside the script; readable text (labels, screens, gauges where numbers matter) → live `LABEL_`/`SCREEN_` surfaces in Unity, not baked geometry; inverted normals → recalculate outside before export; FBX unit 1.0 with applied scale.


#### p0-blender-equipment-s10 — Furniture and transport

- [ ] Self-verification and Sannan manual acceptance recorded.

Source: [Original step](../docs/plans/p0/p0-blender-equipment/steps/p0-blender-equipment-s10-furniture-transport.md). Initial status: Planned; no implementation acceptance claimed.

**What**

Our own procedural Blender Python assets for: Hospital bed / trolley (#3), Wheelchair (#4), Transfer trolley + portable monitor + oxygen cylinder (#38), Medication trolley + drug chart (#32), Anti-embolism stockings + IPC device (#34), Clippers (#35) and skin marker (#36), Desk + computer workstation, Phone, Clinical waste bin. Each is clinically recognisable, built from its reference sheet, VR-ready, and carries the interaction points its row in `equipment-interaction-spec.md` needs.

**How to implement**

1. Write/confirm the reference sheet for each item (p0-medical-research-data-s09); free-model references are visual only.
2. One script per asset: `tools/blender/assets/<category>/<asset_id>.py` using `procsense_bpy` helpers; runs from Blender's Scripting tab or headless (`blender -b -P …`).
3. Organisation: collection per asset, objects `EQ_<AssetId>_<Part>`, shared `M_*` materials, applied transforms, origin at floor contact / natural grip; separate parts for anything that moves or changes state.
4. `task blender-build -- <asset>` → `task blender-validate` → `task blender-preview`; the developer reviews the 4-view previews with the 1.7 m reference, then you review in Blender and in the headset.
5. **Hospital bed / trolley (#3)** — frame, mattress, side rails `PIVOT_Rail_L/R`, head section `PIVOT_Head` (flat ↔ head-up), castors, brake pedal, controls `SCREEN_BedControl`; patient `SOCKET_Lie`/`SOCKET_Sit`.
6. **Wheelchair (#4)** — frame, wheels (rotating), footrests `PIVOT_Footrest`, brakes, push handles `GRAB_Push`, `SOCKET_Seat`.
7. **Transfer trolley + portable monitor + oxygen cylinder (#38)** — trolley with cylinder holder and monitor bracket; small portable monitor `SCREEN_Portable`; cylinder with gauge `PIVOT_Needle` + `LABEL_Cylinder`.
8. **Medication trolley + drug chart (#32)** — lockable trolley with drawers `PIVOT_Drawer_n`, top surface, chart holder `SCREEN_DrugChart`.
9. **Anti-embolism stockings + IPC device (#34)** — stockings (on-leg / folded states), IPC sleeves + pump unit `SCREEN_IPC`.
10. **Clippers (#35) and skin marker (#36)** — handheld clipper with head, marker pen with cap.
11. **Desk + computer workstation** — desk, monitor `SCREEN_EMR`, keyboard, mouse, folder slot `SOCKET_Folder`, report tray `SOCKET_Report`.
12. **Phone** — wall/desk phone with handset `GRAB_Handset`, cord curve, ring light emissive.
13. **Clinical waste bin** — pedal bin with lid `PIVOT_Lid`, pedal `PIVOT_Pedal`.

**Verification**

Per asset: validator green (triangles within class budget, naming, required empties, scale in metres, UVs, no non-manifold), previews approved by you, imports into Unity as a working prefab via the import pipeline, and the interaction-spec row works with the greybox stand-in replaced.

**Files changed**

`tools/blender/assets/<category>/*.py`, `docs/research/equipment/*.md`, exported FBX in `unity/Assets/_Project/Art/Equipment/` (rebuildable). Starting budgets (B-S1/B-S8, tunable by EXP-BL-3): small props ≤ 5k tris, large machines ≤ 20k, textures ≤ 1K (≤ 2K for towers/monitors), ~512 px/m texel density; per view ≤ 150 batches, ≤ 750k tris; ≥ 72 fps.

**Dependencies**

p0-blender-equipment-s04 (quality bar locked), s05 (import pipeline)

**Common issues**

Soft/organic shapes (bags, tubing, cloth, gloves) → cloth/curve simulation baked inside the script; readable text (labels, screens, gauges where numbers matter) → live `LABEL_`/`SCREEN_` surfaces in Unity, not baked geometry; inverted normals → recalculate outside before export; FBX unit 1.0 with applied scale.


#### p0-blender-equipment-s11 — OT machinery 1

- [ ] Self-verification and Sannan manual acceptance recorded.

Source: [Original step](../docs/plans/p0/p0-blender-equipment/steps/p0-blender-equipment-s11-ot-machinery-1.md). Initial status: Planned; no implementation acceptance claimed.

**What**

Our own procedural Blender Python assets for: Operating table + positioning padding (#40), Ceiling surgical lights + control panel (#44), Anaesthesia machine + check log (#41), OT patient monitor + capnograph (#9/#42). Each is clinically recognisable, built from its reference sheet, VR-ready, and carries the interaction points its row in `equipment-interaction-spec.md` needs.

**How to implement**

1. Write/confirm the reference sheet for each item (p0-medical-research-data-s09); free-model references are visual only.
2. One script per asset: `tools/blender/assets/<category>/<asset_id>.py` using `procsense_bpy` helpers; runs from Blender's Scripting tab or headless (`blender -b -P …`).
3. Organisation: collection per asset, objects `EQ_<AssetId>_<Part>`, shared `M_*` materials, applied transforms, origin at floor contact / natural grip; separate parts for anything that moves or changes state.
4. `task blender-build -- <asset>` → `task blender-validate` → `task blender-preview`; the developer reviews the 4-view previews with the 1.7 m reference, then you review in Blender and in the headset.
5. **Operating table + positioning padding (#40)** — column base, table top in sections (head, back, seat, legs) each `PIVOT_*`, side rails, arm boards, hand control `SCREEN_TableControl`; gel pads with `SOCKET_Pad_n`.
6. **Ceiling surgical lights + control panel (#44)** — ceiling mount, spring arms with joints `PIVOT_Arm_n`, two light heads with LED arrays (emissive + spotlight anchor `LIGHT_Head_n`), sterile handle `GRAB_Handle`; wall control panel `SCREEN_LightControl` (on/off, intensity).
7. **Anaesthesia machine + check log (#41)** — workstation body, drawers `PIVOT_Drawer_n`, ventilator screen `SCREEN_Vent`, flowmeters, vaporiser, breathing circuit + bag, APL valve, check-log clipboard `SCREEN_CheckLog`, `ATTACH_Circuit`.
8. **OT patient monitor + capnograph (#9/#42)** — larger arm/boom-mounted monitor `SCREEN_OTMonitor` with module slots; capnography module + sampling line `ATTACH_EtCO2`.

**Verification**

Per asset: validator green (triangles within class budget, naming, required empties, scale in metres, UVs, no non-manifold), previews approved by you, imports into Unity as a working prefab via the import pipeline, and the interaction-spec row works with the greybox stand-in replaced.

**Files changed**

`tools/blender/assets/<category>/*.py`, `docs/research/equipment/*.md`, exported FBX in `unity/Assets/_Project/Art/Equipment/` (rebuildable). Starting budgets (B-S1/B-S8, tunable by EXP-BL-3): small props ≤ 5k tris, large machines ≤ 20k, textures ≤ 1K (≤ 2K for towers/monitors), ~512 px/m texel density; per view ≤ 150 batches, ≤ 750k tris; ≥ 72 fps.

**Dependencies**

p0-blender-equipment-s04, s05; p0-environment-rooms-s06 (OT anchors)

**Common issues**

Soft/organic shapes (bags, tubing, cloth, gloves) → cloth/curve simulation baked inside the script; readable text (labels, screens, gauges where numbers matter) → live `LABEL_`/`SCREEN_` surfaces in Unity, not baked geometry; inverted normals → recalculate outside before export; FBX unit 1.0 with applied scale.


#### p0-blender-equipment-s12 — OT machinery 2

- [ ] Self-verification and Sannan manual acceptance recorded.

Source: [Original step](../docs/plans/p0/p0-blender-equipment/steps/p0-blender-equipment-s12-ot-machinery-2.md). Initial status: Planned; no implementation acceptance claimed.

**What**

Our own procedural Blender Python assets for: Laparoscopic tower (#45), CO2 cylinder + spare (#46), Electrosurgical unit + return electrode (#47), Forced-air warming device + blanket (#37). Each is clinically recognisable, built from its reference sheet, VR-ready, and carries the interaction points its row in `equipment-interaction-spec.md` needs.

**How to implement**

1. Write/confirm the reference sheet for each item (p0-medical-research-data-s09); free-model references are visual only.
2. One script per asset: `tools/blender/assets/<category>/<asset_id>.py` using `procsense_bpy` helpers; runs from Blender's Scripting tab or headless (`blender -b -P …`).
3. Organisation: collection per asset, objects `EQ_<AssetId>_<Part>`, shared `M_*` materials, applied transforms, origin at floor contact / natural grip; separate parts for anything that moves or changes state.
4. `task blender-build -- <asset>` → `task blender-validate` → `task blender-preview`; the developer reviews the 4-view previews with the 1.7 m reference, then you review in Blender and in the headset.
5. **Laparoscopic tower (#45)** — cart with 4 stacked units — camera control unit, light source, insufflator (`SCREEN_Insufflator` pressure/flow/fault), recorder — monitor on arm `SCREEN_LapMonitor`, power buttons, cable bundle, status LEDs (fault = red); budget ≤ 20k (B-S1 example).
6. **CO2 cylinder + spare (#46)** — cylinder with valve, regulator and gauge `PIVOT_Needle` (level from scenario) + `LABEL_Cylinder`, trolley/strap, swap interaction `SOCKET_Regulator`.
7. **Electrosurgical unit + return electrode (#47)** — generator box with `SCREEN_ESU` ('pad OK' / alarm), footswitch, active cable, return pad (packaged / applied states) with `SOCKET_PatientThigh`.
8. **Forced-air warming device + blanket (#37)** — unit with hose, `SCREEN_Warmer`, power button; blanket (folded / applied states).

**Verification**

Per asset: validator green (triangles within class budget, naming, required empties, scale in metres, UVs, no non-manifold), previews approved by you, imports into Unity as a working prefab via the import pipeline, and the interaction-spec row works with the greybox stand-in replaced.

**Files changed**

`tools/blender/assets/<category>/*.py`, `docs/research/equipment/*.md`, exported FBX in `unity/Assets/_Project/Art/Equipment/` (rebuildable). Starting budgets (B-S1/B-S8, tunable by EXP-BL-3): small props ≤ 5k tris, large machines ≤ 20k, textures ≤ 1K (≤ 2K for towers/monitors), ~512 px/m texel density; per view ≤ 150 batches, ≤ 750k tris; ≥ 72 fps.

**Dependencies**

p0-blender-equipment-s04, s05; p0-environment-rooms-s06

**Common issues**

Soft/organic shapes (bags, tubing, cloth, gloves) → cloth/curve simulation baked inside the script; readable text (labels, screens, gauges where numbers matter) → live `LABEL_`/`SCREEN_` surfaces in Unity, not baked geometry; inverted normals → recalculate outside before export; FBX unit 1.0 with applied scale.


#### p0-blender-equipment-s13 — OT trolleys, storage and hygiene

- [ ] Self-verification and Sannan manual acceptance recorded.

Source: [Original step](../docs/plans/p0/p0-blender-equipment/steps/p0-blender-equipment-s13-ot-trolleys-storage-hygiene.md). Initial status: Planned; no implementation acceptance claimed.

**What**

Our own procedural Blender Python assets for: Difficult-airway trolley + laryngoscope + tubes (#43), Resuscitation trolley + defibrillator (#50), Instrument trays + sterility indicator (#48), Mayo / instrument trolley + back table, Storage cupboards, Scrub sink, hand-rub dispenser, wipes dispenser, PPE dispensers (cap, mask, gown, gloves) (#8), Drapes (#49). Each is clinically recognisable, built from its reference sheet, VR-ready, and carries the interaction points its row in `equipment-interaction-spec.md` needs.

**How to implement**

1. Write/confirm the reference sheet for each item (p0-medical-research-data-s09); free-model references are visual only.
2. One script per asset: `tools/blender/assets/<category>/<asset_id>.py` using `procsense_bpy` helpers; runs from Blender's Scripting tab or headless (`blender -b -P …`).
3. Organisation: collection per asset, objects `EQ_<AssetId>_<Part>`, shared `M_*` materials, applied transforms, origin at floor contact / natural grip; separate parts for anything that moves or changes state.
4. `task blender-build -- <asset>` → `task blender-validate` → `task blender-preview`; the developer reviews the 4-view previews with the 1.7 m reference, then you review in Blender and in the headset.
5. **Difficult-airway trolley + laryngoscope + tubes (#43)** — trolley with labelled drawers `PIVOT_Drawer_n`; laryngoscope handle + blade with hinge `PIVOT_Blade` (light on when open); tracheal tubes, bougie, supraglottic device.
6. **Resuscitation trolley + defibrillator (#50)** — trolley with tamper seal (intact/broken), drawers, oxygen holder; defibrillator `SCREEN_Defib`, pads, paddles.
7. **Instrument trays + sterility indicator (#48)** — wrapped tray (wrapped / opened states), instruments laid out, indicator strip with colour material (pass/fail from scenario).
8. **Mayo / instrument trolley + back table** — stainless surfaces with `SOCKET_Tray_n`, drape surface.
9. **Storage cupboards** — wall + floor units, glass/solid doors `PIVOT_Door_n`, drawers, shelf `SOCKET_Item_n` positions for retrievable kit.
10. **Scrub sink, hand-rub dispenser, wipes dispenser** — trough sink with sensor/elbow taps `PIVOT_Tap`, water emitter anchor; wall dispensers with `GRAB_`/proximity triggers.
11. **PPE dispensers (cap, mask, gown, gloves) (#8)** — wall dispenser boxes; wearable items (mask, cap, gown, gloves) with on-body states.
12. **Drapes (#49)** — folded pack + draped-on-patient state (cloth simulation baked).

**Verification**

Per asset: validator green (triangles within class budget, naming, required empties, scale in metres, UVs, no non-manifold), previews approved by you, imports into Unity as a working prefab via the import pipeline, and the interaction-spec row works with the greybox stand-in replaced.

**Files changed**

`tools/blender/assets/<category>/*.py`, `docs/research/equipment/*.md`, exported FBX in `unity/Assets/_Project/Art/Equipment/` (rebuildable). Starting budgets (B-S1/B-S8, tunable by EXP-BL-3): small props ≤ 5k tris, large machines ≤ 20k, textures ≤ 1K (≤ 2K for towers/monitors), ~512 px/m texel density; per view ≤ 150 batches, ≤ 750k tris; ≥ 72 fps.

**Dependencies**

p0-blender-equipment-s04, s05; p0-environment-rooms-s06

**Common issues**

Soft/organic shapes (bags, tubing, cloth, gloves) → cloth/curve simulation baked inside the script; readable text (labels, screens, gauges where numbers matter) → live `LABEL_`/`SCREEN_` surfaces in Unity, not baked geometry; inverted normals → recalculate outside before export; FBX unit 1.0 with applied scale.


#### p0-blender-equipment-s14 — Boards, doors and signs

- [ ] Self-verification and Sannan manual acceptance recorded.

Source: [Original step](../docs/plans/p0/p0-blender-equipment/steps/p0-blender-equipment-s14-boards-doors-signs.md). Initial status: Planned; no implementation acceptance claimed.

**What**

Our own procedural Blender Python assets for: WHO checklist board (#51), Diagnostic doors + sign plates, OT doors. Each is clinically recognisable, built from its reference sheet, VR-ready, and carries the interaction points its row in `equipment-interaction-spec.md` needs.

**How to implement**

1. Write/confirm the reference sheet for each item (p0-medical-research-data-s09); free-model references are visual only.
2. One script per asset: `tools/blender/assets/<category>/<asset_id>.py` using `procsense_bpy` helpers; runs from Blender's Scripting tab or headless (`blender -b -P …`).
3. Organisation: collection per asset, objects `EQ_<AssetId>_<Part>`, shared `M_*` materials, applied transforms, origin at floor contact / natural grip; separate parts for anything that moves or changes state.
4. `task blender-build -- <asset>` → `task blender-validate` → `task blender-preview`; the developer reviews the 4-view previews with the 1.7 m reference, then you review in Blender and in the headset.
5. **WHO checklist board (#51)** — wall board/screen `SCREEN_WHOBoard` with frame; separate extension area.
6. **Diagnostic doors + sign plates** — double swing doors with `PIVOT_Leaf_L/R`, kick plates, vision panels, wall sign plate `LABEL_Sign` (CT Scan, Imaging, X-ray, Laboratory, MRCP); trainee-block collider helper `COL_Block`.
7. **OT doors** — sliding or swing OT doors with `PIVOT_`, theatre sign `LABEL_Sign`.

**Verification**

Per asset: validator green (triangles within class budget, naming, required empties, scale in metres, UVs, no non-manifold), previews approved by you, imports into Unity as a working prefab via the import pipeline, and the interaction-spec row works with the greybox stand-in replaced.

**Files changed**

`tools/blender/assets/<category>/*.py`, `docs/research/equipment/*.md`, exported FBX in `unity/Assets/_Project/Art/Equipment/` (rebuildable). Starting budgets (B-S1/B-S8, tunable by EXP-BL-3): small props ≤ 5k tris, large machines ≤ 20k, textures ≤ 1K (≤ 2K for towers/monitors), ~512 px/m texel density; per view ≤ 150 batches, ≤ 750k tris; ≥ 72 fps.

**Dependencies**

p0-blender-equipment-s04 (quality bar locked), s05 (import pipeline)

**Common issues**

Soft/organic shapes (bags, tubing, cloth, gloves) → cloth/curve simulation baked inside the script; readable text (labels, screens, gauges where numbers matter) → live `LABEL_`/`SCREEN_` surfaces in Unity, not baked geometry; inverted normals → recalculate outside before export; FBX unit 1.0 with applied scale.


#### p0-blender-equipment-s15 — LOD and performance pass (EXP-BL-3, EXP-BL-4)

- [ ] Self-verification and Sannan manual acceptance recorded.

Source: [Original step](../docs/plans/p0/p0-blender-equipment/steps/p0-blender-equipment-s15-lod-performance-pass.md). Initial status: Planned; no implementation acceptance claimed.

**What**

Profile the fully dressed OT and patient bay on Quest 2 (worst views); generate LODs for large machines; tighten or relax budget classes; time `task blender-build-all` to decide whether generated FBX files are committed or rebuilt in CI.

**How to implement**

1. OVR Metrics in the worst OT view with all P0 assets + characters placed.
2. `lod.py` LOD1/LOD2 for items > 5k tris; LODGroup set by the importer.
3. Update `budgets.json`; re-run validator over all assets.
4. EXP-BL-4: time the full build; decide commit vs rebuild (with EXP-FND-1).

**Verification**

Worst OT view ≤ 150 batches, ≥ 72 fps; decisions recorded.

**Files changed**

`tools/blender/budgets.json`, README experiment log.

**Dependencies**

p0-blender-equipment-s06–s14; p0-environment-rooms-s06.

**Common issues**

LODs that pop visibly → adjust LOD transition distances, not geometry. CI build time grows with assets — cache unchanged assets by script hash.


### p0/p0-characters


#### p0-characters-s01 — Choose the base human set (ADR)

- [ ] Self-verification and Sannan manual acceptance recorded.

Source: [Original step](../docs/plans/p0/p0-characters/steps/p0-characters-s01-base-human-set.md). Initial status: Planned; no implementation acceptance claimed.

**What**

Compare the candidate human sources (Microsoft Rocketbox avatars; MPFB 2 / MakeHuman in Blender) on: Quest poly budget (≤ 25k tris per staff character with 2 LODs, B-S3), rig compatibility with Mixamo/Humanoid, outfits available, facial blend shapes for visemes and expressions, skin-tone range; team picks one; ADR written.

**How to implement**

1. Import 2 samples from each candidate into Unity; check Humanoid rig, retarget a Mixamo walk and talk clip.
2. Profile 1 patient + 3 staff on screen on Quest 2.
3. Check each candidate's terms when downloading (you said licence filtering isn't needed — record terms anyway in the ADR for the attributions page).
4. ADR `docs/decisions/ADR-0002-base-humans.md` with the decision and alternatives.

**Verification**

ADR approved by you; the chosen set imports and animates cleanly.

**Files changed**

`docs/decisions/ADR-0002-base-humans.md`, test imports.

**Dependencies**

p0-vr-platform-s01.

**Common issues**

Foot sliding → root motion off + speed matching (B-S3 troubleshooting).


#### p0-characters-s02 — Patient models for the 17 P0 cases

- [ ] Self-verification and Sannan manual acceptance recorded.

Source: [Original step](../docs/plans/p0/p0-characters/steps/p0-characters-s02-patient-models.md). Initial status: Planned; no implementation acceptance claimed.

**What**

Patient characters covering each P0 case's demographics (age, sex, build — e.g. Mrs Ayesha Khan 42 F, BMI 27; Mr Usman 34 M; Ms Sadia 24 F), in gown or own clothes per scenario, with body sockets for every bedside interaction: wrist (wristband, cannula), finger (SpO2), upper arm (BP cuff), chest + limbs (10 ECG electrodes), ear (thermometer), abdomen regions (examination hotspots), face (oxygen devices), legs (stockings), thigh (diathermy pad).

**How to implement**

1. Generate/adjust from the chosen base; reuse models where demographics match (YAGNI).
2. Outfits (gown, own clothes) scripted in Blender with the same pipeline as equipment.
3. Socket empties named `SOCKET_<Part>` consistent with `equipment-interaction-spec.md`.
4. Case file references a `patient_model` ID.

**Verification**

Every P0 case maps to a model; the pilot wristband, SpO2 probe and BP cuff snap to the right sockets.

**Files changed**

`unity/Assets/_Project/Art/Characters/Patients/*`, outfit scripts.

**Dependencies**

p0-characters-s01.

**Common issues**

Keep shape keys ≤ 15 per mesh to limit skinning cost (B-S2).


#### p0-characters-s03 — Clinical signs: shaders and blend shapes (EXP-CHR-1)

- [ ] Self-verification and Sannan manual acceptance recorded.

Source: [Original step](../docs/plans/p0/p0-characters/steps/p0-characters-s03-clinical-signs.md). Initial status: Planned; no implementation acceptance claimed.

**What**

Visible signs driven by Core state: skin overlay shader params `_Pallor`, `_Flush`, `_Rash`, `_Sweat`, jaundice (skin + yellow sclera), cyanosis cue; expression blend shapes Pain, Fear, Anxious, Calm, Confused, Nauseated, Faint; opioid sedation (small pupils + drowsy); S2 guarding (curled holding right lower abdomen); anaphylaxis (rash/flush + swollen lips) — all blended over 0.3–1 s.

**How to implement**

1. URP Shader Graph skin overlay with mask texture for rash regions; sclera material param for jaundice.
2. Blend shapes sculpted/scripted on the chosen base; mapping table Core state → params in `content/visuals/signs.json`.
3. EXP-CHR-1: screenshots per sign across skin tones in the review pack; reviewers identify the sign (target ≥ 80%).
4. Skin-tone caveat (inventory D138): test pallor/cyanosis/jaundice on darker skin tones too.

**Verification**

T-AV3 cues visible; EXP-CHR-1 ≥ 80% recognition per sign (or fix list recorded).

**Files changed**

Shader Graph, blend shapes, `content/visuals/signs.json`, review screenshots.

**Dependencies**

p0-characters-s02.

**Common issues**

Rapid changes must blend smoothly (T-AV4).


#### p0-characters-s04 — Staff avatars (people physically present)

- [ ] Self-verification and Sannan manual acceptance recorded.

Source: [Original step](../docs/plans/p0/p0-characters/steps/p0-characters-s04-staff-avatars.md). Initial status: Planned; no implementation acceptance claimed.

**What**

Characters for the roles physically present in A rooms: ward/ED nurse, triage nurse, anaesthetist, surgeon, scrub nurse, circulating/ODP, porter (recovery nurse in P1) — role-coloured uniforms for recognisability, Humanoid rigs, NPC controller (waypoints/NavMesh, look-at, gestures).

**How to implement**

1. Base meshes from the chosen set; uniforms scripted/re-textured per role (B-S3 Step 1).
2. `Scripts/Characters/NPCController.cs`: positions from room manifests (`NPC_<Role>`), look-at the trainee when speaking, gesture triggers from staff intents.
3. LOD ×2, ≤ 25k tris each (B-S3 config).
4. Role recognisability check: show 5 testers each avatar without labels; ≥ 4/5 name the role correctly.
5. Name badges with role text (live `LABEL_` surface) as a backup cue.

**Verification**

Each role placed and recognisable in the headset; T-ST-A2: 5 NPCs in the OR within budget.

**Files changed**

`unity/Assets/_Project/Art/Characters/Staff/*`, outfit Blender scripts `tools/blender/assets/outfits/*.py`, `Scripts/Characters/NPCController.cs`, room manifests (NPC anchors).

**Dependencies**

p0-characters-s01.

**Common issues**

Two NPCs on the same path → avoidance priorities (T-ST-A4).


#### p0-characters-s05 — Animation set and behaviour mapping

- [ ] Self-verification and Sannan manual acceptance recorded.

Source: [Original step](../docs/plans/p0/p0-characters/steps/p0-characters-s05-animation-behaviour-mapping.md). Initial status: Planned; no implementation acceptance claimed.

**What**

Clip set: idle, breathing (speed = RR / 15 so visible breaths match the monitor), talk loop, nod, shake head, point, hand-over, wince, guard abdomen, fidget, slump, walk, sit, lie; Animator params Mode (int), Intensity (float), EventCue (trigger); `BehaviourView` maps Core behaviour mode + intensity to Animator, shapes and shader params.

**How to implement**

1. Mixamo (or chosen set) clips retargeted to Humanoid; avatar masks for upper-body gestures.
2. `Scripts/Characters/BehaviourView.cs` subscribes to Observable behaviour; interpolates weights (no popping).
3. Breathing layer speed from Core RR; EXP-CHR-3: count breaths per minute vs monitor RR.

**Verification**

T-AV1 each mode distinct; breaths counted = RR; T-ST-A1 every clip plays.

**Files changed**

Animator controllers `unity/Assets/_Project/Art/Characters/Animation/*`, `Scripts/Characters/BehaviourView.cs`, retargeted clips.

**Dependencies**

p0-characters-s04; p0-physiology-events-behaviour-s10.

**Common issues**

Breathing animation too subtle to count in VR → exaggerate chest/abdomen motion slightly while keeping the rate exact.


#### p0-characters-s06 — Patient walking, wheelchair, trolley and porter escort

- [ ] Self-verification and Sannan manual acceptance recorded.

Source: [Original step](../docs/plans/p0/p0-characters/steps/p0-characters-s06-movement-escort.md). Initial status: Planned; no implementation acceptance claimed.

**What**

NavMesh agents for the patient and porter: patient walks (stable), sits in a wheelchair pushed by the porter, or lies on a trolley pushed by the porter (IK hands on handles) with an escort nurse following; enter/exit B doors.

**How to implement**

1. Greybox capsules first (M2) using the same controller; real characters swap in later.
2. `Scripts/Characters/Movement/PatientMover.cs` modes: Walk, Wheelchair, Trolley; driven by SceneDirector.
3. Porter push with IK hand targets on `GRAB_Push` empties (T-ST-A3).
4. Patient agent speed/gait from behaviour mode (in pain → slower); escort nurse keeps beside the trolley.
5. Collision/avoidance with other NPCs (B-S3 T-ST-A4).

**Verification**

Each transport mode plays end to end to every B door and to the OT.

**Files changed**

`Scripts/Characters/Movement/*`.

**Dependencies**

p0-vr-platform-s06.

**Common issues**

Keep movement cosmetic — Core owns timing.


#### p0-characters-s07 — Characters performance pass (EXP-CHR-2)

- [ ] Self-verification and Sannan manual acceptance recorded.

Source: [Original step](../docs/plans/p0/p0-characters/steps/p0-characters-s07-performance-pass.md). Initial status: Planned; no implementation acceptance claimed.

**What**

Profile patient + up to 5 staff in the OT on Quest 2; tune LODs, texture sizes, skinned-mesh counts and blend-shape counts.

**How to implement**

1. OVR Metrics capture in the OT with the full cast; Unity profiler skinning cost (T-AV2, T-ST-A2).
2. Adjust and record EXP-CHR-2.
3. Worst case: patient + anaesthetist + surgeon + scrub + circulating + porter in the OT during Time Out.
4. Levers: LOD bias, skin weights ≤ 2 bones for distant LODs, texture size 1K, blend shapes only on LOD0.

**Verification**

OVR Metrics capture within budget recorded in the characters README (EXP-CHR-2).

**Files changed**

`docs/plans/p0/p0-characters/README.md` (EXP-CHR-2 result), LOD settings on character prefabs in `unity/Assets/_Project/Art/Characters/*`, profiler captures in `docs/plans/p0/p0-vr-platform/perf/`.

**Dependencies**

p0-characters-s05.

**Common issues**

Profiling in the Editor is meaningless for Quest — always capture on device. Disabling blend shapes on distant LODs is invisible to users but saves a lot.


#### p0-characters-s08 — Nurse enters the room and replaces the wristband

- [ ] Self-verification and Sannan manual acceptance recorded.

Source: [Original step](../docs/plans/p0/p0-characters/steps/p0-characters-s08-nurse-entry-and-wristband-replacement.md). Initial status: Planned; no implementation acceptance claimed.

**What**

A visible 3D nurse who, when the Core resolves a wristband problem (`NurseTaskChanged` events from p0-simulation-workflow-s18), **comes into the patient bay from outside**, walks to the bedside,
checks the notes, and **replaces the incorrect (or missing) wristband on the patient's wrist**, then leaves or stays as the scene needs.

1. **Entry**: the nurse starts outside the bay (corridor side), enters through the bay doorway on the Core's `CALLED` event, and walks a NavMesh path to `ANCHOR_NurseBedside`.
2. **Bedside**: "checking records" pose (notes/tablet in hand) on `AT_BEDSIDE`.
3. **Replacement**: IK/animation to the patient's wrist socket `ANCHOR_Wristband`; the old band mesh is removed and a new band mesh appears with the corrected text from the Core observable.
4. **Resolution**: nurse steps back, gives a short nod/line (voice: p0-voice-audio-s06), then walks out via `ANCHOR_NurseExit` or returns to her normal place.
5. **Missing band**: the same sequence, with no old band to remove; for a reported allergy she also attaches the allergy band (colour set by the institution decision).
6. **Greybox first**: a capsule nurse with labels uses the same controller in the walking skeleton; real avatar and animations replace it (p0-characters-s04).

**How to implement**

1. `Scripts/Characters/Staff/NurseWristbandController.cs`: a view only. The Core publishes `CALLED → AT_BEDSIDE → REPLACING → DONE` in one Apply (no simulated-time delay, owner decision 09 Oct 2026); the controller queues them and plays the walk-in, check and replacement at its own pace (cosmetic, same rule as p0-characters-s06).
2. Anchors from p0-environment-rooms: `ANCHOR_BayDoor` (outside → inside), `ANCHOR_NurseBedside`, `ANCHOR_NurseExit`; add them to `room_manifest_bay.json` and the real-room model.
3. Reuse the staff avatar and walk/gesture clips from p0-characters-s04/s05; add clips `Nurse_CheckNotes`, `Nurse_ReplaceWristband` (Mixamo or own), with IK target on the wrist socket.
4. Wristband swap: the band prefab (p0-blender-equipment-s06) with a text field bound to the observable; the nurse's replacement band is the same prefab with the new text.
5. If the path is blocked (trainee standing in the doorway), the nurse waits and says an "excuse me" line; the Core is never delayed by the animation.
6. Play Mode test: drive the Core to each nurse state and assert the nurse reaches the right anchor and the wrist band text equals the observable.

**Verification**

Device simulator: report a mismatch on the mismatch fixture → nurse enters, replaces, the new band reads correctly. Headset: same, plus the missing-band fixture.

**Files changed**

`unity/Assets/_Project/Scripts/Characters/Staff/*`, animation clips, prefabs, anchors in the room manifest, Play Mode test.

**Dependencies**

p0-simulation-workflow-s18, p0-characters-s04 (staff avatars), p0-characters-s05 (animation mapping), p0-characters-s06 (NavMesh movement), p0-environment-rooms (bay doorway and anchors),
p0-blender-equipment-s06 (wristband asset), p0-vr-platform-s06 (scene director).

**Common issues**

- The bay needs a real doorway/corridor side; the greybox bay must include one or the nurse cannot "enter from outside".
- The band text always comes from the Core observable; the view only decides *when to show it* (at the replace moment of its animation), so the new text never appears before the nurse reaches the wrist.


### p0/p0-diagnostic-transitions


#### p0-diagnostic-transitions-s01 — Core events for the diagnostics loop (EXP-DT-2)

- [ ] Self-verification and Sannan manual acceptance recorded.

Source: [Original step](../docs/plans/p0/p0-diagnostic-transitions/steps/p0-diagnostic-transitions-s01-core-events.md). Initial status: Planned; no implementation acceptance claimed.

**What**

One deterministic loop in Core: `InvestigationRequested` → transport (patient) or sample dispatch (lab) → `PatientDeparted` / `SampleSent` → time-skip by the test's turnaround while physiology and events keep running → `ResultReady` → `PatientReturned` / `ReportDelivered`.

**How to implement**

1. Wire investigations (p0-clinical-logic-s06) + transport (p0-simulation-workflow-s13) + time-skip (p0-core-engine-s06).
2. Lab tests: no patient movement; samples dispatched via porter/tube station.
3. EXP-DT-2: untreated S2 fixture sent to CT deteriorates on the clock during the trip.
4. Tests: `dotnet test --filter FullyQualifiedName~DiagnosticsLoop`.

**Verification**

Loop test and EXP-DT-2 pass headless.

**Files changed**

`src/ProcSense.Core/Diagnostics/DiagnosticsLoop.cs`, event types in `src/ProcSense.Core/Events/`, `tests/ProcSense.Core.Tests/DiagnosticsLoopTests.cs`.

**Dependencies**

p0-clinical-logic-s06; p0-simulation-workflow-s13; p0-core-engine-s06.

**Common issues**

Time-skip must stop early if an event demands attention during the wait (e.g. critical-result call, deterioration alarm) — otherwise the trainee 'misses' something they never had a chance to see.


#### p0-diagnostic-transitions-s02 — Doors, signs and trainee blocking

- [ ] Self-verification and Sannan manual acceptance recorded.

Source: [Original step](../docs/plans/p0/p0-diagnostic-transitions/steps/p0-diagnostic-transitions-s02-doors-blocking.md). Initial status: Planned; no implementation acceptance claimed.

**What**

B doors (CT Scan, Imaging, X-ray, Laboratory, MRCP) with sign plates in the corridor; the trainee can walk up but not pass (short message: 'Patients and staff only — wait for the result'); patient/porter pass on their layer.

**How to implement**

1. Door prefabs (greybox first, Blender versions from p0-blender-equipment-s14).
2. Proximity message UI; leaves animate open for the patient/porter only.
3. Message text from content (localisable), shown once per approach with a 5 s cooldown.
4. Doors use the Blender door asset's `PIVOT_Leaf_*` when available; greybox hinge until then.

**Verification**

In the headset: walking into a B door shows the message and you can't pass; the patient walks through on the next trip.

**Files changed**

Door prefabs (greybox now, Blender `tools/blender/assets/rooms/doors.py` later), `unity/Assets/_Project/Scripts/Diagnostics/DoorController.cs`, `content/diagnostics.json` (messages).

**Dependencies**

p0-environment-rooms-s07.

**Common issues**

Physics push-through: the trainee rig can still be teleported past a blocker — exclude door areas from teleport targets too.


#### p0-diagnostic-transitions-s03 — Patient goes to the door and back

- [ ] Self-verification and Sannan manual acceptance recorded.

Source: [Original step](../docs/plans/p0/p0-diagnostic-transitions/steps/p0-diagnostic-transitions-s03-patient-transport-visuals.md). Initial status: Planned; no implementation acceptance claimed.

**What**

The patient goes to the right door by the trainee's chosen mode (walk / wheelchair / trolley + escort), disappears inside, and returns when Core reports `PatientReturned`; the trainee may accompany to the door.

**How to implement**

1. SceneDirector + PatientMover; wait overlay while inside (s06).
2. Mode visuals: walk (patient agent), wheelchair (patient seated, porter pushing), trolley (patient lying, porter pushing, escort following, portable monitor attached).
3. Door sequence: leaves open → patient enters → leaves close → patient hidden at the `DOOR_<Dest>_Inside` anchor → reverse on return.
4. Accompanying trainee is teleported to the corridor side of the door.

**Verification**

Play Mode test for each mode to the CT door; replay of the exported session gives the same end state.

**Files changed**

`unity/Assets/_Project/Scripts/Diagnostics/DoorSequence.cs`, `Scripts/Characters/Movement/PatientMover.cs` (modes), door 'inside' anchors in `room_manifest_corridor.json`, Play Mode tests.

**Dependencies**

p0-vr-platform-s06; p0-characters-s06.

**Common issues**

Patient visually 'arrives' before Core says so → Unity must wait for the Core state, never the other way round.


#### p0-diagnostic-transitions-s04 — Lab samples via porter or tube station

- [ ] Self-verification and Sannan manual acceptance recorded.

Source: [Original step](../docs/plans/p0/p0-diagnostic-transitions/steps/p0-diagnostic-transitions-s04-lab-samples.md). Initial status: Planned; no implementation acceptance claimed.

**What**

Lab tests don't move the patient: after bedside sampling and labelling, the trainee places the tubes in the specimen drop / pneumatic-tube station (or hands them to the porter); samples travel to the Laboratory door; results return after the test's turnaround; unlabelled or pre-labelled tubes trigger the walkthrough's rejection / wrong-blood consequences.

**How to implement**

1. Socket on the tube station emits `send_samples(tubes[])`; optional porter pickup animation.
2. Core validates labels (p0-clinical-logic-s07).
3. Station accepts only labelled tubes; unlabelled tube → returned with a lab rejection message after a delay (Core decides).
4. Porter route (optional) uses the same PatientMover agent type without a patient.

**Verification**

Play Mode test: labelled LFT tube → result after turnaround; unlabelled → rejection message and re-take required.

**Files changed**

`unity/Assets/_Project/Scripts/Diagnostics/TubeStation.cs`, tube-station prefab (Blender asset from p0-blender-equipment-s08), `content/diagnostics.json`, Play Mode tests.

**Dependencies**

p0-diagnostic-transitions-s01.

**Common issues**

Tubes left on the bed table never get sent — the nurse can prompt in practice mode (beginner safety net), not in assessment.


#### p0-diagnostic-transitions-s05 — Report hand-back and EMR update

- [ ] Self-verification and Sannan manual acceptance recorded.

Source: [Original step](../docs/plans/p0/p0-diagnostic-transitions/steps/p0-diagnostic-transitions-s05-report-handback-emr.md). Initial status: Planned; no implementation acceptance claimed.

**What**

Returning patient/porter carries a printed report (live text from the result); the trainee takes it and places it on the desk or in the folder; the EMR updated at `ResultReady` (badge on the tab).

**How to implement**

1. Printed report prop (`SCREEN_Sheet`) with content from Core; desk/folder sockets.
2. EMR listens to ResultReady (p0-patient-records-emr-s05).
3. Printed report prop spawns in the returning patient's/porter's hand; the porter hands it over with a short line; if ignored it's left on the desk (no loss).
4. `file_document` action when placed in the folder; EMR already updated at ResultReady.

**Verification**

Play Mode test: CT trip → report prop arrives → filed in folder → visible as a folder page; EMR Imaging tab has the same report.

**Files changed**

`unity/Assets/_Project/Scripts/Records/PrintedReport.cs`, desk/folder sockets, `Scripts/Records/Emr/EmrResultListener.cs`, Play Mode tests.

**Dependencies**

p0-patient-records-emr-s05.

**Common issues**

Report prop text must come from Core (same as EMR) — never a separate copy that can disagree.


#### p0-diagnostic-transitions-s06 — Wait overlay, notifications and usability (EXP-DT-1)

- [ ] Self-verification and Sannan manual acceptance recorded.

Source: [Original step](../docs/plans/p0/p0-diagnostic-transitions/steps/p0-diagnostic-transitions-s06-wait-overlay-usability.md). Initial status: Planned; no implementation acceptance claimed.

**What**

During a B-room visit: a small overlay shows the patient's location and the sim-clock advance (~10 s on screen, configurable in content); a notification when results are ready; the trainee can keep working on other tasks.

**How to implement**

1. `Scripts/Diagnostics/WaitOverlay.cs`; wait length from `content/diagnostics.json`.
2. EXP-DT-1 with testers (no instructions).
3. Overlay shows: patient location ('In CT'), expected back time on the sim clock, and a fast-forward indicator while time-skips.
4. Notification: tablet + EMR badge + nurse line 'Results are back' when ResultReady fires.

**Verification**

EXP-DT-1 result in the README; changing the wait length in `content/diagnostics.json` changes the on-screen wait without code changes.

**Files changed**

UI, `content/diagnostics.json`, results notes.

**Dependencies**

p0-diagnostic-transitions-s03.

**Common issues**

Too short a wait feels instant and unrealistic; too long is boring — keep it configurable and decide with testers (EXP-DT-1).


### p0/p0-patient-records-emr


#### p0-patient-records-emr-s01 — Records data model in Core

- [ ] Self-verification and Sannan manual acceptance recorded.

Source: [Original step](../docs/plans/p0/p0-patient-records-emr/steps/p0-patient-records-emr-s01-records-data-model.md). Initial status: Planned; no implementation acceptance claimed.

**What**

Core `Records`: current-episode documents (clerking note, observation chart, consent, ward checklist, printed results) vs historical record (previous admissions, reports, tests, scans from `content/records/`); documents created by events (`ResultReady`, consent signed, clerking lines); verbs `open_document`, `view_image`, `search_patient` logged for scoring ('did the trainee look at the previous HbA1c?').

**How to implement**

1. `src/ProcSense.Core/Records/*`: Document {id, type, episode: current | historical, date_offset, title, body, image_ref?}.
2. Folder view = current-episode documents; EMR view = all documents.
3. Tests: ResultReady adds a document visible in both views; historical documents only in the EMR.
4. Document types enum in content (clerking, obs_chart, consent, ward_checklist, lab_report, imaging_report, referral, discharge_summary…).
5. Search (`search_patient`) returns only the case patient + distractor patients (to practise finding the right record).

**Verification**

`dotnet test --filter FullyQualifiedName~Records` green: ResultReady → document in both views; historical only in EMR; `open_document` logged.

**Files changed**

`src/ProcSense.Core/Records/*`, tests.

**Dependencies**

p0-core-engine-s04.

**Common issues**

Two copies of a result (folder vs EMR) that can disagree → one Core document, two views.


#### p0-patient-records-emr-s02 — Physical folder interactions

- [ ] Self-verification and Sannan manual acceptance recorded.

Source: [Original step](../docs/plans/p0/p0-patient-records-emr/steps/p0-patient-records-emr-s02-physical-folder.md). Initial status: Planned; no implementation acceptance claimed.

**What**

Pick up the folder, open the cover, flip pages with animated page turns, pick up / place reports in the pocket, read documents (live text), view printed images.

**How to implement**

1. Greybox folder first; Blender folder (p0-blender-equipment-s06) with page rig later.
2. `Scripts/Records/FolderController.cs`: page turn on grab-drag or button; pages render Core documents.
3. Page turning independent of frame rate.
4. Page content rendering: each page is a RenderTexture or TMP canvas bound to a Core document; long documents paginate.
5. Place report: drop on folder pocket `SOCKET_Report` → `file_document` action → becomes a page.
6. Grab-drag page turn with a threshold; button fallback for accessibility.

**Verification**

Play Mode test: open folder, turn 3 pages, file a report; in the headset page turns feel natural (EXP-REC-3 later).

**Files changed**

`Scripts/Records/Folder*`.

**Dependencies**

p0-patient-records-emr-s01.

**Common issues**

Page meshes clipping through each other while turning → turn one page at a time with a short lock; text unreadable at an angle → snap the folder to a reading pose when held.


#### p0-patient-records-emr-s03 — EMR shell: search, patient list, tabs

- [ ] Self-verification and Sannan manual acceptance recorded.

Source: [Original step](../docs/plans/p0/p0-patient-records-emr/steps/p0-patient-records-emr-s03-emr-shell-search-tabs.md). Initial status: Planned; no implementation acceptance claimed.

**What**

Workstation UI on `SCREEN_EMR`: search by name / MRN / DOB, quick patient list, virtual keyboard, tabs across the top that open/close/switch (several open at once), consistent hospital-system look.

**How to implement**

1. `Scripts/Records/Emr/EmrShell.cs`, `SearchView`, `TabBar`.
2. Pointer + poke input; quick list first because VR typing is slow.
3. Search ranks exact MRN > name prefix > DOB; results show name, MRN, DOB, location.
4. Tabs: max 6 open, close button, reorder not needed (YAGNI).
5. Screen visual style: plain hospital-system look (white/grey, clear headers) — readable over decorative.
6. Pointer = controller ray with poke fallback; keyboard is a world-space keypad.

**Verification**

Play Mode test: search 'khan' → VAR-S1-A first; open Profile, Labs, Imaging tabs; switch; close one.

**Files changed**

`Scripts/Records/Emr/*`.

**Dependencies**

p0-patient-records-emr-s01.

**Common issues**

Searching with the virtual keyboard is slow — the quick list must already show the current patient first.


#### p0-patient-records-emr-s04 — EMR sections

- [ ] Self-verification and Sannan manual acceptance recorded.

Source: [Original step](../docs/plans/p0/p0-patient-records-emr/steps/p0-patient-records-emr-s04-emr-sections.md). Initial status: Planned; no implementation acceptance claimed.

**What**

Sections: profile (identity, allergies, alerts), current episode (reason, plan, NEWS2 trend), labs (current + previous, flags, comments, trend charts), imaging (reports + image viewer), reports (all documents), medical history, medications (current list + confirmed prescriptions), previous admissions/tests.

**How to implement**

1. One view per section reading Core documents; trend chart for repeated labs/NEWS2.
2. Image viewer for approved images only.
3. Labs section: table with H/L flags + trend chart for analytes with ≥ 2 values (e.g. NEWS2, K, creatinine).
4. Imaging viewer: approved images only, zoom/pan, report beside the image; report-only notice where no image exists.
5. History section from `content/records/<case>.json` with dates relative to 'today'.

**Verification**

Every P0 case shows all sections populated from content; a case with no images shows the report-only notice.

**Files changed**

`Scripts/Records/Emr/Sections/*`.

**Dependencies**

p0-patient-records-emr-s03; p0-medical-research-data-s14, s16.

**Common issues**

Historical data that contradicts the current case misleads trainees — every historical record must be derived from or consistent with walkthrough facts (p0-medical-research-data-s14).


#### p0-patient-records-emr-s05 — Automatic updates and badges

- [ ] Self-verification and Sannan manual acceptance recorded.

Source: [Original step](../docs/plans/p0/p0-patient-records-emr/steps/p0-patient-records-emr-s05-auto-update-badges.md). Initial status: Planned; no implementation acceptance claimed.

**What**

New results appear instantly in the right section with a badge on its tab; the badge clears when opened.

**How to implement**

1. Subscribe to ResultReady; badge state per tab.
2. `EmrTabBadges` keeps unseen document IDs per tab; ResultReady adds, opening the document removes; state lives in Core Records (so replay reproduces it), not only in Unity.
3. Critical results also flash the phone; the EMR badge never replaces the call.
4. Play Mode test: send VAR-S1-A LFTs → after turnaround the Labs tab shows a badge with count 1 → open → badge cleared → log contains `open_document`.

**Verification**

Play Mode test green; in the headset the Labs badge appears when the patient returns from the lab door and clears when you open the result.

**Files changed**

`src/ProcSense.Core/Records/Badges.cs` (seen/unseen state), `unity/Assets/_Project/Scripts/Records/Emr/TabBadge.cs`, Play Mode test.

**Dependencies**

p0-patient-records-emr-s03.

**Common issues**

Badges that never clear (e.g. document opened in the folder, not the EMR) → define 'seen' as opened anywhere; Core tracks it.


#### p0-patient-records-emr-s06 — Records usability (EXP-REC-1…3)

- [ ] Self-verification and Sannan manual acceptance recorded.

Source: [Original step](../docs/plans/p0/p0-patient-records-emr/steps/p0-patient-records-emr-s06-records-usability.md). Initial status: Planned; no implementation acceptance claimed.

**What**

Tests with users: find the last HbA1c / previous report (EXP-REC-1, target ≤ 30 s), readability at desk distance (EXP-REC-2), page flipping with controllers (EXP-REC-3); fix the top issues.

**How to implement**

1. Run, record, fix, re-test.
2. Tasks: (1) 'Find Mrs Khan's previous ultrasound report' (2) 'What was the last HbA1c for Mrs Nasreen?' (3) 'Add the new LFT report to the folder' (4) 'Read the consent form page 2'.
3. Measure time and errors per task; note readability complaints at desk distance (EXP-REC-2) and page-flip failures (EXP-REC-3).
4. Fix the top 3 issues (e.g. larger tab labels, quick-list sorting, page-turn grab zone) and re-test with 2 new users.

**Verification**

Results table in `docs/plans/p0/p0-patient-records-emr/results.md`: EXP-REC-1 median ≤ 30 s, EXP-REC-2 text size chosen, EXP-REC-3 no help needed — or the fixes made.

**Files changed**

`docs/plans/p0/p0-patient-records-emr/results.md`, task script `docs/plans/p0/p0-patient-records-emr/usability-tasks.md`, fixes in `Scripts/Records/*`.

**Dependencies**

p0-patient-records-emr-s04.

**Common issues**

Participants unfamiliar with hospital EMRs need a 30-second orientation — keep it identical for everyone.


### p0/p0-vr-interaction-ui


#### p0-vr-interaction-ui-s01 — Skeleton 1 interactions

- [ ] Self-verification and Sannan manual acceptance recorded.

Source: [Original step](../docs/plans/p0/p0-vr-interaction-ui/steps/%E2%9C%85%20p0-vr-interaction-ui-s01-skeleton1-interactions.md). Initial status: Historically approved; re-verification and Sannan review pending.

**What**

Make Steps 1–3 playable in the greybox bay:
- **Tablet** (hand-held): "Talk" menu (introduce yourself, ask name + DOB, read name to patient, ask allergies, ask what happened, ask why here,
  ask responsible adult, history topics, summarise) → patient answer shown as subtitle + added to the clerking note panel; "Confirm identity" /
  "Report mismatch" buttons enabled per Core state.
- **Wristband**: grab/point at the wrist → identifier card shows the band text (from Core observable).
- **Notes folder**: grab → simple panel with the notes identifiers (full folder/page-flip comes in p0-patient-records-emr).
- **Monitor**: SpO2 probe and BP cuff placeholders snap into sockets on the patient; "Measure" button on the monitor; values appear only when Core reports them.
- **Thermometer, RR count, ACVPU, pain**: tablet buttons for skeleton 1.
- Rejected actions show a short hint (e.g. "Identity not confirmed").

**How to implement**

1. Views: `TabletView`, `SubtitleView`, `ClerkingNoteView`, `IdentityCardView`, `MonitorView` — all read `ObservableState` only.
2. Emitters on each placeholder (SocketEmitter on probe/cuff sockets, UIButtonEmitter on tablet buttons).
3. All text shown comes from Core/content (no strings with clinical content in Unity).
4. Play Mode test drives the same sequence as `golden/skeleton1_correct` through the emitters and compares the log hash.

**Verification**

Device simulator: complete Steps 1–3 using only tablet buttons and sockets; Play Mode golden test passes. Headset: same sequence by hand.

**Files changed**

`unity/Assets/_Project/Scripts/Views/*`, tablet prefab, placeholder prefabs with emitters, Play Mode test.

**Dependencies**

p0-vr-platform-s03, p0-environment-rooms-s01, p0-simulation-workflow-s04.

**Common issues**

Text unreadable on Quest → raise font size (start of EXP-UI-2); sockets too fussy → enlarge snap radius.

**Carried-over requirements (added 09 October 2026)**

- The **tablet "Read wristband" button** (alternative when the band cannot be grabbed, test T01-5) and the nurse/missing-band interactions are specified in p0-vr-interaction-ui-s12 (source reference: p0-vr-interaction-ui-s12-wristband-nurse-interaction.md) (DT-008). Build the fallback button here if skeleton 1 needs it first.


#### p0-vr-interaction-ui-s02 — Interaction framework and equipment state machines

- [ ] Self-verification and Sannan manual acceptance recorded.

Source: [Original step](../docs/plans/p0/p0-vr-interaction-ui/steps/p0-vr-interaction-ui-s02-interaction-framework.md). Initial status: Planned; no implementation acceptance claimed.

**What**

Data-driven `EquipmentStateMachine` (states/transitions from `content/equipment/*.json`): trigger (grab | socket | activate | ui) → emits a SimAction → Core confirms → visual state (child objects / material params); socket library (finger, arm, chest, wrist, ear, face, stand, trolley slots) with interaction layers; poke panels built from `panel` entries; fault visuals from Observable equipment faults.

**How to implement**

1. `Scripts/Interaction/EquipmentStateMachine.cs`, `SocketLibrary`, `PanelBuilder`, `FaultListener`.
2. Example chain (B-S9): fluid bag SEALED → SPIKED (giving set socket) → HUNG (pole) → CONNECTED (cannula) → RUNNING (pump button), each a Core action.
3. Haptics on snap; hover highlight; one item per socket (T-IN4).
4. Every interaction has a tablet fallback emitting the same action (T-IN5).

**Verification**

T-IN1, T-IN2, T-IN4, T-IN5 green with the pilot assets.

**Files changed**

`unity/Assets/_Project/Scripts/Interaction/*`, `content/equipment/*.json`.

**Dependencies**

p0-vr-interaction-ui-s01; p0-blender-equipment-s05.

**Common issues**

Objects fall through → kinematic sockets + collision layers.


#### p0-vr-interaction-ui-s03 — Tablet: tabs, talk menu, orders, known facts, hints

- [ ] Self-verification and Sannan manual acceptance recorded.

Source: [Original step](../docs/plans/p0/p0-vr-interaction-ui/steps/p0-vr-interaction-ui-s03-tablet-shell.md). Initial status: Planned; no implementation acceptance claimed.

**What**

Hand-held / wrist-anchored tablet with tabs — Admission, Ask (talk menu by history section), Known (revealed facts by category), Assess (ASA, VTE, scores), Orders (tests with expected turnaround; medicine/fluid/oxygen requests), Results, Treat (pending orders + required-check ticks), Consent (element tracker in practice), Checklist, Handover (SBAR builder), Debrief — plus the practice-mode hint chip.

**How to implement**

1. `Scripts/UI/Tablet/TabletController.cs` + `TabRegistry`; panels as prefabs.
2. All lists (topics, tests, medicine classes) generated from content/Observable — no clinical strings in Unity.
3. Rejected action → short hint, no visual change (T-B2).
4. Panel order follows the walkthrough steps so trainees learn the real sequence; 'Pending' list shows outstanding goals (practice only).

**Verification**

Every P0 Step 1–12 action reachable from the tablet; T-UI1 each panel renders state.

**Files changed**

`Scripts/UI/Tablet/*`, tablet prefab.

**Dependencies**

p0-vr-interaction-ui-s01.

**Common issues**

Blurry text → canvas scale / dynamic resolution.


#### p0-vr-interaction-ui-s04 — Patient monitor: numbers, traces, alarms (EXP-UI-3)

- [ ] Self-verification and Sannan manual acceptance recorded.

Source: [Original step](../docs/plans/p0/p0-vr-interaction-ui/steps/p0-vr-interaction-ui-s04-monitor-traces-alarms.md). Initial status: Planned; no implementation acceptance claimed.

**What**

Monitor view: values only for attached sensors; ECG trace from PTB-XL templates time-scaled to HR (ring buffer), pleth trace, capnography in the OT; NIBP cycle progress then values; display zones colouring; alarms by Core alarm state with acknowledge/silence; artefact visuals when the probe is poorly placed.

**How to implement**

1. `Scripts/UI/Monitor/MonitorView.cs`, `WaveformRenderer.cs` (fixed update rate + interpolation — no jitter).
2. Alarm limits from content (institutional).
3. EXP-UI-3: medical reviewer assesses realism; record fixes.
4. Pleth amplitude/quality reflects probe placement (artefact state from Core); 'no waveform → recheck' teaching (walkthrough Part E).

**Verification**

T-UI3 waveform ≤ 0.5 ms; T-B1 attach → value appears; reviewer verdict recorded.

**Files changed**

`unity/Assets/_Project/Scripts/UI/Monitor/{MonitorView,WaveformRenderer,AlarmIndicator}.cs`, monitor prefab, `content/physio/ecg_templates.json` (consumed), alarm limits in content.

**Dependencies**

p0-clinical-logic-s03; p0-medical-research-data-s15.

**Common issues**

Throttle text to 10 Hz; waveforms at fixed rate.


#### p0-vr-interaction-ui-s05 — Results viewer and critical-result phone

- [ ] Self-verification and Sannan manual acceptance recorded.

Source: [Original step](../docs/plans/p0/p0-vr-interaction-ui/steps/p0-vr-interaction-ui-s05-results-phone.md). Initial status: Planned; no implementation acceptance claimed.

**What**

Results viewer modes: table (value, H/L flag, units, local range, lab comment like 'haemolysed', previous value, acknowledge button), image (licensed images), ECG strip; interpretation picker → decision card; phone that rings for critical results with read-back choices and acknowledgement.

**How to implement**

1. `Scripts/UI/Results/ResultsView.cs` (table / image / strip), `PhoneView.cs`.
2. Same Core data as the EMR (one source).
3. T-UI4 correct H/L flags.
4. Previous values shown with dates (from Records history) and a trend arrow.
5. Lab comment shown verbatim from content (e.g. 'sample haemolysed — please repeat').

**Verification**

Play Mode test: VAR-S1-J fixture shows K 6.4 with the haemolysis comment, repeat shows 4.6; critical fixture rings the phone and blocks until read-back.

**Files changed**

`unity/Assets/_Project/Scripts/UI/Results/{ResultsView,InterpretPicker,PhoneView}.cs`, phone prefab, Play Mode tests.

**Dependencies**

p0-clinical-logic-s08.

**Common issues**

Units must display exactly as content (mmol/L, g/L) — never converted in Unity.


#### p0-vr-interaction-ui-s06 — Bedside equipment interactions

- [ ] Self-verification and Sannan manual acceptance recorded.

Source: [Original step](../docs/plans/p0/p0-vr-interaction-ui/steps/p0-vr-interaction-ui-s06-bedside-interactions.md). Initial status: Planned; no implementation acceptance claimed.

**What**

Implement every patient-bay row of `equipment-interaction-spec.md`: wristband rotate-to-read, allergy band, bed head-up/flat, monitor attach, BP cuff wrap + measure, SpO2 clip, tympanic thermometer, 12-lead ECG electrode placement + printout, stethoscope auscultation audio per site, glucose meter, blood sampling + bedside tube labelling + send to lab, cultures, urine/pregnancy test with timed result, cannula, fluids, pump, oxygen devices + flowmeter, medication trolley/drug chart, sharps, stockings/IPC, clippers vs razor, wheelchair/trolley transport choice, folder, consent form.

**How to implement**

1. One spec row at a time: state machine data + emitters + view; test in the device simulator; tablet fallback each.
2. Specific interactions from B-S9 Step 7: rotate wrist to read band; count breaths from the avatar; pre-labelling possible (→ event); clippers vs razor; tape ring; stockings.
3. Order of work: identity (#1, #2) → monitoring (#9–#15) → sampling (#16–#19) → access/fluids/oxygen (#25–#28) → medicines + safety (#32–#35) → transport (#4, #38) → documents (#5, #7).
4. Each row gets a Play Mode test emitting the same SimActions as the hand interaction.

**Verification**

Every bay spec row works in the headset; T-IN1 states in order.

**Files changed**

`Scripts/Interaction/Bedside/*`, `content/equipment/*.json`.

**Dependencies**

p0-vr-interaction-ui-s02; p0-blender-equipment-s06–s10.

**Common issues**

Small items (tubes, probes) hard to grab with controllers → larger grab colliders than visuals, snap-to-hand poses; hand tracking tested in EXP-VR-2.


#### p0-vr-interaction-ui-s07 — OT equipment interactions

- [ ] Self-verification and Sannan manual acceptance recorded.

Source: [Original step](../docs/plans/p0/p0-vr-interaction-ui/steps/p0-vr-interaction-ui-s07-ot-interactions.md). Initial status: Planned; no implementation acceptance claimed.

**What**

Implement every OT row of `equipment-interaction-spec.md`: lights on/intensity + aiming, table positions, anaesthesia machine check + check log, OT monitor + capnography, airway trolley drawers + laryngoscope light, suction test, lap tower power/faults, CO2 gauge + swap, diathermy pad placement, instrument tray sterility indicator, drapes, resus trolley seal/drawers, forced-air warmer, scrub sink/hand-rub, PPE donning, storage cupboards/drawers, work surfaces, WHO board.

**How to implement**

1. One row at a time; station checks emit `check_station` / `report_fault` to Core.
2. Fault visuals from Observable (e.g. insufflator low supply message, red LED).
3. Perf: T-IN3 OR with all items ≥ 72 fps.

**Verification**

Every OT spec row works in the headset; VAR-S1-A Scene 10 readiness achievable by hand.

**Files changed**

`Scripts/Interaction/OT/*`, `content/equipment/*.json`.

**Dependencies**

p0-vr-interaction-ui-s02; p0-blender-equipment-s11–s13; p0-simulation-workflow-s14.

**Common issues**

Too many poke buttons crowded on machines → panels pop out on approach; station state must come from Core, not from local UI flags.


#### p0-vr-interaction-ui-s08 — WHO checklist board and ward checklist

- [ ] Self-verification and Sannan manual acceptance recorded.

Source: [Original step](../docs/plans/p0/p0-vr-interaction-ui/steps/p0-vr-interaction-ui-s08-checklist-boards.md). Initial status: Planned; no implementation acceptance claimed.

**What**

WHO board bound to the SafetyChecklist observable: Sign In / Time Out (Sign Out in P1) items in order, pending → confirmed (green), STOPPED state in red with the reason, ProcSense extensions in a separate section; ward checklist panel with verify buttons.

**How to implement**

1. `Scripts/UI/Checklist/ChecklistBoard.cs` on the board's `SCREEN_WHOBoard` (greybox quad first).
2. The trainee directs each question to a role (menu) — wording from content only.
3. Each item shows who should answer (role icon); the trainee directs the question via a menu; NPC answers appear with voice.
4. STOPPED state shows the reason and what fixes it; resumes after the correction action (T-SC6).
5. Ward checklist panel: verify buttons show the state evidence (e.g. 'pregnancy test: negative 08:42').

**Verification**

Play Mode test drives Sign In + Time Out through the board for VAR-S1-A; a deliberate wrong allergy answer shows STOPPED in red.

**Files changed**

`Scripts/UI/Checklist/*`.

**Dependencies**

p0-simulation-workflow-s16.

**Common issues**

Board order from the `order` field.


#### p0-vr-interaction-ui-s09 — Debrief screen and timeline

- [ ] Self-verification and Sannan manual acceptance recorded.

Source: [Original step](../docs/plans/p0/p0-vr-interaction-ui/steps/p0-vr-interaction-ui-s09-debrief-ui.md). Initial status: Planned; no implementation acceptance claimed.

**What**

Debrief panel readable in ~2 minutes: end state, critical items first, category bars, items with evidence links (tap → log time, what happened, rule, source), timeline with guideline windows and lateness (e.g. sepsis antibiotic window), unnecessary tests with 'cost', next-case recommendation (practice), AI narrative tab hidden until P1.

**How to implement**

1. `Scripts/UI/Debrief/DebriefView.cs` reading the ScoreReport/DebriefFacts JSON.
2. Timeline markers link to log sequence numbers (T-DU1).
3. Collapse minor items by default (B-S5 troubleshooting).
4. Evidence drawer: tap an item → log time, what the trainee did/said, rule ID, source (register ID → citation).
5. Template/AI toggle present but AI tab hidden until P1 (T-DU2 offline path).

**Verification**

T-DU1–T-DU4 green; testers read the debrief in ≤ 2 minutes and can name their top mistake (EXP-SC-2).

**Files changed**

`unity/Assets/_Project/Scripts/UI/Debrief/{DebriefView,TimelineView,EvidenceDrawer}.cs`, debrief prefab.

**Dependencies**

p0-scoring-debrief-s04.

**Common issues**

Too long → collapse minor items by default; an item with no evidence link means the scorer didn't attach log references (fix in scoring, not UI).


#### p0-vr-interaction-ui-s10 — Accessibility and readability (EXP-UI-2)

- [ ] Self-verification and Sannan manual acceptance recorded.

Source: [Original step](../docs/plans/p0/p0-vr-interaction-ui/steps/p0-vr-interaction-ui-s10-accessibility-readability.md). Initial status: Planned; no implementation acceptance claimed.

**What**

Minimum text size (≥ 24 px equivalent at 1 m), contrast AA, subtitles size/position, left-hand mode, seated mode, colour-blind-safe status colours (shape/text, not colour alone), UI scale setting persisted.

**How to implement**

1. Run EXP-UI-2 (sizes at arm's length and desk distance); write `ui-guidelines.md`; apply to all panels.
2. Settings panel: UI scale, subtitles, handedness.
3. Test sizes 18/24/32 px equivalent at 1 m and at desk distance (0.6 m) with 3 users; choose minimums.
4. Contrast check of every panel colour pair against WCAG AA.
5. Status never by colour alone: icons + text for normal/abnormal/critical.
6. Settings persisted with PlayerPrefs (per-viewer convenience only).

**Verification**

`ui-guidelines.md` written; T-UI2 legible at 1 m; a settings change persists across restarts.

**Files changed**

`docs/plans/p0/p0-vr-interaction-ui/ui-guidelines.md`, settings panel.

**Dependencies**

p0-vr-interaction-ui-s03.

**Common issues**

World-space text blurs at a distance → use TextMeshPro SDF and adequate canvas scale; dynamic resolution can soften text — test on device.


#### p0-vr-interaction-ui-s11 — Usability pass (EXP-UI-1, EXP-VR-2)

- [ ] Self-verification and Sannan manual acceptance recorded.

Source: [Original step](../docs/plans/p0/p0-vr-interaction-ui/steps/p0-vr-interaction-ui-s11-usability-pass.md). Initial status: Planned; no implementation acceptance claimed.

**What**

Think-aloud sessions with 5 new users: Step 1 identity check, attaching sensors, ordering a test, reading results, leading Sign In; controllers vs hand tracking for reading the wristband and labelling a tube.

**How to implement**

1. Script the tasks; record success, time, confusion points.
2. Fix the top issues; re-test.
3. 5 participants new to ProcSense; think-aloud; observer notes; session logs captured.
4. Hand tracking variant: enable Meta hand tracking with XRI hands for the same tasks; compare success/time (EXP-VR-2).
5. Top 3 issues fixed and re-tested with 2 new users.

**Verification**

EXP-UI-1 (≥ 4/5 complete Step 1 unaided) and EXP-VR-2 recorded in `results/usability-pass.md`.

**Files changed**

`docs/plans/p0/p0-vr-interaction-ui/results/*.md`.

**Dependencies**

p0-vr-interaction-ui-s06, s07.

**Common issues**

Testers who are gamers aren't the target users — recruit at least 3 medical students. Don't coach during think-aloud; note where they get stuck instead.


#### p0-vr-interaction-ui-s12 — Wristband interactions: report, nurse hand-over, tablet fallback, missing band

- [ ] Self-verification and Sannan manual acceptance recorded.

Source: [Original step](../docs/plans/p0/p0-vr-interaction-ui/steps/p0-vr-interaction-ui-s12-wristband-nurse-interaction.md). Initial status: Planned; no implementation acceptance claimed.

**What**

Everything the trainee does with wristbands in VR, including the failure paths:

1. **Read the band (normal)**: grab or point at the wrist, rotate to read name / DOB / MRN (text from the Core observable) — already in p0-vr-interaction-ui-s01.
2. **Tablet fallback (T01-5)**: if the band cannot be grabbed (VR failure, accessibility, seated play) a **"Read wristband"** button on the tablet's Admission tab shows the same card. It emits the same `read_wristband` action, so Core, log and replay do not know the difference.
3. **Report mismatch**: the "Report mismatch" button (tablet) or pointing at the band and choosing "Report problem". Core starts the nurse task; the tablet shows "Nurse called".
4. **Nurse hand-over**: while the nurse replaces the band the trainee sees the nurse (p0-characters-s08) and a status chip ("Nurse at bedside — checking records"); when the Core says `DONE` the trainee **reads the new band again** and presses **Confirm identity**.
5. **Missing band**: the wrist shows no band; tablet shows "No wristband on patient" and a **"Ask nurse to apply a band"** button (`request_wristband`). The allergy band, when the case has one, is visible after it is applied.
6. **Feedback**: red banner "Identity not confirmed" until verified (practice mode), green tick after; rejected actions show their reason text from the Core (`ST1_MISMATCH`, `WRISTBAND_MISSING`, `IDENTITY_NOT_CONFIRMED`).

**How to implement**

1. Views read `ObservableState` only: `IdentityCardView` (extend), `NurseStatusChip`, `WristbandSocketView`. Emitters: `TabletReadWristbandEmitter`, `ReportMismatchEmitter`, `RequestWristbandEmitter`.
2. Button enabled/disabled strictly from Core state (never from local flags).
3. All wording from Core/content (no clinical text in Unity).
4. Accessibility: the tablet path must be reachable seated, with large text (ties into p0-vr-interaction-ui-s10).
5. Play Mode tests: (a) grab path and tablet path produce the **same log hash**; (b) mismatch fixture → nurse → re-read → confirm; (c) missing-band fixture; (d) hash equals the headless golden from p0-simulation-workflow-s18.

**Verification**

Device simulator: complete Step 1 on the normal, mismatch and missing-band fixtures using only the tablet, then again by hand in the headset.

**Files changed**

`unity/Assets/_Project/Scripts/Views/*`, tablet prefab (Admission tab), emitters, Play Mode tests.

**Dependencies**

p0-vr-interaction-ui-s01, p0-vr-interaction-ui-s03 (tablet shell), p0-simulation-workflow-s18, p0-characters-s08.

**Common issues**

Band text unreadable at arm's length → font size/contrast (EXP-UI-2); two buttons doing the same job confuse players → label the tablet one clearly as the alternative.


### p0/p0-voice-audio


#### p0-voice-audio-s01 — Voice-line catalogue

- [ ] Self-verification and Sannan manual acceptance recorded.

Source: [Original step](../docs/plans/p0/p0-voice-audio/steps/p0-voice-audio-s01-voice-line-catalogue.md). Initial status: Planned; no implementation acceptance claimed.

**What**

Every spoken line with an ID (e.g. `PAT-VAR-S1-A-IDENTITY`), speaker, text, the Core event that plays it, and its content source — generated from case files (patient answers), approved staff intents and checklist NPC answers.

**How to implement**

1. `tools/voice/catalogue.py` builds `content/voice/catalogue.json` from content (no hand duplication).
2. Missing line for an event → build warning (T-VP1).
3. First-person spoken variants for walkthrough narration answers are drafted by the developer, flagged `spoken_variant: authored` and sent for review.
4. Staff lines come from approved intents; checklist NPC answers from content.

**Verification**

`task voice-catalogue` builds the catalogue; it reports 0 events without a line for P0 cases and lists authored variants awaiting review.

**Files changed**

`tools/voice/catalogue.py`, `content/voice/catalogue.json` (generated), `content/voice/spoken_variants.json` (authored, review status).

**Dependencies**

p0-medical-research-data-s10–s12.

**Common issues**

Lines in two places drift apart → the catalogue is generated from content only, never edited by hand.


#### p0-voice-audio-s02 — Offline TTS rendering (EXP-VOX-1)

- [ ] Self-verification and Sannan manual acceptance recorded.

Source: [Original step](../docs/plans/p0/p0-voice-audio/steps/p0-voice-audio-s02-offline-tts.md). Initial status: Planned; no implementation acceptance claimed.

**What**

Render the catalogue with an offline TTS engine run as an external tool (Piper voices; compare Kokoro in EXP-VOX-1), one voice per character (`voices.yaml`), batch on CPU, loudness-normalise to −16 LUFS (ffmpeg loudnorm), encode Ogg Vorbis, write the line → file index.

**How to implement**

1. `tools/voice/render.py` (uv; parallel CPU workers) + `voices.yaml` (character → voice + terms).
2. Pronunciation overrides for drug/medical terms (respelling dictionary).
3. EXP-VOX-1 blind listening test (Piper vs Kokoro); record choice per role.
4. Output `unity/Assets/_Project/Audio/Voice/*.ogg` (LFS) + `catalogue.json` paths.

**Verification**

T-VP2 all lines rendered; T-VP3 voice terms recorded; EXP-VOX-1 result recorded.

**Files changed**

`tools/voice/*`, `voices.yaml`, audio files.

**Dependencies**

p0-voice-audio-s01.

**Common issues**

Piper is GPL — run it only as an external tool (never link it into the app). Mispronounced drug names → respelling dictionary. Re-render only changed lines (hash the text + voice).


#### p0-voice-audio-s03 — Subtitles and voice playback

- [ ] Self-verification and Sannan manual acceptance recorded.

Source: [Original step](../docs/plans/p0/p0-voice-audio/steps/p0-voice-audio-s03-subtitles-playback.md). Initial status: Planned; no implementation acceptance claimed.

**What**

Voice playback driven by Core events: each line plays from the speaking character's position (spatial AudioSource on the avatar's head), with an always-on subtitle (speaker name + text), a queue so lines don't overlap, an interrupt policy (the patient finishes the sentence; urgent staff lines pre-empt), and pooled AudioSources.

**How to implement**

1. `Scripts/Audio/VoicePlayer.cs`, `AudioCatalog.cs`, `SubtitleView.cs`.
2. Interrupt policy: patient finishes sentence, staff urgent lines pre-empt.
3. Subtitles show speaker name + line; stay ≥ 2 s; positioned in view, not over the patient's face.
4. Missing audio file → subtitle only + warning in the log (never silent failure).

**Verification**

Play Mode test: every catalogue line used by VAR-S1-A plays with its subtitle; a removed audio file still shows the subtitle.

**Files changed**

`Scripts/Audio/*`.

**Dependencies**

p0-voice-audio-s02.

**Common issues**

Long lines block urgent ones — urgent priority must pre-empt. Subtitles over the patient's face hide clinical signs — position them below eye level.


#### p0-voice-audio-s04 — Lip sync

- [ ] Self-verification and Sannan manual acceptance recorded.

Source: [Original step](../docs/plans/p0/p0-voice-audio/steps/p0-voice-audio-s04-lip-sync.md). Initial status: Planned; no implementation acceptance claimed.

**What**

uLipSync on patient and staff faces with a viseme profile calibrated on the chosen TTS voices.

**How to implement**

1. Configure per character; calibrate profile; profile CPU cost.
2. Install uLipSync from its repository (record version); one `uLipSync` component per speaking avatar fed by the line's AudioSource; `uLipSyncBlendShape` maps phonemes (A/I/U/E/O/N) to the face's viseme blend shapes.
3. Calibrate a profile per TTS voice family (male/female) using 10 rendered lines; store profiles in `Audio/LipSync/`.
4. Profile CPU cost with 2 simultaneous speakers on Quest 2; disable lip sync on LOD1+ characters.

**Verification**

In the headset: mouths move in time with every voiced line; profiler shows lip sync within budget with 2 speakers; mismatched profile → recalibrate.

**Files changed**

uLipSync package entry in `unity/Packages/manifest.json`, profiles in `unity/Assets/_Project/Audio/LipSync/`, character prefabs.

**Dependencies**

p0-characters-s05; p0-voice-audio-s03.

**Common issues**

Lip sync drifts with streamed/long clips → feed the AudioSource that actually plays; mismatched profile → recalibrate per voice.


#### p0-voice-audio-s05 — Ambience, equipment sounds and alarms (EXP-VOX-2)

- [ ] Self-verification and Sannan manual acceptance recorded.

Source: [Original step](../docs/plans/p0/p0-voice-audio/steps/p0-voice-audio-s05-ambience-alarms.md). Initial status: Planned; no implementation acceptance claimed.

**What**

Room ambience (ward, ED, OT), equipment sounds (cuff inflation, suction, pump, phone ring, insufflator), and monitor alarm sounds tied to Core alarm states (priority levels), with a sound list recording each source.

**How to implement**

1. `Scripts/Audio/AlarmAudioView.cs` subscribed to Observable alarms.
2. Sound list `docs/research/sounds.csv` (source, author) for attributions.
3. EXP-VOX-2: medical reviewer checks alarm behaviour.
4. Alarm priorities (low/medium/high) mapped from Core alarm states; high-priority alarm interrupts ambience ducking.
5. Spatial audio on device sources (monitor, pump, suction) with sensible rolloff.
6. Acknowledge/silence on the monitor stops the sound but logs the acknowledgement.

**Verification**

Play Mode test: SpO2 below threshold fixture triggers the alarm sound; acknowledge silences it and logs the action; reviewer accepts realism (EXP-VOX-2).

**Files changed**

`Scripts/Audio/*`, `Audio/*`, `docs/research/sounds.csv`.

**Dependencies**

p0-vr-interaction-ui-s04.

**Common issues**

Alarm fatigue: continuous loud alarms annoy testers — follow real priority patterns and allow acknowledge; never mute without logging.


#### p0-voice-audio-s06 — Nurse lines for the wristband workflow

- [ ] Self-verification and Sannan manual acceptance recorded.

Source: [Original step](../docs/plans/p0/p0-voice-audio/steps/p0-voice-audio-s06-wristband-nurse-lines.md). Initial status: Planned; no implementation acceptance claimed.

**What**

The spoken and subtitled lines of the nurse during a wristband mismatch or missing band, each with a catalogue ID, speaker, trigger event and source:

| Line (draft intent — final wording authored at implementation) | Trigger |
|---|---|
| Acknowledges the report, says she will check ("Let me check") | event `CALLED` |
| Greets at the bedside / asks to excuse her if the doorway is blocked | event `AT_BEDSIDE` |
| Says she is checking the records | event `AT_BEDSIDE` (second line) |
| Says she is replacing the band and what she found (procedural only) | event `REPLACING` |
| Confirms the new band is on and asks the trainee to check it | event `DONE` |
| Missing band: acknowledges and says she will apply a band | `request_wristband` accepted (after verification) |
| Missing band, asked too early: asks the trainee to verify identity first | rejection `VERIFY_IDENTITY_FIRST` |
| Missing band with allergy: mentions the allergy band | allergy case |
| Nothing to fix (request when the band is fine) | effect `nothing_to_resolve` |

**How to implement**

1. Add the lines to the voice-line catalogue generator input (`content/voice/…`), speaker `NURSE`, `spoken_variant: authored`, flagged for review (the lines are procedural, no clinical values).
2. Render with the offline TTS (p0-voice-audio-s02); subtitles and playback via s03; lip sync via s04 for the nurse avatar.
3. Missing-line check: every nurse task state in p0-simulation-workflow-s18 must have a catalogue line (build warning T-VP1 otherwise).
4. Subtitles are always shown (hearing accessibility) and match the audio text exactly.

**Verification**

Catalogue build has no warnings; in the Play Mode scenario each nurse state plays its line with subtitles.

**Files changed**

`content/voice/*`, catalogue tool input, rendered audio, tests.

**Dependencies**

p0-voice-audio-s01…s04, p0-simulation-workflow-s18, p0-characters-s08.

**Common issues**

Do not put clinical facts in nurse lines (what she "found" must be generic — "the details on the band did not match your records"); anything patient-specific comes from Core data.


### p0/p0-evaluation-performance


#### p0-evaluation-performance-s01 — Skeleton 1 Quest replay check

- [ ] Self-verification and Sannan manual acceptance recorded.

Source: [Original step](../docs/plans/p0/p0-evaluation-performance/steps/p0-evaluation-performance-s01-skeleton1-quest-replay-check.md). Initial status: Planned; no implementation acceptance claimed.

**What**

Prove the headset and the headless engine agree:
1. Play skeleton 1 by hand on the Quest; export the session log.
2. `task sim -- replay --log <quest session>` on the PC → same final-state hash (**EXP-CORE-1**).
3. Record the first real performance numbers on Quest 2/3 in the greybox bay (**EXP-VR-1**, first data point).
4. Note usability observations (could the tester read the band/monitor? find the buttons?) — first input for EXP-UI-1/2.

**How to implement**

1. Pull the log: `adb pull /sdcard/Android/data/<package>/files/sessions/<file>.jsonl` (Taskfile `task quest-pull-session`).
2. Replay + compare hash; write result into `p0-core-engine/README.md` experiment log (EXP-CORE-1).
3. OVR Metrics Tool capture during play; record FPS/GPU/CPU in `p0-vr-platform/README.md` (EXP-VR-1).
4. Short notes file in `docs/plans/p0/p0-evaluation-performance/results/skeleton1.md`.

**Verification**

Hash match reported; numbers recorded; skeleton 1 marked done in `walking-skeleton-1.md`.

**Files changed**

`Taskfile.yml` (`quest-pull-session`), experiment logs in phase READMEs, `results/skeleton1.md`.

**Dependencies**

p0-vr-interaction-ui-s01.

**Common issues**

Hash mismatch → check float formatting/culture, dictionary iteration, `Time.deltaTime` leaking into Core (only fixed steps allowed), IL2CPP differences.


#### p0-evaluation-performance-s02 — Golden suite: 17 cases × 4 paths in CI

- [ ] Self-verification and Sannan manual acceptance recorded.

Source: [Original step](../docs/plans/p0/p0-evaluation-performance/steps/p0-evaluation-performance-s02-golden-suite.md). Initial status: Planned; no implementation acceptance claimed.

**What**

Correct / novice / dangerous / edge action scripts for all 17 P0 cases with expected end states, key facts and score reports, run in CI on every change.

**How to implement**

1. Write paths from the walkthrough (Part D §16–19 for stacked cases; variant teaching points for others).
2. `tests/golden/<case>_<path>.actions.json` + expected JSON; T-SCO1 expected reports.
3. Diff-friendly expected files; deliberate updates via `task golden -- --update`.
4. Path definitions: correct = walkthrough correct path; novice = misses non-critical items (e.g. no summary, incomplete SBAR); dangerous = violates a blocker (e.g. bloods before identity, proceed with fasting breach); edge = timing/ordering corner cases (deadline on tick boundary, repeated skips).
5. Expected files contain end state, critical items, key score items and log hash; review diffs in PRs.

**Verification**

`task golden` runs 68 scripts; CI shows the count; a deliberate change to one expected file makes CI fail with a readable diff.

**Files changed**

`tests/golden/*`, tests.

**Dependencies**

p0-case-library-s02.

**Common issues**

Score changes → the golden diff in the PR explains why.


#### p0-evaluation-performance-s03 — Weekly on-device profiling

- [ ] Self-verification and Sannan manual acceptance recorded.

Source: [Original step](../docs/plans/p0/p0-evaluation-performance/steps/p0-evaluation-performance-s03-weekly-profiling.md). Initial status: Planned; no implementation acceptance claimed.

**What**

A fixed demo route on Quest 2 (bay → corridor → CT door → OT Sign In/Time Out) profiled every week with OVR Metrics (fps, GPU/CPU, memory, hitches); results table; any regression gets a fix step.

**How to implement**

1. Route script/checklist; capture CSV per week into `docs/plans/p0/p0-vr-platform/perf/`.
2. Perf gate: ≥ 72 fps avg, no hitch > 50 ms, ≤ 2.5 GB.
3. Fixed route script: Boot → patient bay (VAR-S1-A Steps 1–3) → corridor → CT door transition → OT greybox/real → Sign In + Time Out; 5 minutes.
4. Capture OVR Metrics CSV (fps, GPU/CPU level, frame time, memory) + Unity Profiler snapshot of the worst moment.
5. Weekly table row: date, build hash, avg fps, 1% low, worst hitch, memory, notes; regressions > 5% open a fix step in the owning phase.

**Verification**

One row per week in `docs/plans/p0/p0-vr-platform/perf/README.md`; any regression has a linked fix step.

**Files changed**

`docs/plans/p0/p0-vr-platform/perf/README.md` (weekly table), `docs/plans/p0/p0-vr-platform/perf/<date>.csv`, `docs/plans/p0/p0-evaluation-performance/profiling-route.md`.

**Dependencies**

p0-vr-platform-s07.

**Common issues**

Thermal throttling skews long captures — always start from a cool headset and the same battery level range.


#### p0-evaluation-performance-s04 — Assessment-mode lock

- [ ] Self-verification and Sannan manual acceptance recorded.

Source: [Original step](../docs/plans/p0/p0-evaluation-performance/steps/p0-evaluation-performance-s04-assessment-mode-lock.md). Initial status: Planned; no implementation acceptance claimed.

**What**

Assessment mode loads only APPROVED_FOR_ASSESSMENT content, disables hints and difficulty adaptation, uses the fixed event schedule, and logs critical items strictly; tested with an unapproved case/value.

**How to implement**

1. Integration test across loader, events, difficulty and UI hints.
2. Single integration test `AssessmentModeTests` runs a session in assessment mode with: one case value marked SOURCE_IDENTIFIED (not approved) → loader refuses with the item ID; an event below assessment status → not loaded (T-E6); difficulty selection → returns the fixed order (T-DF3); hints → absent from ObservableState.
3. Unity: mode chosen only from the main menu (not changeable mid-session); the mode is written into the session log header and shown on the debrief.
4. Practice-only UI (hint chip, consent element tracker, nurse prompts) hidden in assessment via one `Mode` check in the views.

**Verification**

`dotnet test --filter FullyQualifiedName~AssessmentMode` green; a VR session started in assessment mode shows no hints and its log header says `mode: ASSESSMENT`.

**Files changed**

`tests/ProcSense.Core.Tests/AssessmentModeTests.cs`, mode check in `unity/Assets/_Project/Scripts/UI/ModeGate.cs`, log header field `mode`.

**Dependencies**

p0-core-engine-s06.

**Common issues**

Practice-only UI leaking into assessment through a missed view → one central mode check, tested.


#### p0-evaluation-performance-s05 — Usability study (EXP-EVAL-1)

- [ ] Self-verification and Sannan manual acceptance recorded.

Source: [Original step](../docs/plans/p0/p0-evaluation-performance/steps/p0-evaluation-performance-s05-usability-study.md). Initial status: Planned; no implementation acceptance claimed.

**What**

Formal study with medical students / junior doctors (ethics approved): S1 and S2 sessions, System Usability Scale, NASA-TLX, task success and time; analysis script; honest reporting.

**How to implement**

1. Protocol + consent form + task list; schedule sessions.
2. `tools/eval/analyse_usability.py` (uv) → tables/plots.
3. Report results without over-claiming.
4. Participants: target users (medical students / junior doctors) recruited via the supervisor; consent form; pseudonymous IDs only.
5. Session (~45 min): briefing, controls practice (5 min), one S1 case + one S2 case, SUS questionnaire, NASA-TLX, short interview.
6. Measures: SUS score, NASA-TLX subscales, task success per step, time to complete, errors, end states reached.
7. `tools/eval/analyse_usability.py` → tables + plots in `results/`; report median/IQR, no inferential claims beyond the sample.

**Verification**

Results tables and plots generated from the raw session data; protocol and consent forms stored.

**Files changed**

`docs/plans/p0/p0-evaluation-performance/results/*`, analysis script.

**Dependencies**

M8.

**Common issues**

Small samples: report medians/ranges and qualitative findings; avoid claims like 'significantly better' without a proper design.


#### p0-evaluation-performance-s06 — Clinical realism review (walkthrough Part I, EXP-EVAL-2)

- [ ] Self-verification and Sannan manual acceptance recorded.

Source: [Original step](../docs/plans/p0/p0-evaluation-performance/steps/p0-evaluation-performance-s06-clinical-realism-review.md). Initial status: Planned; no implementation acceptance claimed.

**What**

The medical professional plays or watches all P0 cases and completes the walkthrough Part I checklist A–G (medical facts, numbers, surgery, anaesthesia, consent, local policy, VR realism); outcomes recorded; fixes planned.

**How to implement**

1. Review pack from the checklist + videos; session; import decisions.
2. Pack: walkthrough Part I checklist A–G as rows (Item / OK / Correction / Comment) + case videos + debrief samples + open issues list; ≤ 45 min per session, split by section if longer.
3. Session: reviewer marks each item; corrections become issues with owners.
4. `tools/content/import_decisions.py` updates register/issues; content fixes re-validated; affected golden runs re-run.

**Verification**

Checklist A–G completed in `results/clinical-review.md`; every correction has an issue ID and status.

**Files changed**

`docs/research/review-packs/<date>-realism.md`, `docs/plans/p0/p0-evaluation-performance/results/clinical-review.md`, updated `docs/research/issues.md` + register.

**Dependencies**

M8.

**Common issues**

Reviewer time is the bottleneck — prioritise items affecting P0 cases and split packs above 45 minutes.


#### p0-evaluation-performance-s07 — Attributions page and user guide

- [ ] Self-verification and Sannan manual acceptance recorded.

Source: [Original step](../docs/plans/p0/p0-evaluation-performance/steps/p0-evaluation-performance-s07-attributions-user-guide.md). Initial status: Planned; no implementation acceptance claimed.

**What**

In-app and docs attributions generated from the registers (guidelines cited, datasets, images, voices, sounds, character set, tools) and a short trainee/teacher guide (controls, scenarios, debrief).

**How to implement**

1. `tools/eval/attributions.py` → `docs/ATTRIBUTIONS.md` + in-app page.
2. Sources for attributions are only the registers: `docs/research/source_register.csv` (guidelines cited), `dataset_register.csv`, `content/images/index.json`, `tools/voice/voices.yaml`, `docs/research/sounds.csv`, the character-set ADR, third-party packages (Unity packages, NuGet, uv lockfiles).
3. `tools/eval/attributions.py` groups by kind, de-duplicates, and fails if any register row lacks an author/source field.
4. In-app: a 'Credits & sources' panel in the main menu reading a generated `attributions.json` from StreamingAssets.
5. User guide sections: controls (teleport, grab, tablet, push-to-talk in P1), how a case flows (Steps 1–14), what end states mean (postponement = success), the debrief, practice vs assessment mode, safety statement (educational only).

**Verification**

`task attributions` regenerates `docs/ATTRIBUTIONS.md` with no missing fields; a teammate who has never used ProcSense completes VAR-S1-A Step 1 using only `docs/USER_GUIDE.md`.

**Files changed**

`docs/ATTRIBUTIONS.md`, `docs/USER_GUIDE.md`, in-app page.

**Dependencies**

M8.

**Common issues**

Attribution text required by some sources has exact wording — copy it from the source page into the register.


#### p0-evaluation-performance-s08 — P0 release (EXP-REL-1, EXP-REL-2)

- [ ] Self-verification and Sannan manual acceptance recorded.

Source: [Original step](../docs/plans/p0/p0-evaluation-performance/steps/p0-evaluation-performance-s08-release-v1.md). Initial status: Planned; no implementation acceptance claimed.

**What**

Clean-machine build following only the README (EXP-REL-1, T-Q2 ≤ 2 h); 10 full S1 + S2 runs on device without crash and within the perf gate (EXP-REL-2); release checklist; tag v1.0 (you push).

**How to implement**

1. Fresh machine test; stability runs; checklist; tag.
2. Clean machine: fresh Windows user/VM; follow README top to bottom (tools, clone + LFS, `task build`, `task test`, Unity open, `task unity-build -- Demo`); time it (T-Q2 ≤ 2 h); note every missing instruction and fix the README.
3. Stability: 10 full runs (S1 + S2 cases) on Quest 2 with OVR Metrics recording; 0 crashes, perf gate held, session logs replay headless with matching hashes.
4. Release checklist `docs/RELEASE.md`: version bump, content_version stamped, assessment content approved, attributions regenerated, APK + SHA-256, release notes; you create and push the tag.

**Verification**

EXP-REL-1 and EXP-REL-2 recorded as passed in the evaluation README; APK checksum in the release notes.

**Files changed**

`docs/RELEASE.md`, release notes.

**Dependencies**

p0-evaluation-performance-s02–s07.

**Common issues**

Clean-machine test reveals undocumented steps — fix the README immediately, then repeat until it passes without help.


#### p0-evaluation-performance-s09 — FYP report and demo material

- [ ] Self-verification and Sannan manual acceptance recorded.

Source: [Original step](../docs/plans/p0/p0-evaluation-performance/steps/p0-evaluation-performance-s09-report-demo-material.md). Initial status: Planned; no implementation acceptance claimed.

**What**

Collect results already recorded in phase READMEs and experiment logs into the FYP report draft (method, architecture, medical proof-of-truth process, experiments, evaluation results); record a demo video.

**How to implement**

1. `tools/eval/collect_experiments.py` → experiment tables.
2. Report draft in `docs/report/`; demo video script.
3. `tools/eval/collect_experiments.py` scans every phase README experiment table and results folder → `docs/report/experiments.md` (question, method, metric, result, decision).
4. Report outline in `docs/report/README.md`: problem & aims, walkthrough-driven design, architecture (Core / Unity / AI boundary), proof-of-truth process (register, issues, review packs), implementation per phase, evaluation (golden suite, usability, clinical realism, performance), limitations, future work (P1/P2).
5. Figures: architecture diagram (from Master Plan §25), sample debrief, performance charts, usability tables — all from real data.
6. Demo video script: VAR-S1-A happy path (3 min) + VAR-S2-PD deterioration with debrief lateness (3 min).

**Verification**

`task report-experiments` regenerates the experiments chapter; you review the draft outline and the demo video.

**Files changed**

`docs/report/*`, `tools/eval/collect_experiments.py`.

**Dependencies**

p0-evaluation-performance-s08.

**Common issues**

Numbers in the report must come from recorded results — never typed in (INSTRUCTIONS §4c real data only).


### p1/p1-theatre-extension


#### p1-theatre-extension-s01 — Step 15: WHO Sign Out engine

- [ ] Self-verification and Sannan manual acceptance recorded.

Source: [Original step](../docs/plans/p1/p1-theatre-extension/steps/p1-theatre-extension-s01-step15-sign-out.md). Initial status: Planned; no implementation acceptance claimed.

**What**

Sign Out phase in the SafetyChecklist: procedure recorded, instrument/sponge/needle counts complete (discrepancy → search, X-ray per local policy, escalate), specimen labelled and **read aloud including the patient's name**, equipment problems pulled from the Step 11 log, key recovery concerns; omitted antibiotic plan at recovery handover → debrief.

**How to implement**

1. Extend `SafetyChecklist` with SIGN_OUT items from `who_checklist.csv`.
2. Equipment problems auto-listed from the OR readiness log.
3. Verb `read_specimen_label` with patient-name check; count verbs + discrepancy branch (EXP-TX-1).
4. Board shows Sign Out items in the same style as Sign In/Time Out (p0-vr-interaction-ui-s08).

**Verification**

`dotnet test --filter FullyQualifiedName~SignOut` → T15-1, T-EXT1 green; count-discrepancy fixture escalates correctly.

**Files changed**

`src/ProcSense.Core/Checklist/SafetyChecklist.cs` (SIGN_OUT phase), `content/who_checklist.csv` (Sign Out rows), tests.

**Dependencies**

p0-simulation-workflow-s16.

**Common issues**

Fault not reported → minor item (E15-A).


#### p1-theatre-extension-s02 — Intra-operative training events (time-lapse)

- [ ] Self-verification and Sannan manual acceptance recorded.

Source: [Original step](../docs/plans/p1/p1-theatre-extension/steps/p1-theatre-extension-s02-intra-op-events.md). Initial status: Planned; no implementation acceptance claimed.

**What**

The procedure is abstracted as a time-lapse (the operation itself is out of scope — walkthrough 2.3) with clinician-approved, clearly labelled 'ProcSense educational extension' events: e.g. adhesions → surgeon asks for the open set (trainee communicates/fetches), bleeding needing suction, unexpected findings (unexpected stone / normal-looking appendix).

**How to implement**

1. Events as data (Type D teaching devices, each requiring clinician validation) from walkthrough C.22–C.24 / F,G stages 14–17.
2. Time-lapse via Core time-skip; event cards in the OT; freezing (no action) → debrief item.
3. Event cards show 'ProcSense educational extension' visibly; each needs reviewer approval before it can appear.

**Verification**

T-EXT1-style tests per event; reviewer approves each event.

**Files changed**

`content/extension_events.json`, OT event-card UI, time-lapse controller, tests.

**Dependencies**

p1-theatre-extension-s01.

**Common issues**

Every event visibly labelled as an extension.


#### p1-theatre-extension-s03 — Recovery handover in the reused patient bay

- [ ] Self-verification and Sannan manual acceptance recorded.

Source: [Original step](../docs/plans/p1/p1-theatre-extension/steps/p1-theatre-extension-s03-recovery-handover.md). Initial status: Planned; no implementation acceptance claimed.

**What**

After Sign Out, the patient is handed over to the recovery nurse in the patient bay layout (approved P6): SBAR including procedure, findings, analgesia/antiemetic plan, antibiotic plan, drains, concerns; omissions scored.

**How to implement**

1. Bay manifest variant `recovery`; recovery nurse avatar (P1 role).
2. Required elements from content; handover subsystem reused.
3. Recovery nurse avatar (from the chosen human set) with role uniform.
4. Required handover elements from content; omissions scored like Step 12.

**Verification**

Recovery handover playable for S1 and S2; omission fixtures scored.

**Files changed**

`unity/Assets/_Project/Data/room_manifest_bay_recovery.json`, `content/handover_required.json` (recovery section), recovery nurse prefab, tests.

**Dependencies**

p1-theatre-extension-s01.

**Common issues**

Recovery handover reusing Step 12 logic must use recovery-specific required elements (analgesia, antiemetic, antibiotic plan, drains).


#### p1-theatre-extension-s04 — P1 equipment: specimen container, recovery setup

- [ ] Self-verification and Sannan manual acceptance recorded.

Source: [Original step](../docs/plans/p1/p1-theatre-extension/steps/p1-theatre-extension-s04-p1-equipment-scripts.md). Initial status: Planned; no implementation acceptance claimed.

**What**

Blender Python assets: specimen container with screw lid (`PIVOT_Lid`) and `LABEL_Specimen`; recovery bay set-up reusing monitor, oxygen and suction assets.

**How to implement**

1. Same pipeline, quality bar and validator as P0 assets; reference sheet first.
2. Specimen container: clear pot with lid thread, label area, biohazard bag variant.
3. Recovery setup: reuse monitor/oxygen/suction prefabs placed via the recovery manifest.

**Verification**

Validator + previews approved; specimen labelling interaction works in the headset.

**Files changed**

`tools/blender/assets/ot/specimen_container.py`, `docs/research/equipment/specimen_container.md`, recovery manifest placements.

**Dependencies**

p0-blender-equipment-s04, s05.

**Common issues**

Specimen label text must be live (patient name from Core) so the 'read aloud including the patient's name' check works.


### p1/p1-voice-input


#### p1-voice-input-s01 — Speech recognition service (EXP-VOX-3)

- [ ] Self-verification and Sannan manual acceptance recorded.

Source: [Original step](../docs/plans/p1/p1-voice-input/steps/p1-voice-input-s01-asr-service.md). Initial status: Planned; no implementation acceptance claimed.

**What**

faster-whisper (`large-v3-turbo`, int8) on the GTX 1660 Super desktop: `/asr` accepts push-to-talk clips ≤ 15 s (16 kHz mono), returns text + word timings + avg_logprob; medical vocabulary initial prompt built from content (tests, equipment, drug classes, roles; ≤ 224 tokens); VAD on, beam 3, temperature 0; low confidence → 'Say again'.

**How to implement**

1. `server/asr/service.py` (model loaded once); `tools/content/build_vocab.py`.
2. EXP-VOX-3: WER on PriMock57 sample + consented team recordings; order accuracy after read-back.
3. Reject clips > 15 s; convert to 16 kHz mono server-side.
4. Concurrency: queue requests (T-ASR6), never crash.

**Verification**

T-ASR1 5 s clip ≤ 0.7 s target; T-ASR2…T-ASR6 pass; thresholds documented.

**Files changed**

`server/asr/service.py`, `server/routers/asr.py`, `server/schemas/asr.py`, `tools/content/build_vocab.py`, `tools/ai_eval/asr_wer.py`, `thresholds.yaml` (asr.min_avg_logprob).

**Dependencies**

p1-backend-s06.

**Common issues**

CUDA not found → matching cuDNN/CTranslate2 build; slow on 1660 → beam 1 for short clips.


#### p1-voice-input-s02 — Quest voice client

- [ ] Self-verification and Sannan manual acceptance recorded.

Source: [Original step](../docs/plans/p1/p1-voice-input/steps/p1-voice-input-s02-voice-client.md). Initial status: Planned; no implementation acceptance claimed.

**What**

Push-to-talk (hold B), mic capture (RECORD_AUDIO permission, 16 kHz mono, ≤ 15 s), async upload, transcript bubble + 'Say again', playback on the speaker's avatar + lip sync, FallbackController (health every 30 s, 2.5 s timeout → offline banner + menu/pre-rendered), settings (server URL, voice on/off).

**How to implement**

1. `Scripts/Voice/*` (MicCapture, AIClient Http with cancellation, FallbackController).
2. Mute playback while recording (echo).
3. Interrupt policy: talking while a character speaks queues the clip (T-VC4).
4. Talk-while-speaking policy: queue the clip until the character finishes (T-VC4).

**Verification**

T-VC1–T-VC5 pass on the Quest.

**Files changed**

`unity/Assets/_Project/Scripts/Voice/{MicCapture,AIHttpClient,FallbackController,TranscriptBubble}.cs`, Android manifest (RECORD_AUDIO), settings panel.

**Dependencies**

p1-voice-input-s01.

**Common issues**

Microphone silent on Quest → RECORD_AUDIO in the manifest and runtime permission; echo → mute playback while recording.


#### p1-voice-input-s03 — Voice orders with read-back (EXP-VOX-4)

- [ ] Self-verification and Sannan manual acceptance recorded.

Source: [Original step](../docs/plans/p1/p1-voice-input/steps/p1-voice-input-s03-voice-orders.md). Initial status: Planned; no implementation acceptance claimed.

**What**

Parse transcripts into ORDER_TEST / REQUEST_MEDICATION / HOLD_MED / REQUEST_TRANSPORT / CALL_STAFF with slots via grammar + fuzzy match (threshold 85) against content, LLM fallback only if grammar fails; numbers/units stripped (no doses); always read back ('Confirm: hold metformin?') before any Core action.

**How to implement**

1. `server/orders/grammar.py` + synonym table; LLM fallback schema `order_parse.v1`.
2. Read-back via staff voice; confirm by voice or tablet.
3. Ambiguous drug → clarification question (T-VO2); no match → menu suggestion (T-VO4).
4. Read-back line spoken by the nurse/staff voice; confirmation accepted by voice ('yes', 'confirm') or tablet button; 'no' cancels and offers the menu.
5. Every parsed order and its confirmation logged (intent, slots, confirmed by, latency).

**Verification**

T-VO1–T-VO4 pass; no Core action without confirmation (log check).

**Files changed**

`server/orders/grammar.py`, `server/orders/synonyms.csv`, `server/schemas/order_parse.py`, `unity/Assets/_Project/Scripts/Voice/ReadBack.cs`, tests.

**Dependencies**

p1-voice-input-s02.

**Common issues**

Brand names → synonym table.


#### p1-voice-input-s04 — Live TTS for AI-generated lines

- [ ] Self-verification and Sannan manual acceptance recorded.

Source: [Original step](../docs/plans/p1/p1-voice-input/steps/p1-voice-input-s04-live-tts.md). Initial status: Planned; no implementation acceptance claimed.

**What**

Server-side TTS for lines the LLM generates at runtime: the same voice per character as the offline pipeline where possible, sentence-level streaming (first sentence sent as soon as it's ready), caching of repeated lines, and client playback with lip sync; when offline, subtitles only.

**How to implement**

1. `/tts` route; sentence split; caching of repeated lines.
2. Same voice per character as the offline pipeline where possible; cache repeated lines server-side.
3. Sentence splitter on the LLM output; first sentence synthesised immediately, the rest streamed.
4. Client falls back to subtitles if TTS times out (2.5 s).

**Verification**

First sentence plays within the latency budget; voices consistent with pre-rendered lines.

**Files changed**

`server/routers/tts.py`, `server/tts/engine.py`, voice mapping reused from `tools/voice/voices.yaml`, Unity `Scripts/Audio/StreamingVoice.cs`.

**Dependencies**

p1-ai-language-s04.

**Common issues**

Live TTS on the same GPU as the LLM can stall replies — place TTS on the desktop (B-R1 placement).


### p1/p1-scenario-composer-difficulty


#### p1-scenario-composer-difficulty-s01 — Building blocks and templates

- [ ] Self-verification and Sannan manual acceptance recorded.

Source: [Original step](../docs/plans/p1/p1-scenario-composer-difficulty/steps/p1-scenario-composer-difficulty-s01-blocks-templates.md). Initial status: Planned; no implementation acceptance claimed.

**What**

Block library (21 block types: DEM, PRES, CMB, MED, ALG, PSH, SOC, OBS, FND, RSK, LAB, IMG, CON, PER, PAIN, DIFF, EVT, ENV, STAFF, TIME…) derived from the walkthrough cases, with tags, requires/excludes, enabled rules/events, sources and status; templates per scenario with slot min/max and difficulty limits.

**How to implement**

1. `content/blocks/<type>/*.json`, `tags.yaml`, `content/templates/SCN-S1.json`, `SCN-S2.json`.
2. Composer uses only blocks ≥ APPROVED_FOR_TRAINING.
3. Tag vocabulary `tags.yaml` (e.g. tag:diabetes, tag:anticoagulant) used consistently by requires/excludes.

**Verification**

Validator checks every tag exists; every block lists the case(s) it was derived from.

**Files changed**

`content/blocks/<type>/*.json`, `content/blocks/tags.yaml`, `content/templates/SCN-S1.json`, `SCN-S2.json`, schema `content/schema/block.schema.json`.

**Dependencies**

p1-case-library-extended-s02.

**Common issues**

Blocks invented 'to fill gaps' break proof of truth — every block must cite the walkthrough case it came from.


#### p1-scenario-composer-difficulty-s02 — Constraint solver, case cards, CLI

- [ ] Self-verification and Sannan manual acceptance recorded.

Source: [Original step](../docs/plans/p1/p1-scenario-composer-difficulty/steps/p1-scenario-composer-difficulty-s02-solver-cards-cli.md). Initial status: Planned; no implementation acceptance claimed.

**What**

Deterministic solver: order slots by constraint tightness, sample compatible blocks (seeded), check requires/excludes incrementally, backtrack (≤ 10k steps, dead-end memo); output an authoritative case card + hidden facts; CLI `compose --scenario S1 --difficulty 2 --n 20 --seed 7`; walkthrough case values never contradicted.

**How to implement**

1. `tools/composer/solve.py` (pure Python).
2. Cards pass Stage B (p0-case-library-s04) before review.
3. `--targets 'handover,communication'` biases PER/EVT selection.
4. Property test over 1000 seeds for excluded combos (T-CO1).
5. Card output includes `created_by: composer`, seed and template so any card can be regenerated.

**Verification**

T-CO1–T-CO5 pass; ≥ 8 S1 / ≥ 6 S2 cards pass Stage B.

**Files changed**

`tools/composer/solve.py`, `tools/composer/cli.py`, tests (T-CO1–T-CO5), `Taskfile.yml` (`task compose`).

**Dependencies**

p1-scenario-composer-difficulty-s01.

**Common issues**

Solver slow on tight templates → order slots by tightness and memoise dead ends.


#### p1-scenario-composer-difficulty-s03 — AI Stage A proposals (EXP-COMP-1)

- [ ] Self-verification and Sannan manual acceptance recorded.

Source: [Original step](../docs/plans/p1/p1-scenario-composer-difficulty/steps/p1-scenario-composer-difficulty-s03-stage-a-proposals.md). Initial status: Planned; no implementation acceptance claimed.

**What**

LLM proposes cases using only approved block IDs (enum schema), targeting difficulty/competencies; Stage B defects fed back as 'avoid' hints; passing cases get a validated narrative; each proposal lists problems, types, ladder order, checks, end state, clock and handover items (14B.5 recipe); status stays IMPLEMENTED until clinician approval.

**How to implement**

1. `tools/composer/propose.py`; temperature 0.8; dedupe by block-set hash.
2. Catalogue prompt lists only approved block IDs with one-line descriptions; schema enums per slot type reject invented IDs (T-SA1/T-SA4).
3. Narratives for passing candidates via `/llm/generate` task=narrative, validated for entities; presentation only.
4. Dedupe proposals by block-set hash; temperature 0.8 for diversity.

**Verification**

T-SA1–T-SA4 pass; ≥ 12 distinct block sets in 20 proposals (T-SA3).

**Files changed**

`tools/composer/propose.py`, `prompts/composer/v1.txt`, `server/schemas/proposal.py` (enum schema), candidates folder (dev-only).

**Dependencies**

p1-scenario-composer-difficulty-s02; p1-ai-language-s02.

**Common issues**

Low diversity → raise temperature and penalise duplicate block sets; proposals ignoring difficulty → put limits explicitly in the prompt.


#### p1-scenario-composer-difficulty-s04 — Difficulty explanation

- [ ] Self-verification and Sannan manual acceptance recorded.

Source: [Original step](../docs/plans/p1/p1-scenario-composer-difficulty/steps/p1-scenario-composer-difficulty-s04-difficulty-explanation.md). Initial status: Planned; no implementation acceptance claimed.

**What**

When practice mode picks the next case, the trainee sees why: a template sentence built only from the selector's facts (chosen dimension, weakest competency, recent score), optionally reworded by the LLM through the validator.

**How to implement**

1. Template first; LLM variant behind the feature gate.
2. Template: 'Next: <case title>. Focus: <dimension>. Why: your recent <competency> score was <x>.' — facts from the selector only.
3. Optional LLM rewording via the validator (facts unchanged).

**Verification**

Explanation test: every number/dimension in the text matches the selector output.

**Files changed**

`src/ProcSense.Core/Difficulty/Explanation.cs`, `content/difficulty/explanation_templates.json`, Unity next-case card, tests.

**Dependencies**

p0-case-library-s06.

**Common issues**

An LLM rewording that changes a number or dimension is rejected by the validator — template shown instead.


#### p1-scenario-composer-difficulty-s05 — Composed cases to approval (EXP-COMP-2)

- [ ] Self-verification and Sannan manual acceptance recorded.

Source: [Original step](../docs/plans/p1/p1-scenario-composer-difficulty/steps/p1-scenario-composer-difficulty-s05-composed-cases-approval.md). Initial status: Planned; no implementation acceptance claimed.

**What**

Produce at least 8 S1 and 6 S2 composed cases that pass Stage B, send them to the medical professional in ≤ 45-minute review packs, and add approved ones to the practice pool (never assessment until separately approved).

**How to implement**

1. Batch compose; review pack; import decisions.
2. Compose candidates per scenario until ≥ 8 S1 / ≥ 6 S2 pass Stage B; prefer diversity (different problem sets).
3. Review pack per batch (≤ 45 min); approved cards get status APPROVED_FOR_TRAINING; rejected ones archived with reasons.
4. Track per candidate: Stage B result, reviewer decision, reason — in `docs/research/review-packs/<date>-composed.md`.

**Verification**

Counts met with approval records; composed cases playable via golden correct path.

**Files changed**

`content/cases/COMPOSED-*.json` (approved), `docs/research/review-packs/<date>-composed.md`, Stage B reports.

**Dependencies**

p1-scenario-composer-difficulty-s03.

**Common issues**

Composed cases that look alike → bias the solver toward rarely used blocks and different targets.


### p2/p2-ai-extensions


#### p2-ai-extensions-s01 — On-device ASR spike (EXP-P2AI-1)

- [ ] Self-verification and Sannan manual acceptance recorded.

Source: [Original step](../docs/plans/p2/p2-ai-extensions/steps/p2-ai-extensions-s01-on-device-asr.md). Initial status: Planned; no implementation acceptance claimed.

**What**

sherpa-onnx (or Unity Sentis) speech recognition running on the Quest; compare latency/WER with the server path.

**How to implement**

1. Spike plugin; measure.
2. Check sherpa-onnx's Android/Unity integration path and model sizes first (record version); alternative: Unity Sentis with a small Whisper/ONNX model.
3. Build a minimal Quest app: push-to-talk → on-device transcript → show text; same vocabulary hint list as the server ASR.
4. Measure on Quest 3 and Quest 2: latency for 5 s clips, WER on the same recorded test set used in EXP-VOX-3, CPU/thermal impact while the simulation runs.
5. Decision: adopt as offline fallback, replace server ASR, or drop — ADR.

**Verification**

EXP-P2AI-1 table (device, model, latency, WER, frame impact) + ADR in `docs/decisions/`.

**Files changed**

`unity/Assets/_Project/Plugins/ASR/*` (spike branch), `docs/decisions/ADR-xxxx-on-device-asr.md`.

**Dependencies**

p1-voice-input-s01.

**Common issues**

On-device models compete with rendering for CPU/thermal headroom — measure during actual play, not in an empty scene.


#### p2-ai-extensions-s02 — Retrieval / embeddings for grounding

- [ ] Self-verification and Sannan manual acceptance recorded.

Source: [Original step](../docs/plans/p2/p2-ai-extensions/steps/p2-ai-extensions-s02-retrieval.md). Initial status: Planned; no implementation acceptance claimed.

**What**

A small local embedding model and a flat index over approved content (case facts, explain texts, glossary), used to select context for dialogue and debrief instead of passing everything — measured against the grounding eval.

**How to implement**

1. Index build at content-package time; eval impact.
2. Embed approved content chunks (case facts, explain texts, glossary) with a small local embedding model on the desktop GPU; store a flat index (numpy) — no vector DB unless needed.
3. Use retrieval to select context for dialogue and debrief instead of passing everything.
4. Measure grounding failures and token count before/after on the eval suite.

**Verification**

Grounding eval no worse and prompt tokens reduced — or decision recorded not to ship.

**Files changed**

`server/retrieval/*`, index build script, eval report.

**Dependencies**

p1-ai-language-s04.

**Common issues**

Retrieval that misses a needed fact causes 'I don't know' answers — measure both leaks and unhelpful answers.


#### p2-ai-extensions-s03 — Distilled small classifier (EXP-P2AI-2)

- [ ] Self-verification and Sannan manual acceptance recorded.

Source: [Original step](../docs/plans/p2/p2-ai-extensions/steps/p2-ai-extensions-s03-small-classifier.md). Initial status: Planned; no implementation acceptance claimed.

**What**

Replace the LLM-based communication classifier with a small, fast model trained on the human-labelled set (plus flagged LLM-labelled extra data), keeping the frozen test split and the critical-act guard.

**How to implement**

1. Train; compare F1 on the frozen test split.
2. Training data: human-labelled split + LLM-labelled extra data (flagged), frozen test split unchanged.
3. Model: small text classifier (e.g. fine-tuned compact transformer or logistic regression on embeddings) — start with the simplest that meets F1.
4. Compare macro-F1 and latency vs the LLM classifier; keep the critical-act guard.

**Verification**

EXP-P2AI-2 table; ship only within the agreed F1 margin.

**Files changed**

`tools/classifier/train.py`, model artefact (dev-only), comparison report, ADR.

**Dependencies**

p1-ai-language-s07.

**Common issues**

Training on LLM labels can copy the LLM's mistakes — evaluate only on the human-labelled test split.


#### p2-ai-extensions-s04 — Urdu / Urdu-English speech

- [ ] Self-verification and Sannan manual acceptance recorded.

Source: [Original step](../docs/plans/p2/p2-ai-extensions/steps/p2-ai-extensions-s04-urdu-speech.md). Initial status: Planned; no implementation acceptance claimed.

**What**

ASR + TTS support for Urdu and code-switched speech via TTS augmentation + small consented recordings (no suitable free dataset known).

**How to implement**

1. Consented data collection; WER evaluation.
2. Scope: Urdu and Urdu-English code-switching for patient questions/answers (trainee speech first); no translation of clinical content without review.
3. Data: consented recordings from volunteers (consent form, speaker IDs, no identifiers), plus TTS-augmented Urdu prompts; dataset card.
4. Evaluate Whisper multilingual on the set (WER per language mix); vocabulary prompt with transliterated medical terms.
5. TTS: check available Urdu voices for the offline pipeline; record terms.

**Verification**

WER by category recorded; dataset card complete; ADR on whether Urdu input ships.

**Files changed**

`data/urdu/*` (dev-only, consented), `tools/voice/urdu_eval.py`, dataset card, `docs/decisions/ADR-xxxx-urdu.md`.

**Dependencies**

p1-voice-input-s01.

**Common issues**

Code-switched speech confuses language detection → force multilingual decoding and evaluate per mix category.


#### p2-ai-extensions-s05 — Medical LLM benchmark (EXP-P2AI-3)

- [ ] Self-verification and Sannan manual acceptance recorded.

Source: [Original step](../docs/plans/p2/p2-ai-extensions/steps/p2-ai-extensions-s05-medical-llm-benchmark.md). Initial status: Planned; no implementation acceptance claimed.

**What**

Benchmark a medical LLM (e.g. MedGemma — accept its terms first) against the chosen general model on the full AI eval suite, same prompts and schemas: grounding leaks, schema validity, latency p95 on the 6 GB GPU, and a blind reviewer rating of dialogue quality.

**How to implement**

1. Benchmark; ADR update.
2. Candidate: MedGemma (accept its terms) or another medical model that fits 6 GB quantised; record version and quantisation.
3. Run the full `tools/ai_eval/run.py --suite all` with the candidate as the LLM backend; same prompts/schemas.
4. Compare grounding leaks, schema validity, latency p95, dialogue quality (blind reviewer rating on 50 samples).

**Verification**

Comparison table + ADR update; switch only if grounding/quality improve without breaking latency.

**Files changed**

`docs/decisions/ADR-xxxx-llm-choice.md` (update), eval reports.

**Dependencies**

p1-ai-language-s09.

**Common issues**

Medical models may be more eager to give clinical advice — check the dose-policy and 'no advice' rejections specifically.


## 11. Original B-S acceptance and test matrix

These excerpts retain the older Implementation Guide test IDs for traceability. Interpret them using the current architecture and tiers; do not duplicate Core clinical rules in Unity. The original document supplies interfaces and troubleshooting details. Historical wording about cutting Step 15 at Gate 6 is superseded by the current full-scope/no-deadlines decision: Step 15 stays in P1. Any historical PR-based review wording is superseded by the current Git workflow plus Sannan's explicit acceptance/message gates.


### B-S6 — Unity project setup, XR rig & locomotion

**Tests**

| ID | Type | Scenario | Pass condition |
|---|---|---|---|
| T-U1 | normal | Build on clean machine | APK installs and runs |
| T-U2 | perf | Empty scene | 72 fps |
| T-U3 | failure | No headset | Device simulator works |
| T-U4 | normal | Teleport all anchors | Works |
| T-U5 | comfort | Snap turn/fade | No discomfort reports |

**Acceptance criteria (Definition of Done)**

- Clean-machine build proven


### B-S7 — SimulationHost bridge, ActionEmitters, StateViews & SceneDirector

**Tests**

| ID | Type | Scenario | Pass condition |
|---|---|---|---|
| T-B1 | normal | Attach sensor | Monitor shows value |
| T-B2 | failure | Core rejects action | Hint shown; no visual change |
| T-B3 | perf | Observable render | ≤1 ms |
| T-B4 | normal | Transport animation | Matches state |
| T-B5 | failure | Core exception | Paused + log, no crash |

**Acceptance criteria (Definition of Done)**

- No clinical logic in MonoBehaviours


### B-S8 — Environment import, optimisation & room anchors

**Tests**

| ID | Type | Scenario | Pass condition |
|---|---|---|---|
| T-EN1 | perf | Each room on Quest | ≥72 fps |
| T-EN2 | normal | Teleport all anchors | Correct placement |
| T-EN3 | perf | Worst view in OT | ≤150 batches |
| T-EN4 | memory | Scene load | ≤2.5 GB |

**Acceptance criteria (Definition of Done)**

- All used rooms within budget


### B-S9 — Interaction framework & equipment state machines

**Tests**

| ID | Type | Scenario | Pass condition |
|---|---|---|---|
| T-IN1 | normal | Each equipment path | States in order |
| T-IN2 | failure | Invalid transition | Core rejects, visual reverts |
| T-IN3 | perf | OR with all items | ≥72 fps |
| T-IN4 | edge | Grab two items same socket | Only first snaps |
| T-IN5 | fallback | Tablet path | Same action emitted |

**Acceptance criteria (Definition of Done)**

- Every equipment row playable or tablet fallback


### B-S10 — In-VR UI framework (tablet, monitor, results viewer, checklist boards)

**Tests**

| ID | Type | Scenario | Pass condition |
|---|---|---|---|
| T-UI1 | normal | Each panel | Renders state |
| T-UI2 | usability | Readability on Quest | Legible at 1 m |
| T-UI3 | perf | Monitor waveform | ≤0.5 ms |
| T-UI4 | normal | Results flags | Correct H/L |

**Acceptance criteria (Definition of Done)**

- All panels usable


### B-S11 — Quest builds, profiling & Unity CI

**Tests**

| ID | Type | Scenario | Pass condition |
|---|---|---|---|
| T-Q1 | perf | Demo path | Gate passes |
| T-Q2 | clean | Clean machine | Build ≤2 h |
| T-Q3 | normal | Each variant | Builds |
| T-Q4 | perf | Memory | ≤2.5 GB |

**Acceptance criteria (Definition of Done)**

- All gates have captures


### B-S1 — Blender custom equipment models

**Tests**

| ID | Type | Scenario | Pass condition |
|---|---|---|---|
| T-3D1 | perf | Tri/draw audit | Within budget |
| T-3D2 | normal | Scale check in VR | Real size |
| T-3D3 | normal | Each prefab grab | Natural grip |
| T-3D4 | licence | Register | Every asset has entry |

**Acceptance criteria (Definition of Done)**

- All required equipment exists with licences


### B-S2 — Patient blend shapes, shaders & behaviour animation mapping

**Tests**

| ID | Type | Scenario | Pass condition |
|---|---|---|---|
| T-AV1 | normal | Each mode | Distinct expression |
| T-AV2 | perf | Skinned mesh cost | Within budget |
| T-AV3 | normal | Event cues | Pallor/rash visible |
| T-AV4 | edge | Rapid mode changes | Smooth blends |

**Acceptance criteria (Definition of Done)**

- Every mode/cue visible


### B-S3 — Staff avatar rigs

**Tests**

| ID | Type | Scenario | Pass condition |
|---|---|---|---|
| T-ST-A1 | normal | Each clip | Plays |
| T-ST-A2 | perf | 5 NPCs in OR | Budget |
| T-ST-A3 | normal | Porter push trolley | Hands on handle |
| T-ST-A4 | edge | Two NPCs same path | No overlap |

**Acceptance criteria (Definition of Done)**

- All roles usable


### B-S4 — Voice client (push-to-talk, fallback, subtitles, lip sync)

**Tests**

| ID | Type | Scenario | Pass condition |
|---|---|---|---|
| T-VC1 | offline | Kill server | Session continues |
| T-VC2 | perf | Voice round trip | No frame drops |
| T-VC3 | normal | Subtitles | Always shown |
| T-VC4 | edge | Talk while patient speaking | Queue / interrupt policy |
| T-VC5 | failure | Bad URL | Offline mode |

**Acceptance criteria (Definition of Done)**

- Server kill test passes


### B-S5 — Debrief & difficulty-explanation UI

**Tests**

| ID | Type | Scenario | Pass condition |
|---|---|---|---|
| T-DU1 | normal | Every item has evidence link | Yes |
| T-DU2 | offline | No AI | Template only |
| T-DU3 | normal | Critical items first | Yes |
| T-DU4 | usability | Read time | ≤2 min |

**Acceptance criteria (Definition of Done)**

- Evidence-linked debrief


### B-S12 — Theatre extension scenes: intra-op events, Sign Out, recovery (P1)

**Tests**

| ID | Type | Scenario | Pass condition |
|---|---|---|---|
| T-EXT1 | normal | Sign Out | Items confirmed |

**Acceptance criteria (Definition of Done)**

- Step 15 playable or formally cut at Gate 6


Appendix inventory: 96 original phase steps and 12 B-S work packages. This inventory is a planning snapshot, not a completion count.


## 12. SAN-LAYOUT — familiar Unity editing layout (10 October 2026)

**Authorization:** Sannan requested “please make it more familiar, organize how it should be”. Implementation remains uncommitted and awaits his manual acceptance; no commit message has been approved.

### Delivered changes

- Moved the six `_Project` folders through Unity's AssetDatabase into `Assets/Scenes`, `Scripts`, `Editor`, `Settings`, `Tests` and `Data`, preserving all 64 original asset/folder GUIDs. `_Project` was removed only after confirming it was empty.
- Moved greybox materials out of configuration data into `Assets/Materials/Prototype`.
- Renamed the room scene `Skeleton1_PatientBay` to `PatientBay`; updated build-scene paths, BootLoader's default and serialized scene list, and generator/setup paths.
- Added `ProcSense -> Open Patient Bay`, which opens Sys_Core, PatientBay and DevOnly with the bay active, and uses normal Unity Play behavior. The workspace menu focuses the Scene tab with its camera inside the room.
- The regeneration menu is now `ProcSense -> Tools -> Rebuild Prototype Scenes`, with a confirmation explaining that it replaces manual scene edits. Regeneration was not run during this change.
- Restored the missing `UniversalController.fbx` plus its original metadata from the installed XRI 3.6.1 sample. Scene inspection found 18 unresolved controller mesh references before restoration and none afterward.
- Created `Assets/Prefabs/XR/PlayerRig.prefab` as a variant of the toolkit rig and connected the bay instance to it. Four missing material-feedback renderer targets were wired to the relevant controller-body renderer; the scene generator now uses this application prefab.
- Added a Unity-local `.gitignore` exception for actual Unity model assets: the repository-wide `models/` rule was hiding the sample's Models directory. The downloaded AI-model ignore rule outside Unity is untouched.
- Updated Unity README with the folder map, editing/run workflow and regeneration caveat.

### Verification and current limits

- Original 64 GUIDs checked after moving through Unity and again against their metadata on disk: preserved, zero mismatches.
- EditMode: 3/3 passed (all golden runs, Unity content loading and pinned skeleton replay hash).
- Initial PlayMode: 9/12 passed; the three patient-bay tests failed on existing missing renderer/affordance references. The source mesh and four renderer targets were subsequently repaired. Final PlayMode rerun: **12/12 passed**, zero failures, duration 3.93 seconds.
- Scene inspection after restoring the mesh: zero missing scripts and zero unresolved serialized references across the three editing scenes. The controller-feedback repair additionally fixed null renderer configuration that wasn't a broken serialized asset reference.
- A custom Play-start override was tried, found to interfere with test startup, and removed. Normal Play runs the open scene set; Boot remains the application build entry. Tests must not inherit an overriding Play scene.
- Visual inspection passed: early capture showed the wall/background because the Scene camera was outside the enclosed room. Corrected the workspace framing to keep the camera inside. Inspected `artifacts/unity-layout/scene-view.png` (room/bed visible in Edit mode) and `artifacts/unity/bay_overview.png` (room, tablet, identity and monitor visible during Play). These are still greybox assets; this task did not add final hospital art or author the runtime-generated UI as edit-time Canvases.
- Editor temporarily stopped answering main-thread commands during a rerun because it was waiting for Save modified scenes. Sannan confirmed he clicked Save. The Editor subsequently reported ready, the test run completed successfully, and final scene inspection passed with zero missing scripts or broken serialized references. This save confirmation is not implementation or commit-message approval.
- `git diff --check` passed; all 109 work-plan Markdown links resolve; no stale `_Project` paths in executable application scripts.
- The repository .NET checks were initially blocked because SDK 9, Task and uv were unavailable. Sannan installed them; verified SDK 9.0.318, uv 0.13.0 and Task 3.54.0. Full `task ci` subsequently passed **27/27 checks**, including build, unit tests, golden runs, replay, formatting, dependency audit, medical content, asset data, plan and repository checks. The SDK pin was unchanged. Quest APK/device verification remains pending.
- Existing URP global settings, Pipeline package manifest/lock changes and shared Core `.meta` files predated this reorganization; preserve and review separately. No Core C# or medical content files were modified.

### Sannan manual review after automated verification is finished

1. In Project, inspect Assets: Scenes, Scripts, Editor, Data, Settings, Materials/Prototype, Prefabs/XR and Tests. `_Project` should no longer be present.
2. Choose `ProcSense -> Open Patient Bay`. The Scene tab should show the room, with PatientBay active alongside Sys_Core and DevOnly in Hierarchy.
3. Select PatientBay or individual room objects and inspect/edit them normally. Save intended scene changes; use the room manifest for changes that must survive regeneration.
4. Press Play. Confirm one camera, working tablet/controller input and no missing-renderer exceptions. Stop Play; the editing scene set should remain available.
5. Complete introduction, ask identity, read wristband, open notes and confirm identity on the tablet; attach SpO2 and confirm the monitor updates.
6. Double-click Boot and run it separately; it must still load Sys_Core, PatientBay and the Editor simulator.
7. Open the rebuild menu only to inspect its warning, then Cancel. Do not regenerate just to inspect the room.
8. Report any visual/layout issue or unexpected Console error. Only after fixes and verification explicitly accept the implementation. Commit-message approval is a separate later step.

**Commit identity:** the commit containing this log on `sannan47/unity-layout`, subject `refactor(unity): organize assets and improve editor workspace` (no body). Resolve its SHA using `git log -1 --format=%H -- unity/sannan-execution-plan.md`; the SHA is intentionally stored in Git history rather than recursively embedding a commit's own hash in its content. **Author:** Sannan, using the commit email supplied directly in this chat. **Manual implementation acceptance:** Sannan accepted proceeding to commit on 10 October 2026 ("ok now i think we should commit"), and reiterated "go ahead" after installing the tools. **Exact commit-message approval:** the one-line subject above. **Next action:** no push without authorization; proceed to SAN-BASE only when implementation is requested. Pre-existing Pipeline/settings/Core-metadata changes remain outside this commit's scope.

## 13. SAN-FOUNDATION — collision-safe desktop walking and session ownership

Started/updated: 10 October 2026, 23:12 Asia/Karachi. Original scope: Stage 1 first slice, p0-vr-platform-s04 ownership prerequisite. Authorization: Sannan reported walking through walls/falling out of the bay, stated quality requirements and requested proceeding with the next step. Status: self-verified functional checks; awaiting Sannan manual review. Full Stage 1 remains in progress.

### Changes and inspection

- Connected to the existing Unity 6000.5.10f1 Editor at this checkout's absolute `unity` path. The sandbox initially hid the running Pipeline instance; authorized escalated inspection found it ready. No second Editor, package upgrade or scene regeneration.
- Live inspection confirmed one solid floor and four solid wall colliders were already enabled. The application rig already had a CharacterController and XRI constrained body transformer. The simulator's default FPS implementation changed tracked-device positions directly and bypassed these colliders.
- Added `Scripts/XR/SimulatorLocomotion.cs` to the saved DevOnly simulator instance via the Editor. It suppresses the simulator's direct FPS body translation and routes planar walking through `XRBodyTransformer` at 1.5 m/s with downward grounding. Controller manipulation remains available; individual-device pose controls are still developer tools and do not prove collision-safe walking. The adapter disables itself outside the Editor.
- Tuned only the application `PlayerRig.prefab` collision capsule through PrefabUtility: radius 0.25 m, skin 0.025 m, step offset 0.2 m, minimum move distance zero. Third-party sample prefabs unchanged. SceneBuilder includes the adapter in future explicit rebuilds; no rebuild was run.
- SimulationHost now disables/removes a duplicate host component before it can replace the active Session. It does not introduce DontDestroyOnLoad at this stage; Sys_Core remains loaded during additive room operations. Moving rig/services out of PatientBay and handling actual room replacement belong to the next slice.
- Added six meaningful PlayMode checks: duplicate host, repeated additive room creation/unload, walls/floor/corner, clear movement, bed collision and end-to-end simulator input. Added required XRI/CoreUtils/InputSystem test assembly references. README documents desktop controls and limitations.

### Verification and evidence

- Live Pipeline commands always included `--caller plugin --skill unity-cli --project-path E:/Study/University/Semester_7/FYP/Procsense/unity`.
- `run_tests --mode playmode --async_tests true`, collected with `test_status`: final **18/18 passed**, zero failed/skipped, 11.46 s. Evidence: `Logs/foundation-playmode.json` (ignored).
- `run_tests --mode editor --async_tests true`: **3/3 passed**, zero failed/skipped, 0.48 s. Evidence: `Logs/foundation-editmode.json` (ignored). Golden/replay and content-loading checks retained.
- First collision test spawned inside the bed; corrected its setup into the clear aisle and added a dedicated bed-blocking test. First end-to-end input fixture used an unowned InputActionReference; fixed it to use a temporary InputActionAsset. Final rerun passed all coverage. Compilation/reload interrupted an early test dispatch; only completed final results above are acceptance evidence.
- End-to-end input test drove an isolated virtual gamepad axis through the simulator's translation action: rig moved and stopped at the wall, head position relative to the rig stayed unchanged, floor height remained valid. No headset pass is claimed.
- Final live inspection: Sys_Core, PatientBay and DevOnly open, all clean; zero missing scripts; saved capsule radius 0.25 m; adapter present. PatientBay/Sys_Core layouts unchanged.
- Inspected `artifacts/unity/bay_overview.png`: room, controllers, tablet and monitor present; still greybox assets and prototype UI, not final visual quality acceptance.
- `git diff --check` for scripts, Editor code, tests, README and this plan passed. Full diff check reports five trailing spaces emitted by Unity serialization in PlayerRig/DevOnly empty YAML values; cosmetic normalization remains before any eventual commit, without changing asset identities or scene wiring.
- Full `task ci` not rerun for this uncommitted Unity slice; mandatory before any later commit. Quest APK, device replay, comfort, sustained frame time and memory remain unavailable until supervisor supplies Quest 3. Editor test timing is not a Quest performance measurement.

### Sannan's short manual review

1. Choose `ProcSense -> Open Patient Bay`, press Play and focus Game. Use default FPS mode (no individual simulator device selected).
2. Use WASD toward all walls, the bed and a corner. Expected: walking stops/slides at solid boundaries; player stays on the floor. Looking upward/downward must not turn walking into flying.
3. Stop and start Play, then repeat briefly. Expected: consistent walking and no new Console errors.
4. Use tablet introduction/identity/wristband/notes/confirmation and attach SpO2. Expected: normal interaction responses and monitor update.
5. Report problems or explicitly accept this foundation slice. Technical service counts and test verification are handled by the assistant; no technical audit is assigned to Sannan.

Implementation acceptance: Sannan explicitly accepted on 10 October 2026, 23:13 Asia/Karachi: "yea that feels great. now to commit". Exact commit-message approval: Sannan approved `fix(unity): constrain simulator walking` with no body on 10 October 2026, 23:15 Asia/Karachi ("that'll do"). Branch remains `sannan47/unity-layout`; no push authorized. Commit identity: the commit containing this entry, with the approved subject and author Sannan <sannanshakeel04@gmail.com>; resolve its SHA with `git log -1 --format=%H -- unity/sannan-execution-plan.md`.

Changed areas: Unity simulator adapter/DevOnly, application rig capsule, SimulationHost, generator compatibility, PlayMode tests, README and this plan. Preserve pre-existing modified `docs/plans/milestones.md`, URP global settings, package manifest/lock and five untracked shared Core `.meta` files; none included in this slice. Preserve all generated metadata.

Next action after acceptance: complete persistent rig/service ownership and additive room loading with validated arrival anchors and fades, then corridor/OT greyboxes and blocked diagnostic doors. Final UI/assets/animations retain separate quality review gates.

Pre-commit preparation (10 October 2026, 23:13 Asia/Karachi): reviewed exact Unity code/test/asset/documentation scope. Normalized only five Unity-generated trailing spaces through the Editor and reimported the two assets; no object IDs, GUIDs or semantic serialized values changed. Asset whitespace check now passes. Required `task ci` completed: **27/27 passed**, exit 0. Staged exactly the 11 reviewed Unity files; staged whitespace check passed and CI left the unrelated change set intact. Reimport inspection confirmed capsule values and adapter GUID unchanged. Approved subject: `fix(unity): constrain simulator walking` (no body). Author for eventual commit: Sannan <sannanshakeel04@gmail.com>. Both implementation and exact message approvals are recorded above; no push authorized.

## 14. SAN-ROOM-LOADING — persistent rig and additive trainee navigation

Started: 10 October 2026, 23:21 Asia/Karachi. Updated: 23:31 Asia/Karachi. Authorized by Sannan: "alright go ahead" for persistent services, additive room transitions, validated arrival anchors, fades, corridor/OT greyboxes and blocked diagnostic doors. Scope is the next Stage 1 slice / p0-vr-platform-s04; this does not authorize final clinical transfer/checklist integration or final asset production. Status: self-verified Editor functionality; awaiting Sannan manual review. Updated 23:36 Asia/Karachi.

Scope: Unity runtime RoomLoader/RoomAnchor/RoomDoor; incremental Editor NavigationSetup; existing bay/core scenes; new Corridor/OT scenes and metadata; generator compatibility, simulator suspension, build scene list, tests, README and this log. Branch: sannan47/unity-layout. Pre-existing milestone, URP global settings, Pipeline package manifest/lock and five Core metadata changes remain excluded.

Implementation: moved the existing PlayerRig instance from PatientBay to Sys_Core through the live Editor. Sys_Core owns session, rig, EventSystem and RoomLoader and stays loaded; DevOnly stays loaded in the Editor. Each allowed route fades out/in over 0.3 s each, loads the destination additively, hides source colliders for arrival validation, checks ground/capsule clearance, aligns the tracked user's ground position/yaw to the room anchor, and unloads the source room. Duplicate requests and invalid routes are rejected. Unsafe arrivals roll back, restore source objects/movement and show panel feedback. Disabling the loader during a load cancels the transition and unloads its destination after the operation finishes. Fade time uses unscaled time. Clinical simulation is neither paused nor mutated by navigation; normal Core time still advances.

Scene changes were incremental; no saved bay layout was regenerated. Existing bay room/equipment GUIDs preserved. New rooms have primitive solid wall/floor colliders and signs, with a single OT table placeholder. Labeled world-space Enter controls use the existing UiKit/XR UI pointer plumbing. CT/MRI and ultrasound signs have no Enter control and solid walls behind them. These are trainee-only navigation; no patient transfer action, clinical state change or readiness/checklist behavior is implemented. RoomAnchor's LOC-BAY/LOC-CORRIDOR/LOC-OT identifiers are presentation anchors, not a substitute for Core transport contracts. Greybox room geometry is not final procedural equipment art.

Verification: first completed PlayMode suite passed 22/22 (23.88 s), including two repeated full round trips with session/observable/action-log identity unchanged when automatic Core ticking is disabled; one rig/camera/host/EventSystem; exact source room unload; persistent sensor display after returning; invalid/restricted routes; blocked arrival rollback/recovery; disabling loader during load and retry. Final full PlayMode suite: **23/23 passed**, 25.17 s, zero failures/skips; EditMode **3/3 passed**, 0.36 s. After strengthening the preview test to invoke an enabled Enter button, its targeted rerun passed **1/1**, 4.92 s; no runtime changes followed the full-suite pass. Evidence: ignored Logs/navigation-playmode.json, navigation-editmode.json and navigation-button-check.json. Test runner stays in the connected pinned Editor, never a second batch Editor. Source/docs whitespace check passed. No package/SDK versions or clinical files changed.

Manual review after final verification: Open Patient Bay, Play and focus Game. Confirm identity and attach SpO2. On the bay right wall use CORRIDOR / Enter. In the corridor inspect the locked CT/MRI and ultrasound panels, then use OPERATING THEATRE / Enter at the far-right end. In OT use CORRIDOR / Enter on the front wall; return through PATIENT BAY at the corridor's left arrival end. Expected: smooth fade, valid floor placement, normal walking/collisions, one camera, same confirmed identity and SpO2 on return. Repeat once and report unexpected Console errors or uncomfortable panel placement. Technical ownership audit is handled by the assistant.

Known limits: Quest 3 unavailable; no headset comfort, APK/device replay, actual 50 ms hitch budget or sustained Quest CPU/GPU/memory acceptance claimed. Final UI/room art and animations remain separate quality gates. Full task ci required before any eventual commit. No implementation acceptance, commit-message approval, staging, commit or push for this slice.

Final inspection: zero missing scripts; persistent rig in Sys_Core; Sys_Core/PatientBay/DevOnly editing scenes clean. Reviewed four route previews in Logs/navigation: corridor signs, OT placeholder, bay Enter panel and OT return panel. Enabled buttons show blue after their state animation settles. Normalized only generated trailing serialization whitespace in changed scenes through the Editor; no object/GUID data changed. Full diff whitespace check recorded at handoff. No final visual-art quality or Quest performance pass inferred.

### SAN-ROOM-LOADING direct-Play regression repair

10 October 2026 (Asia/Karachi): Sannan reported that double-clicking PatientBay and pressing Play produced "Display 1 No cameras rendering". Confirmed in the live Editor: only PatientBay was loaded and Play was active. Cause: persistent rig had moved to Sys_Core without automatic support loading for direct room Play. This is changes-requested feedback, not acceptance of the navigation slice.

Added RoomBootstrap to the existing PatientBay/Corridor/OT roots through the Editor and to future procedural setup. Direct room Play loads missing Sys_Core and DevOnly, initializes the current room/arrival pose, and restores the room as active. Boot and normal transitions retain existing services and skip bootstrap loading. Added a regression test loading each room alone, checking one camera/rig/EventSystem/session, and navigating from it. Updated README so direct Play is supported, rather than requiring the workspace menu as a workaround. Verification running; manual re-review and exact commit-message approval remain pending. No commit/push.

Direct-Play repair verification: full live PlayMode suite **24/24 passed**, 28.99 s, zero failures/skips; evidence Logs/navigation-direct-play.json. Then opened PatientBay alone and entered normal Editor Play to reproduce Sannan's exact path; live service/camera counts recorded at handoff. Source/docs whitespace checks passed. Previous EditMode 3/3 evidence remains applicable to unchanged Core/golden loading. Awaiting Sannan's re-review; all navigation/startup work remains uncommitted.

Pre-commit acceptance (10 October 2026, 23:42 Asia/Karachi): Sannan requested "alright time to commit" after the direct-Play repair. This explicitly accepts the reviewed navigation/startup slice. Branch: sannan47/unity-layout. Required task ci completed: **27/27 passed**, exit 0. Reviewed/staged exactly 24 Unity files; serialized whitespace normalized, GUIDs preserved, staged diff check passed. Exact commit-message approval pending. Proposed subject: feat(unity): add persistent rig and room transitions (no body). Intended author: Sannan <sannanshakeel04@gmail.com>. Only reviewed Unity files will be staged; unrelated milestone, package/URP and shared Core metadata changes remain excluded. No push authorized.

Commit approval (10 October 2026, 23:49 Asia/Karachi): Sannan explicitly approved committing the proposed one-line subject, feat(unity): add persistent rig and room transitions, with no body ("commit it"), on sannan47/unity-layout. Implementation acceptance and 27/27 pre-commit CI are recorded above. Commit author: Sannan <sannanshakeel04@gmail.com>. Commit identity is the commit containing this entry; resolve its SHA using git log -1 --format=%H -- unity/sannan-execution-plan.md. Exactly the reviewed 24 Unity files are included; pre-existing milestone, URP/package and Core metadata changes are excluded. No push or merge/main update authorized. This feature branch is for layout/navigation foundation, not the whole roadmap; later features should use focused sannan47 branches after this foundation is reviewed and integrated, or explicitly depend on it while integration is pending.

## 15. SAN-COMFORT — safe teleport, snap-turn and seated foundations

Started 10 October 2026; updated 11 October 2026, 00:02 Asia/Karachi. Authorized by Sannan: "go ahead, soldier" for safe teleport targets, snap-turn and seated mode. Branch: sannan47/unity-layout. Scope stays under unity; no push/staging/commit authorized. Pre-existing milestone, URP/package and Core metadata changes remain separate.

Implementation: application rig variant uses SafeTeleportProvider, shared LocomotionFade and ComfortControls. Blue primitive development markers identify bay tablet/bedside/exit and OT station/exit anchors; guarded corridor floor area permits free destinations. Each request checks current-room ground support, finite coordinates and CharacterController-sized clearance, blocks overlap with room transitions, fades to black (0.15 s), rechecks dynamic obstruction before XRI relocation, then fades back (0.15 s) and restores input. Destination validation is presentation-only and issues no clinical actions. Shared fade replaces the earlier separate room-loader fade. Teleport interactables use the Starter Assets teleport ray interaction layer, with blocked selection rejected.

45° snap-turn remains default, 30° is selectable, 180° turn-around disabled. Application-specific controller manager flags disable smooth movement/turn actions; desktop WASD collision path remains. Seated calibration adjusts the rig's existing camera/controller offset to 1.6 m virtual eye height and restores its previous standing offset on exit; persistent rig retains setting across rooms. In-VR MOVEMENT panels provide seated calibration and turn-angle controls. Editor review shortcuts: F7 cycles pads; brackets turn; F6 toggles seated. Incremental ComfortSetup preserves room layouts and uses the application prefab rather than modifying imported samples; explicitly requested future prototype rebuild includes the comfort setup.

Verification in progress: initial full suites passed 28/28 and then 29/29 PlayMode checks, including existing clinical/scene regressions, actual SnapTurnProvider manual input, panel button actions, dynamic blocked destination, invalid floor/wall/bed/NaN requests, fade/input lock, seated persistence and unchanged Core observable/action-log hashes. Controller layer audit caught initial default interaction layers on targets; aligned them with both Starter Assets teleport rays and added a selection compatibility test. Final evidence is recorded below; initial compatibility-test compilation was repaired before accepting a completed run.

Manual review after final verification: open PatientBay directly, Play, focus Game. F7 should visit the blue pads with fades; brackets should turn in 45° increments. Use MOVEMENT panel to select 30° and verify brackets; toggle seated and return to standing. Navigate corridor/OT/bay, repeat pad teleport and check persistent mode plus identity/SpO2. In simulator controller mode test teleport rays on valid pads/floor and invalid wall/bed positions. Report clipping, input errors or uncomfortable placement. Request acceptance only after self-verification finishes.

No headset comfort pass claimed: Quest 3 unavailable; EXP-VR-3 (at least three testers, 10-minute nausea/disorientation/eye-strain questionnaire), physical seated reach, real tracking behavior and Quest performance remain pending. Greybox markers/panels are not final equipment/UI art acceptance. Full task ci remains required before any eventual commit.

Comfort verification repair notes (11 October 2026, Asia/Karachi): corrected the deprecated XRBaseInteractor test overload with IXRSelectInteractor; aligned ray and target interaction layers, and enabled teleport-ray detection of trigger pad colliders. Pads now use trigger colliders so walking does not treat visual markers as steps. Direct-room selection lazily binds to the provider created by RoomBootstrap. Room Enter controls are held during teleport as well as room transition.

An existing desktop FPS regression then exposed the persistent rig falling before its room floor was loaded (diagnostic position below floor, solid saved wall/floor colliders). Added a RoomLoader startup barrier: hold locomotion while waiting for the current room, place the rig at the supported startup anchor, then release input. Simulator walking also honors RoomLoader's busy state. Focused walking rerun passed 1/1 (6.63 s). Added controlled Sys_Core-only 1.2-second delayed-floor test to prove no fall while waiting. Final full suite running; no handoff/acceptance inferred from intermediate passes. No clinical logic or shared package changes made.

Final SAN-COMFORT evidence (11 October 2026, 00:18 Asia/Karachi): **32/32 PlayMode passed**, zero failures/skips, 45.44 s (Logs/comfort-playmode.json); **3/3 EditMode passed**, 0.37 s (Logs/comfort-editmode.json). Final tests include a controlled 1.2-second Sys_Core-only delay with rig held on startup, original desktop collision walking, actual snap-provider input, panel actions, ray interaction layers/trigger selection, late-binding direct room Play, invalid/dynamic blocked teleport and room/teleport exclusion. Capture reviewed: Logs/comfort-panel.png (readable panel and blue controls) and artifacts/unity/bay_overview.png (pads/clinical UI visible). Live direct-Play inspection: one rendering camera, rig at (0,0,-1.4), zero missing scripts, room initialized, provider idle, three bay anchors with teleport interaction layer and nonblocking trigger colliders. Normalized only serialization trailing whitespace in application assets; object/asset identities preserved. Status: self-verified Editor functionality, awaiting Sannan manual review. Quest and tester experiment gates remain pending; full task ci required before eventual commit. No staging, commit or push.

Changed areas: application rig, three room scenes, safe teleport/comfort/panel/fade runtime scripts, RoomLoader/RoomDoor startup and transition interlocks, desktop movement startup hold, incremental comfort setup/generator hook, meaningful comfort and existing startup-walking tests, marker material, README and this log. Preserve pre-existing milestone, URP global settings, package manifest/lock and five shared Core metadata files. Unity additionally reserialized imported Samples/Starter Assets/Prefabs/Interactors/Teleport Interactor.prefab and Samples/Starter Assets/Prefabs/XR Origin (XR Rig).prefab (new serialization/default fields and renderer-reference cleanup); keep those generated source-sample changes separate from the application commit. A temporary Test Runner InitTestScene and metadata from the interrupted compile/test dispatch remain separate and must not be staged as application content.

SAN-COMFORT approval (11 October 2026, 00:23 Asia/Karachi): Sannan accepted the implementation and approved the exact subject feat(unity): add safe teleport and comfort controls, with no body ("yea thats fine, commit and then push as well"). Commit branch and explicitly authorized push destination: sannan47/unity-layout -> origin/sannan47/unity-layout (https://github.com/ruhank-dev/procsense-fyp.git). Author: Sannan <sannanshakeel04@gmail.com>. Required task ci completed **27/27 passed**, exit 0. Reviewed/staged exactly 29 application files and staged whitespace check passed. Commit scope is the reviewed application locomotion/rig/scenes/material/setup/tests/docs only; imported sample reserialization, temporary Test Runner artifacts and pre-existing milestone/URP/package/Core metadata remain excluded. Commit identity: the approved-subject commit containing this entry; resolve SHA with git log -1 --format=%H -- unity/sannan-execution-plan.md. Push authorization is recorded here; actual push result will be verified against the remote ref and reported in the handoff.

## 16. SAN-SETUP-SNAPSHOT — share the remaining local project setup

11 October 2026, 00:40 Asia/Karachi. Sannan initially agreed to a clean-checkout reproduction check, then explicitly redirected: "just push the files mate". Honor the narrower immediate instruction: commit and push the remaining project/setup files using the previously proposed subject chore(unity): complete reproducible project setup, no body. Destination: origin/sannan47/unity-layout in https://github.com/ruhank-dev/procsense-fyp.git. Scope explicitly includes the five already-generated Core metadata files outside unity, the two imported sample prefabs, URP global settings and Pipeline manifest/lock. No Core C# or clinical-content changes. Temporary Test Runner scene/metadata are excluded; milestones has no substantive text diff. Preserve files rather than reset/delete.

An isolated git archive of pushed commit 1a27759 was started under ignored unity/Logs/reproduction-1a27759; no reproduction Editor was launched and no clean-checkout test result is claimed. The reproduction procedure was stopped at Sannan's request. Existing 32/32 PlayMode and 3/3 EditMode results describe the local configuration being shared, not a separately verified fresh machine. Required task ci completed **27/27 passed**, exit 0; reviewed/staged exactly 12 setup/documentation files and staged diff whitespace check passed. README records package/tooling/GUID setup and this verification limit.

Commit author: Sannan <sannanshakeel04@gmail.com>. The commit containing this entry has the approved setup subject; resolve its SHA by subject in Git history. Exact staged scope and CI result will be checked before commit, then the authorized remote ref will be verified after push. Prior source-sample serialization/renderer-reference changes are now included as the user requested; no additional sample regeneration or scene rebuilding. No headset performance or comfort acceptance inferred.

Explicit setup approval (11 October 2026, 00:43 Asia/Karachi): after automatic review required exact-payload approval, Sannan replied "yea i approve" to the disclosed 12-file scope, subject chore(unity): complete reproducible project setup with no body, and destination origin/sannan47/unity-layout in ruhank-dev/procsense-fyp. Required CI 27/27 and staged whitespace checks passed. Temporary Test Runner scene/metadata excluded. Commit author Sannan <sannanshakeel04@gmail.com>; commit SHA available in the approved-subject Git history. Actual push result is recorded in ignored Logs/setup-push-status.json and handoff after remote verification.

## 17. SAN-INPUT-AUDIT — session-safe emitter debounce

11 October 2026, 13:54 Asia/Karachi. Authorization: Sannan requested the next step with economical token use. Branch: sannan47/unity-layout; baseline HEAD 54d152a. Scope: Stage 1 step 4, first small reviewable slice of p0-vr-platform-s03 / B-S7. Status: committed as 99b822a after Sannan's explicit commit request below; not pushed in this workflow.

Changed ActionEmitter.cs and BridgeTests.cs only, plus this log. An input before a host/session exists no longer consumes the cooldown; replacement sessions start with a fresh cooldown. Invalid NaN/infinite durations use the default 0.35 seconds; negative durations clamp to zero. Existing cooldown survives component toggling. Core rejection and authoritative state remain Core-owned. No assets, scene layouts, package versions or shared interfaces changed.

Verification: live Unity 6000.5.10f1 through labeled Unity CLI commands targeting this checkout's absolute unity path. recompile_status completed with no errors. run_tests mode playmode, filter ProcSense.Tests.PlayMode: **40/40 passed**, zero failures/skips, 42.64 seconds (unity/Logs/input-audit-playmode.json). run_tests mode editor, filter ProcSense.Tests.EditMode: **3/3 passed**, zero failures/skips (unity/Logs/input-audit-editmode.json). Eight new cases exercise early input, session replacement, real UI/XR event callbacks, unsubscribe/re-enable cycles with debounce disabled to expose duplicate listeners, rejection during paused Unity time, and three invalid duration inputs. Existing golden tablet/Core log and observable hash equivalence passed (VAR-S1-A, seed 42, Practice; current unchanged content). Existing room round trips, lifecycle failures, comfort, and collision tests passed. Reviewed refreshed artifacts/unity/bay_overview.png: expected greybox tablet, wristband, monitor and controllers present. git diff --check passed. Full task ci remains required before eventual commit; no device acceptance claimed.

Manual review:
1. Open ProcSense -> Open Patient Bay, then Play. Quickly double-click Introduce yourself; stop Play to export the session log in Unity's persistentDataPath/sessions. Expect one introduce_self record for clicks within 0.35 seconds. Repeat with a deliberate pause longer than 0.35 seconds; expect both actions logged.
2. Play again immediately; the first Introduce yourself press must work. Complete identity and attach SpO2, navigate corridor -> OT -> bay, and confirm identity/monitor state persists and controls still respond.
3. In simulator controller mode select the wristband, then use the tablet Read wristband fallback. Each route should reveal the same Core-provided card. Report unexpected input loss, duplicate callbacks or Console errors.

Unrelated untracked files preserved: unity/Assets/InitTestScenebf31bc10-a0a4-4081-bbdd-c1a42989b4ad.unity and its .meta. No staging, commit or push. Exact proposed commit subject: fix(unity): preserve first input across session startup (no body). Implementation acceptance and exact-message approval pending. Intended commit author after approval: Sannan <sannanshakeel04@gmail.com>.

Next authorized slice should address fallback registry ownership/reconfiguration and dedicated grab/socket/poke coverage. Current scene XRSelectEmitter uses XRSimpleInteractable; this change verifies its callbacks, not physical grab/socket implementations. Stage 1 is still in progress; Quest replay/comfort/performance gates remain pending hardware. No acceptance inferred from this handoff.

SAN-INPUT-AUDIT commit approval (11 October 2026, Asia/Karachi): after the verification/manual-review handoff, Sannan explicitly requested committing with subject fix(unity): preserve patient state, no body. This authorizes the reviewed three-file implementation scope. Required task ci passed 27/27, exit 0. Branch: sannan47/unity-layout. Commit author: Sannan <sannanshakeel04@gmail.com>. Commit identity: the approved-subject commit containing this entry, resolved through Git history. Review staged scope and whitespace before committing. No push requested; temporary Test Runner scene and metadata remain excluded.

## 18. SAN-FALLBACK-LIFECYCLE — current-source tablet fallbacks

11 October 2026, 14:47 Asia/Karachi. Authorized by Sannan: update the Markdown with completed work and proceed to the next step. Branch sannan47/unity-layout; baseline 99b822a. Scope: Stage 1 step 4 / p0-vr-platform-s03 (B-S7), fallback lifecycle slice. Status: self-verified, awaiting Sannan manual review. Earlier debounce work is committed in 99b822a; this slice remains uncommitted.

Implementation: fallback registrations belong to live enabled emitters and are removed on disable/destruction/scene unload. Configure replaces the owner's previous action/label. The registry resets on entering Play, including domain-reload-disabled startup. Tablet More synchronizes additions/removals/label changes even when Core does not tick, preserves surviving buttons, and deduplicates by full verb/target/channel/parameters instead of label. Built-in controls remain available independently. A dynamic button resolves a current source at click time and uses its Fire/debounce, so removed/reconfigured sources cannot emit stale actions. TabletView.Press now restores the normal built-in button debounce after deterministic test/desktop dispatch. No clinical truth, package versions, scene assets or prefab GUIDs changed. Dedicated grab/socket/poke adapters remain the next separate slice; current XRSelectEmitter still uses XRSimpleInteractable.

Changed areas: ActionEmitter, UIButtonEmitter, XRSelectEmitter, TabletFallbackRegistry, TabletView, BridgeTests, README and this plan (eight tracked files). README now describes the input and fallback behavior; section 9 records actual committed foundation/room/comfort/input progress and resolves the old Pipeline blocker. Existing untracked InitTestScenebf31bc10-a0a4-4081-bbdd-c1a42989b4ad.unity and its .meta are preserved and excluded.

Verification: pinned Unity 6000.5.10f1 live Editor, every CLI command labeled and targeted to this checkout's absolute unity path. Compilation completed without errors. PlayMode **48/48 passed**, zero failed/skipped, 41.31 seconds (Logs/fallback-playmode.json); EditMode **3/3 passed** (Logs/fallback-editmode.json). Eight added tests cover enable/reconfigure/destroy, multiple owners, equal labels with different targets/parameters, stale callbacks, shared cooldown and Core rejection, actual additive scene unload without Core ticks, registry reset, and preserved physical-button debounce. Payload/replay comparison uses current unchanged content, VAR-S1-A, seed 42, Practice; full log/observable hashes match direct Core execution. Existing room/locomotion/identity/monitor regressions passed. git diff --check passed. Full task ci is deferred until a commit is requested; Quest/controller hardware validation remains pending.

Live visual/pointer evidence: temporary runtime-only Wait a moment fixture on More; inspected Logs/fallback-more.png after the normal button color fade settled. One EventSystem and a Canvas GraphicRaycaster verified. Two real uGUI pointer-click dispatches produced exactly one logged action; label changed on the same button; disabling source removed it and a stale callback added no action (Logs/fallback-live.json). Initial visual attempt found Test Runner's empty scene rather than a tablet; stopped Play, verified no unsaved scenes, reopened saved PatientBay, then repeated successfully. Temporary fixture/camera discarded on stopping Play; no scenes saved or rebuilt. Editor left on saved PatientBay outside Play.

Manual review (runtime-only fixture; stop Play to discard changes):
1. Open PatientBay and press Play. On a world object's XRSelectEmitter, uncheck the component, set Verb to wait, clear Param Key/Value, set Fallback Label to Wait a moment, then re-enable. Open tablet More: one Wait a moment button should appear. Built-in Identity/Observe controls remain.
2. Disable that emitter: its extra button disappears. Re-enable: exactly one returns. Rapidly double-click it; after stopping Play, the exported session log should have one wait record for that pair (0.35-second window).
3. Repeat a corridor -> OT -> bay trip. Removed room sources must leave no extra buttons or duplicate listeners; normal identity and SpO2 paths still work. Stop Play without saving runtime changes. Report missing/stale controls or Console errors; explicit acceptance remains pending.

Proposed commit subject: fix(unity): keep tablet fallbacks in sync (no body). No commit-message approval, staging, commit or push for this slice. Next action after review: dedicated physical grab/socket/poke adapters and integration checks; preserve the pending headset gates.

SAN-FALLBACK-LIFECYCLE approval (11 October 2026, 14:51 Asia/Karachi): Sannan requested "commit that" after the implementation handoff and exact proposed subject. Accepted for commit with subject fix(unity): keep tablet fallbacks in sync, no body. Required task ci passed 27/27, exit 0. Reviewed scope: eight Unity runtime/test/documentation files listed above; temporary test scene/metadata excluded. Branch sannan47/unity-layout; author Sannan <sannanshakeel04@gmail.com>. Commit identity is the approved-subject commit containing this entry; resolve SHA from Git history. No push authorized.

Stage 1 closeout scope: consolidate the remaining grab/socket/poke-to-action adapters and their integration checks into one reviewable step, then run the existing room/input lifecycle regression suite and address any demonstrated remaining pause/resume or subscription gaps. Already covered: single Session/rig ownership, bay/corridor/OT transitions and failures, collisions/teleport/snap/seated controls, debounce and fallback lifecycle, repeated entry, and log/state equivalence. Do not reopen completed slices without a concrete regression. Editor completion requires working interaction routes and lifecycle evidence plus Sannan's acceptance; headset acceptance remains explicitly pending device access and does not prevent subsequent Editor work. Next roadmap stage is records/diagnostics: the current default Core subsystem chain still has no investigation/result/transport subsystem, so real integration requires the owner handoff; any interim UI is a clearly labeled development fixture. This clarification authorizes no new implementation in the commit-only turn.

## 19. SAN-STAGE1-CLOSEOUT — XRI routes and view lifecycle

11 October 2026, 18:32 Asia/Karachi. Authorized: Sannan requested the next consolidated step while conserving usage, then requested continuation after the approval service hit its usage limit. Baseline 62a6799 on sannan47/unity-layout. Status: Editor foundations self-verified; awaiting Sannan manual acceptance. No commit/push authorized. The interrupted compile was not executed; continuation successfully compiled and verified the existing edits.

Implemented: XRSelectEmitter binds to XRBaseInteractable and supports existing XRGrabInteractable without adding duplicate interactables. Any retains legacy simple selection; Grab accepts non-socket/non-poke selection of a grabbable; Socket accepts only its explicit Required Socket; Poke accepts XRPokeInteractor. Missing/wrong socket and hand selection on a socket-only action emit nothing. Existing action payload/debounce/fallback behavior is retained. Selection release does not invent a clinical detach/outcome. Final equipment must explicitly wire its defined Core requests. Saved bay remains greybox proxies; no scene/prefab asset edits or package upgrades.

View lifecycle: StateView unsubscribes the old host, clears pending stale state and rebinds a replacement host. Tablet pause hints read the current host rather than a stale pause subscription; restarting/replacing the session clears the old pause hint. Changed six files: XRSelectEmitter.cs, StateView.cs, TabletView.cs, BridgeTests.cs, README.md and this plan.

Verification: Unity 6000.5.10f1, labeled Unity CLI commands targeting E:/Study/University/Semester_7/FYP/Procsense/unity. recompile_status completed without errors. Consolidated PlayMode **53/53 passed**, no failures/skips, 43.05 seconds (Logs/closeout-playmode.json); EditMode **3/3 passed** (Logs/closeout-editmode.json). New cases exercise XRI manager SelectEnter/SelectExit using actual XRDirectInteractor, XRSocketInteractor and XRPokeInteractor types, one interactable per prop, listener toggling, disabled cleanup, wrong/missing socket rejection, preserved fallback debounce, and view recovery after a paused host is replaced. All three selection routes match direct Core log and observable hashes for VAR-S1-A, seed 42, Practice, current unchanged content. Existing room transitions, blocked arrivals, repeated entry, paused-time transition, ownership, locomotion, fallback and golden checks passed. These are manager-level Editor checks, not physical controller/hand tracking or native Quest suspend/resume acceptance.

Initial full run was 50/53: the world-poke fixture auto-created an extra UI EventSystem, causing three existing room ownership assertions to fail. Configured that fixture inactive with UI interaction disabled before enabling it; world-object poke selection still runs through XRI. The corrected full rerun passed 53/53. No application ownership checks were weakened. Refreshed artifacts/unity/bay_overview.png inspected: expected greybox bay/tablet/monitor visible. git diff --check passed. Full task ci remains required before an eventual commit.

Manual review:
1. Open PatientBay, Play; complete identity and attach SpO2 using the existing tablet/world routes. Travel corridor -> OT -> bay twice. Expect the same patient state, working input and no duplicate services/errors. Pause/unpause Editor Play; navigation and input should recover.
2. In Test Runner -> PlayMode, run BridgeTests.Xri_manager_routes_real_interactors_once_with_core_equivalence (three cases), Socket_route_rejects_unassigned_wrong_socket_and_hand_selections, and Views_rebind_after_host_replacement_and_clear_a_previous_pause_hint. Expect all green; no duplicate EventSystem errors in the subsequent room tests.
3. For physical adapter inspection when controllers are available, create a temporary runtime prop with collider, Rigidbody and XRGrabInteractable first, then XRSelectEmitter. Use an existing Core verb such as read_wristband. Grab route emits on selection, not release. Socket route requires an assigned XRSocketInteractor; another socket/hand must not issue that request. Poke route requires the normal XRI world-poke collider/filter setup. Stop Play to discard fixtures. Physical tracking, reach, final equipment behavior and Quest performance are not claimed verified here.

Stage 1 disposition: the planned Editor foundation implementation and consolidated regression pass are ready for acceptance. Do not extend this stage with final models/equipment behavior; those remain their assigned later stages. After acceptance, proceed to Stage 2 records/diagnostics contract assessment and UI shell; full clinical investigation/result/transport flow still requires Core/content owner handoffs. Quest gates remain explicitly pending hardware and do not block unrelated Editor work.

Proposed commit: feat(unity): complete interaction input foundations (no body). Acceptance and exact-message approval pending. Existing untracked InitTestScenebf31bc10-a0a4-4081-bbdd-c1a42989b4ad.unity and its .meta remain untouched; no other unrelated files included. No staging, commit or push.

SAN-STAGE1-CLOSEOUT approval (11 October 2026, Asia/Karachi): Sannan explicitly requested committing the reviewed closeout and starting the next step. Approved subject feat(unity): complete interaction input foundations, no body. Required task ci passed 27/27, exit 0; Unity results remain 53/53 PlayMode and 3/3 EditMode. Scope: six reviewed Unity code/test/Markdown files. Branch sannan47/unity-layout; author Sannan <sannanshakeel04@gmail.com>. Commit identity is the approved-subject commit containing this entry. No push authorized. Stage 1 Editor foundations accepted for commit; physical headset acceptance is still pending.

## 20. SAN-DIAGNOSTICS-PREVIEW — Stage 2 presentation and contract assessment

11 October 2026, 18:58 Asia/Karachi. Authorized by Sannan to commit the Stage 1 closeout and start the next step. Stage 1 committed as 3cbea3f on sannan47/unity-layout, author Sannan <sannanshakeel04@gmail.com>, approved subject feat(unity): complete interaction input foundations (no body); task ci passed 27/27. No push. This new Stage 2 slice is uncommitted and awaits manual review.

Contract assessment: the actual default Core chain has readiness/identity/history/observations/case graph, but no investigation/result/transport subsystem or observable order/report fields. Session time-skip/attention exists; case-file result data is not runtime disclosure authorization. Added diagnostics-contract-handoff.md with exact missing capabilities/owner dependencies and the distinction between planned event names and verified APIs. No shared Core interface, clinical content or guessed production adapter was changed.

Implemented an Editor-only DiagnosticsPreview panel, opened through ProcSense -> Development -> Diagnostics Preview (Play Mode). Clearly marked synthetic patient-trip/lab-sample modes, requested/dispatched/processing/ready/delivered states, attention-stop/resume, report release gating, reset and close. Sample mode keeps the displayed patient bedside. Controls inject fixture states only: no Session.Apply, clock advancement, patient movement, actual report data or EMR updates. This is the Stage 2 UI shell foundation, not a completed clinical diagnostics loop or replacement for physical trainee tasks. Component is guarded by UNITY_EDITOR; menu is in the Editor assembly; preview objects are excluded from saves/builds and explicitly cleaned on Play exit and assembly reload.

Verification: live pinned Editor 6000.5.10f1, labeled CLI targeting this checkout. Compilation passed. PlayMode **56/56 passed**, zero failed/skipped, 43.92 seconds (Logs/diagnostics-playmode.json); EditMode **3/3 passed** (Logs/diagnostics-editmode.json). Three new cases test patient/sample report gating, interruption rejection/resume, terminal/reset/mode-switch behavior, real button callbacks/disabled state and unchanged Core observable/log/time (VAR-S1-A, seed 42, Practice; unchanged content). Full earlier input/scene regressions also pass.

Visual checks found a DontSave lookup/reload issue: a temporary hidden preview could be missed by ordinary object lookup and orphaned across reload. Corrected the menu to use Resources lookup, removed DontUnloadUnusedAsset from flags, and added explicit cleanup. Final menu code recompiled without errors; live checks after correction confirmed two menu invocations create exactly one preview including hidden objects, one EventSystem and a GraphicRaycaster, attention stop blocks advance, and Core state/log/time stay unchanged (Logs/diagnostics-live.json). Stopping Play leaves zero previews, including hidden objects. Inspected final Logs/diagnostics-preview.png: warning, interrupted sample status, hidden-report text and controls are readable without overlap. No saved scenes rebuilt or altered. Editor left outside Play on PatientBay. Full task ci is required again before committing this new slice.

Changed areas: DiagnosticsPreview.cs and Editor-generated .meta; WorkspaceMenu.cs; BridgeTests.cs; README; this plan; diagnostics-contract-handoff.md. Earlier unrelated temporary test scene/metadata remain excluded. git diff --check passed. No clinical integration, APK/device test, physical sample handling, patient transport or real-result acceptance claimed.

Manual review: open PatientBay and Play, then ProcSense -> Development -> Diagnostics Preview (Play Mode). Choose Lab sample and advance to processing; patient stays bedside and report remains unavailable. Inject attention stop: advance is disabled. Resume, inject result ready: synthetic report appears. Reset/switch route: report disappears. Repeat menu: only one panel; close or stop Play: it disappears. Repeat with Patient trip. These buttons control the developer fixture, not trainee medical actions.

Proposed commit subject: feat(unity): add diagnostics development preview (no body). Manual acceptance and message approval pending. Next step: consume the real owner-supplied investigation/result/transport contracts when available; do not expand the synthetic preview into fake clinical logic. Stage 2 runtime integration remains blocked by those dependencies.

Physical-interaction roadmap clarification for Sannan: Stage 1 supplies action routing. Stage 4 integrates pilot equipment prefabs; Stage 5 expands actual equipment; Stage 7 adds character actions; Stage 8 completes examination/treatment/equipment interactions; Stage 9 adds physical records. Tablet fallback remains where specified, but should not replace the primary physical route once that equipment is integrated.

Batch roadmap proposal (11 October 2026, Asia/Karachi; requested by Sannan):
1. Stage 4 pipeline plus pilots as one batch: Blender tooling/shared helpers/export/validation, monitor, IV pole with pump and wristband, inspected renders and Unity imports. Obtain pilot quality acceptance before producing further equipment. Future tooling edits under tools/blender require their exact outside-unity scope to be authorized before editing.
2. Stage 5/8 bedside interaction batch: create/import the supporting notes, probe and cuff assets, replace relevant proxies, wire physical identity/record/sensor actions to existing Core contracts, test rejection/release/reset and retain specified tablet fallbacks. Do not invent treatment or physiological rules.
3. Expand the accepted pipeline across bedside furniture/equipment and room placements, with collision/reach checks and one integrated review per coherent equipment family. Bring in real Stage 2/3 diagnostics/OT contracts when available; leave their missing clinical integration marked blocked rather than adding more synthetic controls.
These are proposed future batches, not implementation begun in this commit-only turn. Each batch gets continuous self-checks and one final manual-review/commit-message handoff. No mass production before pilot acceptance; headset gates remain pending device access.

SAN-DIAGNOSTICS-PREVIEW commit approval (11 October 2026, Asia/Karachi): Sannan requested just committing the reviewed preview and outlining the next batches. Accepted for commit with approved subject feat(unity): add diagnostics development preview, no body. Required task ci passed 27/27, exit 0; Unity evidence is 56/56 PlayMode and 3/3 EditMode plus corrected menu/lifecycle visual checks. Scope: seven preview/menu/test/Markdown files including its Editor-generated metadata. Branch sannan47/unity-layout; author Sannan <sannanshakeel04@gmail.com>. Commit identity is the approved-subject commit containing this entry. Existing temporary Test Runner scene/metadata excluded. No push authorized; next batches remain proposed.

## 21. SAN-BLENDER — first pipeline and pilot batch (12 October 2026)

Started 11 October 2026; updated 12 October 2026 00:01 Asia/Karachi. Sannan's “just get to work buddy” authorized the previously proposed pipeline + monitor/stand + IV pole/pump + wristband batch, including `tools/blender/`, Taskfile commands and output ignore rules. Original steps: blender-equipment s01–s05; this is the first Editor-verifiable pilot implementation, not accepted completion of every source-step criterion.

Branch: `sannan47/unity-layout`. Baseline HEAD `261afd4` was already committed; the unrelated old `InitTestScenebf31bc10-a0a4-4081-bbdd-c1a42989b4ad` scene/meta remain untouched. No commit or push in this batch. Proposed exact subject: `feat(unity): add procedural equipment pilots`; no body. Implementation acceptance and exact-message approval are pending. Eventual identity remains Sannan <sannanshakeel04@gmail.com>.

Implemented:

- Downloaded official portable Blender 4.5.14 LTS to ignored build output and verified the official SHA-256. Added exact version check, factory-startup runner, Task commands, reusable geometry/material/anchor/export helpers and per-asset scripts. No SDK/package upgrades or clinical/shared-interface changes.
- Built monitor/roll stand (3,768 triangles), IV pole/pump (2,452) and flat adult wristband (324), with shared materials and separate moving parts. Main-body monitor/pump dimensions match sourced sheets. Stand/pole dimensions and wristband thickness/fit are provisional; wristband is flat stock, not a fitted wearable.
- Validator checks applied transforms, names, UVs, manifold mesh edges, triangle/material budgets, required anchors and body dimensions. Positive and five independent deliberately invalid cases pass; one-metre hello cube generated. Four orthographic/perspective review angles plus human-scale images rendered for each asset.
- Unity AssetDatabase import creates shared URP materials, nested model prefabs, compound colliders, grab points, isolated bag-hook sockets and observable-state screen/identity surfaces. Reimport preserves existing prefab settings/GUIDs. Wristband grab uses existing `read_wristband`; monitor movement does not invent sensor readings; pump has no clinical rate/dose interaction.
- Separate `EquipmentPilotReview` scene and menu; no PatientBay edits or scene rebuild. Review is offset away from the bay for safe additive capture. Fixed the imported screen-facing direction during visual verification.

Evidence:

- Blender 4.5.14 LTS, Unity 6000.5.10f1. `task blender-build-all`, individual rebuild/preview, validator selftest and hello_cube build ran through the runner. Validation JSON/logs in ignored `build/blender/`; 1024px renders and Unity capture in `build/previews/`.
- Inspected the 15-view contact sheet and corrected wristband preview contrast; inspected the Unity pilot render after isolating it from the existing bay. No headset readability/performance claim.
- Unity compilation passed. Live EditMode tests 7/7 passed, including three dimension/material/socket checks and actual reimport after modifying mass, with original value restored. Repository `task ci`: 27/27 passed. PlayMode verification is recorded below after completion.

Known limits and next work:

- Patient wrist socket/curved fit, working pump door, sensor/cable attachment actions, final button layouts and constraint-aware stand rolling are not implemented. `SOCKET_Wrist`, door pivot and cable endpoints are markers; only bag-hook sockets are active, on reserved interaction-layer bit 8.
- Runtime screen reset/readings need the active Core host; standalone review scene is an appearance preview. Full physical bedside action batch follows pilot review, using the existing real clinical contracts.
- Production LOD policy, curved cable helper, full roster, reference-confirmed stand dimensions and EXP-BL-1 headset approval/EXP-BL-2 shared-vs-baked Quest comparison remain open. Quest 3 is still unavailable. Do not interpret these pilot meshes as the final accepted quality bar.

Manual review:

1. Open **ProcSense → Equipment → Open Pilot Review**; use Scene view to inspect the monitor body/handle/basket/castors, four pump hooks, door/buttons and flat band on its plinth. Expected: correctly scaled solids, shared materials, visible screens, no changes to PatientBay.
2. In a temporary PatientBay placement, add pilot prefabs and enter Play with the existing rig/services. Move/grab the monitor and pump; ordinary props must not snap to the bag hooks. Monitor starts with unavailable readings and updates only after the existing accepted observation actions.
3. Grab the band: one existing read action should reveal permitted identity; a new session must clear identity/readings. Inspect fitted-wrist/readability needs separately; fit is still pending.
4. Discard temporary bay placements. Review the Blender previews and provisional quality bar in `tools/blender/README.md`. Confirm appearance or request changes before roster expansion.

Changed areas awaiting review: `tools/blender/`, `.gitignore`, `Taskfile.yml`, Unity import/review scripts, equipment FBX/metadata/materials/prefabs, surface view, tests, review scene and Markdown docs. All batch work remains uncommitted; old unrelated test scene/meta remain untracked.

Final verification, 12 October 2026 00:04 Asia/Karachi: live PlayMode **57/57 passed** (48 seconds), including the new equipment permitted-state/reset regression. Final exports include named per-asset collections; rebuild and validator selftest passed. Reimported all three via AssetDatabase. Imported the generated hello cube, measured **1 metre on all three Unity axes**, then removed only that temporary smoke asset via AssetDatabase. Final import checks are repeated after this refresh. No clinical case/content changes; test scenario VAR-S1-A, seed 42, Practice. All work remains awaiting Sannan's visual/manual acceptance and exact-message approval.
Final refreshed-import EditMode run: **7/7 passed** (1.03 seconds). Git scope checked; no additional test-scene artifacts beyond the original unrelated pair.

12 October 2026 (Asia/Karachi): Sannan explicitly accepted the batch and approved the exact subject `feat(unity): add procedural equipment pilots` (no body) with 'commit that, push that.' Authorized destination: origin/sannan47/unity-layout. Commit identity: Sannan <sannanshakeel04@gmail.com>. Required CI rerun before commit; unrelated old test scene/meta excluded.

## 22. SAN-UI / SAN-ASSETS — physical bedside monitoring (12 October 2026)

Authorized by Sannan: “crack on with the next steps.” Scope: first physical probe/cuff/monitor batch from blender-equipment s07 and interaction-ui s06, extending the existing pipeline under tools/blender and the Unity client. Prior pilot commit b4aef49 is pushed to origin/sannan47/unity-layout. Current batch is uncommitted on that branch; no new message/commit/push approval inferred.

Implemented:

- Procedural cabled probe and adult cuff, 600 triangles each, shared materials and named grab/fit/cable anchors. Loose and wrapped cuff meshes; 1024px multiview renders and separate wrapped view. Corrected probe hinge clearance, navy cloth material and preview floor clipping during visual review.
- SensorAttachment maps matching physical socket placement/removal to the existing attach_sensor/detach_sensor verbs. Accepted public action logs restore visible attachment after tablet actions or scene return. No hidden PatientState access, additional clinical rules, or shared contracts. Rapid removal is not debounced away; rejected placements return to the tray; cancelled scene teardown emits no detach.
- Probe/cuff interaction bits 9/10 isolate their sockets; controller bit 0 remains available. Dynamic visual cable/hose endpoints follow the equipment. Collider sizes are enlarged for controller usability and are not clinical fit criteria.
- PatientBay now contains the monitor pilot, sensor tray, greybox forearm/finger, probe/cuff and a physical START BP control. Existing monitor objects are disabled, preserved and recoverable. Installer refuses a dirty scene and is idempotent. Scene/prefab/material mutations used the connected Editor/AssetDatabase, not hand-edited YAML or a scene rebuild.
- BP actions remain Core-owned: missing cuff and repeat-cycle rejection, cycle duration, removal cancellation and published values. Tablet controls remain operational and physical poses follow them without duplicate actions.

Verification and corrections:

- Blender 4.5.14 LTS background pipeline; user's open Blender 5.2.2 session was not modified. Validation JSON reports no errors for both assets; scripts/renders in build/blender and build/previews (ignored).
- Initial targeted Bridge tests: 38/38 passed. Full scene run found three teleport-clearance failures plus two tests expecting the replaced MonitorView. Physics overlap identified the tray and loose sensors as the actual obstruction (not the stand). Moved tray and resting poses 0.4 m toward the head of the bed; preserved the existing teleport destination and safety checks. Updated monitor tests to assert the new surface's real values and restored sensor attachment after room returns.
- Unity compilation passed; task ci 27/27 passed. Final scene, import and runtime evidence is appended below when complete. First Unity layout capture reviewed; improved arm/finger material consistency and tray contrast. Clinical test case VAR-S1-A, seed 42, Practice; physical attach/detach log and observable hashes compared with a reference Core session.

Limits: greybox arm/finger/tray, simplified flat-to-wrapped cuff, no cloth or animated jaw, no waveform/signal-quality or cuff-size scoring (Core contracts absent), no cable collision or source-verified hose length. Probe nominal envelope is informed by the reference sheet, but shell/hinge/clearance are modelling choices; cuff bladder reference is not a verified outer textile dimension. Quest reach, readability, hand-tracking and performance remain pending. This is two physical monitoring interactions, not completion of all bedside rows.

Manual review in ProcSense → Open Patient Bay, then Play:

1. Use the bedside teleport spot; it must remain clear. Probe and flat cuff start on the tray, monitor readings unavailable.
2. Grab/release the probe at the finger: SpO2/pulse appear. Pick it off: live values disappear. Taking a prop in hand or using the wrong socket must not attach it clinically.
3. Place cuff at the arm: it changes to the wrapped mesh. Press START BP on the monitor: wait for Core's cycle. Repeated press rejects an active cycle; removing the cuff before completion cancels it. A completed earlier BP reading remains historical per Core, not a continuous signal.
4. Use tablet attach/remove alternatives and verify matching physical poses. Visit corridor/OT and return: accepted attachments and Session persist. New session clears attachments and readings.
5. Inspect controller reach/readability and report clipping or layout concerns; headset acceptance remains separate.

Changed areas: tools/blender sensor assets/helpers/preview pipeline/docs; Unity importer, bedside installer, sensor binding/cable view, sensor models/material/prefabs, PatientBay, tests and Markdown logs. Old unrelated InitTestScene bf31bc10 pair preserved. Temporary current Test Runner scenes are excluded from the intended commit.

Proposed subject: feat(unity): add physical bedside monitoring
Body: none. Implementation review and exact-message approval pending; eventual identity Sannan <sannanshakeel04@gmail.com>. Next batch after review: physical identity/notes handling and remaining examination tools against available contracts.

Final evidence, 12 October 2026 18:22 Asia/Karachi: full PlayMode **62/62 passed** (43.23 s) after clearance fixes; EditMode **9/9 passed**, including both new imported sensor dimensions/materials. Repository CI **27/27 passed**; final documentation/project-rule checks passed. Actual PatientBay Play Mode XRI selections attached both installed props, physical BP control accepted, and Core Tick(45) published the reading: exactly three actions (two attachments + one measure), SpO2 98%, pulse 76 bpm, BP 124/78 mmHg for VAR-S1-A. This was a developer timing shortcut for inspection, not a runtime cycle-duration override. Captured and inspected `build/previews/bedside.png`: fitted cuff/probe, following cables, monitor values and clear tray. Exited Play afterward; temporary session logging disabled only for this inspection. No scene save from Play. Current Test Runner temporary scenes cleaned up by the runner; original unrelated bf31bc10 scene/meta remain.

12 October 2026 22:44 Asia/Karachi — removal correction and commit authorization: Sannan reported that the first tablet removal immediately reattached the sensor, then explicitly requested fixing, committing/pushing this batch and continuing the next step. The existing proposed subject remains approved: `feat(unity): add physical bedside monitoring` (no body), Sannan <sannanshakeel04@gmail.com>, destination origin/sannan47/unity-layout. Added an actual-bay coroutine regression spanning physics frames and two attach/remove cycles for each sensor; before the fix it failed with “spo2 snapped back after one remove.” Synchronizing XRGrabInteractable.SetTargetPose, Rigidbody and Transform alone did not fix stale socket overlap. The final correction also filters socket selection until the prop has cleared its previous hover and requires its fit point to be near the destination. The regression now checks automatic physical reattachment too. Older unit fixtures now place props at the socket before requesting selection and do not force a SelectExit for a filtered-out selection. No clinical contracts or extra trainee actions changed.

12 October 2026 22:55 Asia/Karachi — final removal verification: Unity compilation passed; full live PlayMode 63/63 passed (53.07 s), EditMode 9/9 passed, required task ci 27/27 passed. The multi-frame regression covers repeated single-click removal and subsequent automatic physical placement for both sensors. Manual retest: attach each sensor, press Remove once, wait, then return it physically to the matching socket; it must stay removed until deliberate placement. Headset acceptance remains pending. This reviewed bedside batch is ready for the authorized commit/push; original unrelated test scene/meta excluded.

## 23. SAN-UI / SAN-ASSETS — physical notes opening (12 October 2026)

23:02 Asia/Karachi: bedside batch including one-click removal fix committed as 4fc045a (`feat(unity): add physical bedside monitoring`, no body), author Sannan <sannanshakeel04@gmail.com>, pushed to origin/sannan47/unity-layout. Switched to sannan47/physical-notes for the explicitly requested next step. New work remains uncommitted pending manual review and its own exact-message approval.

Scope: first physical notes interaction from blender-equipment s06 / equipment sheet 05, bounded by the existing open_notes/ObservableState.Notes contract. Added a procedural A4 folder (564 triangles), hinged cover, live readable page, held-activate open/close behavior and live Editor installer. Pickup alone does not reveal notes; closing/reopening known pages emits no duplicate clinical read. Fallback and new-session state synchronize the physical folder. Existing notes proxy/view are disabled and preserved; table remains. No scene rebuild, clinical content changes or shared-interface edits.

Limits: page area is sourced A4; cover allowance/thickness and binder details are modelling choices. This is a controller-trigger opening interaction, not two-hand cover dragging. Only identifiers exist in the current notes contract. Page flipping, report insertion, historical records, signing, full band fitting and the rest of the identity/documents roster are not complete. Human patient mesh/rig/skin/breathing belongs to Stage 7; current arm/finger remain greybox. Physical probe/cuff pickup and placement already replace mandatory tablet use; fallback remains intentional. Quest acceptance remains unavailable.

Manual review: open PatientBay and Play; grip the folder on the notes table, verify pickup alone leaves contents hidden, then activate/trigger to open. Read the name/DOB/MRN on its page, close/reopen, and release. In a new session it must be closed; tablet Open notes must open the same folder. Travel away/back after reading and verify the same accepted contents return. Check readability/reach and cover clipping; simulator evidence does not replace headset review.

Changed areas: tools/blender folder generator/requirements/docs; Unity importer/installer, folder FBX/metadata/prefab, PatientBay, NotesFolderInteraction/NotesPanelView, tests and Markdown logs. Unrelated original InitTestScene bf31bc10 and its meta remain untracked and excluded. Proposed next commit subject: `feat(unity): add physical notes folder` (no body); not yet approved. Verification results follow.

13 October 2026, 15:54 Asia/Karachi — commit/push handoff: Sannan explicitly requested "commit and push please" after the exact subject above was proposed; this authorizes committing the current batch with that subject (no body) and pushing to origin/sannan47/physical-notes. No separate manual-testing result was supplied; do not infer headset acceptance. Self-verification: `task ci` passed 27/27; live Unity 6000.5.10f1 `run_tests --mode editor` passed 10/10 and `run_tests --mode playmode` passed 64/64, zero failures/skips. Folder test covers unheld rejection, pickup privacy, held activation, one read action, close/reopen, new-session reset and tablet synchronization. Import tests include the folder dimensions/materials and prefab preservation. Three-quarter procedural preview inspected at build/previews/patient_folder_three_quarter.png. Staged diff inspection found only Unity-generated trailing spaces in new prefab/meta serialization; source/document whitespace checks passed and generated serialization was preserved. Initial sandbox tooling failures were resolved by authorized local execution outside the sandbox; no package/version change. Quest comfort/readability/device replay remain pending. Commit author: Sannan <sannanshakeel04@gmail.com>. Commit identity is available in Git history for this file; destination is origin/sannan47/physical-notes. Preserve and exclude generated InitTestScene files and their metadata; no clinical/Core changes included.

## 24. SAN-CHAR — visible CGTrader patient trial (13 October 2026)

Updated 18:43 Asia/Karachi. Authorized: Sannan requested a visible patient now, supplied CGTrader item 6984099 and local patientModel downloads, and explicitly authorized local use despite unavailable licence information. This moves the static visual trial forward from Stage 7; it does not authorize clinical animation, redistribution, commit or push. Branch: sannan47/patient-model; baseline ca65524 (physical notes was successfully pushed to origin/sannan47/physical-notes). Status: local static trial self-verified, awaiting Sannan visual review. Licence/provenance is recorded as unverified, not approved for distribution.

Original inspection: one mesh, 845,201 vertices / 1,500,000 triangles, three 4096px textures, no armature, animations, modifiers or facial shape keys. Standing male in blue scrubs, not a patient gown. Original blend SHA-256: E39EB6AC94CD80C059CCD15EA696523F3B78407C70ECA693B6C42BCAED1C96E8. The file was authored in a newer Blender version; pinned 4.5.14 loaded it with compatibility warnings. Original files were read only and remain untouched under patientModel/.

Delivered: a 40,000-triangle static FBX, welded seam vertices and consistent face winding, new UV atlas and 2048px colour bake, URP Lit material with calculated normals and Android ASTC 6x6. Early reductions showed surface holes; texture/lighting isolation identified inconsistent face winding, and welding/correcting normals resolved them before handoff. The original normal map was not reused after topology changes. PatientBay now shows this model lying face-up at a provisional 1.75m height. Existing patient/forearm/finger renderers are disabled; their colliders, sensor sockets and clinical interaction components are preserved. No room rebuild, Core/content/interface edits, animation-driven state or package upgrades. Ordinary Unity save also serialized notes-Canvas shader-channel flags; preserved as generated metadata.

Changed areas: Assets/Models/Characters/PatientTrial (FBX, baked texture, material, provenance README and Unity metadata); PatientBay; unity/Tools/prepare_patient_trial.py; this log. Preparation script reads the original blend and writes to ignored unity/Logs/patient-review; no files under tools/blender changed. Temporary trial captures accidentally written under Assets by capture_scene_view were moved into ignored Logs and their own AssetDatabase entries removed. User downloads and the unrelated original InitTestScene bf31bc10/meta remain untracked and untouched.

Verification: live Unity 6000.5.10f1, commands labelled unity-cli and targeting this checkout. EditMode 10/10 passed (1.53s); PlayMode 64/64 passed, zero failed/skipped. These retain identity/privacy/folder, sensor placement/removal/BP, scene/session and replay regressions. Final mesh count/material/bounds inspected through the Editor. Visual inspection passed for unity/Logs/patient-review/final-bedside.png. Git diff check reports only Unity-generated blank-value trailing whitespace in scene serialization; preserved. Full task ci is not rerun for this uncommitted local asset trial; it remains mandatory before a later authorized commit. Quest performance/readability and clean-machine acceptance are unavailable, not inferred from reduced geometry or Editor tests.

Limitations: static geometry only; no breathing, blinking, speech/lip sync, clinical signs, staff or demographic/case appearance binding. Scrubs remain as supplied. Cuff/probe socket fit to the real hand/forearm is not accepted; existing logic and socket positions are retained for this trial. Final rig/retopology/clinical presentation and licence confirmation remain separate work.

Manual review: open ProcSense -> Open Patient Bay, inspect Patient model trial in the bed, then Play and walk to the bedside. Expect a visible face-up human, no capsule visuals, normal notes/identity and monitoring controls, and no new exceptions. Inspect face, fingers, clothes, bed contact and size; attach/remove probe/cuff and report fit/clipping separately from logic. Stop Play and report whether this appearance is a usable starting model. Explicit acceptance is pending. Proposed eventual commit subject: feat(unity): add static patient model trial (no body); message approval and distribution scope remain pending. No staging, commit or push performed.

13 October 2026, 18:58 Asia/Karachi — Sannan requested committing the current baseline before starting the separately authorized arm/equipment fitting. Approved exact subject: feat(unity): add static patient model trial (no body), author Sannan <sannanshakeel04@gmail.com>, branch sannan47/patient-model. This is acceptance for a local baseline commit; manual/device acceptance and licence verification are not inferred. Only the trial assets, preparation script, saved PatientBay and log are included. Original patientModel downloads and unrelated InitTestScene/meta are excluded. No push authorized. Required CI results follow.
Required pre-commit task ci passed 27/27 (exit 0). Existing Unity evidence remains EditMode 10/10 and PlayMode 64/64. Staged scope inspected: 13 trial/scene/script/log files; only generated Unity blank-value whitespace is present. Commit identity is the approved-subject commit containing this entry; resolve its SHA from Git history. No fitting edits have started at baseline commit.

## 25. SAN-CHAR / SAN-UI — fixed bedside patient demo (13 October 2026)

Authorized: Sannan requested making the current patient work for the 14 October mid evaluation and then explicitly instructed "get to work then i guess" after baseline commit a5e4b3b. Scope: fixed arm adjustment, fitting existing sensors and a live wristband, preserving the current patient until later MPFB replacement. Branch sannan47/patient-model. Status: self-verified in Editor, awaiting Sannan manual review. No commit/message/push authorization for this new batch.

Delivered: preserved base patient_review.fbx; Blender 4.5.14 script under unity/Tools generates separate 40k-triangle patient_demo.fbx with 3,329 vertices adjusted for arm clearance, retaining the base UV atlas. No full rig or animation added; fingers remain curled. Scene model uses a mesh override, leaving the baseline asset intact. Existing cuff/probe sockets follow the posed static arm/finger positions. Wrapped cuff gets a scene-local ellipse/depth adjustment; close-up inspection initially found skin penetration and final clearance was corrected. Finger probe orientation was adjusted to the curled finger. Greybox colliders moved with the sockets; hidden renderers remain hidden.

The fitted wristband annulus is procedurally authored in Blender and exported as OBJ. The Unity run_script fitting tool creates a fixed XRSimpleInteractable with existing read_wristband/XRSelectEmitter and a live EquipmentSurfaceView.Identity label. No identifiers are baked into art. The old wristband proxy is disabled and preserved. The initial Unity-created intermediate ring mesh was replaced by the reproducible Blender OBJ and its own unused asset removed through AssetDatabase. No clinical/Core/shared-interface/package changes, no scene rebuild, no tools/blender changes, and no originals in patientModel modified. The fitting script rejects Play Mode or unsaved PatientBay and reuses the installed wristband.

Verification: labelled live Unity CLI targeting this absolute checkout, Unity 6000.5.10f1. Full PlayMode 65/65 passed, including new real-XRI wristband reading/privacy/reset and existing multi-frame physical attachment/removal and lifecycle regressions. EditMode 10/10 passed. After final equivalent Blender ring import, targeted PatientBayTests 6/6 passed. Required task ci 27/27 passed, exit 0. Scene/script diffs inspected; only normal Unity-generated blank-value trailing spaces are excluded from the whitespace check. No regression is left unresolved.

Actual walkthrough in saved PatientBay: VAR-S1-A, seed 42, Practice, unchanged content. Rejected measure_nibp without cuff; fresh Session used physical XRI selection to read the fitted wristband and attach both sensors, then the existing BP emitter accepted measure_nibp. Exactly four actions in the fresh log. Core Tick(45), a developer-only inspection shortcut, published SpO2 98%, pulse 76 bpm and BP 124/78 mmHg; the runtime cycle duration is unchanged. Final captures inspected at unity/Logs/patient-review/demo-close-corrected.png and demo-final.png. Temporary save-log/AutoTick overrides and inspection hand were confined to Play Mode; exited Play without saving runtime state. Captures show no cuff skin penetration; probe fit and original curled fingers remain approximate prototype geometry. Headset/device readability and comfort remain unverified.

Manual review: (1) ProcSense -> Open Patient Bay, Play, approach the bedside. Expect the static human with more arm clearance and no greybox body/arm visuals. (2) Select the fixed wristband; expect permitted name/DOB/MRN, not a detachable prop. A new Play session shows READ WRISTBAND again. (3) Grip the probe/cuff from the tray and release at the matching finger/arm; expect the prop to fit and permitted SpO2/pulse to appear. (4) Press START BP and wait for the normal cycle; remove the cuff mid-cycle to check cancellation, then replace and retry. Removing a sensor once must return it to the tray and keep it detached until deliberate placement. (5) Travel corridor/back; accepted session/attachments/read identity must persist. Review clipping/reach/readability and report issues. This static demo is not complete Stage 7, a clinical animation system, anatomical validation or Quest acceptance.

Changed areas: PatientBay, PatientBayTests, new posed FBX/OBJ assets and metadata, unity/Tools pose and Editor-fitting scripts, Unity README, asset README, this plan. Original downloads and pre-existing InitTestScene bf31bc10/meta remain untracked and excluded. All fitting changes remain uncommitted. Proposed exact next subject: feat(unity): fit bedside equipment to demo patient (no body). Wait for explicit implementation acceptance and exact-message approval before committing. Next: fixes from review, then later MPFB replacement/rig and authoritative clinical presentation work as separately authorized.

13 October 2026, 19:46 Asia/Karachi — Changes requested and corrected: Sannan rejected the visual arm edit because skin appeared merged/stretched out of the clothes. The prior claim of visual readiness is invalidated. Close-up comparison confirmed the coordinate-based edit deformed both clothing and skin; that approach was removed. PatientBay now uses the original patient_review.fbx, with explicit prefab property recording, and the cuff/probe sockets and wristband were refitted to its original curled-arm pose. No arm straightening is claimed. The rejected generated patient_demo.fbx was confirmed unreferenced, preserved in ignored Logs for evidence, then removed from Assets through AssetDatabase with its own metadata. The former pose script is now make_patient_wristband.py and creates only the Blender wristband. Original patient assets, user downloads and unrelated files remain untouched.

Correction verification: affected PatientBayTests 6/6 passed after restoration, including physical wristband reset and repeated physical sensor placement/removal. Actual XRI walkthrough rerun: rejected BP without cuff, then four accepted actions (wristband read, two sensor attachments, BP request) with both sensors attached and Core-generated 98% / 76 bpm / 124/78 after developer-only Tick(45). Source mesh path confirmed patient_review.fbx during Play. Visually inspected original-arm-refit.png and corrected-original-fit.png under unity/Logs/patient-review; original sleeve silhouette is restored and equipment fits this pose. Original scan texture/topology artifacts and curled fingers remain. Earlier full 65/65 PlayMode, 10/10 EditMode and 27/27 CI are prior batch evidence; the 6/6 rerun and new inspection are the correction evidence. Exited Play without saving runtime state. All correction work remains uncommitted and awaits renewed manual acceptance; proposed subject remains feat(unity): fit bedside equipment to demo patient (no body), not approved. Manual review: Open Patient Bay, inspect the original sleeve/arm shape at close range, then Play and attach/read/remove equipment as in section 25. Rigging/MPFB is still required for proper future posing.

13 October 2026, 19:51 Asia/Karachi — Sannan accepted the working corrected fallback for a local commit and explicitly requested starting MPFB afterward. Approved exact subject feat(unity): fit bedside equipment to demo patient (no body), author Sannan <sannanshakeel04@gmail.com>, branch sannan47/patient-model. Fresh required task ci 27/27 passed, exit 0; correction evidence remains 6/6 bay tests plus original-pose physical walkthrough. Include only corrected scene, wristband OBJ/meta, fitting/generator scripts, test and documentation. Exclude original downloads and unrelated test scene/meta. No push authorized. Commit identity is the approved-subject commit containing this entry. MPFB work will begin from this committed fallback and requires its own later review/message approval.

## 26. SAN-CHAR — rigged MPFB patient candidate (13 October 2026)

21:23 Asia/Karachi: Sannan explicitly requested committing the corrected working fallback, then building an MPFB replacement now. Fallback commit 4b12cb9 has approved subject `feat(unity): fit bedside equipment to demo patient` (no body), author Sannan <sannanshakeel04@gmail.com>, branch sannan47/patient-model. No push authorized. The MPFB candidate is self-verified and remains uncommitted pending manual review and its own exact-message approval.

Delivered: separate MPFBPatientReview scene, menu ProcSense -> Characters -> Open MPFB Patient Review, adult male preset, separate blue gown, pillow and fitted leg cover. Body and garments use a 53-bone rig with a valid Unity Humanoid Avatar; body has 67 facial/speech blendshapes. Character total: 41,796 triangles across eight skinned parts. Arms are posed through bones; cuff socket, finger probe socket and live identity band follow their relevant bones. Equipment attachment/removal and Core-owned BP behavior remain unchanged. Animator is disabled for the fixed bedside pose; skinning and facial shapes remain available. Review exits are disabled and this scene is excluded from production build settings. Original PatientBay remains unchanged from the fallback commit.

Provenance/reproduction: MPFB 2.0.18 with pinned Blender 4.5.14; CC0 MakeHuman system, faceunits01 and visemes02 packs. Exact source URLs and SHA-256 values are recorded in unity/Tools/MPFB/dependencies.json. Recipes, preset, importer, scene fitting and surface-sampling tools are under unity/Tools/MPFB; imported assets/materials/metadata under Assets/Models/Characters/MPFBPatient. Workspace-local downloaded software, asset library, isolated Blender preferences and editable source blends are in ignored build/mpfb. No global Blender configuration, Unity package versions, ProjectVersion, Core, clinical content or shared interfaces changed. Import scale was corrected using bone-weighted bounds; gown clearance and wrist orientation were adjusted without stretching the original scan. Leg-cover surface was sampled from the posed body; final visual inspection prompted an additional 35 mm cover clearance saved through the live Editor.

Verification: connected Unity 6000.5.10f1, commands labelled unity-cli and explicitly targeting this checkout. Full live PlayMode 68/68 passed; EditMode 10/10 passed; task ci 27/27 passed, zero failures/skips. New candidate tests verify Humanoid rig validity, actual arm skin deformation and band following, facial blendshape deformation without Core log changes, automatic physical sensor placement and single-click removal. Initial forearm test used its twist axis; corrected to a bend axis and reran successfully. Actual review-scene walkthrough rejected BP without a cuff, then logged exactly four accepted actions: physical wristband read, both sensor placements, BP request. Core Tick(45) was only an inspection shortcut; published SpO2 98%, pulse 76 bpm, BP 124/78, both sensors attached. Runtime cycle duration remains unchanged. Final face and bed captures inspected under unity/Logs/patient-review/mpfb-final-face.png and mpfb-final.png. The cover-clearance change is decorative only and was visually rechecked after the full test suites. Exited Play without saving inspection state; review scene remains open in Edit Mode.

Limits: this is a usable rigged candidate, not completion or acceptance of Stage 7. Gown/linen are simplified authored geometry, with a rough hem; facial appearance and materials need Sannan's visual review. No automatic breathing, blinking, lip sync, clinical signs, demographic binding, cloth simulation or ragdoll behavior has been added. No Quest performance, headset comfort or anatomical validation is claimed.

Manual review: open ProcSense -> Characters -> Open MPFB Patient Review, then Play. Inspect face, sleeve/skin separation, bed contact and arms from the bedside. Select the wristband to read live identity; place cuff/probe from the tray and expect fitted placement and permitted readings. Remove each once and wait: it must stay removed until deliberate replacement. Press START BP with the cuff attached and wait for the normal cycle; removing the cuff during the cycle must cancel it. Stop Play before switching back through ProcSense -> Open Patient Bay, which still opens the committed CGTrader fallback. Report visual fit/clipping and whether this candidate should replace the fallback; do not infer acceptance from silence.

Changed/uncommitted areas: new MPFB model/material/texture/linen assets and metadata, review scene, editor menu, three PlayMode tests, generation/import/fitting scripts and documentation, Unity README and this plan. Original patientModel downloads and unrelated InitTestScene bf31bc10/meta remain untracked, untouched and excluded. No staging or MPFB commit/push performed. Proposed exact subject: `feat(unity): add rigged MPFB patient review` (no body), eventual identity Sannan <sannanshakeel04@gmail.com>. Next action: Sannan's visual review and any requested fixes, followed by exact-message approval before committing.

13 October 2026, 21:27 Asia/Karachi — Sannan requested "commit that i guess" after the exact proposed subject, authorizing a checkpoint commit with `feat(unity): add rigged MPFB patient review` (no body), Sannan <sannanshakeel04@gmail.com>, branch sannan47/patient-model. He explicitly rejected the gown/bed-cover appearance and requested fixing stiff/floating cloth and legs appearing fused into the mattress after this checkpoint; visual acceptance is therefore not claimed. Fresh task ci passed 27/27 before staging. Original downloads, local personal instructions and unrelated test scene/meta remain excluded. No push authorized. Next: separately authorized fabric/bed-contact correction, followed by renewed visual review.

## 27. SAN-CHAR — patient fabric and bed-contact correction (13 October 2026)

21:51 Asia/Karachi: Sannan requested committing the MPFB checkpoint and explicitly fixing rigid/floating clothing and legs looking fused into the bed. Checkpoint committed as e1c741d with approved subject `feat(unity): add rigged MPFB patient review` (no body), author Sannan <sannanshakeel04@gmail.com>, branch sannan47/patient-model. Fresh pre-commit CI passed 27/27; staged 61 files were inspected, downloads/personal profile/unrelated test scene excluded. No push authorized. His criticism is recorded as changes requested, not visual acceptance of the checkpoint.

Correction: exported actual posed skin, original gown and bed bounds through the live Editor with temporary mesh readability restored afterward. Welded garment export seams, subdivided the gown and fitted it to the torso with restrained folds and interpolated original weights. New gown has 9,168 triangles, all 53 bone bindings, normalized weights; active character total 49,436 triangles. Added a continuous 8,640-triangle blanket spanning both legs, with gown clearance and draped side/foot edges. Separate rounded 140 mm mattress and visible frame/supports replace the solid block visually; original Bed collision remains intact. Original gown/blanket remain preserved but disabled. Forearms posed outward through bones to keep hands, wristband and finger probe exposed beside the cover. Original PatientBay, FBX/body/facial shapes, Unity package/editor pins and clinical/Core/shared interfaces are unchanged.

The first free cloth simulation produced excessive shoulder crumpling and was rejected; final recipe uses deterministic surface fitting. Its initial importer also encountered Unity fake-null component handling and stale native Mesh buffers after topology replacement; explicit component checks and vertex-buffer updates fixed those issues, preserving mesh asset GUIDs. Reimport and runtime inspection confirmed the final geometry. Source code and local snapshots/working Blender files are reproducible through unity/Tools/MPFB; output remains ignored build/mpfb. No runtime cloth solver, clinical animation or extra action introduced. This fit is for the bedside pose; larger future animations require separate visual review.

Verification: connected Unity 6000.5.10f1, every Editor command labelled unity-cli and targeting this checkout. Compilation succeeded. Full live PlayMode 68/68 passed, EditMode 10/10 passed, task ci 27/27 passed. Existing rig test now also verifies normalized garment weights and actual gown deformation after an upper-arm bend, with no Core action changes. Sensor tests still verify automatic placement and one-click removal. Actual runtime VAR-S1-A/seed 42/Practice walkthrough rejected BP without the cuff, then accepted four actions (wristband read, two sensor placements, BP request), both sensors attached. Developer-only Core Tick(45) published 98% SpO2, pulse 76, BP 124/78; clinical cycle timing is unchanged. An inspection call made before host startup was ready was retried successfully; no source behavior was changed to bypass initialization. Final runtime capture inspected at unity/Logs/patient-review/fabric-final.png; gown/blanket overlaps corrected and exposed fingers inspected. Exited Play without saving inspection state. Headset appearance, reach and performance remain unverified.

Manual review: ProcSense -> Characters -> Open MPFB Patient Review, Play, approach the bedside. Inspect the gown's torso fit and sleeve edges, blanket spanning both legs and hanging over the separate mattress/frame. Read the exposed band, physically attach/remove probe and cuff once, and run BP through its normal cycle. Expect matching permitted readings, no immediate reattachment after removal, and accessible fingers. Review from both bedside angles; report remaining clipping or unwanted silhouette. Stop Play afterward. This is awaiting Sannan visual acceptance, not a claim of perfect cloth or complete Stage 7.

Uncommitted scope: review scene, gown material, three new Mesh assets/meta, frame material/meta, fabric snapshot/fitting/mattress/import/pose tools, garment regression test, asset/tool documentation and this plan. Downloads in patientModel and original InitTestScene bf31bc10/meta remain untouched/untracked. No second commit or push performed. Proposed exact next subject: `fix(unity): fit patient gown and bedding` (no body), eventual author Sannan <sannanshakeel04@gmail.com>. Wait for renewed implementation acceptance and exact-message approval. The static fallback and e1c741d MPFB checkpoint remain available.

13 October 2026, 21:55 Asia/Karachi — Sannan explicitly requested committing this correction after the exact subject was proposed. Approved subject `fix(unity): fit patient gown and bedding` (no body), author Sannan <sannanshakeel04@gmail.com>, branch sannan47/patient-model. Fresh required task ci passed 27/27; staged 19-file correction scope and whitespace checked. This is checkpoint acceptance; Sannan identified exposed feet and a floating blanket top and authorized a further inspection from multiple angles with fixes. No headset or final visual acceptance is inferred. Original downloads and unrelated test scene/meta excluded; no push authorized. Next: inspect both bed sides, foot/head views and overhead, then correct remaining fit issues and repeat affected verification.

## 28. SAN-CHAR — multi-angle patient fit inspection (13 October 2026)

22:39 Asia/Karachi: Sannan explicitly requested committing section 27, then inspecting other angles and fixing remaining exposed feet/floating blanket. Approved checkpoint committed as 702c02f, subject `fix(unity): fit patient gown and bedding` (no body), author Sannan <sannanshakeel04@gmail.com>, branch sannan47/patient-model. Fresh pre-commit task ci 27/27 passed and 19-file staged scope/whitespace inspected. No push authorized. Further fixes remain uncommitted and await renewed review/message approval.

Inspection: both low side profiles, foot of bed, overhead, head-side face/neckline close-up and runtime equipment close-up. The first head view was obstructed by the headboard and was replaced with an unobstructed head-side view. Before captures under unity/Logs/patient-review/before-*.png show toe/ball skin penetrating the foot-end turn, an elevated flat waist edge, hands hovering sideways, and pale blue eye fronts. The new angles invalidated relying on the previous three-quarter view alone.

Corrections: lowered shoulder/forearm poses, flattened the hands, rested the heels and relaxed ankle/toe direction through existing bones. Sensor targets and wristband remain bone mounted. Regenerated the fitted gown for this pose. Blanket turn/start and its conservative roof are now based on all foot-region skin vertices, including toe/ball skin; the earlier foot-only bone filter omitted toe tips. Smoothed the transition from feet to legs, removed obsolete hand notches, and fitted the upper 18 cm to gown/mattress contours. Neighboring surface samples prevent steep gown sides cutting through the blanket. The stock brown-eye texture's alpha-zero cornea patch was rendered opaque; enabled alpha clipping in its URP material and importer recipe so the brown irises are visible. No original FBX/body mesh or facial shapes, clinical content/interfaces/Core, Unity package/editor pins or PatientBay fallback changed.

Verification: Blender 4.5.14 coverage report build/mpfb/output/fabric-coverage.json checks all 2,682 skin vertices in the foot region against the actual triangulated blanket: no missing footprint/penetration, minimum vertical clearance 0.0263595 m. Recipe raises an error below 8 mm. Geometry counts unchanged: active character 49,436 triangles; blanket 8,640. Live Unity 6000.5.10f1 full PlayMode 68/68 passed and EditMode 10/10 passed, zero failures/skips; task ci 27/27 passed. Labelled unity-cli commands target this absolute checkout. Final images inspected: final-left.png, final-right.png, final-foot.png, final-top.png, after-head-close.png and final-equipment-close.png. Repeated side checks found and corrected gown-side breakthrough and a sharp foot transition before final handoff. These captures are under unity/Logs/patient-review and remain ignored.

Actual runtime walkthrough after final pose: VAR-S1-A, seed 42, Practice; rejected BP without cuff, then four accepted physical actions (wristband read, both sensor placements, BP request). Both sensors attached; Core-only developer inspection Tick(45) published SpO2 98%, pulse 76 bpm and BP 124/78. Runtime clinical timing remains unchanged. Cuff/probe/band clearance inspected close-up. Exited Play without saving inspection state. Current runner temporary scene cleaned itself up; original bf31bc10 scene/meta and patientModel downloads remain untracked, untouched and excluded. Headset performance/reach and clinical/anatomical appearance acceptance remain unavailable. Fabric remains baked for the fixed bedside pose; no breathing/speech/clinical animation or cloth physics added.

Manual review: open ProcSense -> Characters -> Open MPFB Patient Review, Play, and inspect from both sides, the foot and head side. Expect covered toes, a smooth foot-end drape, the blanket waist edge following the gown, lower resting hands, and visible brown irises. Read the band; attach/remove cuff and probe once each; press START BP with the cuff fitted and wait for the normal cycle. Look for remaining clipping around sleeves, wrists, foot end and waist from low angles. Report corrections or acceptance; do not infer final appearance/headset acceptance from the checkpoint commit.

Uncommitted areas: eye material/importer recipe; fitted gown/blanket Mesh assets; review scene limb overrides; pose/fabric generator and tool README; this plan. Existing asset GUIDs and metadata preserved; no extra download or runtime dependency introduced. Proposed exact next subject `fix(unity): refine patient pose and blanket clearance` (no body), eventual author Sannan <sannanshakeel04@gmail.com>. No staging, further commit or push performed. Next action is Sannan's renewed multi-angle visual review.

13 October 2026, 22:41 Asia/Karachi — Sannan explicitly approved committing the section 28 refinements with "do it" after the exact proposed subject. Approved subject `fix(unity): refine patient pose and blanket clearance` (no body), author Sannan <sannanshakeel04@gmail.com>, branch sannan47/patient-model. Acceptance is for this local checkpoint; headset/final clinical acceptance is not inferred. Reviewed scope is nine Unity files: eye material, fitted gown/blanket assets, review scene, importer/pose/fabric recipes, tool README and this plan. Preserve original downloads and unrelated bf31bc10 scene/meta; no push authorized. Existing implementation evidence remains 68/68 PlayMode, 10/10 EditMode, multi-angle inspection and the 2,682-vertex foot coverage check. Fresh required pre-commit CI result follows.
Fresh pre-commit task ci passed 27/27, exit 0. Staged scope/whitespace rechecked; only the nine approved Unity files are included. Commit identity is the approved-subject commit containing this entry; resolve SHA from Git history. No push authorized and no further implementation started.

## 29. SAN-UI — wristband controller correction and demo launch login (13 October 2026)

23:22 Asia/Karachi: Sannan requested checking the floating "Point at her wrist" screen and interruption/errors when pointing at the band, then a well-designed email/password login on launch with an easy development bypass. He clarified explicitly: fixed demo credentials only; backend authentication is a teammate TODO. Baseline d9d553e is committed on sannan47/patient-model. Current work is uncommitted; no message/commit/push authorization inferred. User downloads and original unrelated bf31bc10 scene/meta remain preserved/untracked.

Wristband diagnosis/fix: the old IdentityCardView still generated the floating prompt although the physical band already displayed live identity. Disabled its preserved host in PatientBay and MPFBPatientReview; fallback wording is now neutral. Original log contained duplicate START BP collider ownership and XRSimulatedController haptic-capability warnings on hover, without a recorded fatal crash stack. The monitor grab now explicitly owns its root colliders while its child BP control owns its separate collider. Updated source generator and existing prefab through the connected Editor. DevOnly now guards vibration feedback only while virtual controllers are present in the Editor; real player-build feedback settings are unchanged. Early regression used obsolete ray-only fixture selection; corrected it to the actual near/far controller. The regression then caught a remaining haptic warning before virtual-device initialization; device-change handling and immediate checks corrected that timing. Final controller hover/select tests pass in both bays, with identity read, one Core action, advancing frames, one camera, correct collider lookup and no haptic/duplicate-registration warnings or errors.

Login: authored Canvas/TMP prefab Assets/Prefabs/UI/LoginScreen.prefab with dark teal background, abstract signal ribbons, rounded light card, email/password fields, password reveal, inline errors, sign-in button and visible demo account. Demo email demo@procsense.app, password ProcSenseDemo!. No real authentication, account storage, network requests, password logging or saved credentials. Entered password clears after success. Backend-owner TODO is explicitly marked in DemoLoginView.Submit. Native code graphics reuse the existing static TMP font; no raster/media download, package or SDK change. Narrow-window layout hides the introductory column; desktop layout was visually verified, not a device acceptance claim.

Boot waits on successful demo sign-in before loading Sys_Core/PatientBay, so clinical state/time does not run behind login. One temporary launch rig and XR-capable EventSystem are destroyed before the application rig is loaded. The Editor bypass is checked by default via local EditorPrefs; menu ProcSense -> Development -> Bypass login in Editor toggles it without changing tracked settings. ProcSense -> Development -> Preview login shows it once while preserving normal bypass. Direct room Play behavior is retained. Assets/Settings/DemoLoginSettings.asset controls launch visibility and optional Development Build bypass; player builds do not use EditorPrefs. XR-active presentation uses a world-space panel; Quest typing, reach and keyboard behavior require device review. The existing fallback PatientBay remains Boot's destination; this step did not promote or rebuild the separate MPFB review scene.

Visual corrections during implementation: launch order initially allowed XRI to auto-create a second EventSystem; creating the intended input system before the rig fixed it. Rounded graphics required explicit CanvasRenderer components; repaired those in place with their references/GUIDs preserved, then checked the rendered login. Desktop canvas uses Screen Space Camera, which supports the capture/render workflow; XR uses world space. Disabled the temporary launch rig's movement panel. Final 1280x800 login screenshot inspected at unity/Logs/login-review/login-default.png: legible labels, card/field/button boundaries, masked password entry and demo credentials. Original room layouts were not rebuilt or overwritten.

Verification: live Unity 6000.5.10f1, labelled unity-cli commands targeting this checkout. Wristband targeted 2/2 passed; login targeted 2/2 passed; full PlayMode 72/72 passed, EditMode 10/10 passed, zero failures/skips. Login tests reject empty/wrong details, exercise Show/Hide, click the actual sign-in button, confirm password clearing and no Session before login, verify one camera/EventSystem after successful or bypass launch, and reject duplicate workspace creation. Existing sensor/folder/navigation/session/replay checks remain passing. task ci 27/27 passed. Final scene/prefab diff inspected: old cards only disabled in bays, explicit monitor collider list, one DevOnly guard, and Boot login references; no clinical content/Core/shared-interface or package/editor-pin changes. Git whitespace check excludes normal Unity blank-value serialization and passes. Headset/native-crash reproduction is not claimed; repeated controller interaction checks remained running without errors.

Manual review: (1) Open MPFB Patient Review or PatientBay and Play; hover/select the fitted band with the controller. Expect no floating card, readable permitted identity on the physical band, and no hover/selection interruption. Check START BP still selects the button. (2) Use Development -> Preview login. Empty/wrong details must stay on the screen with an inline message; Show/Hide must toggle password visibility. Enter the displayed demo details and Sign in: expect the existing PatientBay, one active rig/input system and normal interactions. (3) Stop Play and run Boot normally with Bypass login in Editor checked: expect immediate workspace entry. Uncheck it and Play Boot to require login each run; recheck for routine development. Backend remains TODO; no real account validity is implied.

Changed areas: login runtime scripts/editor builder, UI prefab and settings/meta, BootLoader/Boot references, simulator guard/DevOnly, monitor prefab/generator collider lists, disabled legacy cards in both bays, regression tests, targeted fitting tool and documentation. Original downloads and unrelated test scene/meta excluded. No staging, commit or push performed. Proposed exact eventual subject: `feat(unity): add demo login and fix wristband interaction` (no body), author Sannan <sannanshakeel04@gmail.com>. Wait for explicit review and exact-message approval before committing. Next action: Sannan's login/controller review; backend integration remains separately owned.

## 30. SAN-CHAR / SAN-UI — enabled login and permanent MPFB bay (13 October 2026)

23:49 Asia/Karachi: Sannan accepted the login/controller work and requested enabling login for normal launches, sign-in into the app, retiring the CGTrader patient and making MPFB permanent. Baseline remains d9d553e on sannan47/patient-model; section 29 and this promotion are uncommitted. No commit-message or push authorization inferred.

Implemented: demo login now defaults on in the Editor as well as player launch; local Editor bypass is unchecked. The development toggle remains available as an opt-in. Added ProcSense -> Open Application to open Boot for normal login-first Play. Correct demo credentials still gate Sys_Core/PatientBay startup; no clinical Session exists behind login and backend authentication remains the teammate TODO. Clinical test fixtures use an explicit scoped test override rather than depending on the user's development preference; the override is excluded from ordinary player builds.

Promoted the authored MPFB scene into the existing Assets/Scenes/PatientBay.unity through the connected Editor, preserving PatientBay's asset GUID. Normal app launch, builds, corridor return and the ordinary bay menu now use the rigged MPFB patient, fitted gown/blanket/mattress, bone-mounted band and sensors. Navigation doors are active. Characters -> Open MPFB Patient opens this main bay. Old static Patient model trial object removed. Moved the still-used patient_wristband.obj and its metadata through AssetDatabase into MPFBPatient, preserving GUID/file identities; main bay no longer depends on the PatientTrial graphics.

Cleanup boundary: automatic approval review rejected the initial whole-folder and MPFB-snapshot deletion as broader than explicitly approved, with safer alternatives available. No rejected broad deletion was performed. The approved safer promotion retains the MPFB review snapshot and old documentation folder, verifies dependencies across all scene/prefab consumers, and deletes only patient_review.fbx, patient_albedo.png, patient_trial.mat and their metadata. Original patientModel downloads and unrelated bf31bc10 scene/meta remain untouched/untracked. The retained snapshot also has its CGTrader object removed and is outside build settings; it is not the app destination. Old static fitter refuses to run; MPFB fitting tools now target PatientBay, and the wristband recipe emits into ignored build/mpfb/output. No Core/content/shared-interface or package/editor version changes.

Verification: connected Unity 6000.5.10f1, labelled unity-cli commands explicitly target this checkout. Full PlayMode 72/72 passed, EditMode 10/10 passed, zero failures/skips; task ci 27/27 passed. The actual login regression now asserts that successful sign-in reaches MPFB, with no static trial object. Navigation/room-return, identity privacy/reset, wristband controller hover, sensor placement/removal/BP, folder, rig deformation and facial shape checks remain passing on the promoted main scene. Scene GUID preservation and zero PatientTrial dependency verified through AssetDatabase. Git whitespace and scope checked; only normal Unity generated blank-value whitespace excluded. The moved band has the same metadata; retired assets have no scene/prefab consumers.

Normal launch inspection without test/preview override: Open Application -> Play showed login with bypass=false, testOverride=null, SimulationHost absent. Entered the fixed demo account; acceptance cleared the password and loaded PatientBay with MPFB present, valid Humanoid Avatar, old static patient absent, one camera and one EventSystem. Temporary SaveLogOnDestroy=false was confined to the inspection session; stopped Play without saving runtime state. Leaving a fresh normal login launch ready for manual review. Headset appearance/keyboard/reach/performance acceptance remains pending; rig/clinical animation and demographic binding are separate work.

Manual review: ProcSense -> Open Application, Play, sign in with demo@procsense.app / ProcSenseDemo!. Expect the permanent MPFB bay, covered feet/fitted gown and working wristband/cuff/probe. Visit corridor and return: expect the same MPFB model and preserved clinical session/attachments. Open Patient Bay directly only for room development; normal launch uses Boot/login. Check Development -> Bypass login in Editor only when you want to skip the form. Confirm the CGTrader model no longer appears anywhere in the app. Backend auth remains TODO.

Uncommitted scope includes the preceding login/wristband batch plus promotion: main/review scenes, shared wristband move, retired CGTrader graphics, MPFB menu/fitting recipes, login defaults/app entry, scoped test updates and documentation. Original downloads/unrelated scene excluded; no staging, commit or push performed. Updated proposed exact subject: `feat(unity): enable login and promote MPFB patient` (no body), author Sannan <sannanshakeel04@gmail.com>. Wait for final manual acceptance and exact-message approval before committing; no further roadmap work authorized.

14 October 2026, 00:02 Asia/Karachi — Sannan explicitly requested "commit and push now" after the exact section 30 subject was proposed. Approved subject `feat(unity): enable login and promote MPFB patient` (no body), author Sannan <sannanshakeel04@gmail.com>, branch sannan47/patient-model, destination origin/sannan47/patient-model. This authorizes the reviewed login/wristband/promotion batch and the CGTrader folder cleanup; no headset acceptance inferred.

Cleanup: removed the unreferenced PatientTrial folder through AssetDatabase, preserving MPFB wristband GUID/references. Verified the absolute original patientModel download path remained inside this workspace with no links/junctions before native PowerShell recursive removal. Automatic approval review rejected combining that deletion with extra script/export deletions. Sannan then explicitly approved the seven named leftovers through the follow-up question: prepare_patient_trial.py, FitPatientDemo.cs, patient_review.fbx, patient_demo.fbx, rejected-patient-demo.fbx, patient_baked.png and PatientTrialImport.cs. Those exact files are removed; the mixed MPFB preview/evidence folder and MPFB assets are preserved. No previously rejected broad deletion was bypassed. Unity confirms PatientTrial folder absent, shared MPFB band present, no retired folder dependencies. Old CGTrader graphics/metadata are deleted from the active project; original untracked downloads no longer remain.

Fresh required task ci passed 27/27, exit 0; post-cleanup EditMode 10/10 passed. Full implementation PlayMode evidence remains 72/72, including actual sign-in into permanent MPFB, controller reading and room return; cleanup touched only unreferenced legacy assets/tools and documentation. Stage only the reviewed Unity scope including asset deletions and wristband move. Exclude original unrelated InitTestScene bf31bc10/meta and ignored personal AGENTS.md. Staged inspection and push result follow. No global Git identity or shared Core/content/package/editor pins changed.
Staged scope inspected: 59 files covering the accepted login/wristband work, permanent MPFB bay, authoring/test/documentation updates and approved CGTrader removals. Shared wristband OBJ/meta are an exact move; other small metadata rename suggestions are Git similarity heuristics, not reused Unity GUIDs. Staged whitespace check passed with Unity blank-value serialization preserved. Unstaged tracked diff is empty; only the original unrelated bf31bc10 scene/meta remain untracked. Required CI 27/27 and post-cleanup EditMode 10/10 passed. Commit identity is the approved-subject commit containing this entry; resolve SHA from Git history. Authorized non-force push destination is origin/sannan47/patient-model; remote was observed at 702c02f, an ancestor of this branch. No main merge or history rewrite authorized/performed.

## 31. Main synchronization and local login reconciliation

Updated (Asia/Karachi): 2026-10-14 19:07.
Authorization: Sannan requested pulling main and approved stashing/restoring local edits, noting main contains newer login work.
Branch: sannan47/patient-model; fast-forwarded add7764 to origin/main 0a25a3f (88 commits), without a merge commit or push.
Preservation: original tracked/untracked local work retained in stash@{0}, named Sannan local work before main update 2026-10-14; stash applied and retained as backup. Restored edits left unstaged. Unity generated additional Core metadata during synchronization; left untouched.
Resolution: DemoLoginView.cs combined upstream AuthService online sign-in/offline-demo routing and SessionUploader completion with local input validation, readiness checks, field errors and completion controls. Other pre-existing Unity changes restored, including ShowOnLaunch=false field initializer in DemoLoginSettings.cs.
Checks: HEAD versus origin/main is 0/0; no unmerged paths. Final git diff --check reports trailing whitespace in restored LoginScreen.prefab and PlayerRig.prefab serialization; these pre-existing asset edits were preserved. Unity CLI could not find a connected Editor, including outside the sandbox; compilation and EditMode/PlayMode login tests remain unverified. No runtime acceptance claimed.
Next review: open Unity and check compilation; run LoginValidationTests, DemoLoginTests and LoginServiceTests; verify malformed input remains on form, offline demo opens bay, and real account uses service. Presentation assets remain unimplemented pending authorization.
No new commit proposed for synchronization; original local work and reconciliation remain uncommitted pending verification/review and exact-message approval.

### Repeat main synchronization (2026-10-14 19:49 Asia/Karachi)
Sannan requested pulling main again. Fetched origin/main and fast-forwarded sannan47/patient-model from 0a25a3f to 93ef6fb (3 commits). Existing local edits and backup stash preserved without another stash or conflict. No asset work, new commit or push. Git equality and absence of unmerged paths checked; upstream runtime/CI changes were not tested in this synchronization task.


### Login bypass diagnosis (2026-10-14 20:00 Asia/Karachi)
User reported login not showing. Live Unity 6000.5.10f1 inspection of this checkout found Boot active, Editor stopped, no play-start override, and ProcSense.Login.BypassInEditor=true. Disabled that local Editor preference. Live verification returned bypass=false, ShowOnLaunch=true, ShouldShow()=true with no eval diagnostics. No scene/prefab/source changes for this fix; only this log and the local preference. Manual review: press Play from current Boot scene; expect login before PatientBay. Visual launch not exercised during this preference-only diagnosis; no commit or push.


### Desktop login focus correction (2026-10-14 20:07 Asia/Karachi)
User reported cursor disappearing when clicking fields. Live inspection found ready/interactable fields, unobstructed email raycast, and XRUIInputModule on desktop. Switched the running session to InputSystemUIInputModule; Sannan confirmed typing works. Persistent BootLoader.cs change now disables the launch XR module after rig initialization and assigns standard desktop UI actions when XRSettings.isDeviceActive is false. Headset path retains XR input. Preserved all earlier local changes.
Initial checks passed 9/9 login PlayMode and 14/14 validation EditMode, but a fresh startup inspection revealed the source change had not been imported. Explicit recompile completed with no errors; affected PlayMode tests rerun below. BootLoader diff whitespace check passed. Hardware XR path not device tested.
Manual review pending for persistent restart: click Email and type; click Password and type, use Show/Hide and Tab, then sign in. Expect stable field focus, masked password, and one workspace on success. No commit/push. Proposed eventual commit subject: fix(unity): preserve desktop login keyboard focus (no body); acceptance and exact-message approval still required. Remaining uncommitted work includes prior login UI/validation, rig/test/settings/readme edits, generated files/Core metadata, and this execution log.
Final loaded-source verification: recompile completed without errors; login PlayMode rerun passed 9/9 in 11.40 s. Fresh Boot launch returned ready=true, module=InputSystemUIInputModule, xrEnabled=false, eventSystems=1. Left corrected login running for final Sannan review.

Login commit approval: Sannan requested 'commit and then lets get started with the environment changes' after the final login handoff, approving fix(unity): preserve desktop login keyboard focus (no body). Branch sannan47/patient-model; author Sannan <sannanshakeel04@gmail.com>. Pre-commit task ci completed 78/78 checks; backend runner may skip Docker-dependent checks when unavailable (runner reports PASS). Live Unity results: 9/9 login PlayMode and 14/14 validation EditMode. Reviewed explicit login-only staged diff, including validation/prefab dependencies and log. Rig serialization, wristband test, URP global settings, temporary build files and generated Core metadata excluded. Environment work remains a separate uncommitted batch. Commit SHA is available from Git history for this approved subject.


## 32. SAN-ROOMS — evaluation presentation finishes

Updated (Asia/Karachi): 2026-10-14 20:27. Authorized by Sannan after committing the reviewed login fix. Login commit: 6a91df8, sannan47/patient-model, author Sannan <sannanshakeel04@gmail.com>, approved subject fix(unity): preserve desktop login keyboard focus, no body. No push.

Scope: existing PatientBay (5 x 4 m), Corridor (4 x 10 m), OT (7 x 7 m). Applied using connected Unity 6000.5.10f1 and AssetDatabase/scene APIs. Did not run prototype rebuild. Added 1.12 m raised wood laminate lower panels, 24 mm projection, ivory cap rails, charcoal skirting, warm painted upper walls, bay/corridor wood-effect flooring, blue-grey OT flooring, closed white panel ceilings with combined grid/fixture meshes and unshadowed fill lights. Bay has a shallow frosted fake window, sill/pelmet and two static gathered sage curtains. Imported ambientCG Wood050, WoodFloor040 and PaintedPlaster017 1K Color/NormalGL JPGs; CC0 provenance recorded under Assets/Textures/Hospital/README.md. Android ASTC 6x6, mipmaps and 4x anisotropy. Wall uses weak normal detail with uniform paint color rather than the source albedo's cracks.

Changed areas: Assets/Scenes/{PatientBay,Corridor,OT}.unity; Assets/Editor/Environment/HospitalFinishes.cs; Assets/Textures/Hospital; Assets/Materials/Hospital; Assets/Models/Environment; associated Unity metadata; this log. Existing rig serialization, wristband test changes, URP global settings, temporary build files and generated Core metadata remain unrelated and uncommitted. task ci also refreshed docs/plans/milestones.md line endings; preserved/excluded from login commit. No SDK/package/content/interface changes.

Preservation check: every original YAML document/fileID retained (PatientBay 339, Corridor 76, OT 68 original IDs; zero removed). Existing wall/floor colliders, equipment, anchor objects and navigation kept. Decorative geometry has no collision except the new ceiling. Layout dimensions remain unchanged.

Visual inspection: Temp/EnvironmentReview/{PatientBay,Corridor,OT}.png. Initial review found overly visible plaster cracks and curtain/sill intersections; corrected material albedo/bump strength and curtain clearance/winding. Corrected bay render inspected. Preview cameras and temporary visibility changes restored in finally; no gameplay scripts/materials changed by preview helper. Final script compilation passed without errors. Full live PlayMode suite running; final results below. Quest performance and headset review unavailable; no device acceptance claimed. Existing prototype furniture and OT table remain outside this surface-finishing request.

Manual review: ProcSense > Open Patient Bay, inspect upper/lower wall boundary, projection/trim, wood floor, enclosed ceiling and left-wall window curtains; Play and walk around the bed, use corridor navigation, inspect corridor and enter OT, then return. Check wall/floor collision, teleport, UI panels and sensor/notes interactions. Expected: coordinated finishes across all spaces and preserved existing behavior. Final visual/implementation acceptance pending Sannan.

Proposed environment commit subject: feat(unity): finish hospital room surfaces and bay window (no body). Do not commit until Sannan accepts this environment batch and approves this exact message. No push authorized.

Final environment verification: compilation completed without errors. All rooms report zero missing scripts/materials; four lower panels each; floor colliders retained; only new decorative collider is the ceiling. Finish geometry adds 2,784 triangles/27 renderers in PatientBay, 516/16 in Corridor and 564/16 in OT. These are asset counts, not measured draw calls/frame time.

Live PlayMode full suite: 88 passed, 1 failed, 1 skipped of 90 (94.77 s). Failure: WristbandRayTests.App_band_reads_from_controller_without_feedback_errors could not find a simulated controller within 3 seconds. Isolated WristbandRayTests rerun passed 2/2 (5.64 s), without changes; retain intermittent full-suite simulator readiness issue. LiveServiceTests skipped because credentials were absent. All RoomTransitionTests, ComfortTests, FoundationTests, patient/sensors and login checks passed in the full run.

Live EditMode suite: 56/58 passed (2.08 s). Both failures are unchanged CoreInUnityTests after main synchronization: Every_golden_run_passes_inside_Unity loads a limited content set and now encounters STK-S1-D; The_skeleton_1_log_hash_is_the_one_pinned_for_the_command_line expects old hash dcec6b... while current golden output is b5a464.... No clinical/Core/test expectations changed to hide failures. Existing CLI golden/engine checks passed in the pre-commit 78/78 task ci run. Status: environment implementation complete, partial verification due to these existing test/dependency issues, awaiting Sannan visual/manual acceptance.

Corrected bay/corridor/OT renders inspected. Opened the normal editable PatientBay workspace with Sys_Core/DevOnly after checking no loaded scenes were dirty. No environment changes staged/committed/pushed. Remaining unrelated changes preserved: PlayerRig.prefab serialization; WristbandRayTests.cs; UniversalRenderPipelineGlobalSettings.asset; unity/.utmp files; generated Core metadata and InitTestScene files; docs/plans/milestones.md checkout/line-ending refresh from CI. No automatic approval block remains: inspection/save proposals were narrowed to preserve dirty scenes and avoid saving unrelated work.

Environment acceptance and exact-message approval (2026-10-14 20:38 Asia/Karachi): Sannan replied 'commit it lad' to the final environment handoff and proposed feat(unity): finish hospital room surfaces and bay window (no body). This authorizes the reviewed environment batch and message; no push. Branch sannan47/patient-model; author Sannan <sannanshakeel04@gmail.com>. Explicit staged scope: 73 files comprising three scenes, HospitalFinishes editor utility, Hospital materials/textures and provenance, generated environment meshes, associated .meta files and this log. Staged audit found zero unexpected paths, zero assets missing metadata, and zero unstaged new scene GUID references. Source/document whitespace checks pass; 353 native Unity serialization trailing-whitespace notices retained rather than hand-editing serialized assets. Prior Unity verification and known test/dependency limitations above remain applicable; pre-commit task ci rerun pending below. Unrelated files remain unstaged. Resolve the commit SHA from Git history by the approved subject; no recursive self-hash is embedded.

Environment pre-commit result: required task ci rerun completed 78/78 checks, exit 0, before this approved commit. Unity verification limitations remain explicitly recorded above. Staged diff reviewed; only the approved 73 environment/log files included. Commit uses Sannan <sannanshakeel04@gmail.com> on sannan47/patient-model and the exact approved one-line subject feat(unity): finish hospital room surfaces and bay window, no body. No push authorized or performed.

## 33. SAN-LIGHTING — staged environment polish, lighting first

Updated (Asia/Karachi): 2026-10-14 21:18. Branch: sannan47/environment-polish. Authorization: Sannan requested implementing the suggested improvements one at a time, verifying each with him, committing the accepted step, then moving on. This batch is lighting only. Doors/signage, headwall/furniture, window dressing and additional architecture remain gated behind this lighting review/commit.

Reference: p0-environment-rooms-s03 / Stage 6 lighting/probes; this is a presentation lighting batch, not completion of the full EXP-ENV-2/device/occlusion optimisation stage. Existing 10/15/20 texels/m headset experiments and worst-OT batching/device measurements remain pending. Current artistic preview uses 24 texels/m and 1K atlas maximum; no measured Quest-performance acceptance claimed.

Changes: HospitalLighting Editor utility and room-specific lighting settings; baked scene lighting data/lightmaps and 128-pixel reflection captures; secondary UVs on the existing generated finish meshes via Unity Unwrapping, with GUIDs/geometry retained; light probes (PatientBay 60, Corridor 108, OT 108); warm bay ceiling light and cool fake-window daylight, cooler corridor/OT balance; native scene RenderSettings and light bake associations. Original shadowless directional/point fill objects retained, disabled. Each room has one low-intensity real-time overhead key for dynamic shadows; main soft shadows enabled in URP_Quest, additional-light shadow settings unchanged. Overhead ceiling/grid/fixtures excluded from this key's shadow casting to prevent floor silhouettes. Ceiling/window emission flags/keywords corrected. Native lighting data and EXR files assigned Git LFS attributes in unity/.gitattributes, overriding the root YAML rule for native LightingData.asset; installed LFS filter already existed, no global configuration change.

Runtime: RoomLoader activates its initial visible room after load, so Boot does not retain ambient/probe ownership. BakedProbeRefresh subscribes once per subsystem registration to probe retetrahedralization/active-room changes; requests asynchronous recalculation. Clinical state, Core interfaces, inputs and room layout unchanged.

Review iterations: corrected pinned-URP read-only soft-shadow property access; first bay bake too dim, restored emission and increased baked illumination; corridor initially too orange, cooled its emitters. Route test exposed initial Boot-active probe-null issue, fixed initial active room. Corrected test to use GetSharedLightProbesForScene(...).countSelf rather than deprecated combined count; added return-to-bay sampled-illumination equivalence. First broader test run exposed fixture leakage from the new test into BridgeTests, so added UnityTearDown unloading the owned fixture scenes/session before subsequent tests. Final rerun results below.

Preservation: all original scene YAML documents retained from c73b4ff (PatientBay 458, Corridor 155, OT 144; zero removed). No existing .meta GUID changes. Existing unrelated rig/wristband-test/URP-global-settings/build/Core-metadata/milestone line-ending edits remain excluded. No staging, commit or push in this review batch. No changes outside unity.

Verification so far: all three CPU bakes completed; PatientBay 3 lightmaps, Corridor 2, OT 2. Saved per-scene probe sets verified 60/108/108. Baked reflection capture present for each. Lighting round-trip test passed (1/1, 6.46 s) before test-cleanup refinement. Source/document whitespace checks pass; final compilation no errors. Full PlayMode rerun in progress. Before/after images: Temp/LightingReview/{PatientBay,Corridor,OT}-{before,after}.png; final captures must reflect ceiling-shadow correction. Headset comfort, memory and frame timing pending hardware.

Manual review pending: open PatientBay workspace, Play; inspect daylight around window, illuminated ceiling panels, wall/corner shading, moving patient/equipment and floor contact shadows. Navigate corridor -> OT -> corridor -> bay; verify brightness/readability and no dark props, missing textures, projected ceiling-grid silhouettes or loss of lighting on return. Tell the assistant whether brightness/warmth should change, or explicitly accept this lighting step.

Proposed commit subject: feat(unity): bake hospital room lighting and reflections (no body). Wait for Sannan's manual acceptance and exact-message approval; then task ci, reviewed explicit staging and commit. Do not start the next item before the accepted lighting commit.

Final verification: compilation completed without errors. Full live PlayMode rerun after proper new-test teardown: 90 passed, 0 failed, 1 skipped of 91 (93.06 s); the skipped real-service test requires credentials. This includes the new additive-lighting round trip, return-illumination equivalence, all Bridge/RoomTransition/Comfort/Foundation/patient/login/sensor tests. EditMode rerun: 56/58 passed (1.71 s); same unchanged CoreInUnityTests limited case-set and stale pinned-hash failures recorded in section 32. No clinical/Core expectations changed.

Final visual-only adjustment angled the weak overhead key (bay 70/60 degrees, corridor/OT 75/-30 degrees) to reveal furniture shadows beyond object footprints; no baked light or runtime logic changed. Compiled again, refreshed room reflection captures and rendered all three final views. Inspected final bay/corridor/OT images: lit diffusers/window, room shading and removal of ceiling-grid/fixture floor silhouettes confirmed. Opened normal editable PatientBay workspace with Sys_Core/DevOnly for Sannan review. Assets, native lighting data, runtime/Editor helper, regression test, URP soft-shadow setting, Unity-local Git LFS attributes and log remain unstaged/uncommitted; unrelated baseline files preserved. Status: awaiting Sannan lighting/brightness/warmth review and exact-message approval. Do not proceed to doors/signage until this step is accepted and committed.

Lighting acceptance and commit approval (2026-10-14 21:35 Asia/Karachi): Sannan requested 'commit that' after the lighting handoff, accepting this batch and its exact proposed one-line subject feat(unity): bake hospital room lighting and reflections (no body). Required task ci completed 78/78 checks, exit 0; backend runner retains its documented Docker skip behavior. Branch sannan47/environment-polish; author Sannan <sannanshakeel04@gmail.com>. Explicit lighting-only staging and diff review; unrelated baseline files excluded. No push. Next authorized batch: remove prototype room comfort displays and improve doors/signage, with new manual review before its commit. Quest 3 frame timing/memory remains pending actual hardware; no guarantee inferred from Editor tests. Resolve this commit SHA from Git history by its approved subject.

## 34. SAN-DOORS — door dressing and room display cleanup

Updated (Asia/Karachi): 2026-10-14 21:44. Lighting committed as ebd2952, branch sannan47/environment-polish, Sannan <sannanshakeel04@gmail.com>, approved subject feat(unity): bake hospital room lighting and reflections (no body); task ci 78/78. No push. Sannan authorized removal of the repeated sit/stand/snap-angle screens and continuation to the next suggested doors/signage step. New batch remains uncommitted until manual acceptance and exact-message approval.

In progress: HospitalDoors connected-Editor authoring utility dresses the six existing RoomDoor points across PatientBay/Corridor/OT with ivory frames, sage/theatre-teal leaves, frosted observation insets, stainless handles/hinges/kick plates and teal signs. RoomDoor preserves destinations, restriction flags and fade/navigation logic, replacing floating prototype panels with smaller mounted Enter controls. Restricted door signs have no interaction canvas. ComfortPanel display objects removed only after checking no extra authored content; ComfortSetup stops regenerating them. Rig ComfortControls, teleport, snap-turn and Editor F6/bracket shortcuts retained. Headset settings surface is currently absent after removal; default 45-degree turn/standing offset remains. README updated. Existing walls/colliders remain; leaves are visual dressing, not opening walk-through portals or patient routes. No Core, clinical data or package/interface changes. Rebaking each room and refreshing reflections for new static geometry. Final compilation/tests/visual review pending below.

Verification/handoff (2026-10-14 23:30 Asia/Karachi): all three revised CPU bakes and reflection captures completed (3/2/2 lightmaps; 60/108/108 probes). Full live PlayMode suite: 91 passed, zero failed, one credential-dependent service test skipped of 92 (106.42 s). New mounted-button round trip passed; restricted routes/solid diagnostic doors, all comfort/teleport, startup/login, clinical view and lighting tests passed. EditMode: 56/58 (2.04 s), same existing CoreInUnityTests missing STK-S1-D content and stale golden log hash failures. Final preview-helper-only adjustment permits Sys_Core/DevOnly while guarding multiple clinical rooms; recompiled without errors and verified live captures in all three rooms. Source/document whitespace checks pass. Scene document preservation: only the three documents for the one requested ComfortPanel GameObject/component/Transform removed per room; every other original document retained. Existing .meta files unchanged. Door audit: zero missing scripts/materials or new colliders; door geometry adds 192/768/192 triangles, with 19/76/19 geometry/text renderers (asset counts, not measured draw calls or frame time). Fixed mesh renderers marked for static batching/GI; no new runtime lights or shaders. Preview files: Temp/DoorReview/{PatientBay,Corridor,OT}.png, including actual mounted controls in Play. Normal editable PatientBay workspace restored, Play stopped and temporary login override cleared.

Changed areas for review: HospitalDoors Editor utility and metadata; Hospital/Doors materials; ComfortSetup and RoomDoor; three scenes and their rebaked lighting/reflection files; ComfortTests/RoomTransitionTests; README and this log. Nothing from this batch staged or committed. Unrelated rig serialization, WristbandRayTests, URP global settings, .utmp/generated Core metadata, InitTestScene and milestone line-ending refresh remain preserved/uncommitted; retained stash remains. No push.

Sannan manual review: ProcSense > Open Patient Bay, Play, approach the CORRIDOR door on the right and click Enter; inspect the corridor B01/T01/D01/D02 signs and hardware, enter OT, return through corridor to bay. Expected: six dressed doors, accessible doors have a smaller mounted Enter/Ready control, CT/MRI and ultrasound have NO TRAINEE ACCESS without an Enter button and remain physically blocked. No MOVEMENT screens in any room. Check WASD/teleport and F6 seated calibration; brightness remains stable when returning. Headset menu access for seated calibration/snap-angle selection and device performance remain pending as disclosed. Accept or request visual fixes before this batch's commit; next suggested item after accepted commit is headwall/bedside furnishing.

Proposed exact commit subject: feat(unity): dress hospital doors and remove room comfort panels (no body). Await implementation acceptance and approval of this message; then run task ci and explicitly review/stage only this batch. No further roadmap work before this review/commit.

Door batch acceptance and exact-message/push approval (2026-10-14 23:39 Asia/Karachi): Sannan requested 'commit and push' after the handoff, approving feat(unity): dress hospital doors and remove room comfort panels (no body). Branch sannan47/environment-polish, author Sannan <sannanshakeel04@gmail.com>. Destination origin/sannan47/environment-polish. Reviewed new door-only scope and prior live 91/92 PlayMode result (one credential skip); unrelated files remain excluded. Required pre-commit task ci running; result recorded before commit. Investigate/fix seven reported Unity test failures only after this environment commit/push. No further environment dressing authorized in this turn.
Door pre-commit task ci completed 78/78 checks, exit 0. Staged diff inspected: 45 approved door/material/scene/rebake/UI/test/README/log files, no unrelated rig/wristband/global-settings/Core/generated files. Native Unity YAML whitespace preserved; source/document checks pass. Commit uses the exact approved subject and identity on sannan47/environment-polish; push authorized only to origin/sannan47/environment-polish.
