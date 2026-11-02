# ChatGPT research: animations_sitting

Question: I need free, redistributable (CC0 or CC-BY) humanoid animation clips for Unity 6 URP: seated idle on a bench/chair (adult, elderly, child), seated phone use, seated reading paperwork, standing talk gestures, walking, cough into elbow. Name exact packs with licence and whether they include sitting clips (Quaternius Universal Animation Library, CMU mocap, Kenney, OpenGameArt, Rokoko, Mixamo, Unity Asset Store). Also recommend the best Unity approach for natural seating on real furniture: seat anchors, Animator Rig / Animation Rigging package Two Bone IK + Multi-Aim constraints, OnAnimatorIK, retargeting MakeHuman/MPFB GameEngine-rig characters to the Humanoid avatar, and a reusable grip system for clipboards/phones/papers (grip points, hand IK, finger curl poses). Be honest about what you cannot verify.

**For a public repository, I would start with Quaternius’s Universal Animation Library, then create the missing hospital-specific actions yourself.** I would **not** put downloaded Mixamo, Rokoko, or standard Unity Asset Store animation files into that repository just because they are free to download.

One important limitation: **I cannot inspect the current download archives or licence files here.** Below, I distinguish assets whose identity/licensing I know from clip contents I cannot verify. I cannot honestly promise a verified CC0/CC-BY pack containing all six requested actions.

## 1. Sources and redistribution suitability

| Source / exact asset | Author and URL | Licence / public-repository suitability | Does it include sitting clips? |
|---|---|---|---|
| **Universal Animation Library** | **Quaternius** — https://quaternius.com/packs/universalanimationlibrary.html | Published as **CC0**. This is the strongest starting point for redistributable humanoid animation source files. Preserve a copy of the licence supplied with the download. | **I cannot verify the current clip manifest.** Do not assume that it contains seated phone use, paperwork reading, or cough-into-elbow. Check the actual archive before treating even basic sitting as covered. |
| **Animated Characters 1** | **Kenney** — https://kenney.nl/assets/animated-characters-1 | **CC0**. Suitable for a public repository. This is a character asset pack with animations, rather than a dedicated hospital motion library. | **I cannot verify sitting clips.** Also inspect the skeleton and animation import: “animated character” does not guarantee straightforward Unity Humanoid retargeting. |
| **CMU Graphics Lab Motion Capture Database** | **Carnegie Mellon University Graphics Lab** — http://mocap.cs.cmu.edu/ | **Custom terms, not a blanket CC0/CC-BY licence.** I would not approve it for your stated policy without reviewing the applicable terms. A third-party BVH/FBX conversion does not automatically replace the original terms with CC0. | The database contains sitting-related motion, but **I cannot give you verified subject/trial IDs here**, or confirm the particular seated actions you need. |
| **OpenGameArt** — hosting platform, not one pack | Individual contributors — https://opengameart.org/ | Licences vary per submission, sometimes with multiple options and dependency credits. **I do not have a verified exact 3D humanoid pack for these actions to recommend.** | Not verified. I would rather leave this gap than invent a pack title or clip list. |
| **Rokoko Motion Library / free motion offerings** | **Rokoko and individual motion creators** — https://www.rokoko.com/ | **Do not assume CC0/CC-BY.** I cannot verify a specific current free pack with permission to redistribute its raw clips publicly. Not on my approved list for this repository. | No verified qualifying pack or sitting inventory to name. |
| **Mixamo animation library** | **Adobe / Mixamo** — https://www.mixamo.com/ | Adobe’s custom terms, **not CC0/CC-BY**. Use in a finished game is different from redistribution of downloadable animation assets. **Exclude raw Mixamo animations from the public repository.** | It offers sitting-related motions, but I am not supplying unverified exact clip names. Its licence already disqualifies it for your requirement. |
| **Unity Asset Store** — marketplace, not one pack | Individual publishers — https://assetstore.unity.com/ | Standard Asset Store licensing is **not CC0/CC-BY** and generally does not allow redistributing source assets as a public asset collection. Free price does not change that. Only consider a specifically identified asset with an explicit qualifying alternative licence. | Varies. I have no verified CC0/CC-BY pack there covering your list. |

