# ADR-0030 — Drive the open Unity Editor through Unity's own CLI and Pipeline package, with editor scripts for anything repeatable

**Date:** 28 October 2026 · **Status:** Proposed · **Related:** EXP-VR-4, p0-vr-platform-s08

## Context
The developer works through a coding assistant that cannot see or click the Unity Editor. p0-vr-platform-s08 asked whether an Editor bridge is dependable enough to use (EXP-VR-4: five real tasks, adopt at 4 of 5). The plan named a community MCP server (CoplayDev `unity-mcp`, unverified). Instead, Unity's own command-line tool (`unity`, 1.0.0-beta.14) and its Pipeline package (`com.unity.pipeline` 0.8.0-exp.1, a local HTTP server on port 7800 inside the Editor) were installed on 27 October 2026 and used for the real work of the Unity steps built on 27 and 28 October (vr-interaction-ui s05, s08, s09; patient-records-emr s02, s03; vr-platform s06, s07; voice-audio s03; diagnostic-transitions s06). The results are in the p0-vr-platform README (EXP-VR-4).

## Decision
Use the Unity CLI and the Pipeline package as the Editor bridge: `unity command eval` for C# run inside the Editor (asset refresh, prefab edits, checks), `unity command run_tests` and `test_status` for EditMode and PlayMode runs, `unity open` to start the Editor. Anything that must be repeatable (scene and prefab set-up) stays an editor script under `unity/Assets/Editor`, and a bridge edit to an asset is committed like any other change. Before a bridge edit the git tree is clean, so any change can be reverted. The community `unity-mcp` server is not installed.

## Alternatives
- Editor scripts and screenshots only: works, but every check waits on a person running a menu and sending a picture.
- CoplayDev `unity-mcp`: unverified third-party code with write access to the project; not needed once Unity's own bridge worked.
- Batch mode only (`-batchmode -runTests`): reliable, but cannot run while the Editor holds the project open, and each run starts Unity from cold.

## Consequences
Tests, compilation checks and small asset edits no longer need a person at the Editor. Known limits: a request times out when the Editor's main thread is busy or throttled in the background (retry after a short wait); a modal dialog in the Editor (the API Updater, Safe Mode) blocks every request until a person closes it; the Pipeline package is experimental (0.8.0-exp.1) and may change.

## Revisit if
The Pipeline package changes its interface or is withdrawn, more than one task in five fails in a week of use, or a bridge edit corrupts a scene or prefab.
