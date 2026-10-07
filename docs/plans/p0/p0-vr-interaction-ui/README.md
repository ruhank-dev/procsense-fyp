# p0-vr-interaction-ui

**Tier:** P0 · **Builds on:** p0-vr-platform, p0-blender-equipment (greybox stand-ins OK first), p0-simulation-workflow · **Feeds:** scenarios

## Goal
Everything the trainee touches or reads: grab/inspect/use equipment (wired via the `GRAB_/SOCKET_/ATTACH_/SCREEN_` points from
the Blender assets), equipment state machines, tablet (orders, notes, dialogue choices), patient monitor with traces, results
viewer with flags + lab comments + critical-result call, checklist boards, debrief + timeline screen.

**Per-item spec:** [equipment-interaction-spec.md](equipment-interaction-spec.md) — for every major item, what the trainee does and how the system/visuals respond.
OT equipment is **operational**, not decorative: lights switch on, screens are interactive, monitors show live values, doors/drawers open, states change.

## Scope
- **In:** interaction framework, equipment state machines (attach, power, fault, empty cylinder…), wristband rotate-to-read,
  tube labelling, in-VR UI framework, monitor, results panel, WHO checklist board, dialogue menu, debrief UI, accessibility (text size, subtitles).
- **Out:** speech input (p1-voice-input), art.

## Decisions
| Decision | Chosen | Alternatives | Why | Revisit if |
|---|---|---|---|---|
| Main UI surface | Hand-held tablet + world-space boards | Floating menus; wrist menu | Familiar to clinicians | EXP-UI-1 says it's slow |
| Dialogue input | Choice menu (voice added in P1, menu stays as fallback) | Voice only | Reliable, offline | — |
| UI tech | uGUI world-space | UI Toolkit | Mature in XR | UI Toolkit world-space matures |
| Wiring | Auto-wire components from empty names in the imported assets (editor script) | Manual wiring | Reproducible; the developer can do it | — |
| Monitor traces | Line renderer from Core samples (+ dataset-derived waveform templates) | Video loops | Reacts to real state | Performance |

## Experiments
| ID | Question | Method | Success metric |
|---|---|---|---|
| EXP-UI-1 | New users complete Step 1 (ID + allergy) unaided? | 5 testers, think-aloud | ≥ 4/5 |
| EXP-UI-2 | Minimum readable text size on Quest 2 | Test sizes at arm's length | Size fixed in UI guidelines |
| EXP-UI-3 | Monitor realism | Medical reviewer | Accepted or fix list |

## Exit
Every P0 step completable with controllers; usability tasks pass; UI never writes state directly.

## Who does what
Developer: scripts, state machines, binding, auto-wiring, simulator tests. Team: layout tweaks in Editor, headset readability, UX judgement.

## Sources
Walkthrough Part 16, Part C (VR reasoning cards), Part E; Implementation Guide B-S9, B-S10, B-S5.
