# Animation inventory (P3-ANI-01)

Every clip or state each character type and equipment item needs, where it comes from, and what is still missing. Written 15 November 2026 from the code and assets in `unity/Assets`; the plan page [p3-10-characters-animation](p3-10-characters-animation.md) says what each task builds. Licence rule: Quaternius UAL (CC0) and Rocketbox (own licence, already in the project) are used; Mixamo is not used unless the owner agrees.

## 1. Sources we have

| Source | What it gives | Licence | Where |
|---|---|---|---|
| Quaternius UAL1 Standard (Humanoid, retargets onto Rocketbox) | Idle_Loop, Idle_Talking_Loop, Walk_Loop, Walk_Formal_Loop, Jog_Fwd_Loop, Sprint_Loop, Sitting_Enter, Sitting_Idle_Loop, Sitting_Talking_Loop, Sitting_Exit, Crouch_Idle_Loop, Crouch_Fwd_Loop, Push_Loop, PickUp_Table, Interact, Fixing_Kneeling, Hit_Chest, Hit_Head, Death01, A_TPose (plus combat, spell, swim, dance and driving clips that this project does not use) | CC0 | `Characters/Animations/UAL1_Standard.fbx` |
| Rocketbox avatars | Skeleton, 52 ARKit blend shapes plus visemes on the patient model, clothes | Rocketbox licence | `Characters/Rocketbox` |
| Procedural (code on the avatar's own bones) | Breathing rate and depth, blinks, gaze drift and darting, head drift, foot shifts, mood pose (head yaw, nod, tilt), guarding hand (two-bone solve), look-at (eyes first, then head, 60 degrees yaw and 30 pitch), skin signs | ours | `PatientMotion`, `PatientPose`, `PatientExpression`, `PatientSkin`, `LookAtLimits` |
| Own keyframes (to author) | Clinical gestures UAL does not have (see section 3) | ours | not made yet |

## 2. Patient (Rocketbox Medical_Male_04, supine)

| State or action | Source | Status |
|---|---|---|
| Breathing (rate follows the measured respiratory rate, depth follows the mood) | procedural | done |
| Blink, gaze drift, head drift, foot shift | procedural | done |
| Mood faces (pain, anxious, distressed, confused, drowsy, nauseated, uncooperative) | blend shapes | done (tuned on stills, not live) |
| Mood pose: head, eyes, restlessness | procedural | done |
| Guarding hand on the lower right abdomen | procedural two-bone solve | done (position not checked live) |
| Skin signs: pallor, flush, jaundice, cyanosis, sweat | material property block | done; rash mask not done; not tested on darker skin tones; Core does not report these signs yet |
| Look at the player | procedural | component hook done; nothing sets the target yet |
| Speaking (jaw and visemes) | `PatientSpeechFace` | exists |
| Examination flinch and wince | procedural head, face and hand | done: plays only when the engine's reaction effect says so |
| Offer arm for the cuff | procedural two-bone solve (reuse the guarding hand solve) | done: held out while the cuff is attached; not seen live |
| Nod for consent | procedural head nod | done |
| Sign the form | own keyframes or procedural hand and arm | done: the hand comes over the chest for 3 s after an accepted consent; not seen live |
| Sit up for examination | own keyframes (lying to sitting and back) | missing: the neck pin and bed contact in `PatientMotion` fight a trunk lift, so it needs its own pass |

## 3. Gestures UAL does not cover (the gaps)

Nod, shake head, offer arm, hand over an item, write on a clipboard, point, sit up from lying, flinch. Plan: head gestures and arm offers as procedural motion on the Humanoid bones (cheap on Quest, no clip to import); writing, signing, sitting up and handing over as short own-keyframe clips. Each needs owner review before it goes in a scene.

## 4. Staff

Roles from task P3-AST-04 in `p3-task-index.md` (nurse, doctor, anaesthetist, receptionist, porter). All use Rocketbox avatars and UAL clips through the Humanoid avatar.

| Role | Idle and talk | Move | Task actions | Gaps |
|---|---|---|---|---|
| Nurse | Idle_Loop, Idle_Talking_Loop | Walk_Formal_Loop | clipboard check, wrist reach, nod, exit (greybox `NurseVisitActor` today) | write on clipboard, hand item, real avatar instead of the greybox |
| Doctor (registrar or surgeon) | Idle_Loop, Idle_Talking_Loop | Walk_Formal_Loop | none yet | point, nod, shake head, write |
| Anaesthetist | Idle_Loop, Idle_Talking_Loop | Walk_Formal_Loop | none yet | nod, shake head |
| Receptionist | Sitting_Idle_Loop, Sitting_Talking_Loop (`SeatedPerson`) | Sitting_Enter, Sitting_Exit | none yet | write, hand item |
| Porter | Idle_Loop, Idle_Talking_Loop | Walk_Loop, Push_Loop (bed push) | collect patient (`TransportActor`) | hand item |
| Corridor people | Idle_Loop, Sitting_Idle_Loop | Walk_Loop | none | none planned |

Event states for every staff member (P3-ANI-04): addressed (turn and look), speaking (talk clip and visemes), listening (attentive idle), acknowledge (nod), refuse (head shake), busy. Mapped to the clips and procedural gestures above.

## 4a. Look-at

One standard look-at for every character: eyes first (up to 25 degrees yaw and 15 pitch), then the head for the rest, never past 60 degrees yaw and 30 pitch (`LookAtLimits`, tested). The patient has the hook; the same maths serves standing staff once they have a head target.

## 5. Equipment (P3-ANI-08)

| Item | Needed | Source | Status |
|---|---|---|---|
| BP cuff | inflate and deflate | own keyframes or scale driven by the monitor | missing |
| Monitor | screen states, alarm | screen rendering | exists (`MonitorView`) |
| Bed | back rise (head up) | needs a Core verb and a rigged bed | blocked (see the handoff open list) |
| Trolley | wheels turn | procedural from movement | missing |
| Curtain | pull | `CurtainCloth` | exists |
| Doors | swing | door scripts | exists |

## 6. Open questions for the owner

1. Are procedural head and arm gestures acceptable for nod, shake head and offer arm, or should they be keyframed?
2. Who authors the own-keyframe clips (writing, signing, sitting up, handing over)? Moiz has Blender equipment work; characters are Sannan's.
3. Which Core observable will carry the patient's skin signs (pallor, flush, jaundice, cyanosis, sweat)? Until it exists the skin component is driven only by hand.