**Practical licence rule:** keep the downloaded licence alongside the source files. For CC-BY, also retain the exact licence version, author, source URL, and modification notice. Do not label an entire third-party collection CC0 merely because one included file is CC0.

### Coverage of your requested actions

Here is what I would actually plan production around:

| Required action | Safe production plan |
|---|---|
| **Walking** | Inspect Quaternius first. Verify the actual walking clips, root-motion behaviour, and Humanoid import before committing. |
| **Standing talk gestures** | Inspect Quaternius for suitable gestures; supplement with original additive upper-body gestures. |
| **Seated idle — adult** | Verify whether a suitable base exists; otherwise author one. Furniture fitting still needs procedural adjustment. |
| **Seated idle — elderly and child** | Treat these as **separate authored variants**, not simply scaled adult animation. |
| **Seated phone use** | Author an upper-body action over a seated base, with prop grips and hand IK. No verified qualifying ready-made pack identified here. |
| **Seated reading paperwork** | Same approach, with authored gaze and page/clipboard interaction. |
| **Cough into elbow** | Author a specific action. I cannot identify a verified CC0/CC-BY download for it. Do not substitute “cough into hand” in hospital training. Have the action reviewed by the clinical training team. |

Animations you author from scratch can be released under CC0 or CC-BY. **Editing or retargeting a restricted downloaded animation does not remove its original restrictions.**

## 2. Recommended Unity seating architecture

For Unity 6, I recommend:

**Humanoid Animator → seating/root alignment → Animation Rigging constraints → carefully budgeted secondary motion.**

URP does not change animation compatibility. Desktop and Quest can share the animation architecture, with different constraint and character LOD budgets.

### A. Put seat anchors on the furniture

Create a `SeatDefinition` component with transforms such as:

```text
Chair
└── SeatDefinition
    ├── SeatFrame
    ├── Approach
    ├── FootLeft
    ├── FootRight
    ├── OptionalHandLeft
    ├── OptionalHandRight
    └── LookTarget
```

Define exactly what `SeatFrame` means—for example, seat-surface centre with forward facing out of the chair. Store **per-avatar seated calibration**, rather than expecting that transform to equal every character’s hip position.

A seating sequence should:

1. Reserve the seat and move to `Approach`.
2. Stop or coordinate NavMeshAgent movement and root-motion ownership.
3. Align heading and play the sit transition.
4. Blend root alignment toward the calibrated seated position.
5. Blend in foot, hand, and gaze constraints.
6. Reverse that process when standing.

**Do not snap the hips to an anchor while leaving the feet fixed.** That produces stretched legs, sliding, and seat penetration.

### B. Fit the pelvis first; use IK for residual error

Natural seating depends on:

- Pelvis height and fore/aft position.
- Thigh clearance against the seat.
- Backrest contact.
- Foot support.
- Appropriate knee spacing and posture.

Use a seated base pose, root/pelvis adjustment, and modest pose corrections first. Then use leg IK to place the feet.

For a child on an adult bench, dangling feet may be correct. For an elderly patient, a supported posture may be appropriate, but avoid treating age as a single exaggerated slouch.

**IK cannot repair an unsuitable chair/avatar combination by itself.** You still need collision/clearance checks and occasionally different seating poses.

## 3. Animation Rigging versus `OnAnimatorIK`

### Preferred: Animation Rigging package

Use the Unity **Animation Rigging** package compatible with your Unity 6 editor:

- **Two Bone IK Constraint:** arms and legs.
- **Multi-Aim Constraint:** restrained head/gaze aiming.
- **Multi-Parent or related transform constraints:** where appropriate for prop attachment.
- **Rig layers and weights:** blend from animation to procedural control.

Give elbows and knees explicit hints. Keep targets within reachable distances and test extreme body proportions.

For gaze, avoid driving the whole head directly toward the target at full weight. Use clamped, smoothed targets and small movements; exaggerated continuous tracking looks unnatural.

### Alternative: `OnAnimatorIK`

`OnAnimatorIK` is useful when you want a smaller, Humanoid-only solution:

- Enable **IK Pass** on the relevant Animator layer.
- Set hand/foot IK goals and weights in the callback.
- Use `Animator.SetLookAt...` where appropriate.
- Use body adjustments cautiously.

