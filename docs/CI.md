# CI/CD — what is checked, and how it grows

Living document (INSTRUCTIONS.md §6b). Whenever a step adds a capability, rule, content type, tool or folder, **add or update its check here and in the workflows in the same step**.

**Run everything locally:** `task ci` (or `python tools/ci/run_all.py`; add `--quick` when only docs/content changed). Green locally = green in GitHub, except the Linux-only format check.

## Workflows

| File | Runs when | What it does |
|---|---|---|
| `.github/workflows/ci.yml` | every push to `main`, Dependabot's pull requests, manually, and called by the release | The jobs below. **After** a push it only detects problems; it cannot stop a commit that has already reached `main` |
| `.github/workflows/release.yml` | a tag such as `v0.1.0` is pushed | Runs the full CI, then publishes the headless simulator for Linux and Windows (self-contained, with `content/` and the golden runs) as a GitHub Release with SHA-256 checksums |
| `.github/workflows/main-guard.yml` | when CI finishes on `main`, on force-pushes to `main`, and manually (dry run) | Opens an issue naming the commit and author when CI is red on `main`, or when `main` is force-pushed. Alerts only — see "Protecting `main`" |
| `.github/dependabot.yml` | weekly | Pull requests for NuGet, GitHub Actions and Python (uv) updates. Merge them once their CI run is green |

## Checks in place

