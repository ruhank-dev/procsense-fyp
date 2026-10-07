# p0-characters

**Tier:** P0 · **Builds on:** p0-vr-platform, p0-physiology-events-behaviour (behaviour states) · **Feeds:** voice-audio (lip sync), scenarios

## Goal
Patients for all P0 cases (age/sex/build variety) and the staff physically present (nurse, triage nurse, surgeon, anaesthetist, porter,
theatre staff), rigged and animated, with **visible clinical signs** (pallor, jaundice, sweating, rash, sedation, pain)
driven by Core state.

## Who gets an avatar (environment principle)
Only people **physically present in category-A rooms** get a 3D character: patient, ward/ED nurse, anaesthetist, surgeon, scrub nurse,
theatre/ODP staff, porter (escort + sample carrier). Roles the trainee only talks to by phone or reads from (lab caller, radiologist,
pharmacist, specialty teams) are **voice / text / report only**.

## Movement
Patients walk to diagnostic doors and back when stable (NavMesh + walk animations), sit in a wheelchair, or lie on a trolley pushed by
a porter with an escort when unstable (p0-diagnostic-transitions).

## Scope
- **In:** base human meshes (from an approved candidate set), outfits (gowns, scrubs, uniforms), clinical-sign materials +
  blend shapes, animation set (idle, talk, pain, breathing rate, gestures, walking), behaviour-state → animation mapping, Quest optimisation.
- **Out:** voices (voice-audio), dialogue (p1-ai-language).

## Decisions
| Decision | Chosen | Alternatives | Why | Revisit if |
|---|---|---|---|---|
| Base humans | Open set approved by team (candidates: MPFB/MakeHuman, Rocketbox — in free-model-candidates.md) | Procedural humans in `bpy`; hand-sculpt | Realistic humans are not practical to script from scratch | Team rejects all candidates → MPFB-generated in Blender + our edits |
| Clinical signs | Shader/material parameters + blend shapes driven by Core | Separate meshes per condition | One character, many states | Medical reviewer says not recognisable |
| Outfits / small accessories | Our Blender scripts (gowns, wristbands reuse equipment assets) | Bundled outfits | Consistency | — |
| Animation | Mixamo clips (if approved) + Animator controller written by Claude | Motion capture | Free, fast | — |
| Breathing / pain cues | Procedural (animation layer speed from RR, pain blend shape) | Fixed clips | Matches live vitals | — |

## Experiments
| ID | Question | Method | Success metric |
|---|---|---|---|
| EXP-CHR-1 | Are jaundice/pallor/rash recognisable in VR lighting? | Medical reviewers identify the sign | ≥ 80% correct |
| EXP-CHR-2 | Characters on screen within budget (patient + 3 staff) | Quest profile | Within EXP-VR-1 budget |
| EXP-CHR-3 | Does visible breathing rate match the monitor RR? | Count breaths vs value | Match |

## Exit
All P0 cases have a patient; all P0 NPC roles have a character; signs recognised; budget met.

## Who does what
Claude: sign shaders/blend-shape scripts, outfit scripts, animator logic, behaviour mapping, licence checks. Team: approve base set, visual judgement, headset checks.

## Sources
Walkthrough Part 16 (player view), NPC roster (Part 1.4); Implementation Guide B-S2, B-S3.
