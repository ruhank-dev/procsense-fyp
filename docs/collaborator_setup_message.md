# Collaborator setup message (WhatsApp copy for Sannan and Moiz)

Written 4 Nov 2026 (UTC). This is the short, copy-and-send version of [new_unity_environment.md](new_unity_environment.md). Every command, version, path and link below was run or checked during the handoff test; the test results are in [qa/unity_environment_validation.md](qa/unity_environment_validation.md). Paste everything between the two lines into WhatsApp.

---

*ProcSense: how to get my exact Unity environment (Sannan and Moiz)*

Hi both. Everything is on GitHub, nothing needs to be sent to you by hand. Follow these steps in order. I tested the whole thing myself on a brand-new clone of the repo.

*Repo:* https://github.com/ruhank-dev/procsense-fyp
*Branch with the new environment:* `overhaul/urp-integration` (not main yet)
*Full guide:* https://github.com/ruhank-dev/procsense-fyp/blob/overhaul/urp-integration/docs/new_unity_environment.md
*Licences and where every asset came from:* https://github.com/ruhank-dev/procsense-fyp/blob/overhaul/urp-integration/docs/research/asset_sources_and_licenses.md

*1. Install these first (Windows 11)*
• Git for Windows (includes Git LFS): https://git-scm.com/download/win
• Unity Hub: https://unity.com/download (make a free Unity account)
• Unity Editor *6000.5.10f1* exactly (not a newer one). Open this link and the Hub installs it: unityhub://6000.5.10f1/3bd4f66ad299
  Tick these modules: *Android Build Support* (with OpenJDK and Android SDK and NDK Tools) and *Windows Build Support (Mono)*. Do not install a separate Java or Android SDK.
• Optional: Blender 4.5.14 (only to open the .blend files): https://www.blender.org/download/releases/4-5/ (the restore script in step 3 can install it for you)
• Optional: Python 3.11+ https://www.python.org/downloads/windows/ and ffmpeg (`winget install Gyan.FFmpeg`)
• Sannan only: adb `winget install Google.PlatformTools`, and the Meta Quest Developer Hub https://developers.meta.com/horizon/downloads/package/oculus-developer-hub-win/
• Moiz only, if you want to run the full checks: .NET 9 SDK, uv, Task, Docker Desktop (links are in section B of the guide)

Check Git works: open PowerShell and run
```
git --version
git lfs version
```

*2. Download the project (use a short folder like C:\dev, Windows hates long paths)*
```
mkdir C:\dev
cd C:\dev
git lfs install
git clone --branch overhaul/urp-integration https://github.com/ruhank-dev/procsense-fyp.git
cd procsense-fyp
git lfs pull
git status
```
`git status` must say "nothing to commit, working tree clean". The clone is about 2.5 GB, keep 25 GB free for Unity.

*3. Restore and check all the assets (one command)*
```
powershell -ExecutionPolicy Bypass -File tools\setup\restore_environment.ps1
```
It checks all 4,059 Unity and Blender files one by one (SHA-256) against a list and fixes anything missing from Git. It must end with: *All manifest files verified. Environment restored.* Useful extras:
• `-WithBlender` installs Blender 4.5.14 into the project folder
• `-DownloadSources` re-downloads the original CC0/MIT downloads (characters, textures, HDRIs) from their original sites and checks their checksums (several GB, only needed if you want the originals)
• `-ImportInUnity` opens Unity once in the background so it imports everything and compiles the code

There is *no manual download needed*: all models, the clothed characters (Microsoft Rocketbox, MIT), animations (Quaternius, CC0), textures (Poly Haven and ambientCG, CC0), HDRIs, shaders and Blender files are in the repo. The only things you download yourself are the programs in step 1.

*4. Open it in Unity*
Unity Hub -> Add -> *Add project from disk* -> pick the *`unity`* folder inside the repo (not the repo root) -> open with 6000.5.10f1.
The first open takes 10 to 40 minutes (it downloads 60 packages and imports about 4,000 files). If the first launch stops with "Failed to resolve packages: operation cancelled", just open the project again: that is a known one-time start-up glitch and the second open works.
Then in the menu bar: *ProcSense -> Open Patient Bay*, and press *Play*. WASD moves, right mouse looks. Other scenes: Assets/Scenes/Corridor.unity (it includes the reception and waiting area) and Assets/Scenes/OT.unity. Start the whole app from Assets/Scenes/Boot.unity.

