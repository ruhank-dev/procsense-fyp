# Contributing to ProcSense

Three people work in this repository. Everyone with write access pushes **directly to `main`**: there are no pull requests and no approvals. What keeps `main` working is that every push to `main` is checked first, on your own machine.

**Full step-by-step guide with every command: [docs/TEAM_GIT_WORKFLOW.md](docs/TEAM_GIT_WORKFLOW.md).**

## The workflow in six lines

1. `task setup` once after cloning: installs the pre-push hook and checks your tools.
2. Work on your own branch from the latest `origin/main`: `sannan47/<what>` or `myt1x54/<what>`.
3. Merge `origin/main` into your branch (`git fetch origin`, `git merge origin/main`), fix conflicts keeping both sides.
4. `task ci` must be all `PASS`.
5. Push your branch, fast-forward `main` to it (`git merge --ff-only`), then `git push origin main`.
6. The hook runs `task ci` again on that exact commit and blocks the push if anything is red. GitHub then runs the same checks on Linux and Windows.

Never force-push. Never skip the hook, except the emergency case in the guide (section E).

## What the checks enforce (also see [docs/CI.md](docs/CI.md))

- **Engine determinism** (`src/ProcSense.Core`): no `DateTime.Now`, `System.Random`, `Guid.NewGuid`, Unity types, environment variables, `Console`, or string `GetHashCode()`. Use `SimClock` and `SimRandom`. Core stays free of Unity.
- **No clinical numbers in code.** Medical values live in `content/`, and every one needs a row in `docs/research/source_register.csv`.
- **One folder per service or feature.** Don't put code that belongs to one service in another service's folder. Keep files under about 300 lines; split by responsibility.
- **No secrets, no generated output** in git (build folders, `.env` files, recorded sessions, CLI test output). Run the CLI with `--out "$env:TEMP\..."`.
- **Golden runs** (`tests/golden`) change only on purpose: `task golden -- --update`, then review the diff before committing.
- **Plans:** adding or renaming a step file means running `task plans-index` and committing `docs/plans/milestones.md`.
- **New capability, rule, content type or tool → add its check** in `.github/workflows/ci.yml` and `tools/ci/`, and a row in `docs/CI.md`.
- **Postponed or unfinished work → a row in `docs/deferred-tasks.md`.**

## Commit messages

A short subject line, then 3–8 bullets that say what changed and in which file or folder:

```
feat(core): add wristband resolution

- Add nurse resolution rules (src/ProcSense.Core/Workflow/Steps/WristbandResolution.cs)
- Add tests and a golden run (tests/ProcSense.Core.Tests/, tests/golden/)
```

Describe only what this commit changes.

## Changing the AI plans

The plan for every AI feature lives in [docs/architecture/](docs/architecture/README.md). Edit the pipeline JSON, regenerate, and follow the sync checklist at the bottom of that README; `task ci` fails if the JSON, the plan files or the documents disagree.

## If something goes red

Read the failing `FAIL` block from `task ci`, or the failed job in the GitHub Actions tab. If `main` itself goes red, an issue is opened automatically and assigned to the author: tell the team, pause pushes to `main`, fix it (guide, section E).

## Honest limits

GitHub cannot enforce rules on this private repository on the free plan, so the hook is a local safeguard (it can be bypassed with `--no-verify`) and GitHub Actions only detects problems after a push. See [docs/CI.md](docs/CI.md) and [docs/TEAM_GIT_WORKFLOW.md](docs/TEAM_GIT_WORKFLOW.md).
