# p0-foundation-s01 — Remote repository, Git LFS and backup

Milestone: **M2 Walking skeleton 2 — records + diagnostics loop**

Prerequisites (you): GitHub account; private repo created.

## What
A private GitHub repository holding the project, Git LFS for binary assets, `main` protected so it only changes through reviewed pull requests, and README instructions for cloning with LFS.

## Why
Project data was lost once already; a remote copy makes that impossible to repeat and gives CI a home.

## How to implement
1. You create the private repo on GitHub (needs your account) and share the URL.
2. The developer adds the remote and prepares the push; **you run `git push -u origin main`** (destructive-action rule).
3. `git lfs install`; `git lfs track` for `*.fbx *.blend *.png *.jpg *.wav *.ogg *.tga *.psd` — patterns added to `.gitattributes` (binary hints already there).
4. Branch protection on `main`: PR required, status checks required once CI exists (s02).
5. README: clone + `git lfs pull` instructions; note that Blender outputs are rebuildable from scripts (EXP-FND-1).

## Verification
On a second machine: `git clone` → `git lfs pull` → `task build` + `task test` green.

## Files changed
`.gitattributes`, `README.md`.

## Dependencies
p0-foundation-s00 (✅).

## Common issues
LFS free quota is limited — scripts are the source of truth for generated 3D; decide commit-vs-rebuild in EXP-FND-1 / EXP-BL-4.

## Sources
p0-foundation README; Implementation Guide B-S6 Step 1 (LFS patterns).

## Implementation notes (15 Oct 2026)
- **Repository:** private, `github.com/ruhank-dev/procsense-fyp`, holds the project since 07 Oct 2026 (nothing to create).
- **Git LFS (owner's decision, 15 Oct 2026: guard now, LFS later):** the repository is about 51 MB and its largest file is 3.6 MB, so LFS is not switched on: it would put every clone and CI checkout on GitHub's small free LFS quota and require git-lfs on every machine, for no gain yet. Instead the project rules refuse any tracked file over 10 MB that is not in LFS (`tools/ci/repo_checks.py`, with a test), `.gitattributes` gains the remaining binary hints (exr, tif, tga, psd, hdr, jpeg, mp3, glb) and says how to start LFS when an asset needs it.
- **Protecting `main`:** server-side branch protection is refused on a private free-plan repository (DT-042, blocked). `main` is guarded by the pre-push hook, CI and the main-guard workflow; the README says so.
- **Backup (owner's decision, 15 Oct 2026):** `task repo-backup` (`tools/repo_backup.py`, standard library) fetches, writes a verified bundle of every branch and tag outside the repository (default `Desktop/ProcSense_branch_backup`, newest 5 kept) and prints the restore command; 3 tests (`tools/ci/test_repo_backup.py`: restore by clone gives the same commits and tags, pruning, a folder inside the repository is refused) run with the other checks. First real run: 52.5 MB, 9 branches and tags.
- **README:** "Cloning, large files and backups" (clone, why no `git lfs pull` is needed, how to start LFS, backups, how `main` is protected).
- **Verification:** a fresh clone from GitHub into an empty folder on the owner's laptop (not a second machine) was clean (no changed files), and `task build` and `task test` passed (1,195 tests). A second machine is the remaining check; any teammate's clone counts.
