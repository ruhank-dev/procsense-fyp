# Pulse Physiology Engine (p1-pulse-physiology-s01 to s03)

Status, 18 October 2026 (second session): **the PC build works, four scenarios were run and recorded, the adapter and the comparison with LitePhysio are built and tested. The Quest test is not done (Sannan, check DC-05).** `LitePhysio` stays the engine of every session unless a session names a recorded Pulse run.

## What was checked (17 and 18 October 2026)

| Question | Finding | Source |
|---|---|---|
| Licence | Apache License 2.0. Pulse is Kitware's fork of BioGears 6.1.1 made fully Apache-2.0. The NOTICE file asks to keep the copyright notice (2018-2025 Kitware and contributors). | [NOTICE](https://gitlab.kitware.com/physiology/engine/-/raw/stable/NOTICE) |
| Platforms | C++ engine, Windows, macOS and Linux; ARM and AARCH64 are listed in the documentation. | [pulse.kitware.com](https://pulse.kitware.com/) |
| Bindings | Java, C#, C and Python (a CMake option each). | [Pulse Python support](https://www.kitware.com/pulse-python-support/) |
| Prebuilt wheel | None. A search on 18 October 2026 found no Pulse wheel on PyPI (`pulse-engine` there is an unrelated package) and the official site offers prebuilt binaries only for its Qt explorer; the engine is built from source. | PyPI, pulse.kitware.com |

## The PC build (done 18 October 2026)

Inspected before building (18 October 2026): NVIDIA GeForce GTX 1660 SUPER, 6144 MiB, driver 610.88 (the hardware baseline for every measurement; the laptop was not used); AMD Ryzen 5 2600; 534 GB disk with 123 GB free before and 110 GB after the build and the models downloaded in this session; no C++ compiler, CMake or Java present at the start. Pulse itself runs on the processor; the card is not used by it.

Machine: the development desktop (Windows 11, a Ryzen 5 2600 with 6 cores and 12 threads, shared with other work, 123 GB free after the owner freed space). No Visual Studio was installed, so the free open-source toolchain was used: CMake 4.4.4 and Ninja (winget, user scope), MinGW-w64 GCC 16.2 (WinLibs, winget), a portable Temurin JDK 17 (the data generation needs Java), and a Python 3.13 virtual environment with `numpy pandas matplotlib openpyxl formulas dataframe_image scipy plottable pip`. Sources: `git clone --depth 1 --branch stable https://gitlab.kitware.com/physiology/engine.git` (commit 5e29e5b, Pulse 4.4.0), kept outside the repository.

```
cmake -G Ninja -DCMAKE_BUILD_TYPE=Release -DPulse_JAVA_API=ON -DPulse_PYTHON_API=ON -DPulse_CSHARP_API=OFF -DPython_EXECUTABLE=<venv python> <engine source>
cmake --build .
```

Things that went wrong and what fixed them (so the next build is not a guess):

1. **The compiler's runtime DLLs must come first on `PATH`.** With Git's and Anaconda's older copies earlier, the freshly built `protoc.exe` died with `0xC0000139` and the schema bindings were generated empty; the generator then wrote its "nothing changed" stamp, so later builds skipped it. Fix: put the WinLibs `mingw64\bin` first, delete `Innerbuild\src\schema_last_built`, build again.
2. **`localtime_r` is not declared under MinGW.** `cmake/PulseBuildInformation.cpp.in` guards `localtime_s` with `_MSC_VER` only; the one-line change `#if defined(_MSC_VER) || defined(__MINGW32__)` (and the same in the generated copy) fixed it. This is a patch to a copy of the sources that is not in this repository; it should go upstream.
3. The Python tools of the build need `pip` inside the virtual environment (`uv venv` creates none).
4. The Python module (`PyPulse`) was built by MinGW and the interpreter by MSVC; the module imports only when `os.add_dll_directory` is given the MinGW `bin` folder and Pulse's `install/bin` (`pulse_bridge.py --dll-dir`).

Total: about 1,220 build steps; about two hours of wall time including the failed attempts, with other work running on the same processor; 4.5 GB for the build folder and 0.13 GB for the sources. The HowTo_Dehydration example ran (the engine stabilises a patient file in about 85 seconds; saved states in `install/bin/states` start in seconds).

## What was built on top (s02 and s03)

* `content/physio/pulse_mapping.json`: the Pulse outputs mapped to the Core's variables, the Core effects and patterns that have a Pulse action, and for each that has none the reason (anaphylaxis has no Pulse action; sepsis and dehydration need a 90-second stabilisation of a patient file and the Pulse authors mark the sepsis model incomplete).
* `tools/pulse/pulse_bridge.py run`: starts the engine from a saved state, applies the actions at their times, samples every 10 s and writes `content/physio/pulse_traces/<scenario>.json` (with the Pulse version and git hash and the wall time per simulated second). `check` needs no Pulse.
* Four recorded runs (1,800 s bleeding then a saline bolus; opioid then naloxone; adrenaline; acute stress).
* `src/ProcSense.Core/Physiology/PulseTrace.cs` and `PulseTraceEngine.cs`: the adapter behind `IPhysiologyEngine`, an interface `ISessionPhysiology` that `LitePhysio` also implements, `SessionOptions.PulseTrace`, the header field `pulse_trace` (written only when used, so older logs read the same), the CLI option `--pulse-trace`, and the content loader reading `physio/pulse_traces/index.json`. The engine replays a recorded run: until the effect that starts it is applied the patient is at the case's baseline; then the case moves by what Pulse moved from its own start. It is a recorded scenario, not an interactive patient. 11 tests (`PulseTraceTests`), including **all 142 golden runs with a trace selected: the end state is unchanged in every one and the observable state is identical unless the starting effect was given**.
* `tools/pulse/parity.py` and `docs/research/physio-plots/pulse-parity/`: LitePhysio and Pulse on the same four scenarios, with plots.

## Results

* **Cost on the desktop:** 81 to 84 ms of wall time per simulated second, i.e. about 1.6 ms for each 20 ms step (a processor shared with other work; the first run with the machine busy). The step plan's target is 3 ms a frame on the Quest; the headset figure does not follow from this one (ARM, thermal, Unity beside it) and is Sannan's (DC-05).
* **Comparison (EXP-PULSE-1 and EXP-PULSE-2):** see `docs/research/physio-plots/pulse-parity/parity.md`. Where the Core's effect is the main thing it models (heart rate and pressure for stress, bleeding and adrenaline; respiratory rate for opioid and naloxone) the two engines move the same way; on secondary variables they often do not (9 of the 27 comparisons where either engine moves agree in direction). LitePhysio's fluid bolus raises pressure whatever is causing the loss; Pulse's bleeding goes on and the bolus only slows the fall. Pulse's morphine slows the heart; LitePhysio's opioid pattern does not.
* **Reviewer preference:** none. The plan asked a medical reviewer to pick the more realistic curve; no reviewer has looked, and under ADR-0008 that is not a gate.

## Decision (proposed; no reviewer, no headset)

1. LitePhysio stays the only engine of an ordinary session: it is the walkthrough's design values, fast, deterministic and tested. Nothing in scoring depends on the engine (scoring is by actions).
2. Pulse is available as a **recorded reference**: traces can be generated offline and played with `--pulse-trace` for practice scenarios where a more physiological response is wanted (for example the haemorrhage without control). It is not for assessment mode.
3. Revisit when the Quest cost is measured (DC-05), when a reviewer has compared the plots, or when an interactive bridge (Pulse running beside the session, its values logged and replayed) is wanted. The bridge would be the next step; replay already works for it.

## Steps still to do (not engineering)

1. On the Quest: a blank Unity 6 project with the C# bindings, ARM64 IL2CPP, measure (`docs/testing/SANNAN_EXTERNAL_TESTING.md`, DC-05). Only needed if Pulse is to run on the headset; the recorded-run design does not need it.
2. A reviewer's choice between the curves (optional).
