# Unity environment validation report

Run on 3 and 4 Nov 2026 (UTC) on the owner's Windows 11 machine (Unity 6000.5.10f1, Blender 4.5.14, Git 2.54, Git LFS 3.7.1). Companion: [../new_unity_environment.md](../new_unity_environment.md) (the setup guide), [../collaborator_setup_message.md](../collaborator_setup_message.md) (the WhatsApp copy), [../testing/SANNAN_EXTERNAL_TESTING.md](../testing/SANNAN_EXTERNAL_TESTING.md) (headset items).
Only what was actually executed is listed as passed. Anything not run, or run only in part, says so.

**Branch:** developed on `overhaul/urp-integration`, then fast-forwarded into `main` on 4 Nov 2026 (no merge commit, no force-push) on https://github.com/ruhank-dev/procsense-fyp (the repository is public and stays public).
**Last code and manifest commit that every result below refers to:** `5b564331` ("Manifest: hash .obj and .mtl with LF endings like the other text files"). Commits after it only add documentation (this report, the collaborator message); the GitHub run on the final commit is listed in section 7.

## 1. Summary

| Area | Result |
|---|---|
| Local `task ci` (202 checks: build, 1,369 engine tests, golden runs, backend on PostgreSQL, content, plans, project rules, secret scan, pre-push hook) | **202 / 202 passed** (run 5, tree `5b564331`) |
| Unity EditMode, working project | **204 / 204 passed** |
| Unity PlayMode, working project | Last full run 246 tests: 238 passed, 7 skipped on purpose, **1 failed** (a locomotion fixture); the cause was found and fixed, the fixed class then passed 4 / 4. The full suite was **not** re-run in the working project after that last fix (see the clean clone for a complete green run) |
| Unity EditMode, fresh clone | **204 / 204 passed** (commit `39a4f8c0`) |
| Unity PlayMode, fresh clone | **246 tests: 239 passed, 0 failed, 7 skipped on purpose** (commit `39a4f8c0`) |
| Restore script on the fresh clone | **4,059 / 4,059 files match the manifest** ("All manifest files verified", commit `5b564331`) |
| Blender sources | **53 / 53 open, no linked library, no missing image, export to FBX** (one non-project file removed, see 4) |
| Desktop development build (Windows x64) | **Succeeded**, 0 errors, 1.40 GB; the player ran 50 s with no exception in its log (launch smoke test only) |
| Quest development APK | **Succeeded**, 493,771,980 bytes, SHA-256 `1d8af6a8fd06734dca9ddd4f4a489eb37da4fe062121db0220c26efe3da2f8f5`, valid archive. **Not run on a headset.** |
| GitHub Actions `CI` | Runs on commits `d3b9361d` and `d8ba367b` both **success** (5 of 5 jobs each); final run in section 7 |
| Visual comparison working project vs fresh clone | 16 fixed-camera views: **identical within render noise** (mean difference 0.0 to 0.27 of 255, PSNR 36 to 99 dB) once the working project is on the Windows desktop target; an earlier 1 to 8 level darker clone was traced to the working project being left on the Android target by the Quest build (section 6) |
| Headset checks | **Not done**, assigned to Sannan (item DC-12) |

## 2. Local checks (`task doctor`, `task ci`)

* `task doctor`: all required tools present (Git 2.54, .NET 9.0.318, uv 0.12.22, Task 3.54, Git LFS 3.7.1, Unity 6000.5.10f1 with Android support, Blender 4.5.14, adb). Docker has to be started by hand.
* `task ci` was run five times. What the early runs found and what was done about it:
  * Run 1 (86 passed, 8 failed): `content/manifest.json` and `docs/research/review_pack.md` were stale after an earlier change to the attributions register (breaking replay, verify, report and several unit tests). Fixed by regenerating both.
  * Runs 2 and 3: 23 files above the 10 MB plain-git limit (527 MB). Six needed files moved to Git LFS (animation FBX, two sky HDRs, two wood floor textures, one curtain texture, one rubber texture); 17 diagnostic environment maps used only by one Editor tool (`MaterialParityTest`) are no longer tracked (they stay on disk, git-ignored). Planning pages (step audit, register triage, engineering audit, execution queue, external testing page), the docs index rows, the prompt-history entry format, one deferred-register row with an extra pipe and a name rule were corrected.
  * Timing tests (`PerformanceTests`, `JsonLogicTests`) failed while Docker was starting and other checks ran in parallel; they pass alone (41 / 41) and in the final full run.
  * Run 5 (final tree): **202 / 202**.

## 3. Unity verification

Commands: `task unity-test` (EditMode then PlayMode in batch mode) and `tools/setup/run_unity_tests.ps1` for subsets. The runner now deletes the old result file first; before that it printed the counts of an old result file when Unity failed to compile (a first "143 and 158 passed" report was therefore not a real run and was discarded).

