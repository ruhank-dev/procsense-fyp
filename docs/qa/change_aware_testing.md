# Change-aware testing (permanent)

One question before any check: **what changed, and which checks can it affect?** Nothing else runs, and a valid earlier pass is reused. Built 10 Nov 2026 at the owner's request; the code is in `tools/ci/`.

## Commands
| Command | What it does |
|---|---|
| `task change-select` | Prints the verdict per check (RUN / REUSE / skip). `-- --base origin/main --json` prints the exact object the GitHub workflow reads. |
| `task change-check` | Runs the selected checks, reuses valid passes, reports executed / reused / skipped / deferred. `-- --heavy` adds the heavy Unity checks, `-- --only a,b` picks checks, `-- --dry-run` only plans. |
| `task change-check-final` | The complete gate: every check, **cache ignored**, heavy Unity checks included. |
| `uv run --project tools tools/ci/test_changeaware.py` | The selector and cache tests (19; no Unity; about 1 s). |

`task check` already exists and means the repository-rule checks, so the new tasks are named `change-*`.

## How change detection works
`tools/ci/changeaware.py` asks Git: the working tree against HEAD (modified, staged, deleted, and untracked files that are not ignored). `--base REF` compares `REF...HEAD`, plus the working tree unless `--head` is given (what a push or pull request shows GitHub). `--staged` is the index only. Each changed path is matched against `tools/ci/rules.yml`:

1. It matches `full_triggers` (runner, Taskfile, `.github/**`, hooks, global tool config, the Unity version): **everything runs**.
2. It matches `ignore` (artifacts, logs, generated results): nothing runs.
3. It matches a check's `inputs`: that check runs (a `.meta` file counts as the asset it describes).
4. It matches nothing: **unmapped path, everything runs** (safe default, and the reason says so).
5. Any error while selecting: everything runs.

Shared Unity inputs are listed once (YAML anchors `unity_shared`, `unity_render`), so a change to project settings, packages, asmdefs, URP/XR assets or the test infrastructure selects every Unity check. A needlessly broad input costs one run; a missing input would cost a stale pass, so the rules lean broad.

| Change | Checks selected |
|---|---|
| Documentation only | `docs_check` |
| Planning docs and context | `docs_check`, `plans_check`, `python_tests` |
| Sun shader or materials | `unity_compile`, `unity_editmode`, `unity_shader_visual`, plus the heavy `unity_walkthrough_diff`, `benchmark_stereo` |
| Character scripts, prefabs, animation | `unity_compile`, `unity_editmode`, `unity_playmode_character` |
| City culling or city assets | `unity_compile`, `unity_playmode_exterior`, heavy `unity_walkthrough_diff`, `unity_sweep` |
| Quest runtime settings or XR | `unity_playmode_runtime_settings` plus the shared Unity checks |
| Sun shaft shader, noise texture or shaft host | `unity_playmode_sunshafts` (DT-311) plus `unity_editmode` (`SunShaftContractTests`) |
| Game flow panel (Flow, results rewording client) | `unity_playmode_flow` (MenuFlowTests, ExamPresentationTests) plus the shared Unity checks |
| Patient equipment (ActionEmitters, sockets, equipment prefabs, character culler, patient idle) | `unity_playmode_patient_equipment` (SensorAttachmentFlowTests, MpfbPatientTests, EquipmentDropTests) plus the shared Unity checks |
| Clinical text panels (UiKit, Accessibility, monitor, tablet, EMR, menu) | `unity_playmode_clinical_text` (ClinicalTextTests, AccessibilityTests) plus the shared Unity checks |
| Quest render settings (ProjectSettings, URP/XR assets, scenes, textures, Quest profile scripts) | `quest_config_check` (`tools/unity/verify_quest_config.py`, source level, seconds) plus the Unity checks those paths select |
| Unity project settings or packages | every Unity check |
| Backend (`server/**`) | `backend_tests` |
| Rules engine (`src/**`, `tests/**`) | `rules_engine_tests`, `dotnet_format`, `backend_tests`, `python_tests` |
| `tools/ci/**`, Taskfile, `.github/**` | everything |

