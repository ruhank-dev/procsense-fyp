# Release checklist (Quest APK)

How a tagged build of ProcSense for the Meta Quest is made and recorded (p0-vr-platform-s07). One tag, one APK per variant, each with its SHA-256.

## Variants

| Variant | Define | Build | For |
|---|---|---|---|
| Dev | `PROCSENSE_DEV` | development build, profiler attachable | the team, profiling, the developer menus |
| Demo | `PROCSENSE_DEMO` | release | showing the simulator (supervisor, viva) |
| Study | `PROCSENSE_STUDY` | release | the evaluation sessions with participants |

The running build reports its variant through `ProcSense.VR.Boot.BuildVariant.Name`.

## Steps

1. **Start clean.** `main` is up to date and `git status` shows nothing. Close the Unity Editor (a batch build cannot open a project the Editor holds).
2. **Checks pass.** `task ci` (197 checks) and `task unity-test` (EditMode and PlayMode) both pass.
3. **Content is current.** `task content-refresh` changes nothing (the manifest matches the content that goes into StreamingAssets).
4. **Build each variant.** `task unity-build -- dev`, `task unity-build -- demo`, `task unity-build -- study`. Each writes `artifacts/unity/ProcSense-<variant>.apk` and `ProcSense-<variant>.apk.sha256`; the log is `artifacts/unity/build-<variant>.log`. A build that fails exits non-zero (T-Q3).
5. **Performance gate on the headset (Demo build).** Install with `adb install -r artifacts/unity/ProcSense-demo.apk`, run the fixed route in [the perf procedure](plans/p0/p0-vr-platform/perf/README.md) with OVR Metrics, and keep the CSV. Pass: average at least 72 fps, no frame over 50 ms, memory at most 2.5 GB (T-Q1, T-Q4).
6. **Tag.** `git tag -a vX.Y.Z -m "<what changed>"` on the commit the APKs were built from, then `git push origin vX.Y.Z`.
7. **Record the release.** Add a row below: the tag, the commit, each APK's SHA-256, the perf CSV, who checked it on the Quest.

## Releases

| Tag | Commit | Variant | SHA-256 | Perf capture | Checked on the Quest by |
|---|---|---|---|---|---|
| (none yet) | | | | | |

## Not automated yet

- Unity tests in GitHub Actions: the free runners have no Unity licence. GameCI's personal-licence activation has not been tried; until it is, `task unity-test` is the local check before a hand-off that touches `unity/`.
- The weekly OVR Metrics capture is done by hand on the team's Quest.
