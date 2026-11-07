# p3-11-assets-environment — Missing characters, equipment and supporting 3D assets (AST)

Status: Planned (P3, 7 November 2026). Nothing in this plan is implemented. The approved hospital environment, lighting, materials and architecture are **not** changed by this plan; new objects are added into it.

## 1. Objective and final user-facing behaviour

The patient on the bed looks like the patient in the case (a 42-year-old woman for VAR-S1-A, a 70-year-old man for another case). The staff the player talks to are present and dressed for their role. Every item the scenario needs is in the room and recognisable. The main menu has its own calm space. Nothing already approved is removed or redesigned.

## 2. Current implementation status

- Environment: PatientBay, Corridor (with reception and waiting area), OT with baked sun, PBR materials (Poly Haven, ambientCG CC0), 4,061 manifest-verified files (`tools/setup/asset_manifest.json`, `docs/research/asset_sources_and_licenses.md`). **Verified (desktop).**
- Characters: 17 Rocketbox FBX (MIT) incl. the bay patient `Medical_Male_04`; Quaternius UAL animation clips (CC0). **Partial** (patient sex/age mismatch, missing roles).
- Equipment: monitor, IV pole/pump, patient folder, wristband, allergy band, sensors, bed, OT equipment rebuilt; thermometer placement on Sannan's branch. **Partial** (open Blender steps p0-blender-equipment-s06 to s15).
- Open register items: VO-03 (OT details), VO-04 (character detail), VO-05/VO-11 (exterior), VO-13 (remaining Rocketbox replacements), DT-267 (specimen container). **Planned.**

## 3. Existing code, assets, scenes and systems to reuse

`RocketboxSetup.cs`, `RocketboxPeople.cs`, `fetch_rocketbox.py` (pinned commit), `PatientBayPatient.cs` (bone-based placement on the mattress), `PatientMotion`, Sannan's patient bindings (sensors, band, nurse wrist target, speech anchor), `unity/Tools/*` Blender scripts, `tools/setup/build_manifest.py`, the asset licence documents.

## 4. Missing functionality

**Gap register (first version):**

| Asset | Purpose | Where | Have it? | Source option | Format / prep | Interaction | Quest notes |
|---|---|---|---|---|---|---|---|
| Female adult patient (25-55) | Matches 31 female cases | Bay bed | No | Rocketbox (MIT) female avatar in plain clothing or scrubs, same method as the male patient; https://github.com/microsoft/Microsoft-Rocketbox | FBX, Humanoid, ARKit blendshapes kept | Bindings as male patient | Same budget as current patient |
| Older female and older male patient (60-75) | Age match | Bay bed | No | Rocketbox adult avatars (older faces) | As above | As above | As above |
| Young female patient (16-24) | S2 young cases (incl. 16) | ED bay | No | Rocketbox young adult | As above | As above | As above |
| Ward/ED nurse (final) | Wristband, preparation | Bay | Greybox (branch) | Rocketbox medical avatar | Humanoid | Nurse sequence (branch) | Shared materials |
| Doctor (registrar/surgeon) | Escalation, decision | Bay/nurses' station | No (OT doctors are scenery) | Rocketbox medical avatar | Humanoid | Addressable | — |
| Anaesthetist | Review, fasting | Bay/OT entrance | No | Rocketbox medical avatar | Humanoid | Addressable | — |
| Receptionist | Transport/admin | Reception desk | No (VO-13) | Rocketbox business avatar | Humanoid | Addressable | — |
| Porter | Transport | Corridor | M2 stand-in | Rocketbox avatar | Humanoid | Push bed | — |
| Glucometer, ECG machine and leads | Bedside tests | Bay | No | Own Blender script (project rule) | FBX, ≤ 3k tris each | Use on patient | ASTC 1024 |
| Sample tubes, labels, specimen bag | Sampling | Bay tray | Container modelled, not imported (DT-267) | Own Blender script | Transparent URP material | Grab, label read | Few transparent pixels |
| Oxygen mask and nasal cannula | Oxygen | Bay wall outlet | No | Own Blender script | Skinned strap optional | Fit on patient | Simple mesh |
| Consent form clipboard and pen | Consent signing | Bay table | No | Own Blender script | Paper texture from content | Grab, sign | — |
| Gown, jewellery tray, stockings (props) | Ward preparation | Bay | No | Own Blender script | Static props | Optional physical step | — |
| Theatre reception handover point | Submission | Corridor/OT entrance | No | Existing assets rearranged + sign | — | Submit presentation | — |
| Menu backdrop | Main menu | MainMenu scene | No | Reuse reception assets, own small bake | — | — | Small scene |
| UI 3D elements | Lightbulb, wrist button, board icon, submit button | UI | No | Own meshes or sprites | Single glass material | UI | Shared atlas |
| ED bay variant (S2) | Emergency setting | Bay variant | No (p0-environment-rooms-s05) | Same room, set dressing switch | — | — | No new bake if objects are dynamic or the variant is baked once |

