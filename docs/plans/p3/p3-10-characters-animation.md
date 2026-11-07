# p3-10-characters-animation — Characters that react to the game, and UI motion (ANI)

Status: Planned (P3, 7 November 2026). Nothing in this plan is implemented.

## 1. Objective and final user-facing behaviour

People in the hospital feel present: they breathe, blink, glance at the player when addressed, turn and look while listening, move lips while speaking, nod when they agree, and react to what the player does (the patient winces on pressing the right upper abdomen when the case says so, offers an arm for the cuff, signs the consent form). Staff walk in, hand things over and leave. Equipment moves as it should (cuff inflates, bed back rises, trolley wheels turn). Every panel opens and closes smoothly, buttons respond to hover and press, ticks draw themselves, results count up, and scene changes fade comfortably. All of it follows real game events, not timers.

## 2. Current implementation status

- Patient: Rocketbox `Medical_Male_04` supine with `PatientMotion` (breathing 14/min, blinks, gaze drift, foot shifts, overrides), `PatientSpeechFace` (ARKit blendshapes), `GownSecondaryMotion`, `PatientIdleView` (cosmetic). **Verified (Editor).**
- Corridor/OT people: Rocketbox avatars with Quaternius UAL clips via Humanoid, `StaffIdleView` roles, `SeatedPerson`, `PropGrip` (untested). **Partial.**
- Nurse: greybox walk-in, clipboard check, wrist reach, nod, exit (Sannan's branch). **Not integrated.**
- Transport: `PatientTransportView`, `TransportActor` (marked M2 geometry). **Partial.**
- No event-driven speaking/listening, no look-at-player on address, no lip sync (p0-voice-audio-s04), no examination responses, no UI motion system. **Missing.**

## 3. Existing code, assets, scenes and systems to reuse

`PatientMotion`, `PatientSpeechFace`, `StaffIdleView`, `SeatedPerson`, `PropGrip`, `GownSecondaryMotion`, `CurtainCloth`, `TransportActor`, Quaternius UAL clips (CC0) in `Assets/Characters/Animations`, Rocketbox Humanoid avatars and ARKit blendshapes, `LocomotionFade`.

## 4. Missing functionality

Animation inventory; a standard controller for staff and patient; look-at; event-driven states; lip sync; patient reaction set; staff action set; equipment animations; UI motion library; scene transitions; budgets and culling.

## 5. Architecture and integrations

- **Controller standard (Humanoid):** base layer (idle, walk, sit, lie), upper-body override layer (gestures: nod, shake, point, offer arm, hold clipboard, write), additive layer (breathing), face via blendshapes (blink, visemes, expressions). Driven by a `CharacterPresenter` per character that listens to `SimFeedback` and `VoicePlayer` events: addressed → look at player; speaking → talk state and visemes; listening → attentive idle; accepted action targeted at the character → acknowledge gesture.
- **Look-at:** a LateUpdate head/eye aim with angle limits and smoothing (the method `PatientMotion` already uses), cheaper than an Animation Rigging job on Quest.
- **Lip sync:** amplitude-driven jaw and a small viseme set from the audio clip's energy bands (p0-voice-audio-s04 chooses the method); no runtime ML.
- **UI motion:** a small in-house tween utility (no paid package): scale+fade open/close (180 ms ease-out), hover lift (60 ms), press dip, checkmark stroke draw, number count-up, toast slide.

## 6. Implementation tasks

| ID | Task | Owner | Depends on | Done when | Status |
|---|---|---|---|---|---|
| P3-ANI-01 | Animation inventory: every required clip and state per character type and equipment item, source (UAL CC0, Rocketbox, Mixamo is not used for licence reasons unless the owner agrees, own keyframes) and gaps | Sannan | — | Inventory table in this folder's matrix page | Planned |
| P3-ANI-02 | Standard Humanoid controller (base, upper-body, additive breathing, face) and `CharacterPresenter` | Sannan | P3-ANI-01 | Applied to patient, nurse and one staff member | Planned |
| P3-ANI-03 | Look-at player on address and during conversation (limits 60° yaw, 30° pitch, eyes first then head) | Sannan | P3-ANI-02 | No neck flips at extreme angles (test sweep) | Planned |
| P3-ANI-04 | Event states: addressed, speaking, listening, acknowledge, refuse, busy; driven by `SimFeedback` and voice playback | Sannan | P3-SES-10, P3-ANI-02 | Every accepted panel action targeted at a person gives a visible reaction | Planned |
| P3-ANI-05 | Lip sync on Rocketbox blendshapes for patient and staff lines | Sannan | p0-voice-audio-s04, P3-ANI-02 | Visemes visible within 80 ms of audio | Planned |
| P3-ANI-06 | Patient reactions: examination flinch (only when the engine's finding says so), pain expressions by score, offer arm, nod for consent, sign form, sit up for examination | Sannan | P3-ANI-02, P3-INT-02 | Reaction never reveals a hidden finding before the examination action | Planned |
| P3-ANI-07 | Staff actions: walk in/out on NavMesh, hand item, write on clipboard, push bed (porter), escort | Sannan | P3-ANI-02, P3-AST-04 | Used by nurse, porter and doctor flows | Planned |
| P3-ANI-08 | Equipment: cuff inflate/deflate, monitor screen states, bed back rise, trolley wheels, curtain pull, door swing (exists) | Sannan | P3-INT-07 | Animations follow engine state | Planned |
| P3-ANI-09 | UI motion utility and presets (open/close, hover, press, tick draw, count-up, toast) | Moiz | P3-UIX-01 | Used by every panel of the kit | Planned |
| P3-ANI-10 | Scene transitions: fade to dark, loading card, results dim; comfort-safe (no camera motion) | Moiz | P3-ANI-09 | No frame of camera-driven motion in transitions | Planned |
| P3-ANI-11 | Animation budgets: Animator culling (cull completely off-screen), reduced update rate for distant people, one LOD for corridor crowds | Sannan | P3-PRF-04 | Measured animation CPU time recorded in DC-13 results | Planned |
| P3-ANI-12 | Tests: presenter state machine (EditMode), look-at limits, reaction gating by engine findings, UI tween timings | Sannan | P3-ANI-04, P3-ANI-06 | Green in CI | Planned |

## 7. Dependencies and execution order

Inventory → controller → look-at and event states → reactions and staff actions → lip sync when p0-voice-audio-s04 lands; UI motion (Moiz) in parallel; budgets after the performance decision.

## 8. Frontend components

UI motion presets used by all panels; transition overlays.

## 9. Backend and API changes

None.

## 10. AI and voice integration

Speaking state and visemes follow the voice clip or TTS audio, whichever plays; the dialogue route decides the words, the engine decides the facts, the animation only presents.

## 11. Unity objects, prefabs, animations and assets

Animator controllers per character type, avatar masks, UAL clip subsets, own keyframed clips for clinical gestures (flinch, offer arm, sign), blendshape maps per avatar, equipment animation clips.

## 12. Performance

GPU skinning on Android (P3-PRF-10), Animator culling, max 4 bones per vertex, avatar LOD in the corridor; blendshape counts kept as today (patient 27) and measured (DT-269).

## 13. Error handling and edge cases

Character addressed while walking (stops, then turns); two staff addressed at once; interrupted speech; animation for a character in another room (skipped); missing clip (fallback to idle, warning in log).

## 14. Testing

EditMode for presenters; PlayMode for reactions; Editor captures and short clips for owner review; headset comfort for transitions.

## 15. Acceptance criteria

1. Every contextual action aimed at a person gives a visible reaction within 300 ms.
2. Speaking characters move lips; listening characters look at the player.
3. Transitions cause no reported discomfort in DC-14.

## 16. Definition of Done

All ANI tasks Done, owner review of clips, budgets measured on the headset.
