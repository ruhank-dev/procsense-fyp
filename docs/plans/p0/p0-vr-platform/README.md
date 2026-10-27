# p0-vr-platform

**Tier:** P0 · **Builds on:** p0-core-engine, p0-foundation · **Feeds:** environment-rooms, characters, vr-interaction-ui, voice-audio, scenarios

## Goal
A Unity 6 project running on Meta Quest 2/3 with an XR rig, comfortable movement between rooms, and a clean bridge to Core
(Unity sends actions, reads state — nothing else).

## Scope
- **In:** Unity project + packages, XR rig, locomotion, scene loading between rooms, SimulationHost (owns the Core Session),
  ActionEmitters, StateViews, SceneDirector, Core as a local package (ADR-0001), Android/Quest build pipeline, headset-free test setup.
- **Out:** rooms/art, specific interactions and UI.

## Decisions
| Decision | Chosen | Alternatives | Why | Revisit if |
|---|---|---|---|---|
| Engine | Unity 6 LTS, URP | Unreal; Godot | Original plan; Quest support; C# matches Core | — |
| XR stack | OpenXR + XR Interaction Toolkit (+ Meta XR Core for Quest features) | Meta XR All-in-One / Interaction SDK only | Vendor-neutral, works with XR Device Simulator | Hand tracking needed → Meta Interaction SDK |
| Input | Controllers; hand tracking as option | Hands only | Reliable grabbing of small items (tubes, bands) | EXP-VR-2 favours hands |
| Locomotion | Teleport + snap turn; fade between rooms | Smooth; room-scale only | Comfort for non-gamers | Testers prefer smooth |
| Rendering | Forward URP, baked lighting, single-pass instanced | Realtime lights | Quest GPU budget | — |
| Device floor | Quest 2 | Quest 3 only | Wider availability | EXP-VR-1: Quest 2 can't hold frame rate |
| The developer ↔ Editor | Unity CLI + Pipeline package (ADR-0030); editor scripts for anything repeatable | Manual only; community unity-mcp | The developer can't see the Editor | EXP-VR-4: 5 of 5 tasks done |

## Experiments
| ID | Question | Method | Success metric | Status |
|---|---|---|---|---|
| EXP-VR-1 | Frame budget on Quest 2 with rooms + Core ticking | OVR Metrics Tool + Unity profiler | Stable at target refresh; budgets (draw calls, tris, texture memory) written down | planned |
| EXP-VR-2 | Controllers vs hand tracking for band reading + tube labelling | 3+ testers both ways | Success rate + preference | planned |
| EXP-VR-3 | Teleport vs smooth comfort | Comfort questionnaire | No sickness reports | planned |
| EXP-VR-4 | Unity MCP bridge (UNVERIFIED tool) usable for the developer? | Try 5 real tasks | ≥ 4/5 → adopt | done: 5/5 with Unity's own CLI and Pipeline package, adopted (ADR-0030; results below) |

### EXP-VR-4 results (27 and 28 October 2026)

Unity CLI 1.0.0-beta.14 with `com.unity.pipeline` 0.8.0-exp.1 (a local server in the open Editor), used for the real work of the Unity steps of 27 and 28 October; the community `unity-mcp` server was not needed and is not installed.

| # | Task | How | Result |
|---|---|---|---|
| 1 | Pick up edited scripts and check they compile | `unity command eval` (AssetDatabase refresh, then a type lookup); compile errors read from the Editor log | Done, every step. A refresh sometimes timed out while the Editor was busy; a retry after a minute worked |
| 2 | Run the EditMode and PlayMode tests | `unity command run_tests` and `test_status` | Done, dozens of runs (full suites of 146 and 166 tests). The status call can report the previous run for the first seconds; wait before reading it |
| 3 | Edit a prefab without the Blender export | `eval`: load the patient folder prefab, add FolderController, save | Done; the diff was the one component (13 lines) |
| 4 | Check what a panel looks like | PlayMode tests drawing each panel's canvas to a picture (artifacts/unity) | Done; it found two real layout bugs (keypad letters too wide for their keys, overlapping debrief panels) |
| 5 | Restart the Editor after it closed | `unity open` | Done; the Editor came back ready in about five minutes |

Failures met: a modal dialog in the Editor (the API Updater after a compile error) blocked every request for about 30 minutes until a person closed it, and requests time out while the Editor is throttled in the background. Neither changed a file. Rule kept: a clean git tree before any bridge edit.

## Exit
Quest build boots, Core runs on device (EXP-CORE-1 hash match), budgets recorded.

## Who does what
Developer: scripts, bridge, build automation, tests, editor tooling. Team: install Unity, Editor-only settings, scene layout, headset testing.

## Sources
Implementation Guide B-S6, B-S7, B-S11; Master Plan §25.
