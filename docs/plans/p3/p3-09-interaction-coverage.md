# p3-09-interaction-coverage — Every clinical task has a usable interaction (INT)

Status: Planned (P3, 7 November 2026). Nothing in this plan is implemented.

## 1. Objective and final user-facing behaviour

Every task the engine scores in S1 and S2 can be done in a way the player understands: by touching or using the real object (where it adds learning), by the contextual menu, or by voice, and each gives visible and audible feedback. Nothing in the rooms looks usable but silently does nothing: an object that is not part of the simulation says so when pointed at, or is not highlighted.

## 2. Current implementation status

Engine verbs (about 100) are all reachable from the tablet (144 entries). Physical interactions exist for SpO2 probe and BP cuff (attach/remove, `SensorAttachment`), wristband reading, the notes folder, doors and transport; on Sannan's branch also the thermometer (`measure_temperature` by ear contact), the records workstation (EMR, results, phone, report tray), the ward checklist and WHO boards and the nurse wristband visit. Staff the trainee can address in the scene: only the greybox nurse (branch). **Partial.**

## 3. Existing code, assets, scenes and systems to reuse

`ActionEmitter`, `XRSelectEmitter`, `UIButtonEmitter`, `SensorAttachment`, `SensorPlacementGuide`, `NotesFolderInteraction`, `ContactActionEmitter` (branch), `TabletFallbackRegistry`, equipment prefabs in `Assets/Prefabs/Equipment/`, the open P0 steps for bedside, OT and nurse interactions.

## 4. Missing functionality

Coverage matrix and its check; addressable doctor, anaesthetist, receptionist and porter; physical paths for ECG, glucose, blood sampling and labelling, IV access request, oxygen, consent form signing, gown/jewellery preparation, OT handover point; non-functional object labelling.

## 5. Architecture and integrations

Each interaction is a row: **input** (physical gesture, contextual action, voice intent) → **request** (gateway, P3-SES-03) → **engine validation and state** → **character/object behaviour** (P3-ANI) → **feedback** (visual, audio, P3-SND) → **training tick or examination score** (engine). The engine and the catalogue already define the verbs; this plan adds the presence in the world.

**Coverage matrix (first version; P3-INT-01 generates the maintained one):**

| Area | Engine verbs | Physical today | Contextual (P3-CTX) | Voice today | Missing |
|---|---|---|---|---|---|
| Introduction and identity | introduce_self, ask_identity, read_name_to_patient, read_wristband, open_notes, confirm_identity, report_mismatch, request_wristband | wristband read, notes folder; nurse wristband swap (branch) | patient, nurse | yes (practice) | none beyond merge |
| Allergies, history, reason | ask_allergies, ask_allergy_reaction, ask ×12, summarise, ask_reason_for_admission, ask_responsible_adult | — (conversation) | patient | yes | voice in examination (D3) |
| Observations | attach/detach_sensor, measure_nibp, measure_temperature, count_rr, assess_acvpu, ask_pain_score, acknowledge_alarm | SpO2, cuff; thermometer (branch) | patient, monitor | partly | count_rr gesture (watch chest + confirm), ACVPU prompt |
| Examination | ask_exam_permission, offer_chaperone, examine, murphy_test, airway_check | — | patient | yes | hand-on-abdomen gesture with patient flinch (P3-ANI-06) |
| Bedside tests | perform_bedside_test (glucose, urine pregnancy, ECG) | — | patient, equipment | yes | glucometer, ECG machine props and use (p0-vr-interaction-ui-s06) |
| Investigations and samples | order_investigation, take_sample, send_samples, read_specimen_label | — | doctor/nurse, records | yes (orders) | sample tubes, labels, specimen bag (DT-267) |
| Results and calls | view_result, interpret_result, interpret_imaging, acknowledge_result, answer_phone, confirm_patient_on_call, read_back | records workstation (branch) | records | yes | headset usability (DC) |
| Treatment | request_fluids, request_oxygen, request_iv_access, hold_medicine, document_treatment, check_treatment_timing, request_medication | — | nurse, doctor | yes (orders) | oxygen mask/cannula and IV pole states visible |
| Consent | consent_discussion ×7, teach_back, check_capacity, sign_consent, use_emergency_route, request_interpreter | — | patient | yes | consent form clipboard and pen signing |
| Risk scores | assess_risk, assess_vte, compute_score | — | anaesthetist, records | yes | none (form on panel) |
| Ward checklist | tick_ward_item ×13, prepare ×8, sign_ward_checklist | ward panel (branch) | nurse, ward board | partly | gown, jewellery, stockings props as optional physical steps |
| Escalation and decision | escalate, say_concern, declare_outcome, postpone, redirect, tell_anaesthetist, request_anaesthetic_review | — | doctor, anaesthetist | yes | addressable doctor and anaesthetist characters |
| Transport and handover | request_transport, transfer_to_theatre, finish_handover, submit_patient (new, P3-EXM-05) | transport visuals | receptionist/porter, patient | yes | porter character, theatre reception handover point |
| Theatre (training extension, D6) | check_station ×12, checklist_item ×24, complete_time_out, declare_or_ready | WHO board (branch) | theatre team | partly | OT stations (p0-vr-interaction-ui-s07) |
| Session | wait, time_skip, pause, resume, request_hint (new) | — | wrist menu | yes | — |