| # | Job | Check | Guards against | Local command |
|---|---|---|---|---|
| 1 | Engine (Linux + Windows) | Restore with 3 retries, build in Release with warnings as errors | Compile errors, new warnings, flaky network | `dotnet build ProcSense.sln -c Release -warnaserror` |
| 2 | Engine | Unit tests (xUnit, incl. determinism, content, identity, history, observations, golden, **compiled-code ban list for Core** and **zero-allocation tick**) | Broken engine logic | `dotnet test ProcSense.sln` |
| 3 | Engine | Golden runs through the CLI | A change that alters the pinned log or final-state hash | `task golden` |
| 4 | Engine | Record a session and replay it | Replay no longer reproduces the same hash | `task sim -- run …` then `replay …` |
| 5 | Engine | Same tests and hashes on **Linux and Windows** | Platform differences in the engine (proxy for the PC-vs-Quest check EXP-CORE-1) | both OS jobs |
| 6 | Engine (Linux) | `dotnet format --verify-no-changes` | Style drift against `.editorconfig` | CI only on Windows checkouts (line endings) |
| 7 | Repo checks | `content/` validated against its schema; every clinical value has a source-register row | Untraced or malformed medical content | `task content-validate` |
| 7b | Repo checks | Test-only fixture cases in `tests/fixtures/cases/` get the same schema and source-register validation as real cases | Untraced or malformed fixtures | `task content-validate` |
| 7c | Repo checks | Every rules file (`content/rules/*.json`, test fixtures in `tests/fixtures/rules/`) is validated against `rule.schema.json` and every rule's register ids must exist. Unit tests load the 156 rule-evaluator vectors and run every rule's own test cases | Untraced or untested clinical rules; evaluator regressions | `task content-validate`, `task test` |
| 7d | Repo checks | Every workflow file (`content/workflow/*.json`, fixtures in `tests/fixtures/workflow/`) is validated against `workflow.schema.json` and every goal, decision point and blocker needs register ids that exist. Unit tests cover goals, deadlines, decisions, blockers, hints, circles and the 200-goal budget | Untraced workflow content; case-graph regressions | `task content-validate`, `task test` |
| 7e | Repo checks | `extract_walkthrough.py --check` parses the walkthrough and fails if a count changed (15 steps, 270 answered question slots, 47 what-ifs, 24 VR cards, 8 + 20 stacked cases, 2 × 20 flow stages); `trace.py --check` fails if `docs/research/traceability.md` is stale or a walkthrough part is neither mapped, planned in a real step nor marked "not used" with a reason; any content table (CSV) must have a `ref` column with existing register ids; the tools have their own tests | A changed walkthrough nobody noticed; content with no trail back to the book | `task content-extract`, `task content-trace`, `uv run --project tools tools/content/test_content_tools.py` |
| 7f | Repo checks | `case_gaps.py --check`: the list of what each case still lacks is regenerated from `content/case_seeds.csv` | A case changed without the gaps list being refreshed | `uv run --project tools tools/content/case_gaps.py` |
| 7g | Repo checks | `validate_images.py` checks every ultrasound, ECG, chest X-ray, CT, MRI and MRCP picture and its report: file hash, size, finding code, licence and attribution for real images, the visible SIMULATION mark read back from the pixels of synthetic ones, no EXIF/ICC data, report numbers inside the book's limits, every picture has a report, DRAFT unless an approval is recorded | A changed or swapped picture, a missing mark, a report contradicting its picture or the book, a missing licence | `uv run --project tools tools/content/validate_images.py` |
| 7k | Repo checks | `imaging_library.py --check`: every picture-returning test has a clean result and problem results, each with at least 4 stored pictures; every result is produced by a case or a what-if; every case_imaging and whatif_imaging row names a real case, a known test and a problem code of that test; `imaging_library.csv` regenerated | A test with no clean result, a code with one picture only, a case pointing at a wrong code | `uv run --project tools tools/content/imaging_library.py` |
| 7h | Repo checks | `image_review_pack.py --check`: the clinician review pack lists every picture with its report | A picture added without being put in front of the clinician | `uv run --project tools tools/content/image_review_pack.py` |
| 7i | Repo checks | `dataset_cards.py --check`: the dataset register and cards match the extraction results | Numbers or licences on a card that differ from the data | `uv run --project tools tools/data/dataset_cards.py` |
| 7j | Repo checks | `test_images.py`: 37 tests, including breaking a copy of the real set (changed picture, painted-over mark, missing licence, EXIF, number outside the limit, ECG rate contradicting its rhythm) and expecting the checker to notice | The image checks silently passing everything | `uv run --project tools tools/content/test_images.py` |
| 8 | Repo checks | Plan index (`milestones.md`) up to date; step references resolve | Out-of-date plans, links to missing steps | `task plans-check` |
| 9 | Repo checks | `tools/ci/repo_checks.py`: **core-rules** (no wall clock, `System.Random`, Unity, env vars, console, string hash codes in `src/ProcSense.Core`), **hygiene** (no build output, session files, env files, secrets, big files), **docs-links** (relative Markdown links resolve), **deferred-register** (ids unique, statuses valid), **prompt-history** (numbered 1..N), **steps** (file names and titles agree) | Project rules no compiler checks | `task check` |
| 10 | Repo checks | GitHub workflow and Dependabot files validate against their schemas | A broken workflow file | `uvx check-jsonschema --builtin-schema vendor.github-workflows .github/workflows/*.yml` |
| 11 | Dependencies | No vulnerable NuGet packages (direct or transitive); deprecated packages warn | Known-vulnerable dependencies | `dotnet list ProcSense.sln package --vulnerable --include-transitive` |
| 12 | Release | Tag must equal `CoreInfo.Version` | Releasing a mislabelled build | `release.yml` |
| 13 | Repo checks | **team-guards**: the contributor guides, protection config, hook, hook tests, setup check and workflows must exist; the hook must still run `task ci` and have no bypass variable; `ci.yml` must run on pushes to `main` | Someone deleting or weakening the files that guard `main` | `task check` |
| 14 | Repo checks | **Pre-push hook tested in an isolated temporary repository** (`tools/ci/test_pre_push_hook.py`, 17 cases): feature branches free, `main` pushes run `task ci`, failures/force-push/delete/dirty tree/wrong commit/stale `main`/missing tool blocked, the guide's two-teammate sequence works | A regression in the hook | `uv run --project tools tools/ci/test_pre_push_hook.py` |
| 15 | Main guard (after CI on `main`) | Issue naming the commit and author when CI is red on `main`; issue when `main` is force-pushed | A red `main` going unnoticed | `.github/workflows/main-guard.yml` (test safely: Actions → Main guard → Run workflow, dry run) |
| 16 | Repo checks | **no-tool-attributions**: no tracked file may name an AI tool or carry a co-author trailer (INSTRUCTIONS section 2c); the baseline list of excused files (`tools/ci/tool_mentions_baseline.txt`) may only shrink and is empty | Tool attributions creeping back into commits and files | `task check` |

## Protecting `main` (teammates push directly)

**Workflow:** Sannan47 and Myt1x54 push straight to `main`; there are no pull requests and no approvals ([TEAM_GIT_WORKFLOW.md](TEAM_GIT_WORKFLOW.md)). Everything below is about checking *before* the push and noticing *after* it.

GitHub refuses branch protection and rulesets on this private repo on the free plan: *"Upgrade to GitHub Pro or make this repository public"* (checked 09 Oct 2026). The repository stays private and free, so **there is no server-side enforcement**: a collaborator with write access can push to `main` without any check if they skip the local hook.