New tests for the handoff (`unity/Assets/Tests/EditMode/EnvironmentIntegrityTests.cs`): every scene in the build opens with no missing script, missing prefab, mesh without mesh, null material or unsupported (pink) shader; PatientBay, Corridor and OT have baked lightmaps (real textures, not LFS pointers), light probes, reflection-probe data and the URP asset; the 17 Rocketbox prefabs have a humanoid avatar, skinned mesh and materials and the six animation controllers exist; every GUID a material references resolves to an asset; the two custom shaders (`ProcSense/Sun Shaft`, `ProcSense/Sun Dust Mote`) compile.

Failures found in the real runs and what they were:

| Failure | Cause | Action |
|---|---|---|
| 3 login tests, 4 fixture classes | Rooms now load more slowly (Rocketbox people, eight lightmaps); fixtures waited a fixed 30 to 60 frames or 10 s | Fixtures wait for the host, the room and a settled scene set |
| Corridor "more than one Floor" | The sunlit rooms behind the door glass reused the names "Floor" and "Ceiling" | Renamed ("Sunroom floor", "Sunroom ceiling") in the generator and the scene |
| `NeutralDaylightContractTests` (3), `MonitorMaterialContractTests` (2) | Contracts predate this session's approved changes (24 degree bay sun, 2.08 m window, mask-driven monitor plastics, preview bake settings) | Contracts updated with comments; bake settings met by running the final-quality bake (70 min, 8 lightmaps) |
| Login test leaving a second `Sys_Core` and camera behind | The previous test ended while the boot loader was still adding scenes | Tests wait until the loaded scene set is stable for 40 frames |
| `Left_stick_...` locomotion test | A room service restored the controller's input action during the test, cancelling the simulated stick; it also assumed the rig faces open floor | The test keeps the manual stick value applied and faces a clear direction |

Skipped (7, by design, not failures): `LiveServiceTests` needs a live backend and three environment variables (`PROCSENSE_E2E_URL`, `_EMAIL`, `_PASSWORD`); six `TabletShellTests` cases are clinical cases not yet approved for practice ("clinical sign-off pending").

Not covered by automatic tests and therefore not claimed: the look of volumetric shafts and dust (checked in screenshots and clips only), characters' animation quality, gameplay on a headset.

## 4. Blender

`blender/sources/**/*.blend` (53 files, 45 MB; made by the generators in `unity/Tools/*`) were opened with Blender 4.5.14 by `tools/setup/validate_blender.py`: no linked libraries, no missing external images, each exported to FBX with the project's Unity settings (-Z forward, Y up). Result file: [blender_validation.json](blender_validation.json). One extra file (`WoodFloor040_4K-JPG.blend`, the material file shipped inside an ambientCG download, referencing JPGs that are not kept) was removed from `blender/sources`.
The 33 generated staff `.blend` files of the MPFB pipeline (about 520 MB) are **not** in Git (size); they are regenerated by `unity/Tools/MPFB/build_staff.py`, and their Unity-ready FBX results are in `unity/Assets/Models/Characters`. Character rigs in Unity were checked by the integrity test above and by PlayMode (`MpfbPatientTests`, `PatientIdleTests`, `GownPoseTests`), not by re-opening the rig in Blender. Model scale and materials were checked indirectly (FBX export succeeded; the scenes load and render in the comparison below); no per-model numeric scale check was run.

## 5. Builds

| Build | Command | Result |
|---|---|---|
| Windows desktop, development | `Unity.exe -batchmode -nographics -projectPath unity -executeMethod ProcSense.EditorTools.BuildScript.BuildDesktop -variant dev` | Succeeded, 0 errors, 1,403,724,362 bytes (22,528 shader variants, about 55 min). Started the player for 50 s: no crash, no exception in `unity/Logs/desktop_run.log`. |
| Quest APK, development | `task unity-build -- dev` | Succeeded (Bee result "Success"; one IL2CPP warning about a TextMeshPro static constructor is counted as "errors=1" by the script, it is not a build failure). 493,771,980 bytes, SHA-256 matches the `.sha256` file, archive tests clean, contains `AndroidManifest.xml`, dex code and `libil2cpp`. 45,056 shader variants, about 35 min. |
| Desktop release, Quest demo and study APKs | not run | `BuildDesktop -variant demo` and `task unity-build -- demo|study` exist but were not run in this handoff |

The Quest APK has **not been installed or run on a headset**. Frame rate, comfort and appearance on the Quest 2 are open items in [../testing/SANNAN_EXTERNAL_TESTING.md](../testing/SANNAN_EXTERNAL_TESTING.md) (item DC-12).

## 6. Clean-clone test and visual comparison

Procedure (exactly the documented one): `git clone --branch overhaul/urp-integration https://github.com/ruhank-dev/procsense-fyp.git C:\pstest` (a new folder, nothing copied from the working project), LFS objects downloaded by the clone (26 LFS files, 291 MB), `tools\setup\restore_environment.ps1`, Unity opened with 6000.5.10f1.