## 6. Implementation tasks

| ID | Task | Owner | Depends on | Done when | Status |
|---|---|---|---|---|---|
| P3-INT-01 | Coverage tool: generate `docs/plans/p3/interaction-matrix.md` from the verb registry, the catalogue, the emitters in scenes and the voice catalogue; `--check` in CI | Ruhan | P3-CTX-01 | Generated page matches the scenes; CI row | Planned |
| P3-INT-02 | Patient: examination gesture with flinch, count-RR interaction, pain score prompt, preparation props (gown, jewellery) as optional physical steps | Sannan | P3-CTX-02, p0-vr-interaction-ui-s06 | Each sends the same request as the menu | Planned |
| P3-INT-03 | Nurse: replace greybox with the final character, keep the wristband sequence; add hand-over of items and the preparation tasks | Sannan | P3-SES-01, p0-vr-interaction-ui-s12 | Nurse actions reachable physically and by panel | Planned |
| P3-INT-04 | Doctor (registrar/surgeon) as an addressable character at the bay or nurses' station for escalation, review and decisions | Sannan | P3-AST-04, P3-CTX-02 | SBAR escalation reachable in both modes | Planned |
| P3-INT-05 | Anaesthetist as an addressable character (review, fasting breach, airway concerns) | Sannan | P3-AST-04, P3-CTX-02 | Anaesthetic review reachable in both modes | Planned |
| P3-INT-06 | Receptionist and porter: transport request and patient collection presented by characters | Sannan | P3-AST-04, P3-CTX-02 | Transport visible with the porter | Planned |
| P3-INT-07 | Bedside equipment: glucometer, ECG, sample tubes with labels, specimen bag, oxygen mask/cannula, IV pole states, consent clipboard and pen | Sannan | p0-blender-equipment-s07, p0-blender-equipment-s08, p0-blender-equipment-s09 | Each item usable or clearly a prop | Planned |
| P3-INT-08 | Theatre reception handover point in the corridor/OT entrance where `submit_patient` is presented (patient leaves with the porter) | Sannan | P3-EXM-04, p0-vr-interaction-ui-s07 | Submission has a visible handover | Planned |
| P3-INT-09 | Non-functional objects: remove highlight or show "Not used in this simulation" on point | Sannan | P3-CTX-03 | No silent highlighted object in the three rooms (PlayMode sweep) | Planned |
| P3-INT-10 | Coverage check: every verb reachable from a contextual entity or the wrist menu; every goal of S1 and S2 completable by menu only and by voice only (voice per D3) | Ruhan | P3-INT-01, P3-VOX-04 | Check green in CI | Planned |

## 7. Dependencies and execution order

Matrix tool first; addressable staff need the character set (P3-AST-04); equipment needs the open Blender equipment steps; the coverage check closes the plan.

## 8. Frontend components

Contextual panels (P3-CTX), item tooltips, "not used" label.

## 9. Backend and API changes

None.

## 10. AI and voice integration

Each character's voice name and examples go into the catalogue for addressee targeting (P3-VOX-05); staff replies use the existing approved lines and the dialogue route.

## 11. Unity objects, prefabs, animations and assets

Characters (P3-AST-04), equipment (P3-AST-05), interaction anchors, animation triggers (P3-ANI-06, P3-ANI-07).

## 12. Performance

Characters added to the bay must stay inside the character budget of P3-PRF (skinned meshes, bones, materials); idle characters update at reduced rate.

## 13. Error handling and edge cases

Physical action while the menu action is pending; dropping an item; using equipment on the wrong body part; staff member busy (escorting) when addressed; patient away.

## 14. Testing

Matrix check; PlayMode per interaction (one request per use); menu-only and voice-only golden playthroughs (P3-QA-02).

## 15. Acceptance criteria

1. The generated matrix shows no required verb without an interaction.
2. S1 and S2 complete by menu only; by voice only for every step voice is allowed for.
3. No highlighted object does nothing.

## 16. Definition of Done

All INT tasks Done, matrix current in CI, headset interaction checks in DC-14.