## 5. Architecture and integrations

- **Patient avatar profiles:** `PatientAvatarProfile` assets (id, prefab, bone map, mattress offsets, bindings) selected by the case's avatar profile (P3-SCN-10) when the session starts. Swap keeps one patient root so sensors, band, nurse target, speech face and transport find it as today.
- **Staff placement:** characters are scene objects with `Interactable` and a `CharacterPresenter`; their presence per room is fixed; per-case variation is not needed.
- **Licences:** every new asset gets a manifest source record (`build_manifest.py`), a row in `docs/research/asset_sources_and_licenses.md` and, where required, `docs/research/licence_provenance_register.md`.

## 6. Implementation tasks

| ID | Task | Owner | Depends on | Done when | Status |
|---|---|---|---|---|---|
| P3-AST-01 | Maintain the gap register above as a checked table (asset, owner, source, licence, status) | Sannan | — | Table current; every row has a licence entry or "own script" | Planned |
| P3-AST-02 | Patient avatars: female adult, older female, older male, young female (plus the existing male adult), clothed, Humanoid, blendshapes kept, placed on the mattress like the male patient | Sannan | P3-SCN-10 | Five profiles render in the bay, no clipping, chest moves | Planned |
| P3-AST-03 | Runtime patient swap by profile at session start, preserving all bindings (sensors, band, nurse wrist target, speech face, transport visibility) | Sannan | P3-AST-02, P3-SES-01 | Existing nurse, transport and sensor PlayMode tests pass for every profile | Planned |
| P3-AST-04 | Staff set: final nurse, doctor, anaesthetist, receptionist, porter (Rocketbox), placed with interaction anchors | Sannan | P3-ANI-02 | Each addressable and animated | Planned |
| P3-AST-05 | Bedside equipment of the gap register through the open Blender equipment steps | Sannan | p0-blender-equipment-s07, p0-blender-equipment-s08, p0-blender-equipment-s09 | Each item imported with prefab, collider and licence/provenance | Planned |
| P3-AST-06 | Menu backdrop scene (with P3-MNU-02) | Sannan | P3-MNU-02 | Owner approves capture | Planned |
| P3-AST-07 | UI 3D elements and icon meshes (lightbulb, board icon, wrist button, submit button) | Moiz | P3-UIX-03 | In the kit | Planned |
| P3-AST-08 | ED bay variant for S2 cases (set dressing switch, signage) | Sannan | p0-environment-rooms-s05 | S2 sessions show the ED variant | Planned |
| P3-AST-09 | Manifest, licence documents and restore script updated for every new asset; fresh-clone restore re-run | Sannan | P3-AST-02, P3-AST-04, P3-AST-05 | `restore_environment.ps1` verifies all files on a fresh clone | Planned |
| P3-AST-10 | Quest budgets per new asset (triangles, materials, texture size ASTC, LOD) recorded and within P3-PRF limits | Sannan | P3-PRF-04 | Budget table filled with measured numbers | Planned |

## 7. Dependencies and execution order

Profiles and swap first (they affect every case); staff next (needed by interactions); equipment through the existing steps; variant and backdrop in parallel; licences and budgets close the plan.

## 8. Frontend components

None (assets only); UI meshes support P3-UIX.

## 9. Backend and API changes

None.

## 10. AI and voice integration

Each character gets a voice name for addressee targeting and a voice for TTS/pre-rendered lines (content/voice roles).

## 11. Unity objects, prefabs, animations and assets

As listed; prefabs under `Assets/Prefabs/Characters/` and `Assets/Prefabs/Equipment/`; avatars under `Assets/Characters/Rocketbox/` (Git LFS for FBX and large textures, as today).

## 12. Performance

New characters share materials where possible, Android texture overrides (ASTC 6x6 at 1024 as most current textures), GPU skinning, LOD for crowd avatars; the bay keeps at most the patient plus two staff animated at once.

## 13. Error handling and edge cases

Profile missing for a case (fallback to the nearest profile and a log line; P3-SCN-08 substitutes the case in the examination); bindings fail (session refuses to start, message); LFS file missing on a clone (restore script reports it).

## 14. Testing

PlayMode per profile (placement, bindings, breathing), manifest check, licence document check, captures for owner approval.

## 15. Acceptance criteria

1. Every playable case shows a patient of the right sex and age band.
2. Every addressable role exists in the rooms.
3. Fresh-clone restore verifies all new files.

## 16. Definition of Done

All AST tasks Done; owner approval of captures; Quest budgets measured.
