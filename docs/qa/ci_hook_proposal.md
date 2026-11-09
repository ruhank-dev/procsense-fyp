# Proposal: do not run `task ci` twice before main (NOT applied; needs the owner's approval)

Status: proposal only. Nothing in `.githooks/` or the repository protections was changed.

## What the mandatory hook does today (`.githooks/pre-push`, installed by `task setup`)
- Applies only to a push that updates `main` or `master`; feature branches and tags pass straight through.
- Refuses deleting main and anything that is not a fast-forward of the remote main (no force-push).
- Requires the checked-out commit to be the one pushed and a clean working tree (nothing uncommitted or untracked).
- Requires git, task, dotnet and uv on PATH, then runs `task ci` (it builds, tests, runs the goldens, checks format and runs every content check; about an hour on this PC) and blocks the push on any failure.
- GitHub Actions then runs the same checks after the push, so every main push is checked twice: once on the PC, once remotely.

## Proposed change (one added shortcut, no protection removed)
Before running `task ci`, the hook asks GitHub whether the **exact commit being pushed** already has a successful complete CI run:

    gh run list --workflow CI --commit <HEAD sha> --status success --json conclusion,headSha --limit 1

If it does, the hook prints "complete GitHub CI already green for <sha>" and skips the local `task ci`. If `gh` is missing, not logged in, offline, or finds no green run, the hook runs `task ci` exactly as today. Every other rule (fast-forward only, clean tree, no delete) stays.

Flow for final integration: push the integration branch (the hook passes it through and GitHub runs the complete workflow on that commit), wait for green, then push the same commit to main (the hook sees the green run and skips the duplicate hour).

## Why it is safe
- The commit hash identifies the exact content; a green run on it is the same evidence `task ci` would produce, produced on a clean runner.
- It fails closed: any doubt runs the full local check.

## Cost and what needs approval
- It needs one branch push before main (the "controlled preview push" the owner must approve) and `gh` logged in.
- Linux-only checks (format) are stronger remotely; Windows differences are covered by the engine job's Windows leg.
- It changes a mandatory safeguard, so it is not applied until the owner approves.
