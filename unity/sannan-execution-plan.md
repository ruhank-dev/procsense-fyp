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
| SAN-SCENES | Persistent scene lifecycle/input | Planned | Stage 1; reuse existing components |
| SAN-DIAG | Records/diagnostics skeleton | Blocked | Investigation/result/transport runtime contracts and case integration required |
| SAN-OT | Greybox theatre/WHO skeleton | Blocked | Readiness/checklist/consent/transport runtime contracts required |
| SAN-BLENDER | Procedural pipeline and pilot assets | Planned | Stage 4; Blender/tool setup and pilot reference review |
| SAN-ASSETS | Full equipment roster | Blocked | Validated pilot/import pipeline and accepted quality bar |
| SAN-ROOMS | Final shells/layout | Blocked | Supervisor assets/access not verified; keep greybox usable |
| SAN-CHAR | Characters and nurse sequence | Planned | Prototype can use stand-ins; human sources/animation approval required |
| SAN-UI | Complete clinical interactions/UI | Planned | Incremental against real contracts; unsupported services recorded individually |
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
| SAN-B01 | Quest availability/access and current device setup unverified | Establish during Stage 0; headset-dependent acceptance remains pending |
| SAN-B02 | Supervisor shell files not located in this inspection | Inventory during authorized room work; greybox does not block logic |
| SAN-B03 | Detailed case files not driving normal sessions yet | Obtain/integrate real runtime contract from Core/content work; do not count files as playable cases |
| SAN-B04 | Investigation, treatment, scoring and complete checklist services missing from inspected default pipeline | Record exact owner handoff per adapter; fixtures do not close this blocker |
| SAN-B05 | 13 open equipment dimension rows in research review pack | Preserve explicit uncertainty; source and approve before verified dimensional claims |
| SAN-B06 | Historical plans/status notes inconsistent | Use actual files and evidence; resolve applicable discrepancies in future reviewed changes |
| SAN-B07 | Live Editor Pipeline not present in inspected manifest | Check again and set up only when implementation is authorized |
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