*5. How to check that it looks right*
• Patient bay: sun streaks with window-frame shadows on the floor, the patient in blue scrubs lying in the middle of the bed, his chest moving, a city outside the window, no pink materials.
• Corridor: long sun beams from the door windows, people on the benches, plants, wheelchair, reception TV.
• Operating theatre: two staff talking, theatre equipment.
• The soft light shafts and floating dust only show on PC, never on Quest (on purpose).
• Run the tests: Window -> General -> *Test Runner*. EditMode: all 204 should pass. PlayMode: 246 tests, 239 pass and 7 are skipped on purpose (1 needs a live server login, 6 are clinical cases that are not signed off yet), 0 should fail.
This is exactly what I got in a fresh clone: EditMode 204 of 204, PlayMode 239 passed, 0 failed, 7 skipped.
Honest note: screenshots from the fresh clone and from my machine match (every pixel difference is small, about 1 to 3 shades out of 255). I could not find the cause of that tiny difference, it is not a missing file.

*6. Sannan: Quest 2*
1. Quest: Meta Horizon phone app -> Devices -> your headset -> Developer mode ON. Plug in USB and accept "Allow USB debugging" inside the headset. `adb devices` must show your headset as `device`.
2. Close Unity. Build the Dev APK either from the Unity menu *ProcSense -> Build -> Dev*, or from the repo folder with `task unity-build -- dev` (needs Task, uv and Python). The first build compiles about 45,000 shader variants and takes 30 to 60 minutes.
3. The file is `artifacts\unity\ProcSense-dev.apk` (a `.sha256` file is next to it).
4. Install and run:
```
adb install -r artifacts\unity\ProcSense-dev.apk
```
Then in the headset: Apps -> Unknown Sources -> ProcSense.
5. Please test and write down: frame rate (OVR Metrics Tool), pink or missing materials, flat or black rooms (missing lightmaps), the patient breathing and blinking, comfort. The checklist is item *DC-12* in docs/testing/SANNAN_EXTERNAL_TESTING.md. Results go in docs/testing/results/DC-12.md.
I built the APK and checked it is a valid file, but *I did not test it on a headset*: that part is yours.

*7. Moiz: working in the shared project*
• Work on your own branch from `overhaul/urp-integration`, merge the latest into it before pushing, never force-push (docs/TEAM_GIT_WORKFLOW.md). Run `task setup` once, then `task ci` before pushing.
• Never commit Library, Temp, Logs or UserSettings. Always commit the `.meta` file together with its asset, never delete a `.meta` by hand (it holds the GUID that scenes use).
• Two people must not edit the same scene at the same time (Corridor, PatientBay, OT). Tell the group which scene you are editing.
• Baked lighting (Lightmap-*.exr, LightingData.asset) is in Git LFS and the free LFS quota is small: do not rebake unless you really change lighting. A rebake of the three rooms takes about 70 minutes at final quality.
• Added a new third-party asset? Add it to `tools/setup/build_manifest.py` (function `source_records`), run `python -I tools/setup/build_manifest.py` and commit the changed files. `python -I tools/setup/build_manifest.py --check` tells you if anything is out of date.
• Models come from Python/Blender scripts in `unity/Tools/*` (Blender 4.5.14); the Blender source files are in `blender/sources`.

*8. If something is missing or broken*
• Pink materials / missing textures / missing scripts / grey rooms: run the restore script from step 3 again, it names the exact files and restores them from Git. Then `git lfs pull`.
• A file of about 130 bytes that starts with `version https://git-lfs` is an LFS placeholder: run `git lfs install` and then `git lfs pull`.
• Unity says wrong version / red errors at start: check you use exactly 6000.5.10f1, close Unity, delete the folder `unity\Library`, open again.
• "Failed to resolve packages": open the project again (needs internet the first time).
• Android SDK problems: Unity Hub -> Installs -> the gear on 6000.5.10f1 -> Add modules -> Android Build Support with OpenJDK and SDK and NDK Tools.
• Anything else: section G of the guide has the full list.

*What I tested myself on the fresh clone (4 Nov 2026):* restore script all 4,059 files verified; Unity import; all scenes load; EditMode 204/204; PlayMode 239 passed, 0 failed, 7 skipped on purpose; Windows development build and Quest Dev APK both build; GitHub CI green. *Not tested:* anything on a real Quest headset.

---

(End of the message. Branch, commit and test numbers are repeated in docs/qa/unity_environment_validation.md.)