It is not a general arbitrary-bone constraint system.

**Do not let `OnAnimatorIK` and Animation Rigging independently control the same limb.** Pick one owner, or explicitly establish and test evaluation order. Otherwise you can get last-writer overrides and jitter.

For your reusable prop interactions, I would choose **Animation Rigging as the primary system** and use `OnAnimatorIK` only for a specific, justified need.

### Quest 2 considerations

Start with a modest rig and profile on-device:

- Reduce constraint count for background NPCs.
- Disable grip/gaze rigs when not needed.
- Avoid updating distant interaction targets every frame.
- Use simpler animation-only seated LODs.
- Check Animator culling settings so a visible NPC does not resume in a broken pose.

Do not assume a large desktop crowd rig will fit Quest’s CPU budget.

## 4. MakeHuman / MPFB → Unity Humanoid

**Do not assume “GameEngine rig” guarantees automatic Unity Humanoid compatibility.** Exporter versions, bone names, rest poses, and hierarchy details matter.

Recommended pipeline:

1. Export a clean skeleton with consistent scale and transforms.
2. In Unity, select **Rig → Humanoid → Create From This Model**.
3. Open **Configure** and inspect every required mapping.
4. Resolve the reference pose, using **Enforce T-Pose** where appropriate.
5. Verify hips, spine, shoulders, hands, feet, and finger chains.
6. Test a known animation before building your interaction system.
7. Tune per-avatar seating, grip, and reach profiles.

Each differently proportioned character should have its **own valid Avatar**. Reuse animation clips through Humanoid retargeting; do not indiscriminately copy another model’s Avatar definition onto a different skeleton.

Humanoid retargeting preserves general motion, **not exact chair contact or precise prop alignment**. That is why the seat and grip corrections are necessary.

Also review the character generator, clothing, and accessory licences separately. Animation licensing does not cover the models.

## 5. Reusable phone, clipboard, and paper grips

Build this as data, not one-off animation code.

### Prop-side data

Each prop should define:

- Primary and optional secondary grip transforms.
- Supported grip types.
- Optional gaze target.
- Optional interaction targets, such as a phone screen or page corner.

### Character-side data

Each character should define:

- Hand/palm calibration offsets.
- Finger pose presets.
- Reach limits and elbow-hint preferences.
- Optional left/right variants.

**A wrist bone is not the same thing as a palm contact point.** Correct for the offset between the hand bone and the intended grip surface.

Conceptually:

```text
desired hand world transform
    = prop grip world transform
      × inverse(hand-local contact transform)
```

### Avoid the attachment feedback loop

Choose one of these modes:

**Hand-led prop**
- Animation drives the primary hand.
- Prop follows a socket on that hand.
- Secondary hand uses IK to reach the prop.

**Prop-led interaction**
- A stable interaction anchor drives the prop.
- Both hands use IK to reach its grip points.

Do not parent a prop to a hand and then use that same prop to drive that same hand’s IK. That creates circular ownership and can cause unstable results.

### Finger curls

Arm IK will not produce a believable grasp by itself. Author reusable finger poses such as:

- Phone hold.
- Clipboard edge hold.
- Paper pinch.
- Relaxed seated hand.

Blend these with an additive finger layer or a dedicated finger-pose system. Use a consistent reference pose and avoid having two systems overwrite the same finger bones.

For paperwork, also account for the page itself: rigid sheets often look wrong, so a lightly curved mesh and a few authored paper poses can be more useful than elaborate hand simulation.

## Recommended project decision

1. **Approve Quaternius Universal Animation Library as the first CC0 source**, after checking the downloaded licence and clip inventory.
2. **Evaluate Kenney Animated Characters 1 as a secondary CC0 resource**, not a promised sitting library.
3. **Do not approve CMU, Mixamo, Rokoko, or standard Asset Store downloads for raw public redistribution under your CC0/CC-BY policy.**
4. Author the clinically specific seated actions and elbow cough yourself.
5. Build one shared seating/grip framework, then give adult, elderly, and child characters individual calibration and pose profiles.

That is a more dependable route than expecting one free motion pack to solve furniture contact, different body proportions, and clinical correctness simultaneously.