| Safeguard | Kind | What it does | How it can be bypassed |
|---|---|---|---|
| `.githooks/pre-push`, installed by `task setup` | Local, before the push | For any push that updates `main`: runs the full `task ci`; blocks force-pushes and deleting `main`; requires a clean tree that is exactly the commit pushed; fails with an install hint if `task`, `dotnet`, `uv` or `git` is missing. Feature-branch and tag pushes are not checked | `git push --no-verify`, deleting `core.hooksPath`, a clone before `task setup`, editing the hook. The only sanctioned use is the emergency rule in the guide, section E |
| `tools/ci/test_pre_push_hook.py` (in `task ci` and in CI) | Local + GitHub | Proves the hook still behaves, in a throw-away repository | — |
| `ci.yml` on every push to `main` | GitHub, **after** the push | Linux + Windows engine jobs, content and project rules, dependency audit | Cannot prevent anything: the commit is already on `main` |
| `main-guard.yml` | GitHub, after | Issue naming the commit and author when CI is red on `main` or `main` is force-pushed | Alerts only |
| `team-guards` check | GitHub + local | Fails if the safeguard files are deleted or weakened | A determined person can edit the check too |
| `.github/branch-protection.json`, `task protect-main` | **Not applied** | A checks-only protection config for the day the plan allows it. If it is ever enabled, CI must also run on feature-branch pushes, otherwise direct pushes of unchecked commits would be rejected | Do not run on the free private plan: GitHub refuses it |

What the hook runs is exactly `task ci` (`tools/ci/run_all.py`): build with warnings as errors, unit tests, golden runs, replay round trip, no vulnerable packages, content + source-register validation, plan-index freshness, project rules (including the team-guards check) and the hook's own tests. The code-format check runs in GitHub only (Linux line endings).

**What remains vulnerable:** anyone with write access can bypass the hook; checks on GitHub arrive after the commit; two simultaneous pushes can interleave (the second is rejected by git or the hook, but nothing re-tests the combined result until GitHub runs); the owner's admin rights are not restricted. Real enforcement needs GitHub Pro (students can usually get it free) or a public repository (DT-042).

## Roadmap — checks to add as the project grows

Each row becomes a real check in the step that creates the thing it guards. Tracked in `docs/deferred-tasks.md` (DT-044).

| Add when… | Check to add | Planned in |
|---|---|---|
| The Unity project exists | **Added locally:** `task unity-test` runs the Edit-mode and Play-mode tests in batch mode (`tools/unity/run_unity.py`). Still to do: run them in GitHub Actions (needs a free Unity personal licence activation, DT-043) | p0-vr-platform-s02, s07 |
| Quest builds exist | Build Dev/Demo/Study variants; performance gate (≥ 72 fps average, no hitch > 50 ms, memory ≤ 2.5 GB); APK + checksum on release | p0-vr-platform-s07 |
| Quest sessions are recorded | Replay a Quest log headless and compare the final-state hash (skeleton 1 done condition) | p0-evaluation-performance-s01 |
| Blender equipment scripts exist | Headless Blender validation + preview render per asset (size, scale, naming, polygon budget) | p0-blender-equipment-s03 |
| Case packaging exists | Content manifest hash (T-CL-5), loader mode checks, contradiction validator, mutation tests | p0-case-library-s01, s04, s05 |
| Python tools grow | `pytest` for `tools/` | with the first tool that has logic worth testing |
| Rules / clinical logic exist | Worked-example tests from the walkthrough (EXP-CL-1), property tests | p0-clinical-logic |
| The backend exists (P1) | FastAPI tests, OpenAPI schema check, Docker build (two-stage), `uv` lock check | p1-backend |
| AI paths exist (P1) | Grounding validator and AI evaluation suite | p1-ai-language, p1-evaluation |
| Repo plan allows it | Branch protection with required checks, code scanning / secret scanning (private repos need a paid GitHub plan) | DT-042 |
| xunit v3 migration done | Remove the deprecated-package warning | DT-041 |

## Cutting a release

1. Update `CoreInfo.Version` (`src/ProcSense.Core/CoreInfo.cs`) and commit.
2. `git tag v<version>` (for example `v0.1.0`) and `git push origin v<version>`.
3. The Release workflow runs CI, builds `procsense-sim-v<version>-linux-x64.zip` and `…-win-x64.zip` with `.sha256` files and creates the GitHub Release with generated notes.

## Status

CI first run on GitHub: **green on 09 Oct 2026 (about 13:20 UTC)** — run 37935972959: Engine (ubuntu-latest), Engine (windows-latest), Content/plans/project rules and Dependency audit all succeeded. The same hashes passed on Linux and Windows. GitHub reported Node 20 deprecation warnings for the actions; Dependabot's pull requests bump the actions (their CI runs were green).

Hook: tested in an isolated temporary repository, 17/17 cases (09 Oct 2026). `main-guard.yml` (rewritten for direct pushes) has been validated against GitHub's workflow schema but has **not yet run on GitHub**; after it is pushed, test it safely with Actions → Main guard → Run workflow (dry run) — DT-050.
