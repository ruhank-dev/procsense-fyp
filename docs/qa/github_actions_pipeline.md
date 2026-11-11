# GitHub Actions pipeline (9 November 2026, UTC)

Status: **prepared and validated locally, not pushed, no run triggered.** Validation: `actionlint` clean on every workflow, `check-jsonschema` (vendor.github-workflows) ok, YAML parses. The selector `tools/ci/select_checks.py` (change-aware testing, `tools/ci/`) runs through `uv run --project tools` (it needs PyYAML) with `--no-cache` on GitHub; if it is missing or fails every job runs, so nothing is ever hidden. Real-run against temporary commits on a throw-away worktree: docs-only -> only `repo_checks`; a `server/` file -> only `server`; a shader -> `unity_compile`/`unity_tests`/`android_build` only; a `.github/**` change -> everything.

## 1. Audit: what runs remotely and what only locally

The repository is public, so standard GitHub-hosted runners are free. `ci.yml` already ran **five jobs in parallel** (Engine on ubuntu and on windows, Backend on PostgreSQL 17, Content/plans/project rules, Dependency audit). Measured on the last green run on `main` (run 42397550968, 7 Nov 2026): wall time **about 5.5 minutes**; Engine 1.9 to 2.0 min, Dependency audit 0.8 min, Backend 4.5 min, Content/plans/rules 5.5 min (the slowest, serial: about 120 steps). Earlier green runs: 4.5 to 10 min (measured, `gh run list`).

| Check (local `task ci` / `tools/ci/run_all.py`) | In GitHub CI before | Now |
|---|---|---|
| dotnet build (warnings as errors), unit tests, golden runs, replay, verify, format | yes (Engine, both OS) | yes, selector-gated |
| Backend tests on real PostgreSQL, image build and `/health` | yes (Backend) | yes, selector-gated |
| Rules-engine twin tests, Python tests, content/plans/docs checks, secret scan, pre-push hook test, workflow lint | yes (Content, plans and project rules) | yes, selector-gated |
| NuGet vulnerability audit | yes (Dependency audit) | yes, selector-gated |
| Unity EditMode tests and compile | **no** (never in CI, never in `run_all.py`) | `unity.yml`, **off until a usable licence exists** |
| Unity PlayMode tests, desktop benchmark, walkthrough/visibility sweeps, APK builds | no | stay **local** (need a GPU; standard runners have none) |
| Full run on a branch without a PR | no (only `main`, `myt1x54/**`, PRs) | `quest-perf/**` added |
| Weekly full run | no | yes (Monday 03:17 UTC) |

So the hour-long wait was the **local serial** run of checks that GitHub already runs in parallel in about 5 minutes. Remote CI therefore needs no new test jobs; it needed change-awareness, branch coverage and one result gate.

## 2. What changed in `.github/workflows/`

* `ci.yml` (one workflow, no duplicate): a `changes` job picks the jobs; `engine`, `server`, `repo-checks`, `dependencies` have `needs: changes` and an `if`. **Full run (nothing skipped, the final-integration gate):** push to `main`, tag (release), `workflow_call`, weekly schedule, manual dispatch (input `full`, default true). **Change-aware run:** pushes to other branches and pull requests, via `python3 tools/ci/select_checks.py --json --base <sha> --head <sha>`; if the script is missing, fails or the base is unknown, everything runs. A final `CI result` job (always runs) prints executed/skipped per job in the run summary, fails if any selected job failed, and fails a full run that skipped anything.
* Concurrency: runs on branches and pull requests cancel the obsolete run of the same ref; runs on `main` never cancel. Fail-fast reporting: jobs are independent and parallel, `result` goes red as soon as any of them does.
* Caching already present and kept: NuGet (`actions/cache`, keyed on csproj/global.json), uv (`setup-uv`, keyed on `server/uv.lock` and `tools/uv.lock`). Results are never cached on GitHub, so a full run really executes everything.
* `unity.yml` (new): game-ci `unity-test-runner` (EditMode, compiles every assembly, `-nographics`) with a Library cache keyed on the `.meta` files, packages lock and ProjectSettings; optional Android build via `game-ci/unity-builder` (manual dispatch only). Skipped unless the repository variable `UNITY_CI_ENABLED` is `true`.
* Not changed: `release.yml`, `main-guard.yml`, `deploy.yml`, backup/monitor/scheduled workflows, `.github/branch-protection.json`, the local `.githooks/pre-push`.

## 3. Unity on GitHub: what is and is not possible

