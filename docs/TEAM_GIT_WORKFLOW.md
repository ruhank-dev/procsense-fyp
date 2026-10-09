# Team Git workflow — direct pushes to `main`, checked locally first

For **Sannan47** and **Myt1x54** (collaborators with write access). The owner is **ruhank-dev**.
Repository: **https://github.com/ruhank-dev/procsense-fyp** (private).

## The idea in one paragraph

You work on your own branch, merge the latest `main` into it, run **`task ci`** (the same checks GitHub runs), push your branch, then push the tested result straight to `main`. **No pull request and no approval are needed.** A git hook that `task setup` installs runs `task ci` again automatically on every push to `main` and blocks the push if anything fails. GitHub Actions then runs the checks once more on `main` and raises an alert if something is red.

## What is and is not protected (read this once)

| Protection | What it does | Limit |
|---|---|---|
| Pre-push hook (`.githooks/pre-push`, installed by `task setup`) | Before `main` is updated it runs `task ci`, refuses force-pushes and deleting `main`, and requires a clean working tree that is exactly the commit being pushed | It lives on your machine. `git push --no-verify` or removing `core.hooksPath` skips it. It is not server-side enforcement |
| GitHub Actions CI (`ci.yml`) | Runs the same checks on Linux **and** Windows after every push to `main` | It runs **after** the push: it can detect a bad commit, not stop it |
| Main guard (`main-guard.yml`) | Opens an issue naming the commit and author if CI fails on `main`, or if `main` is force-pushed | Also after the fact |
| GitHub branch protection | **Not available**: this is a private repository on the free plan, and GitHub refuses it | So write-access users *can* push to `main` without any check, if they bypass the hook. This workflow depends on everyone following it |

Never force-push. Never use `--no-verify` except in the emergency case in section E.

---

## A. First-time setup

### 1. Check your tools

```powershell
git --version
task --version
dotnet --version
uv --version
```

`dotnet --version` must start with `9.`. Install anything missing, then **close and reopen PowerShell**:

```powershell
winget install --id Git.Git -e
winget install --id Task.Task -e
winget install --id astral-sh.uv -e
winget install --id Microsoft.DotNet.SDK.9 -e
```

### 2. Clone the repository