## What is cached and what invalidates it
A result is stored only for a **pass**. Failures and informational benchmarks are never reused. The store is `D:/ProcSenseWork/ci_cache` (override `PROCSENSE_CI_CACHE`; fallback `tools/ci/cache/`, gitignored). The key is the sha256 of:
- the check definition (commands, Unity filter and environment, failure pattern, salt) and the result-schema version;
- the version of each tool the check declares (dotnet, uv, python, docker, the Unity editor);
- the **path and content id of every file matching the check's inputs** (Git blob id for clean files, sha256 of the bytes for modified or untracked ones).

So an unrelated change keeps the key (reuse). A changed input file, a new input file, a changed command or a tool upgrade changes it (re-run). `dependency_audit` also expires after 24 hours, because advisories change without any file changing. `--final` never reads the cache.

Not cached by this system, listed so nothing is assumed: the imported Unity assets and shader compilation outputs (Unity keeps its own `unity/Library`, which stays on disk and is reused by every Unity run), benchmark timings (informational, always re-measured), and per-frame reference images. The `unity_shader_visual` check writes its frames to `D:/ProcSenseWork/frames/ci_shader_visual`; automatic comparison against the previous render is a follow-up for the video-QA lane (`docs/qa/quest2_final_verification.md`).

## Unity checks
They run through `tools/ci/unity_check.py`: one Unity at a time (atomic `D:/ProcSenseWork/unity.lock`, waits up to 4 hours), hidden window (never steals focus), temp and Gradle on D:, stale results deleted first. A missing result file, or zero passed tests, is a **failure**. Heavy checks (`unity_walkthrough_diff`, `unity_sweep`, `benchmark_stereo`, `android_build`) are reported as *deferred* unless `--heavy` or `--final`, and are never reported as passed.

**Capture and diagnostic tools are not regression tests** (DT-307, 15 Nov 2026 UTC): PlayMode fixtures that only write pictures or logs (`B58VisualCapture`, `TextureCropProbe`, `OverhaulVideoCapture`, and the other `[Explicit]` capture tools) carry `[Explicit("capture tool: ...")]`, and the two probes also `[Category("Diagnostics")]`, so a full PlayMode run lists them as skipped. Run one by naming it, e.g. `python tools/ci/unity_check.py tests --platform PlayMode --filter ProcSense.Tests.PlayMode.TextureCropProbe --env TEXPROBE_DIR=D:/ProcSenseWork/reports/texprobe --env TEXPROBE_TAG=U --env TEXPROBE_SELECT=1` (or `tools/unity/texture_probe.ps1`; `B58VisualCapture` takes `B58V_DIR`, `B58V_CONFIGS`, `B58V_POSES`, see `docs/qa/audit/benchmark_requests.md`). A missing required variable fails at once with a message naming it. Every fixture that loads Boot or a room cleans up in its `[UnityTearDown]` (or starts with `TestHostHygiene.ResetToEmptyScene()`), which unloads every scene but the runner's and destroys the persistent rig, host, camera and event system, so no fixture depends on the order of the run.

## What always runs at final integration
The complete GitHub Actions workflow (all jobs, nothing skipped), `task change-check-final` locally for the Unity gates the free runners cannot run (GitHub-hosted runners have no Unity licence here), and the unchanged mandatory pre-push hook (`task ci`). A reused development result is never the final gate. `docs/qa/ci_hook_proposal.md` describes the one safe way to avoid running the same hour twice; it needs approval and is not applied.

## Adding a check
Add it under `checks:` in `tools/ci/rules.yml` with `job`, `cmds` (or `pwsh` or `unity`), `tools`, `group` (checks in one group run one after another, groups run in parallel) and `inputs`. `test_changeaware.py` fails if a check has no inputs or no command.

## Measured
`repo_checks.py` (the `docs_check` command) took 111 s; the same check on unchanged inputs is reused in 2 s. Selecting takes about 2.5 s on this repository (7,876 tracked files). The first real run also caught a malformed deferred-task row (DT-287, 5 columns instead of 6) that `task ci` would have failed on; it is fixed.
