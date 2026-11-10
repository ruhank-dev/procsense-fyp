# New Unity environment: reproduce the current ProcSense environment from a clone

Authoritative guide, written 3 Nov 2026 (UTC) for someone who has never worked on ProcSense and has a fresh Windows 11 computer with nothing installed.
Companion files: [research/asset_sources_and_licenses.md](research/asset_sources_and_licenses.md) (licences, the existing licensing document), [qa/unity_environment_validation.md](qa/unity_environment_validation.md) (what was tested, with results), [collaborator_setup_message.md](collaborator_setup_message.md) (the short WhatsApp version), [TEAM_GIT_WORKFLOW.md](TEAM_GIT_WORKFLOW.md) (how we push), [testing/SANNAN_EXTERNAL_TESTING.md](testing/SANNAN_EXTERNAL_TESTING.md) (headset checks).

Everything here was checked against the repository on the day it was written. Where something could not be tested (the Meta Quest headset) it says so.

---

## A. Project overview

**ProcSense** is a VR clinical-procedure trainer for Meta Quest 2/3 (and a desktop simulator) built as a university final-year project. The shared simulation engine is C# (`src/ProcSense.Core`), the VR app is Unity, the backend is a FastAPI service.

| Question | Answer |
|---|---|
| Which Unity project do I open? | The folder **`unity/`** inside the repository (not the repository root). Add that folder in Unity Hub. |
| Unity version | **6000.5.10f1** (revision `3bd4f66ad299`) |
| Render pipeline | **URP 17.5.0** (Universal Render Pipeline). Desktop uses `URP_Quest.asset` in `unity/Assets/Settings`. From branch `quest-perf/d7-optimisations` (7 Nov 2026, not yet merged, DT-280) Android's quality level uses its own copy `URP_Quest_Android.asset` (renderer with intermediate texture Auto); before that, Quest and desktop shared one asset. |
| Is there an HDRP project? | **No.** The repository contains exactly one Unity project (`unity/`, URP). A separate HDRP project was discussed, but none exists in the repository or on the owner's machines, so there is nothing to preserve or to keep apart. Two older experiment copies of the URP project (`D:\ProcSenseExperiments\A_urp_original`, `B_urp_enhanced`) live outside the repository and are not part of this handoff. |
| Current working baseline | Branch **`main`** includes the visual overhaul: PBR materials, baked sunlight and Rocketbox characters. Develop on a feature branch from main. |
| Scenes | `Boot` (first in the build), `Sys_Core` (simulation services), `MainMenu`, `PatientBay`, `Corridor` (the corridor **includes the reception desk and waiting area**; there is no separate Reception scene), `OT` (operating theatre), `Recovery`, `DevOnly` (Editor XR simulator), plus review scenes `EquipmentPilotReview` and `MPFBPatientReview`. |
| Which scene first? | Use the menu **ProcSense → Open Patient Bay**: it opens `PatientBay` together with `Sys_Core` and `DevOnly` and places the Scene view in the room. For Corridor/OT use the same menu group. |
| Size to expect | Clone about 2.5 GB (history + LFS), working copy about 1.7 GB of assets, first Unity import adds about 10 GB in `unity/Library` (git-ignored, regenerated). Keep 25 GB free. |

### What is in Git, what is restored, what is downloaded

| Kind | Where it comes from | Your action |
|---|---|---|
| Project files: scenes, prefabs, materials, scripts, shaders, ProjectSettings, `.meta` files (GUIDs), models (FBX), textures, characters, animations, HDRIs, Blender sources | **In Git** (large baked lighting files via Git LFS) | `git clone` + `git lfs pull` |
| Unity packages (URP, XR, Input System, …) | **Unity Package Manager**, versions pinned in `unity/Packages/manifest.json` and `packages-lock.json` | Automatic when Unity opens the project |
| Original downloads (ambientCG, Poly Haven, Rocketbox, MakeHuman, Blender) | **`tools\setup\restore_environment.ps1`** verifies every file against SHA-256 and can re-download the originals, checksum-checked | Run the script (one command) |
| Unity Editor, Android modules, Blender, Git, … | Official installers (section B) | Install once |

Nothing is downloaded while the app runs: all downloads are development-time only.

---

## B. Required software

Install in this order. "Verify" is a command you can paste to prove it worked.