Accept the collaborator invitation from GitHub first (email or https://github.com/ruhank-dev/procsense-fyp/invitations), otherwise the clone says "repository not found". Sign in when git asks (a browser window opens), or run `gh auth login` if you use the GitHub CLI.

```powershell
cd $HOME
git clone https://github.com/ruhank-dev/procsense-fyp.git
cd procsense-fyp
```

### 3. Install the safeguards

```powershell
task setup
```

This sets `core.hooksPath` to `.githooks` and then checks your tools and the hook. It must end with `setup complete`. If it says `MISSING`, run the install command it prints, restart PowerShell and run `task setup` again.

### 4. Verify

```powershell
git branch --show-current          # main
git remote -v                      # both lines: https://github.com/ruhank-dev/procsense-fyp.git
git status --short                 # no output = clean working tree
git config --get core.hooksPath    # .githooks
Get-Content .githooks\pre-push -TotalCount 2
task ci
```

`task ci` prints one `PASS` or `FAIL` line per check and ends with `N/N checks passed`. The first run downloads packages and takes longer.

### 5. If a check fails

Read the `FAIL` block under the check name: it shows the last lines of that tool's output. Fix it and run `task ci` again. If the failure is on a fresh clone and you did not change anything, send the full output to the team chat (copy the PowerShell text, not a photo) and say which command you ran. Do not push until it passes.

---

## B. Updating an existing clone

```powershell
cd $HOME\procsense-fyp          # wherever your clone is
git status
```

**If `git status` shows changes you want to keep**, save them first without losing anything:

```powershell
git stash push -u -m "wip before update"      # later: git stash pop
```

Then bring `main` up to date (this never discards work):

```powershell
git fetch origin --prune
git switch main
git merge --ff-only origin/main
git log --oneline -5
task setup
```

- `git merge --ff-only` only moves `main` forward. If it says *"Not possible to fast-forward"*, your local `main` has commits `origin/main` does not. **Do not reset.** Keep them on a branch and carry on from there:
  ```powershell
  git switch -c rescue/my-local-main-commits
  git switch main
  git merge --ff-only origin/main
  ```
- If you are in the middle of a feature branch, do not switch. Follow section D to merge `origin/main` into it.
- Run `task setup` again after pulling: it is safe to repeat and picks up hook updates.

---

## C. Feature branches

Name your branch `sannan47/<what-you-do>` or `myt1x54/<what-you-do>` (lowercase, hyphens), for example `sannan47/tablet-wristband-card`. Separate branches let you work independently; they reduce conflicts but do not remove them: two people editing the same lines still conflict.

```powershell
git fetch origin
git switch -c sannan47/short-description --no-track origin/main      # replace with your own branch name

# ...work...

git status --short                  # what changed
git diff                            # unstaged changes, line by line
git add path\to\file1 path\to\file2 # only the files you mean to commit - never "git add ." blindly
git diff --cached                   # exactly what will be committed
git commit -m "feat(area): short subject" -m "- what changed (folder\file)
- what else changed (folder\file)"
git push -u origin HEAD             # saves your branch on GitHub
```

Commit messages: a short subject, then 3–8 bullets that say what changed and in which file or folder (`CONTRIBUTING.md`, section 4). Never commit secrets, build output, `.env` files, or CLI test output.

---

## D. Before pushing to `main`

**When to run `task ci`:** after every merge of `origin/main` into your branch, and always before step 7 below. The hook runs it again, automatically, on the exact commit you push to `main`.

**What the hook checks** (all of `task ci`): build with warnings as errors, unit tests, golden runs, a record-and-replay round trip, no vulnerable packages, content and source-register validation, plan-index freshness, project rules (determinism rules, repo hygiene, doc links, registers, safeguards), and the hook's own tests. The code-format check runs on GitHub (Linux) only, because Windows line endings differ.

### The sequence

```powershell
# 1. Where am I? Is everything committed?
git branch --show-current
git status --short            # must be empty; commit or "git stash push -u" first

# 2. Get the latest GitHub state
git fetch origin --prune

# 3. Bring the newest main into your branch (a merge: nothing is rewritten, nothing is lost)
git merge origin/main

# 4. If git reports conflicts, see "Conflicts" below. Otherwise continue.

# 5. Run the checks
task ci

# 6. Save the tested branch on GitHub
git push origin HEAD

# 7. Move local main to your tested branch (fast-forward only: nothing new is created).
#    Your branch already contains origin/main from step 3, so this is a straight move forward.
git switch main
git merge --ff-only sannan47/short-description      # your branch name

# 8. Check what you are about to publish
git status --short            # empty
git log --oneline -5

# 9. Publish. The hook runs task ci on this exact commit and blocks the push if anything fails.
git push origin main
```

If step 7 says *"Not possible to fast-forward"*, your local `main` has commits of its own that your branch does not contain: do not reset; rescue them first (section B), then repeat from step 2.

### Concurrent pushes can race

If someone else pushed to `main` after your step 2, your local fast-forward in step 7 still works (your computer does not know yet). The **push in step 9** is then blocked by the hook (*"has commits you do not have"*) or rejected by GitHub (*"fetch first"* / *"non-fast-forward"*). That is the system working. Recover like this:

```powershell
git switch sannan47/short-description      # back to your branch
git fetch origin --prune
git merge origin/main                      # bring in what the other person pushed
task ci
git switch main
git merge --ff-only sannan47/short-description
git push origin main
```

Never answer a rejected push with `--force`.

### Conflicts

`git status` lists files under *"Unmerged paths"* (*both modified*). For each one:

1. Open the file and find the markers `<<<<<<<`, `=======`, `>>>>>>>`. The part above `=======` is yours, below is from `main`.
2. Edit it so the result keeps **both** people's intent. If you cannot tell what the other change is for, ask its author.
3. Delete the three marker lines, save, then `git add path\to\file`.
4. When every file is added: `git commit` (accept the message git proposes), then `task ci`.

Lost? `git merge --abort` puts your branch back exactly as it was before the merge. Never settle a conflict by taking "ours" or "theirs" wholesale (`-X ours`, `-X theirs`, `git checkout --ours/--theirs`), and never delete someone else's lines to make the markers go away.

### What the hook tells you

| Message | Meaning | What to do |
|---|---|---|
| `has commits you do not have` | `main` on GitHub moved | Steps 2–5 above, then push again |
| `not a fast-forward` | You tried to rewrite `main` (or it moved) | Same; never force |
| `uncommitted or untracked files found` | The checks would test files you are not pushing | Commit them or `git stash push -u` |
| `not the one checked out` | You pushed `branch:main` while another branch is checked out | `git switch main`, then push |
| `'task' is not installed` | A required tool is missing | Install it (`task setup` tells you how) |
| `'task ci' failed` | A check is red | Read the `FAIL` lines, fix, commit, push again |

---

## E. When CI fails after a push

A red run on GitHub after a direct push is **detection, not prevention**: the commit is already on `main`.

**1. Find out what failed.** On GitHub: Actions tab → the red run → the red job (`Engine (ubuntu-latest)`, `Engine (windows-latest)`, `Content, plans and project rules`, or `Dependency audit`) → the red step. Or in PowerShell:

```powershell
gh run list --branch main --limit 5
gh run view <run-id> --log-failed
```

An issue titled **"CI is red on main (abc1234)"** is opened automatically, assigned to the author, naming the commit.

**2. Tell the team** in the group chat: which job failed, the commit, and who is fixing it.

**3. Pause.** Nobody pushes to `main` until it is green. Everyone can keep working on their own branch; merge `origin/main` into it after the fix.

**4. Reproduce locally.**

```powershell
git fetch origin --prune
git switch main
git merge --ff-only origin/main
task ci
```

If it only fails on one operating system (the job name says which), reproduce on that system or read the log carefully.

**5. Fix it on a branch, then follow section D.**

```powershell
git switch -c sannan47/fix-ci --no-track origin/main
# fix, commit
task ci
# then section D, steps 2 to 9
```

If the fastest safe fix is to undo the bad commit, make a *new* commit that reverses it: `git revert <commit>`. Never rewrite or force-push `main`.

**Emergency bypass.** If GitHub or the local tools are broken for reasons unrelated to the code and `main` genuinely must be updated, the only allowed bypass is `git push --no-verify origin main`, **after** you post in the team chat what you are doing and why. Run `task ci` as soon as the tools work again and post the result. Never use it to get past a failing check.

---

## F. Messages to send

Ready-to-send messages, one per teammate:

- [Message for Sannan47](team/message-to-Sannan47.md)
- [Message for Myt1x54](team/message-to-Myt1x54.md)

## Limits of this setup (be honest with yourselves)

- Anyone with write access can still push to `main` without the hook (`--no-verify`, a fresh clone before `task setup`, or editing the hook). Server-side enforcement needs GitHub Pro or a public repository.
- The hook tests the commit on **your** machine. Windows and Linux differences are caught by GitHub, after the push.
- Two pushes can still interleave; the second is rejected, but the checks on `main` are only re-run on GitHub after the fact.
- If this ever matters more than it does now, the owner can enable real protection with `task protect-main` once the plan allows it (`docs/CI.md`, DT-042).
