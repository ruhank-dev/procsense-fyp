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