| # | Software | Version used | Required? | Download | Notes | Verify |
|---|---|---|---|---|---|---|
| 1 | **Git for Windows** (includes Git LFS and Git Bash) | 2.54.0 (2.40+ works) | Mandatory | https://git-scm.com/download/win | Accept the defaults; keep "Git LFS" ticked. | `git --version` |
| 2 | **Git LFS** | 3.7.1 (3.x) | Mandatory (comes with Git for Windows; standalone: https://git-lfs.com) | | Run once: `git lfs install` | `git lfs version` |
| 3 | **Unity Hub** | latest | Mandatory | https://unity.com/download | Sign in or create a free Unity ID. | open the Hub |
| 4 | **Unity Editor 6000.5.10f1** | 6000.5.10f1 (revision 3bd4f66ad299) | Mandatory | Click `unityhub://6000.5.10f1/3bd4f66ad299` (opens the Hub) or find it in the archive https://unity.com/releases/editor/archive | Modules to tick: **Android Build Support** with **OpenJDK**, **Android SDK & NDK Tools**, and **Windows Build Support (Mono)** (default). Do **not** install a separate JDK/SDK/NDK: the Hub module brings the exact ones. | `"C:\Program Files\Unity\Hub\Editor\6000.5.10f1\Editor\Unity.exe" -version` |
| 5 | **Python 3.11+** | 3.12 (anaconda 3.13 also works) | Needed for the restore script's rebuild steps and Blender-pipeline scripts, not to open Unity | https://www.python.org/downloads/windows/ | Then `python -m pip install pillow numpy opencv-python` | `python --version` |
| 6 | **Blender 4.5.14 LTS** | 4.5.14 | Optional (only to open the `.blend` sources or rebuild models) | https://www.blender.org/download/releases/4-5/ ; portable zip https://download.blender.org/release/Blender4.5/blender-4.5.14-windows-x64.zip | Easiest: `powershell -ExecutionPolicy Bypass -File tools\setup\restore_environment.ps1 -WithBlender` installs the portable copy (SHA-256 checked) to `build\blender`. | `build\blender\blender-4.5.14-windows-x64\blender.exe --version` |
| 7 | **.NET SDK 9** | 9.0.318 (`global.json` needs 9.0.100+) | Needed only for `task ci` (engine tests), not for Unity | https://dotnet.microsoft.com/download/dotnet/9.0 | | `dotnet --version` |
| 8 | **uv** | 0.12.22 (0.5+) | Needed only for `task ci` / Python tools | https://docs.astral.sh/uv/getting-started/installation/ (`winget install astral-sh.uv`) | | `uv --version` |
| 9 | **Task** (go-task) | 3.54.0 (3.x) | Needed for `task ci`, `task unity-test`, `task unity-build` | https://taskfile.dev/installation/ (`winget install Task.Task`) | | `task --version` |
| 10 | **Docker Desktop** | 29.5.3 | Optional: backend tests and the service image only | https://www.docker.com/products/docker-desktop/ | | `docker version` |
| 11 | **ffmpeg** | 9.0.2 | Optional: encodes capture videos | `winget install Gyan.FFmpeg` | | `ffmpeg -version` |
| 12 | **Android platform-tools (adb)** | 1.0.41 | Mandatory for the Quest (Sannan) | https://developer.android.com/tools/releases/platform-tools (`winget install Google.PlatformTools`) | | `adb version` |
| 13 | **Meta Quest Developer Hub** | latest | Recommended for Quest | https://developers.meta.com/horizon/downloads/package/oculus-developer-hub-win/ | Developer mode: Meta Horizon phone app → Devices → Headset settings → Developer mode. | open the Hub |
| 14 | **Visual Studio Code** (or Visual Studio 2022 with "Game development with Unity") | any | Optional editor for C# | https://code.visualstudio.com/ | Unity → Preferences → External Tools to pick it. | |

**Not required:** a separate JDK, Android SDK/NDK, Visual Studio build tools, any Unity Asset Store account, any paid licence. Unity Personal is enough.

Check everything in one go (after cloning): `task doctor`.

---

## C. Unity packages and plugins

Versions are pinned by `unity/Packages/manifest.json` and `unity/Packages/packages-lock.json` (both committed); Unity restores them automatically from the Unity registry the first time the project opens. No package points at a folder on anyone's computer: the one local package, `com.procsense.core`, is `file:../../src/ProcSense.Core` inside the repository. There are no custom registries, no git-URL packages, no Asset Store packages, no addressables, no timeline assets and no plug-in DLLs outside packages.

<!-- BEGIN GENERATED:packages -->
| Package id | Version (locked) | Source | Direct (manifest) | Depends on | Restored by |
|---|---|---|---|---|---|
| `com.procsense.core` | file:../../src/ProcSense.Core | local | yes | - | this repository (file package `src/ProcSense.Core`) |
| `com.unity.ai.navigation` | 2.0.16 | registry | yes | - | Unity Package Manager (Unity registry), automatic on first open |
| `com.unity.burst` | 1.8.30 | registry | no (transitive) | `com.unity.mathematics` | Unity Package Manager (Unity registry), automatic on first open |
| `com.unity.collections` | 6.5.0 | builtin | no (transitive) | `com.unity.burst`, `com.unity.nuget.mono-cecil`, `com.unity.test-framework`, `com.unity.test-framework.performance` | built into the Unity Editor |
| `com.unity.ext.nunit` | 2.1.0 | builtin | no (transitive) | - | built into the Unity Editor |
| `com.unity.inputsystem` | 1.20.1 | registry | yes | - | Unity Package Manager (Unity registry), automatic on first open |
| `com.unity.mathematics` | 1.4.0 | builtin | no (transitive) | - | built into the Unity Editor |
| `com.unity.multiplayer.center` | 1.0.1 | builtin | yes | - | built into the Unity Editor |
| `com.unity.nuget.mono-cecil` | 1.11.6 | registry | no (transitive) | - | Unity Package Manager (Unity registry), automatic on first open |
| `com.unity.nuget.newtonsoft-json` | 3.2.2 | registry | no (transitive) | - | Unity Package Manager (Unity registry), automatic on first open |
| `com.unity.pipeline` | 0.8.0-exp.1 | registry | yes | `com.unity.test-framework`, `com.unity.nuget.mono-cecil`, `com.unity.nuget.newtonsoft-json` | Unity Package Manager (Unity registry), automatic on first open |
| `com.unity.render-pipelines.core` | 17.5.0 | builtin | no (transitive) | `com.unity.burst`, `com.unity.collections` | built into the Unity Editor |
| `com.unity.render-pipelines.universal` | 17.5.0 | builtin | yes | `com.unity.render-pipelines.core`, `com.unity.shadergraph`, `com.unity.render-pipelines.universal-config` | built into the Unity Editor |
| `com.unity.render-pipelines.universal-config` | 17.5.0 | builtin | no (transitive) | `com.unity.render-pipelines.core` | built into the Unity Editor |
| `com.unity.searcher` | 4.9.5 | registry | no (transitive) | - | Unity Package Manager (Unity registry), automatic on first open |
| `com.unity.shadergraph` | 17.5.0 | builtin | no (transitive) | `com.unity.render-pipelines.core`, `com.unity.searcher` | built into the Unity Editor |
| `com.unity.test-framework` | 1.7.0 | builtin | yes | `com.unity.ext.nunit` | built into the Unity Editor |
| `com.unity.test-framework.performance` | 3.5.0 | registry | no (transitive) | `com.unity.test-framework` | Unity Package Manager (Unity registry), automatic on first open |
| `com.unity.ugui` | 2.5.0 | builtin | yes | - | built into the Unity Editor |
| `com.unity.xr.core-utils` | 2.6.0 | registry | no (transitive) | - | Unity Package Manager (Unity registry), automatic on first open |
| `com.unity.xr.interaction.toolkit` | 3.6.1 | registry | yes | `com.unity.ugui`, `com.unity.inputsystem`, `com.unity.mathematics`, `com.unity.xr.core-utils` | Unity Package Manager (Unity registry), automatic on first open |
| `com.unity.xr.legacyinputhelpers` | 3.0.1 | registry | no (transitive) | - | Unity Package Manager (Unity registry), automatic on first open |
| `com.unity.xr.management` | 4.7.0 | registry | yes | `com.unity.xr.core-utils`, `com.unity.xr.legacyinputhelpers` | Unity Package Manager (Unity registry), automatic on first open |
| `com.unity.xr.openxr` | 1.18.0 | registry | yes | `com.unity.inputsystem`, `com.unity.xr.core-utils`, `com.unity.xr.management`, `com.unity.xr.legacyinputhelpers` | Unity Package Manager (Unity registry), automatic on first open |

Plus 36 built-in Unity modules (`com.unity.modules.*`, all version 1.0.0, shipped inside the Editor, nothing to install).
<!-- END GENERATED:packages -->

Settings that matter and are already committed in `unity/ProjectSettings`: Android target API and OpenXR loader (checked by `BuildScript` before every Quest build), XR Plug-in Management (OpenXR with Meta Quest feature group), Input System (new), URP asset and renderer in `unity/Assets/Settings`, quality levels, physics layers, tags. `unity/Assets/Samples` (XR Interaction Toolkit starter assets) and `unity/Assets/TextMesh Pro` (essential resources) are kept in the project on purpose.

---

## D. Complete external asset manifest

Every externally sourced asset the current environment uses. The table is generated from the machine-readable manifest [`tools/setup/asset_manifest.json`](../tools/setup/asset_manifest.json) (so it cannot drift from what the restore script checks); the same table is in [research/asset_sources_and_licenses.md](research/asset_sources_and_licenses.md). Per-file SHA-256 of all 4,000+ tracked Unity/Blender files is in the same JSON (`files`), and the download checksums (SHA-256 for ambientCG/Blender/MakeHuman, MD5 from the Poly Haven API, Git blob SHA-1 at a pinned commit for Rocketbox) are in `tools/setup/remote_metadata.json`.

The repository is **public**, so only redistributable files are kept in it. Every external asset here is CC0, MIT, or generated from CC0/MIT sources by our own scripts. Mixamo, Unity Asset Store, BlenderKit and Sketchfab downloads are **not** used.

<!-- BEGIN GENERATED:inventory -->
| Asset | Category | Creator | Source page | Version | Format | Licence (terms) | Attribution | Redistribution | Account | In Git | Local path | Used in | Restore |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| Microsoft Rocketbox avatars (17 clothed, rigged characters) | Character | Microsoft / Rocketbox Studios GmbH | https://github.com/microsoft/Microsoft-Rocketbox | git commit 0943055db6ec570bcef9f2c8b41c9e5467c808f9 | FBX (Biped, 176 facial blend shapes) + TGA textures converted to JPG/PNG | [MIT](https://github.com/microsoft/Microsoft-Rocketbox/blob/master/LICENSE.md) | Keep the MIT licence text (copied to unity/Assets/Characters/Rocketbox/LICENSE.md) | Yes (MIT) | No | Yes | `unity/Assets/Characters/Rocketbox` | Corridor (seated and standing people), OT (two staff), PatientBay (patient: Medical_Male_04 recoloured) | script: `python -I unity/Tools/Characters/fetch_rocketbox.py [Name ...]` |
| Universal Animation Library [Standard] (43 clips) | Animation | Quaternius (Quaternius.com) | https://quaternius.com | Standard pack as downloaded in Nov 2026 (file hash in the manifest) | FBX (humanoid clips) | [CC0 1.0](https://creativecommons.org/publicdomain/zero/1.0/) | Not required; credited in docs | Yes (CC0; copy of the licence text at unity/Assets/Characters/Animations/LICENSE_Quaternius_UAL_CC0.txt) | Pack page of the author (no purchase for the Standard pack as used) | Yes | `unity/Assets/Characters/Animations` | All Rocketbox avatars through Unity Humanoid retargeting (Corridor, OT, PatientBay) | git-or-manual |
| Poly Haven model potted_plant_01 | 3D model | Poly Haven contributors | https://polyhaven.com/a/potted_plant_01 | 2K glTF as served by the Poly Haven API on 3 Nov 2026 (md5 per file) | glTF + JPG, decimated and converted to FBX by convert_ph_model.py | [CC0 1.0](https://polyhaven.com/license) | Not required | Yes (CC0) | No | Yes | `unity/Assets/Models/Environment/PolyHaven/potted_plant_01`<br>`unity/Assets/Textures/PolyHaven/potted_plant_01` | Floor plants, corridor and patient bay | script: `python -I unity/Tools/Environment/fetch_ph_model.py potted_plant_01  then  python -I unity/Tools/Environment/convert_ph_model.py potted_plant_01  (needs Blender 4.5.14)` |
| Poly Haven model potted_plant_02 | 3D model | Poly Haven contributors | https://polyhaven.com/a/potted_plant_02 | 2K glTF as served by the Poly Haven API on 3 Nov 2026 (md5 per file) | glTF + JPG, decimated and converted to FBX by convert_ph_model.py | [CC0 1.0](https://polyhaven.com/license) | Not required | Yes (CC0) | No | Yes | `unity/Assets/Models/Environment/PolyHaven/potted_plant_02`<br>`unity/Assets/Textures/PolyHaven/potted_plant_02` | Floor plants (syngonium), corridor and patient bay | script: `python -I unity/Tools/Environment/fetch_ph_model.py potted_plant_02  then  python -I unity/Tools/Environment/convert_ph_model.py potted_plant_02  (needs Blender 4.5.14)` |
| Poly Haven model potted_plant_04 | 3D model | Poly Haven contributors | https://polyhaven.com/a/potted_plant_04 | 2K glTF as served by the Poly Haven API on 3 Nov 2026 (md5 per file) | glTF + JPG, decimated and converted to FBX by convert_ph_model.py | [CC0 1.0](https://polyhaven.com/license) | Not required | Yes (CC0) | No | Yes | `unity/Assets/Models/Environment/PolyHaven/potted_plant_04`<br>`unity/Assets/Textures/PolyHaven/potted_plant_04` | Tabletop succulent (not yet placed) | script: `python -I unity/Tools/Environment/fetch_ph_model.py potted_plant_04  then  python -I unity/Tools/Environment/convert_ph_model.py potted_plant_04  (needs Blender 4.5.14)` |
| Poly Haven model wheelchair_01 | 3D model | Poly Haven contributors | https://polyhaven.com/a/wheelchair_01 | 2K glTF as served by the Poly Haven API on 3 Nov 2026 (md5 per file) | glTF + JPG, decimated and converted to FBX by convert_ph_model.py | [CC0 1.0](https://polyhaven.com/license) | Not required | Yes (CC0) | No | Yes | `unity/Assets/Models/Environment/PolyHaven/wheelchair_01`<br>`unity/Assets/Textures/PolyHaven/wheelchair_01` | Parked wheelchairs in the corridor | script: `python -I unity/Tools/Environment/fetch_ph_model.py wheelchair_01  then  python -I unity/Tools/Environment/convert_ph_model.py wheelchair_01  (needs Blender 4.5.14)` |
| Poly Haven model fire_alarm | 3D model | Poly Haven contributors | https://polyhaven.com/a/fire_alarm | 2K glTF as served by the Poly Haven API on 3 Nov 2026 (md5 per file) | glTF + JPG, decimated and converted to FBX by convert_ph_model.py | [CC0 1.0](https://polyhaven.com/license) | Not required | Yes (CC0) | No | Yes | `unity/Assets/Models/Environment/PolyHaven/fire_alarm`<br>`unity/Assets/Textures/PolyHaven/fire_alarm` | Wall call points | script: `python -I unity/Tools/Environment/fetch_ph_model.py fire_alarm  then  python -I unity/Tools/Environment/convert_ph_model.py fire_alarm  (needs Blender 4.5.14)` |
| Poly Haven model korean_fire_extinguisher_01 | 3D model | Poly Haven contributors | https://polyhaven.com/a/korean_fire_extinguisher_01 | 2K glTF as served by the Poly Haven API on 3 Nov 2026 (md5 per file) | glTF + JPG, decimated and converted to FBX by convert_ph_model.py | [CC0 1.0](https://polyhaven.com/license) | Not required | Yes (CC0) | No | Yes | `unity/Assets/Models/Environment/PolyHaven/korean_fire_extinguisher_01`<br>`unity/Assets/Textures/PolyHaven/korean_fire_extinguisher_01` | Floor extinguishers | script: `python -I unity/Tools/Environment/fetch_ph_model.py korean_fire_extinguisher_01  then  python -I unity/Tools/Environment/convert_ph_model.py korean_fire_extinguisher_01  (needs Blender 4.5.14)` |
| Poly Haven model hanging_picture_frame_02 | 3D model | Poly Haven contributors | https://polyhaven.com/a/hanging_picture_frame_02 | 2K glTF as served by the Poly Haven API on 3 Nov 2026 (md5 per file) | glTF + JPG, decimated and converted to FBX by convert_ph_model.py | [CC0 1.0](https://polyhaven.com/license) | Not required | Yes (CC0) | No | Yes | `unity/Assets/Models/Environment/PolyHaven/hanging_picture_frame_02`<br>`unity/Assets/Textures/PolyHaven/hanging_picture_frame_02` | Framed photograph | script: `python -I unity/Tools/Environment/fetch_ph_model.py hanging_picture_frame_02  then  python -I unity/Tools/Environment/convert_ph_model.py hanging_picture_frame_02  (needs Blender 4.5.14)` |
| Poly Haven model wall_clock | 3D model | Poly Haven contributors | https://polyhaven.com/a/wall_clock | 2K glTF as served by the Poly Haven API on 3 Nov 2026 (md5 per file) | glTF + JPG, decimated and converted to FBX by convert_ph_model.py | [CC0 1.0](https://polyhaven.com/license) | Not required | Yes (CC0) | No | Yes | `unity/Assets/Models/Environment/PolyHaven/wall_clock`<br>`unity/Assets/Textures/PolyHaven/wall_clock` | Wall clocks | script: `python -I unity/Tools/Environment/fetch_ph_model.py wall_clock  then  python -I unity/Tools/Environment/convert_ph_model.py wall_clock  (needs Blender 4.5.14)` |
| Poly Haven model fancy_picture_frame_01 | 3D model | Poly Haven contributors | https://polyhaven.com/a/fancy_picture_frame_01 | 2K glTF as served by the Poly Haven API on 3 Nov 2026 (md5 per file) | glTF + JPG, decimated and converted to FBX by convert_ph_model.py | [CC0 1.0](https://polyhaven.com/license) | Not required | Yes (CC0) | No | Yes | `unity/Assets/Models/Environment/PolyHaven/fancy_picture_frame_01`<br>`unity/Assets/Textures/PolyHaven/fancy_picture_frame_01` | Imported, gilded frame, not placed | script: `python -I unity/Tools/Environment/fetch_ph_model.py fancy_picture_frame_01  then  python -I unity/Tools/Environment/convert_ph_model.py fancy_picture_frame_01  (needs Blender 4.5.14)` |
| Poly Haven model fancy_picture_frame_02 | 3D model | Poly Haven contributors | https://polyhaven.com/a/fancy_picture_frame_02 | 2K glTF as served by the Poly Haven API on 3 Nov 2026 (md5 per file) | glTF + JPG, decimated and converted to FBX by convert_ph_model.py | [CC0 1.0](https://polyhaven.com/license) | Not required | Yes (CC0) | No | Yes | `unity/Assets/Models/Environment/PolyHaven/fancy_picture_frame_02`<br>`unity/Assets/Textures/PolyHaven/fancy_picture_frame_02` | Imported, gilded frame, not placed | script: `python -I unity/Tools/Environment/fetch_ph_model.py fancy_picture_frame_02  then  python -I unity/Tools/Environment/convert_ph_model.py fancy_picture_frame_02  (needs Blender 4.5.14)` |
| Poly Haven model hanging_picture_frame_01 | 3D model | Poly Haven contributors | https://polyhaven.com/a/hanging_picture_frame_01 | 2K glTF as served by the Poly Haven API on 3 Nov 2026 (md5 per file) | glTF + JPG, decimated and converted to FBX by convert_ph_model.py | [CC0 1.0](https://polyhaven.com/license) | Not required | Yes (CC0) | No | Yes | `unity/Assets/Models/Environment/PolyHaven/hanging_picture_frame_01`<br>`unity/Assets/Textures/PolyHaven/hanging_picture_frame_01` | Framed pictures | script: `python -I unity/Tools/Environment/fetch_ph_model.py hanging_picture_frame_01  then  python -I unity/Tools/Environment/convert_ph_model.py hanging_picture_frame_01  (needs Blender 4.5.14)` |
| Poly Haven model hanging_picture_frame_03 | 3D model | Poly Haven contributors | https://polyhaven.com/a/hanging_picture_frame_03 | 2K glTF as served by the Poly Haven API on 3 Nov 2026 (md5 per file) | glTF + JPG, decimated and converted to FBX by convert_ph_model.py | [CC0 1.0](https://polyhaven.com/license) | Not required | Yes (CC0) | No | Yes | `unity/Assets/Models/Environment/PolyHaven/hanging_picture_frame_03`<br>`unity/Assets/Textures/PolyHaven/hanging_picture_frame_03` | Framed pictures | script: `python -I unity/Tools/Environment/fetch_ph_model.py hanging_picture_frame_03  then  python -I unity/Tools/Environment/convert_ph_model.py hanging_picture_frame_03  (needs Blender 4.5.14)` |
| Poly Haven HDRI homecoming_center_rooftop (8k) | HDRI / sky | Dario Barresi | https://polyhaven.com/a/homecoming_center_rooftop | 8k .hdr (md5 f9e33fa7626046df17fe866bdca7420a) | HDR | [CC0 1.0](https://polyhaven.com/license) | Not required | Yes (CC0) | No | Yes | `unity/Assets/Textures/Environment/Sky/city_skyline_8k.hdr` | Exterior sky behind the windows (PatientBay, Corridor, OT); processed by CityExterior.cs | download-copy |
| Poly Haven HDRI kloofendal_48d_partly_cloudy_puresky (4k) | HDRI / sky | Greg Zaal, Jarod Guest | https://polyhaven.com/a/kloofendal_48d_partly_cloudy_puresky | 4k .hdr (md5 816340c969d2e60d659051d13ac3c4dc) | HDR | [CC0 1.0](https://polyhaven.com/license) | Not required | Yes (CC0) | No | Yes | `unity/Assets/Textures/Environment/Sky/sky_partly_cloudy_4k.hdr` | Exterior sky behind the windows (PatientBay, Corridor, OT); processed by CityExterior.cs | download-copy |
| Poly Haven texture white_plaster_02 | PBR texture | Poly Haven contributors | https://polyhaven.com/a/white_plaster_02 | 2K JPG maps (md5 per file) | JPG (Diffuse, nor_gl, Rough, AO) | [CC0 1.0](https://polyhaven.com/license) | Not required | Yes (CC0) | No | Yes | `unity/Assets/Textures/Hospital/PBR/hosp_wall_*` | Hospital materials (see docs/research/asset_sources_and_licenses.md table) | script: `python -I unity/Tools/Environment/pack_pbr.py polyhaven <scan_dir> white_plaster_02 unity/Assets/Textures/Hospital/PBR <prefix>` |
| Poly Haven texture rubber_tiles | PBR texture | Poly Haven contributors | https://polyhaven.com/a/rubber_tiles | 2K JPG maps (md5 per file) | JPG (Diffuse, nor_gl, Rough, AO) | [CC0 1.0](https://polyhaven.com/license) | Not required | Yes (CC0) | No | Yes | `unity/Assets/Textures/Hospital/PBR/hosp_floor_*` | Hospital materials (see docs/research/asset_sources_and_licenses.md table) | script: `python -I unity/Tools/Environment/pack_pbr.py polyhaven <scan_dir> rubber_tiles unity/Assets/Textures/Hospital/PBR <prefix>` |
| Poly Haven texture oak_veneer_01 | PBR texture | Poly Haven contributors | https://polyhaven.com/a/oak_veneer_01 | 2K JPG maps (md5 per file) | JPG (Diffuse, nor_gl, Rough, AO) | [CC0 1.0](https://polyhaven.com/license) | Not required | Yes (CC0) | No | Yes | `unity/Assets/Textures/Hospital/PBR/oak_veneer_*` | Hospital materials (see docs/research/asset_sources_and_licenses.md table) | script: `python -I unity/Tools/Environment/pack_pbr.py polyhaven <scan_dir> oak_veneer_01 unity/Assets/Textures/Hospital/PBR <prefix>` |
| Poly Haven texture concrete_pavement_02 | PBR texture | Poly Haven contributors | https://polyhaven.com/a/concrete_pavement_02 | 2K JPG maps (md5 per file) | JPG (Diffuse, nor_gl, Rough, AO) | [CC0 1.0](https://polyhaven.com/license) | Not required | Yes (CC0) | No | Yes | `unity/Assets/Textures/Hospital/PBR/sidewalk_*` | Hospital materials (see docs/research/asset_sources_and_licenses.md table) | script: `python -I unity/Tools/Environment/pack_pbr.py polyhaven <scan_dir> concrete_pavement_02 unity/Assets/Textures/Hospital/PBR <prefix>` |
| ambientCG OfficeCeiling001 | PBR texture | ambientCG (Lennart Demes) | https://ambientcg.com/view?id=OfficeCeiling001 | OfficeCeiling001_2K-JPG.zip | ZIP of JPG maps (Color, NormalGL, Roughness, AO, Metalness, Displacement) | [CC0 1.0](https://docs.ambientcg.com/license/) | Not required | Yes (CC0) | No | Yes | `unity/Assets/Textures/Hospital/PBR/acoustic_ceiling_*` | Hospital materials (walls, ceilings, steel, cloth, plastics, city and facades) | script: `python -I unity/Tools/Environment/pack_pbr.py ambientcg <unzipped_dir> OfficeCeiling001 unity/Assets/Textures/Hospital/PBR <prefix>` |
| ambientCG Metal011 | PBR texture | ambientCG (Lennart Demes) | https://ambientcg.com/view?id=Metal011 | Metal011_2K-JPG.zip | ZIP of JPG maps (Color, NormalGL, Roughness, AO, Metalness, Displacement) | [CC0 1.0](https://docs.ambientcg.com/license/) | Not required | Yes (CC0) | No | Yes | `unity/Assets/Textures/Hospital/PBR/satin_steel_*` | Hospital materials (walls, ceilings, steel, cloth, plastics, city and facades) | script: `python -I unity/Tools/Environment/pack_pbr.py ambientcg <unzipped_dir> Metal011 unity/Assets/Textures/Hospital/PBR <prefix>` |
| ambientCG Metal009 | PBR texture | ambientCG (Lennart Demes) | https://ambientcg.com/view?id=Metal009 | Metal009_2K-JPG.zip | ZIP of JPG maps (Color, NormalGL, Roughness, AO, Metalness, Displacement) | [CC0 1.0](https://docs.ambientcg.com/license/) | Not required | Yes (CC0) | No | Yes | `unity/Assets/Textures/Hospital/PBR/brushed_steel_*` | Hospital materials (walls, ceilings, steel, cloth, plastics, city and facades) | script: `python -I unity/Tools/Environment/pack_pbr.py ambientcg <unzipped_dir> Metal009 unity/Assets/Textures/Hospital/PBR <prefix>` |
| ambientCG Fabric066 | PBR texture | ambientCG (Lennart Demes) | https://ambientcg.com/view?id=Fabric066 | Fabric066_2K-JPG.zip | ZIP of JPG maps (Color, NormalGL, Roughness, AO, Metalness, Displacement) | [CC0 1.0](https://docs.ambientcg.com/license/) | Not required | Yes (CC0) | No | Yes | `unity/Assets/Textures/Hospital/PBR/curtain_weave_*` | Hospital materials (walls, ceilings, steel, cloth, plastics, city and facades) | script: `python -I unity/Tools/Environment/pack_pbr.py ambientcg <unzipped_dir> Fabric066 unity/Assets/Textures/Hospital/PBR <prefix>` |
| ambientCG Fabric061 | PBR texture | ambientCG (Lennart Demes) | https://ambientcg.com/view?id=Fabric061 | Fabric061_2K-JPG.zip | ZIP of JPG maps (Color, NormalGL, Roughness, AO, Metalness, Displacement) | [CC0 1.0](https://docs.ambientcg.com/license/) | Not required | Yes (CC0) | No | Yes | `unity/Assets/Textures/Hospital/PBR/twill_cotton_*` | Hospital materials (walls, ceilings, steel, cloth, plastics, city and facades) | script: `python -I unity/Tools/Environment/pack_pbr.py ambientcg <unzipped_dir> Fabric061 unity/Assets/Textures/Hospital/PBR <prefix>` |
| ambientCG Fabric086 | PBR texture | ambientCG (Lennart Demes) | https://ambientcg.com/view?id=Fabric086 | Fabric086_2K-JPG.zip | ZIP of JPG maps (Color, NormalGL, Roughness, AO, Metalness, Displacement) | [CC0 1.0](https://docs.ambientcg.com/license/) | Not required | Yes (CC0) | No | Yes | `unity/Assets/Textures/Hospital/PBR/cotton_white_*` | Hospital materials (walls, ceilings, steel, cloth, plastics, city and facades) | script: `python -I unity/Tools/Environment/pack_pbr.py ambientcg <unzipped_dir> Fabric086 unity/Assets/Textures/Hospital/PBR <prefix>` |
| ambientCG Plastic010 | PBR texture | ambientCG (Lennart Demes) | https://ambientcg.com/view?id=Plastic010 | Plastic010_2K-JPG.zip | ZIP of JPG maps (Color, NormalGL, Roughness, AO, Metalness, Displacement) | [CC0 1.0](https://docs.ambientcg.com/license/) | Not required | Yes (CC0) | No | Yes | `unity/Assets/Textures/Hospital/PBR/white_plastic_*` | Hospital materials (walls, ceilings, steel, cloth, plastics, city and facades) | script: `python -I unity/Tools/Environment/pack_pbr.py ambientcg <unzipped_dir> Plastic010 unity/Assets/Textures/Hospital/PBR <prefix>` |
| ambientCG Terrazzo001 | PBR texture | ambientCG (Lennart Demes) | https://ambientcg.com/view?id=Terrazzo001 | Terrazzo001_2K-JPG.zip | ZIP of JPG maps (Color, NormalGL, Roughness, AO, Metalness, Displacement) | [CC0 1.0](https://docs.ambientcg.com/license/) | Not required | Yes (CC0) | No | Yes | `unity/Assets/Textures/Hospital/PBR/terrazzo_*` | Hospital materials (walls, ceilings, steel, cloth, plastics, city and facades) | script: `python -I unity/Tools/Environment/pack_pbr.py ambientcg <unzipped_dir> Terrazzo001 unity/Assets/Textures/Hospital/PBR <prefix>` |
| ambientCG Plastic011 | PBR texture | ambientCG (Lennart Demes) | https://ambientcg.com/view?id=Plastic011 | Plastic011_2K-JPG.zip | ZIP of JPG maps (Color, NormalGL, Roughness, AO, Metalness, Displacement) | [CC0 1.0](https://docs.ambientcg.com/license/) | Not required | Yes (CC0) | No | Yes | `unity/Assets/Textures/Hospital/PBR/dark_plastic_*` | Hospital materials (walls, ceilings, steel, cloth, plastics, city and facades) | script: `python -I unity/Tools/Environment/pack_pbr.py ambientcg <unzipped_dir> Plastic011 unity/Assets/Textures/Hospital/PBR <prefix>` |
| ambientCG Rubber004 | PBR texture | ambientCG (Lennart Demes) | https://ambientcg.com/view?id=Rubber004 | Rubber004_2K-JPG.zip | ZIP of JPG maps (Color, NormalGL, Roughness, AO, Metalness, Displacement) | [CC0 1.0](https://docs.ambientcg.com/license/) | Not required | Yes (CC0) | No | Yes | `unity/Assets/Textures/Hospital/PBR/rubber_black_*` | Hospital materials (walls, ceilings, steel, cloth, plastics, city and facades) | script: `python -I unity/Tools/Environment/pack_pbr.py ambientcg <unzipped_dir> Rubber004 unity/Assets/Textures/Hospital/PBR <prefix>` |
| ambientCG Asphalt031 | PBR texture | ambientCG (Lennart Demes) | https://ambientcg.com/view?id=Asphalt031 | Asphalt031_2K-JPG.zip | ZIP of JPG maps (Color, NormalGL, Roughness, AO, Metalness, Displacement) | [CC0 1.0](https://docs.ambientcg.com/license/) | Not required | Yes (CC0) | No | Yes | `unity/Assets/Textures/Hospital/PBR/asphalt_*` | Hospital materials (walls, ceilings, steel, cloth, plastics, city and facades) | script: `python -I unity/Tools/Environment/pack_pbr.py ambientcg <unzipped_dir> Asphalt031 unity/Assets/Textures/Hospital/PBR <prefix>` |
| ambientCG Road007 | PBR texture | ambientCG (Lennart Demes) | https://ambientcg.com/view?id=Road007 | Road007_2K-JPG.zip | ZIP of JPG maps (Color, NormalGL, Roughness, AO, Metalness, Displacement) | [CC0 1.0](https://docs.ambientcg.com/license/) | Not required | Yes (CC0) | No | Yes | `unity/Assets/Textures/Hospital/PBR/road_marked_*` | Hospital materials (walls, ceilings, steel, cloth, plastics, city and facades) | script: `python -I unity/Tools/Environment/pack_pbr.py ambientcg <unzipped_dir> Road007 unity/Assets/Textures/Hospital/PBR <prefix>` |
| ambientCG Facade019A | PBR texture | ambientCG (Lennart Demes) | https://ambientcg.com/view?id=Facade019A | Facade019A_2K-JPG.zip | ZIP of JPG maps (Color, NormalGL, Roughness, AO, Metalness, Displacement) | [CC0 1.0](https://docs.ambientcg.com/license/) | Not required | Yes (CC0) | No | Yes | `unity/Assets/Textures/Hospital/PBR/facade_dark_glass_*` | Hospital materials (walls, ceilings, steel, cloth, plastics, city and facades) | script: `python -I unity/Tools/Environment/pack_pbr.py ambientcg <unzipped_dir> Facade019A unity/Assets/Textures/Hospital/PBR <prefix>` |
| ambientCG Facade018A | PBR texture | ambientCG (Lennart Demes) | https://ambientcg.com/view?id=Facade018A | Facade018A_2K-JPG.zip | ZIP of JPG maps (Color, NormalGL, Roughness, AO, Metalness, Displacement) | [CC0 1.0](https://docs.ambientcg.com/license/) | Not required | Yes (CC0) | No | Yes | `unity/Assets/Textures/Hospital/PBR/facade_brick_glass_*` | Hospital materials (walls, ceilings, steel, cloth, plastics, city and facades) | script: `python -I unity/Tools/Environment/pack_pbr.py ambientcg <unzipped_dir> Facade018A unity/Assets/Textures/Hospital/PBR <prefix>` |
| ambientCG Facade001 | PBR texture | ambientCG (Lennart Demes) | https://ambientcg.com/view?id=Facade001 | Facade001_2K-JPG.zip | ZIP of JPG maps (Color, NormalGL, Roughness, AO, Metalness, Displacement) | [CC0 1.0](https://docs.ambientcg.com/license/) | Not required | Yes (CC0) | No | Yes | `unity/Assets/Textures/Hospital/PBR/facade_curtainwall_*` | Hospital materials (walls, ceilings, steel, cloth, plastics, city and facades) | script: `python -I unity/Tools/Environment/pack_pbr.py ambientcg <unzipped_dir> Facade001 unity/Assets/Textures/Hospital/PBR <prefix>` |
| ambientCG PaintedPlaster017 | PBR texture | ambientCG (Lennart Demes) | https://ambientcg.com/view?id=PaintedPlaster017 | PaintedPlaster017_1K-JPG.zip | ZIP of JPG maps (Color, NormalGL, Roughness, AO, Metalness, Displacement) | [CC0 1.0](https://docs.ambientcg.com/license/) | Not required | Yes (CC0) | No | Yes | `unity/Assets/Textures/Hospital/PaintedPlaster017_1K-JPG_*` | Hospital materials (walls, ceilings, steel, cloth, plastics, city and facades) | script: `python -I unity/Tools/Environment/pack_pbr.py ambientcg <unzipped_dir> PaintedPlaster017 unity/Assets/Textures/Hospital/PBR <prefix>` |
| ambientCG Wood050 | PBR texture | ambientCG (Lennart Demes) | https://ambientcg.com/view?id=Wood050 | Wood050_1K-JPG.zip | ZIP of JPG maps (Color, NormalGL, Roughness, AO, Metalness, Displacement) | [CC0 1.0](https://docs.ambientcg.com/license/) | Not required | Yes (CC0) | No | Yes | `unity/Assets/Textures/Hospital/Wood050_1K-JPG_*` | Hospital materials (walls, ceilings, steel, cloth, plastics, city and facades) | script: `python -I unity/Tools/Environment/pack_pbr.py ambientcg <unzipped_dir> Wood050 unity/Assets/Textures/Hospital/PBR <prefix>` |
| MakeHuman system assets + MPFB 2.0.18 + face units + visemes (generated civilians, staff, MPFB patient) | Character source | MakeHuman Community (MPFB add-on authors and asset artists) | https://static.makehumancommunity.org/mpfb.html | MPFB v2.0.18 (commit d0a32e5); system assets CC0 pack | ZIP (Blender add-on and assets) | [Assets CC0; MPFB add-on: see its repository licence](https://github.com/makehumancommunity/mpfb2) | Credit MakeHuman Community | Models generated from CC0 assets are redistributed; the add-on itself is a download | No | Yes | `unity/Assets/Models/Characters` | PatientBay (hidden rig under the visible Rocketbox patient: sensors, idle hooks), older corridor/OT staff before the Rocketbox replacement | script: `see unity/Tools/MPFB/README.md (Blender 4.5.14 + the four archives of dependencies.json)` |
| Blender 4.5.14 (build tool for the project's Blender scripts) | Tool | Blender Foundation | https://www.blender.org/download/releases/4-5/ | 4.5.14 (LTS) | ZIP (portable) or installer | [GPL-2.0-or-later (Blender)](https://www.blender.org/about/license/) | None | Not redistributed (download) | No | No (download) | n/a (tool) | All unity/Tools/*/build_*.py generators and conversions | download-extract |
| Unity packages and package content kept in the project (TextMesh Pro, XR Interaction Toolkit samples, XRI settings) | Plugin / package | Unity Technologies | https://docs.unity3d.com/Manual/upm-ui.html | see manifest.json | UPM | [Unity Companion License / package licences](https://unity.com/legal/licenses/unity-companion-license) | None | Package manager restores packages; the sample and TextMesh Pro copies in Assets are part of the project | No (Unity Hub needs a free Unity ID to activate the Editor) | Yes | `unity/Assets/TextMesh Pro`<br>`unity/Assets/Samples`<br>`unity/Assets/XRI`<br>`unity/Assets/XR` | All scenes (UI text, XR rig) | unity-package-manager |
| Project-owned models, textures, scripts, scenes and baked lighting | Project | ProcSense team | https://github.com/ruhank-dev/procsense-fyp | this commit | FBX, PNG, JPG, EXR, Unity YAML, C#, Blender .blend and Python | Project (academic FYP) | n/a | Yes (our own work; generated from CC0 sources where noted) | No | Yes | `unity/Assets`<br>`blender/sources` | All scenes | git |
<!-- END GENERATED:inventory -->

**Assets that need an individual manual step:** only the **Quaternius Universal Animation Library** has no stable direct link (the author's page); its FBX is in Git, so a clone already has it. If you ever need to replace it, download the pack from https://quaternius.com, put the zip in `build\external\manual\` and rerun the restore script. Everything else restores automatically.

**Characters and clothing in the current scenes**

| Role | Asset (all in `unity/Assets/Characters/Rocketbox`) | Where |
|---|---|---|
| Patient | `Medical_Male_04`, scrubs recoloured to hospital blue by `m162_body_color_patient.png`, mask/cap submesh hidden by `Hidden part.mat`, motion by `Assets/Scripts/Views/PatientMotion.cs` (breathing, blinking, gaze, head drift) | PatientBay |
| Waiting-area people | `Male_Adult_16`, `Female_Adult_14`, `Male_Child_01`, `Female_Adult_05`, `Female_Adult_08`, `Male_Adult_13`, `Female_Adult_15`, `Male_Adult_03` | Corridor |
| Medical staff | `Medical_Female_02`, `Medical_Male_03` (and `Medical_Male_01/05`, `Medical_Female_01`, `Business_Female_03`, `Female_Child_01`, `Male_Adult_08` available) | OT |
| Animation | Quaternius UAL clips, retargeted through Unity Humanoid (controllers in `unity/Assets/Animations/Characters`) | all |

---

## E. Complete setup instructions (the short path)

1. **Install Git and Git LFS** (section B, rows 1–2). Open **PowerShell** and check: `git --version` and `git lfs version`.
2. **Accept the GitHub collaborator invitation** if you were invited (https://github.com/ruhank-dev/procsense-fyp/invitations). The repository is public, so cloning needs no sign-in, but pushing does.
3. **Clone** (use a short path such as `C:\dev`; Windows has a 260-character path limit):
   ```powershell
   cd C:\dev
   git lfs install
   git clone https://github.com/ruhank-dev/procsense-fyp.git
   cd procsense-fyp
   git checkout main
   git lfs pull
   ```
   Then `git status` must say "nothing to commit, working tree clean".
4. **Submodules:** the repository has none; nothing to do.
5. **Install Unity Hub** and the **Editor 6000.5.10f1 with the Android and Windows modules** (section B, rows 3–4).
6. **Restore and verify the assets** (one command, safe to rerun):
   ```powershell
   powershell -ExecutionPolicy Bypass -File tools\setup\restore_environment.ps1
   ```
   It checks the tools, runs `git lfs pull`, and compares every tracked Unity/Blender file with the manifest by SHA-256 (about 30 seconds). It ends with `All manifest files verified. Environment restored.` and exit code 0. If it lists files, it restores them from Git; files you changed yourself are reported as "modified" and never overwritten (use `-Force` to discard your change).
   Useful options:
   | Option | What it does |
   |---|---|
   | `-VerifyOnly` | Only check, change nothing |
   | `-DownloadSources` | Download every original source (ambientCG, Poly Haven, Rocketbox, MakeHuman) into `build\external\cache` and check each against its checksum (several GB) |
   | `-Source acg_Metal011,ph_model_wall_clock` | Only those sources (ids are in the table of section D) |
   | `-WithBlender` | Install Blender 4.5.14 portable into `build\blender` |
   | `-ImportInUnity` | Run Unity once in batch mode: restores packages, imports every asset, compiles the C# (20–40 minutes the first time) and fails on any compile error |
   | `-Offline` | Skip every network step |
7. **Open the project:** Unity Hub → **Add → Add project from disk** → choose the `unity` folder inside the clone → open it with 6000.5.10f1. The first open imports about 4,000 files and restores packages (20–40 minutes). If Unity offers "Enter Safe Mode" because of a compile error, stop and see section G.
8. **Check the render pipeline:** *Edit → Project Settings → Graphics*: the Default Render Pipeline slot shows the URP asset (no "None"). *Window → Rendering → Lighting*: Lightmapping Settings show **Progressive GPU/CPU**, Mixed lighting **Shadowmask**.
9. **Check lighting and baked data:** open `Assets/Scenes/PatientBay.unity`. The folder `Assets/Scenes/PatientBay` must contain `Lightmap-0…7_comp_light.exr`, `LightingData.asset` and four `ReflectionProbe-*.exr` (real files, not 130-byte text pointers; section G if they are). The connected-room bake contains 8 lightmaps.
10. **Open the main scene:** menu **ProcSense → Open Patient Bay**. You should see the bay with the window, sunlight streaks on the floor, the clothed patient on the bed and the city outside.
11. **Run:** press **Play**. WASD moves through the simulator, right mouse looks. (Quest headset play through Link: `unity/README.md`.)
12. **Quest development** (Sannan): section F.
13. **Build:** section F.

---

## F. Running, testing and building

### Open each scene
| Scene | How |
|---|---|
| PatientBay | **ProcSense → Open Patient Bay** (opens `PatientBay` + `Sys_Core` + `DevOnly`) |
| Corridor (with the reception desk and waiting area) | Project window: `Assets/Scenes/Corridor.unity` (double-click). Add `Sys_Core` and `DevOnly` additively to play (right-click the scene → *Open Scene Additive*). |
| Operating Theatre | `Assets/Scenes/OT.unity`, same way |
| Whole app flow | open `Assets/Scenes/Boot.unity` and press Play (Boot loads the rooms in order, login screen first; the demo login is in `docs/USER_GUIDE.md`) |

### Automated tests
```powershell
task doctor            # tools present and versions
task ci                # everything GitHub runs (engine, backend, content, plans, repo rules); about 12-20 minutes, needs Docker running
task unity-test        # Unity EditMode + PlayMode in batch mode (close the Unity Editor first)
```
Results land in `artifacts/unity/EditMode.xml` and `PlayMode.xml`. The environment-specific checks (scene loading, materials, lighting) are listed in [qa/unity_environment_validation.md](qa/unity_environment_validation.md).

### Check that the look is right
1. PatientBay: the sunlit patches on the floor have window-frame shadows; the patient lies centred on the mattress and his chest moves; no pink materials; outside the window there is a city (not black or grey).
2. Corridor: long sunbeams from the right-hand door windows on the floor and opposite wall, soft shafts with floating dust in the air, people on the benches, plants, wheelchair, reception TV.
3. OT: two staff talking, lit theatre, equipment present.
4. Shafts and dust: on `main` they are hidden on Quest by the `DesktopOnly` component; builds from branch `quest-perf/d7-optimisations` (owner decision D7, 7 Nov 2026) show them on Quest in both eyes (DC-12, DC-13).
5. Identify missing assets: `restore_environment.ps1 -VerifyOnly` names every missing file; *Edit → Select All* does not; in the Console search `Missing` and `The referenced script`.

### Build for desktop
```powershell
"C:\Program Files\Unity\Hub\Editor\6000.5.10f1\Editor\Unity.exe" -batchmode -nographics -projectPath unity -executeMethod ProcSense.EditorTools.BuildScript.BuildDesktop -variant dev -logFile unity\Logs\build-desktop.log
```
Output: `artifacts\unity\desktop\ProcSense-dev.exe`. Close the Editor first.

### Build for Meta Quest 2 (Sannan)
1. Quest: enable Developer mode (Meta Horizon app), plug in USB, accept "Allow USB debugging" inside the headset. `adb devices` must list the headset as `device`.
2. Close the Unity Editor, then from the repository root: `task unity-build -- dev` (use `demo` or `study` for the release variants). Output `artifacts\unity\ProcSense-dev.apk` with `ProcSense-dev.apk.sha256` beside it.
3. Install and start: `adb install -r artifacts\unity\ProcSense-dev.apk`, then in the headset: Apps → Unknown Sources → ProcSense.
4. **After a Quest build the Editor stays on the Android target.** On Android the Editor imports textures and picks quality settings for the headset, so the desktop scenes look slightly darker (1 to 4 levels, ceilings up to 8) and captures, bakes and reviews no longer match a Windows clone. Switch back before you work on the desktop look: *File > Build Profiles > Windows > Switch Platform*, or run `Unity.exe -batchmode -quit -projectPath unity -executeMethod ProcSense.EditorTools.EnvironmentReimport.SwitchToDesktop`. (Measured: after switching back, the working project and a fresh clone rendered identically, mean difference 0.0 to 0.27 of 255.)
5. Quest 2 hints: Quest 2 has no passthrough dependency here; the shafts and dust are disabled on Android by design; check frame time with OVR Metrics; record every result in `docs/testing/results/<ID>.md` as described in [testing/SANNAN_EXTERNAL_TESTING.md](testing/SANNAN_EXTERNAL_TESTING.md).
6. Not verified by the author (no headset was available): frame rate, comfort, lighting appearance on the Quest 2 panel. These are open items for Sannan (item DC-12 in the testing file).

### Working in the shared project (Moiz and everyone)
* Work on your own branch, merge the latest `main` into it before pushing, never force-push ([TEAM_GIT_WORKFLOW.md](TEAM_GIT_WORKFLOW.md)).
* Commit `.meta` files together with their assets (Unity creates them next to every file); never delete a `.meta` by hand. Never commit `Library/`, `Temp/`, `Logs/`, `UserSettings/`.
* Scenes and prefabs are text YAML with a merge driver (`unityyamlmerge`); two people editing the **same scene** still conflict, so agree who edits which scene.
* Baked lighting (`Lightmap-*.exr`, `LightingData.asset`, reflection probes) is stored with Git LFS: after a rebake commit the whole scene folder together. A rebake of the three connected rooms takes about 11–25 minutes (`powershell -File unity\Tools\Rooms\light_cycle.ps1 -Tag mytag`). Do not commit a bake you did not mean to change. LFS has a free quota (1 GiB storage and bandwidth per month on GitHub Free): avoid committing repeated bakes.
* Models are built by Python/Blender scripts in `unity/Tools/*` (Blender 4.5.14, section B row 6). The generated `.blend` sources are kept in `blender/sources/`; exported FBX files are under `unity/Assets/Models`.
* New third-party asset? Add a record in `tools/setup/build_manifest.py` (`source_records`), run `python -I tools/setup/build_manifest.py`, commit the manifest and the two regenerated documents. `python -I tools/setup/build_manifest.py --check` fails when anything is out of date (it is part of `task check`).

### Blender
* Source files: `blender/sources/**/*.blend` (Blender 4.5.14). They use only procedural materials and texture files that are in the repository; no linked libraries (checked, see the validation report).
* Generators: `unity/Tools/Environment/*.py`, `unity/Tools/Equipment`, `unity/Tools/Floor`, `unity/Tools/MPFB`. Run one with `build\blender\blender-4.5.14-windows-x64\blender.exe --background --python <script>`; outputs go to `unity/Logs/<Name>/` (git-ignored) and the importers (`ProcSense →` menus) copy them into `Assets`.
* The character generator (MPFB) needs the four MakeHuman archives of `unity/Tools/MPFB/dependencies.json`: `restore_environment.ps1 -Source makehuman_mpfb` downloads and checks them. The 33 large generated staff `.blend` files (about 520 MB, Blender `build/mpfb/output`) are **not** kept in Git: they can be regenerated with `unity/Tools/MPFB/build_staff.py`; the Unity-ready FBX results are in `unity/Assets/Models/Characters`.

---

## G. Troubleshooting

| Symptom | Cause | Fix |
|---|---|---|
| Pink materials | URP shader missing or material uses a shader of another pipeline | Check *Project Settings → Graphics* shows the URP asset. Run `restore_environment.ps1 -VerifyOnly`; in the Console look for `shader` errors. Shaders are in `Assets/Shaders` (`SunShaft`, `SunDustMote`) and package URP 17.5.0. |
| `Missing script` / "referenced script is missing" | `.meta` file missing or changed (GUID changed) | `restore_environment.ps1` (restores `.meta`); never regenerate `.meta` by deleting files. |
| Missing textures / grey surfaces | LFS pointer or deleted file | Run the restore script; it names the files; or `git lfs pull`. |
| Lightmaps missing, rooms flat or black | LFS pointers instead of EXR data | `git lfs pull`, then the restore script. A file of about 130 bytes starting with `version https://git-lfs` is a pointer. |
| Incorrect lighting after opening | Lighting data not matched to the scene | Open the scene once, *Lighting window → Generate Lighting* only if the baked files are really missing. Do not rebake otherwise. |
| Broken prefabs, "Missing Prefab" | `Assets/Prefabs` or a model `.meta` changed | Restore script; `git checkout -- <file>`. |
| Missing packages / "Package Manager" red | No internet or offline registry | Open once with internet; `unity/Packages/packages-lock.json` pins every version. |
| Missing character models | Rocketbox folder missing | `restore_environment.ps1`; or `-Source rocketbox` + `python -I unity\Tools\Characters\fetch_rocketbox.py`. |
| Missing Blender linked textures | `.blend` references an absolute path | The committed sources use repository-relative or no external images; if Blender says "missing", use *File → External Data → Find Missing Files* and point to `unity/Assets/Textures`. |
| "This is a Git LFS pointer" / tiny asset files | `git lfs install` not run before cloning | `git lfs install` then `git lfs pull`. |
| First launch ends at once with "Failed to resolve packages: operation cancelled" (seen when a fresh clone is opened for the first time, in batch mode) | The package manager is cancelled while it starts (a one-time race) | Just start Unity again: the second launch resolves all 60 packages in about 35 seconds and imports normally. `restore_environment.ps1 -ImportInUnity` retries once by itself. |
| Desktop scenes look slightly darker or cooler than in a colleague's screenshot, or than yesterday | The Editor was left on the Android build target by a Quest build | Switch the platform back to Windows (File > Build Profiles), see section F "Build for Meta Quest 2", item 4 |
| Failed Unity imports, red errors at start | Editor version is not 6000.5.10f1 | Check the bottom-left of Unity Hub; install the exact version; delete `unity/Library` and reopen. |
| Android SDK problems / "SDK not found" | Android module not installed through the Hub | Hub → Installs → gear → Add modules → Android Build Support + OpenJDK + SDK & NDK Tools; in *Preferences → External Tools* tick the "installed with Unity" boxes. |
| Quest build error "OpenXR loader not started" | Android XR settings changed | *ProcSense → Project setup* (the build script says this itself). |
| Path too long on Windows | Clone path is deep | Clone into `C:\dev`; `git config --global core.longpaths true`. |
| `task ci` backend step fails | Docker not running | Start Docker Desktop. |
| Restore script says "modified" | You changed that file | Intentional; `-Force` restores the repository version. |

If something is still missing after the restore script says all files are verified, it is not a missing file: it is a Unity setting. Open an issue with the Console log.

---

## H. Verification status

See [qa/unity_environment_validation.md](qa/unity_environment_validation.md) for what was executed, with results, and what remains for the headset.
