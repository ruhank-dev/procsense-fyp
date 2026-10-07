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
| The developer ↔ Editor | Editor scripts + screenshots; Unity MCP if EXP-VR-4 passes | Manual only | The developer can't see the Editor | — |

## Experiments
| ID | Question | Method | Success metric |
|---|---|---|---|
| EXP-VR-1 | Frame budget on Quest 2 with rooms + Core ticking | OVR Metrics Tool + Unity profiler | Stable at target refresh; budgets (draw calls, tris, texture memory) written down |
| EXP-VR-2 | Controllers vs hand tracking for band reading + tube labelling | 3+ testers both ways | Success rate + preference |
| EXP-VR-3 | Teleport vs smooth comfort | Comfort questionnaire | No sickness reports |
| EXP-VR-4 | Unity MCP bridge (UNVERIFIED tool) usable for the developer? | Try 5 real tasks | ≥ 4/5 → adopt |

## Exit
Quest build boots, Core runs on device (EXP-CORE-1 hash match), budgets recorded.

## Who does what
Developer: scripts, bridge, build automation, tests, editor tooling. Team: install Unity, Editor-only settings, scene layout, headset testing.

## Sources
Implementation Guide B-S6, B-S7, B-S11; Master Plan §25.
