# Pulse Physiology Engine spike (p1-pulse-physiology-s01)

Status, 17 October 2026: **desk research done; the PC build and the Quest test are not done.** Nothing in the engine depends on Pulse; `LitePhysio` stays the physiology model.

## What was checked (17 October 2026)

| Question | Finding | Source |
|---|---|---|
| Licence | Apache License 2.0. Pulse is Kitware's fork of BioGears 6.1.1 made to be fully Apache-2.0 (the Code Synthesis exception was removed). The NOTICE file asks to keep the copyright notice (2018-2025 Kitware and contributors). | [NOTICE](https://gitlab.kitware.com/physiology/engine/-/raw/stable/NOTICE), [introduction](https://kitware.com/introducing-the-pulse-physiology-engine-open-source-computational-models-for-human-medical-simulation) |
| Platforms | C++ engine, Windows, macOS and Linux; the documentation also lists ARM and AARCH64. | [pulse.kitware.com](https://pulse.kitware.com/) |
| Bindings | Java, C#, C and Python; the Python API is built when the CMake option `Pulse_PYTHON_API` is on (the default). | [Pulse Python support](https://www.kitware.com/pulse-python-support/) |
| Prebuilt wheel | None found: a build from source with CMake is the route. | search on 17 October 2026 |
| Unity | Kitware publishes a C# API; an Unreal Engine marketplace plugin exists. No ready Unity asset was found, so the Quest test needs the C# bindings built for Android ARM64. | search |

## What is not known and decides the approach

1. Does it build for Android ARM64 (the Quest) and what does it cost per frame? The plan's target is 3 ms a frame. Unknown until the headset test (Sannan's side).
2. Can it run on the server instead and stream its results into the session log? That keeps replay deterministic (the logged values are replayed), but needs the build on the server image.

## Steps still to do

1. On a PC: install CMake and a C++ toolchain, clone `https://gitlab.kitware.com/physiology/engine`, build with `-DPulse_PYTHON_API=ON`, run one scenario (dehydration) and read heart rate, blood pressure and SpO2 over time. Expected effort: an afternoon and several GB of disk.
2. On the Quest: a blank Unity 6 project with the C# bindings, ARM64 IL2CPP, measure.
3. Record the answer here, then decide whether the adapter (s02) is worth building (DT-207).
