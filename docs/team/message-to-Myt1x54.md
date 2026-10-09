# Message for Myt1x54 (copy everything inside the box and send)

````text
Hi Myt,

We now push straight to main, with no pull requests and no approvals. To keep main working there is a local check that runs before every push to main. Please set it up first and read the full guide: docs/TEAM_GIT_WORKFLOW.md in the repo.

Repo: https://github.com/ruhank-dev/procsense-fyp   (private - accept the GitHub collaborator invitation first)

1) FIRST TIME (PowerShell)
   winget install --id Git.Git -e
   winget install --id Task.Task -e
   winget install --id astral-sh.uv -e
   winget install --id Microsoft.DotNet.SDK.9 -e
   (close and reopen PowerShell, then:)
   cd $HOME
   git clone https://github.com/ruhank-dev/procsense-fyp.git
   cd procsense-fyp
   task setup        # installs the pre-push hook and checks your tools; must end with "setup complete"
   task ci           # runs every check; all lines must say PASS

2) ALREADY HAVE A CLONE
   cd <your clone>
   git status                              # if you have changes: git stash push -u -m "wip"
   git fetch origin --prune
   git switch main
   git merge --ff-only origin/main         # never use reset; if this refuses, ask me
   task setup

3) WORK ON YOUR OWN BRANCH
   git fetch origin
   git switch -c myt1x54/<what-you-do> --no-track origin/main
   (work, then)  git status --short ; git diff
   git add <only the files you mean to commit>
   git commit -m "feat(area): subject" -m "- what changed (folder\file)"
   git push -u origin HEAD

4) BEFORE PUTTING YOUR WORK ON MAIN
   git fetch origin --prune
   git merge origin/main        # fix conflicts keeping BOTH sides; "git merge --abort" undoes the merge
   task ci                      # must be all PASS
   git push origin HEAD         # saves your branch
   git switch main
   git merge --ff-only myt1x54/<what-you-do>
   git push origin main         # the hook runs task ci again and blocks the push if anything fails

5) RULES
   - NEVER force-push (no --force). NEVER use --no-verify, except the emergency case in section E of the guide, after telling the team.
   - If a push to main is rejected or blocked ("fetch first", "not a fast-forward"): go back to your branch, git fetch origin, git merge origin/main, task ci, then repeat step 4 from "git switch main".
   - If task ci fails: read the FAIL lines, fix, run it again. Send me the output (text, not a photo) if you are stuck. Do not push while it is red.
   - If GitHub shows a red run on main or an issue "CI is red on main": tell the team in the chat, stop pushing to main until it is fixed (guide section E).
   - Don't commit secrets, .env files, build folders or output files. Don't touch another person's folder without telling them.
   - Tell the others in the chat when you push something big (you don't need permission, just a heads-up).

Full guide with every command: docs/TEAM_GIT_WORKFLOW.md
````