* **Licence (blocker).** Unity's manuals say manual activation does not cover Unity Personal; Personal is activated through Unity Hub. GameCI's Personal flow needs a one-time activation and a licence file that expires and has to be renewed, and Unity 6 Personal is not documented as supported in CI. The local Hub token that expired on 9 Nov 2026 is the same mechanism. A Pro/Plus serial (`UNITY_SERIAL` plus email and password) is the supported route. I did not create or guess any secret; set `UNITY_LICENSE` or `UNITY_SERIAL`, `UNITY_EMAIL`, `UNITY_PASSWORD` and the variable `UNITY_CI_ENABLED=true` to switch it on.
* **Disk and cores.** The standard ubuntu runner has 2 cores, 7 GB RAM and about 14 GB of free disk; the project's player build was 1.77 GB and its Library is several GB. The workflow frees about 20 GB first, and the Library cache must stay under the 10 GB per-repository cache limit (not verified for this project).
* **Image.** The GameCI image for 6000.5.10f1 (and its Android variant) must exist; this was not verified and shows up on the first run.
* **GPU.** No GPU on standard runners: PlayMode rendering tests, benchmarks and visibility sweeps cannot run there. They stay local (targeted, by the change-aware selector).

## 4. Pre-push hook and duplicate work (decision for the owner)

The hook runs the full local `task ci` only for a push that updates `main`. After the single approved final push the same suite then runs again on GitHub (about 5.5 minutes). I did **not** change the hook (a mandatory safeguard). Options: keep it (safe, the local run is the slow serial one), or run the full suite remotely first on the branch (`workflow_dispatch` with `full`) and have the hook accept a recorded green remote run for that exact commit. Needs your decision.

## 5. One controlled preview push (needs approval; not done)

Pushing `quest-perf/d7-optimisations` would now trigger one `CI` run on that branch (it is covered by the new `quest-perf/**` filter). Because `.github/**` and `tools/ci/**` would be in the diff, the selector runs everything: expected about 5 to 7 minutes on free runners, no Unity job, no cost. It is the only way to see the remote result before the final integration. A dispatch of the same workflow needs the workflow file on the default branch first, so a branch push is the realistic preview.

## 6. Runtimes

| Scenario | Before | After |
|---|---|---|
| Full remote CI | about 5.5 min wall (measured, 7 Nov 2026) | unchanged (about 5.5 to 7 min expected; the extra `changes` and `result` jobs add under a minute of runner time, they overlap the others) |
| Docs-only or isolated change on a branch | full 5.5 min | expected 1 to 2 min (selector skips Engine, Backend, dependencies; not measured yet) |
| Local `task ci` before a push to main | serial, long (not re-measured here) | unchanged (hook untouched) |
| Unity EditMode on GitHub | none | not measurable until a licence exists |

## 7. Known issues found

* `.github/branch-protection.json` lists `Engine (ubuntu-latest)` but the job is named `Engine (ubuntu-24.04)`, so applying that file would require a check that never reports. The repo is public now, so server-side protection may be available (DT-042); fix the name before using `task protect-main`.
* Existing job `repo-checks` is one 120-step serial job and is the slowest remote check (5.5 min); splitting it into parallel jobs would cut the wall time but changes the required-check names, so it needs the owner's go-ahead.

## 8. Branch protection (prepared, NOT applied)

`.github/branch-protection.json` now requires exactly one status context: **`CI result`**. Reasons:

* `CI result` is the last job of `ci.yml`, runs with `if: always()` on every run (push to `main`, `myt1x54/**`, `quest-perf/**`, every pull request, tags via the release, manual, weekly), and fails when any selected job failed or was cancelled, when the selection job itself failed, or when a **full** run skipped anything. So it is the whole gate in one name.
* The per-job names are real (`Engine (ubuntu-24.04)`, `Engine (windows-latest)`, `Backend (FastAPI on PostgreSQL)`, `Content, plans and project rules`, `Dependency audit`) but must **not** be required one by one: on a change-aware run the selector skips jobs, and GitHub reports a skipped matrix job under its unexpanded name (`Engine (${{ matrix.os }})`), so a required `Engine (ubuntu-24.04)` would stay "Expected" forever and block a legitimate merge. The old file also named `Engine (ubuntu-latest)`, which never existed.
* A status context exists for pull requests and for pushes to `main` and the two branch patterns. A push to another branch without a pull request has no CI run at all (that was already true); the protection only governs pushes and merges into `main`.
* `enforce_admins` stays `false` as before, so the owner can still push to `main` through the local pre-push hook.

Apply it after the preview run has produced a `CI result` check on the branch (GitHub only offers a context it has seen), on the public repository:

```
gh api -X PUT repos/ruhank-dev/procsense-fyp/branches/main/protection --input .github/branch-protection.json
```

(`task protect-main` runs the same command; its description still says not to run it on the free private plan, which no longer applies now the repository is public.)