* First launch of the fresh clone ended after a few seconds with "Failed to resolve packages: operation cancelled" (the package manager is cancelled while it starts). The second launch resolved all 60 packages in 36 s and imported everything. The guide documents this and `restore_environment.ps1 -ImportInUnity` retries once.
* After the first import Unity rewrote one tracked file in the clone (`UniversalRenderPipelineGlobalSettings.asset`, an emptied runtime-settings list). With the committed version restored, the renders were unchanged, so it is not the cause of the residual below; Unity did not rewrite it a second time.
* File comparison of `Assets`, `ProjectSettings` and `Packages` between the working project and the clone (`tools/setup/diff_trees.py`): identical, except one line-ending difference in the hidden `patient_pillow.obj` (hashes are now taken with LF endings) and files that are on purpose only in the working project (17 git-ignored diagnostic maps, generated `StreamingAssets`, two empty test scenes).
* Same-camera stills (`unity/Tools/Rooms/env_captures.ps1`: 5 PatientBay, 7 Corridor, 4 OT views, 1400 px) in both projects, compared with `tools/setup/compare_captures.py` (sheets and metrics in [env_compare/](env_compare/)):

| | Mean difference (0 to 255) | Pixels changed by more than 24 levels | PSNR |
|---|---|---|---|
| Working project vs itself (second capture) | 0.0 to 0.25 | 0.0 to 0.36 % | 37 to 99 dB |
| Working project (Android target left by the Quest build) vs fresh clone | 1.7 to 4.5 (OT ceiling 7.9), the clone consistently darker | 0.0 to 0.66 % | 29 to 40 dB |
| **Working project (Windows desktop target) vs fresh clone** | **0.0 to 0.27** | **0.0 to 0.38 %** | **36 to 99 dB** |

  **Root cause found.** The first comparison showed the fresh clone slightly darker everywhere (1 to 4 levels, ceilings up to 8, a little cooler), while the working project differed from itself by almost nothing. All files, settings and packages were identical, and a forced reimport of the lighting data changed nothing. The working project's `Library/EditorUserBuildSettings.asset` showed the **active build target Android**: the Quest build (`BuildScript.BuildQuest`) switches the Editor to Android and leaves it there, and on Android the Editor imports textures and chooses quality settings for the headset. The fresh clone was still on Windows desktop. After `EnvironmentReimport.SwitchToDesktop` put the working project back on Windows, the same views differ by 0.0 to 0.27 levels (signed mean within 0.15), the same as run-to-run noise, and [env_compare/final_*](env_compare/) holds the sheets. Lesson for the team: after a Quest build, switch the Editor back to Windows (File > Build Profiles > Windows, or the menu method) before looking at, capturing or baking the desktop look. ChatGPT's image analysis of the first four sheets (`unity/Logs/QA/EnvCompare/round_01`) found the same geometry, placement, exterior and lighting direction and two minor items: one character posed slightly differently (people animate continuously and the captures were taken at different moments) and low-level shading differences, which the Android target explains. The earlier signed-difference tool is `tools/setup/signed_diff.py`.

## 7. GitHub Actions

Workflows in `.github/workflows`: `ci.yml` (jobs: Dependency audit, Engine on Ubuntu, Engine on Windows, Content plans and project rules, Backend FastAPI on PostgreSQL; supports manual dispatch), `main-guard.yml`, `scheduled-jobs.yml`, `monitor.yml`, `backup.yml`, `deploy.yml`, `publish-image.yml`, `release.yml`.
`ci.yml` only runs by itself on pushes to `main`, branches `myt1x54/**` and pull requests, so it was started by hand with `gh workflow run ci.yml --ref overhaul/urp-integration` (no pull request was opened, `main` was not touched):

| Run | Commit | Result |
|---|---|---|
| [41685743581](https://github.com/ruhank-dev/procsense-fyp/actions/runs/41685743581) | `d3b9361d` | success, 5 / 5 jobs |
| [41733830580](https://github.com/ruhank-dev/procsense-fyp/actions/runs/41733830580) | `d8ba367b` | success |
| final run on the last commit of the branch | see the run list at https://github.com/ruhank-dev/procsense-fyp/actions/workflows/ci.yml | recorded in the hand-over message |

Not run, with the reason: `deploy.yml`, `publish-image.yml` and `release.yml` need release tags, server credentials or the container registry and were not triggered; `backup.yml`, `monitor.yml` and `scheduled-jobs.yml` run on a schedule and their latest scheduled runs on `main` were green. GitHub's Git LFS quota (1 GiB storage, 1 GiB bandwidth per month on the free plan) could not be read with this account's token (the billing endpoint returns 404); about 0.4 GB of new LFS objects were uploaded in this handoff and the pushes were accepted.

## 8. Not done and open items

* Everything on the Quest 2 headset (item DC-12 and the existing headset items), for Sannan.
* Full PlayMode suite of the working project after the last test-fixture fix (the fresh clone has a complete green run of the same code).
* Desktop release build, Quest demo and study builds.
* The hidden old MPFB patient rig stays in the bay (invisible) because the sensors and idle hooks use it; 13 of the tests that touch it needed that to stay.
* Dust motes of the soft light shafts are not fully confined to the beams (cosmetic, desktop only).
* Public repository: only CC0, MIT and project-owned assets are in it; there is no Mixamo, Asset Store or BlenderKit content. The Quaternius UAL pack has no stable direct download link; its FBX is in Git.
