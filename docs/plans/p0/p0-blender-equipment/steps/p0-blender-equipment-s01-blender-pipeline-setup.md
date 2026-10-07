# p0-blender-equipment-s01 — Blender pipeline setup

Milestone: **M6 Environment & assets — rooms, equipment, characters, voice**

Prerequisites (you): Blender installed.

## What
Install and pin Blender, create the `tools/blender/` layout (runner, helper library folder, assets per category, budgets/requirements files), and add Taskfile commands `blender-build -- <asset|all>`, `blender-validate`, `blender-preview`, `blender-build-all`, all running Blender headless with `--factory-startup` so user add-ons/settings can't change the output.

## Why
Every asset script must give identical results for every teammate and in CI; a pinned version and factory startup remove the most common sources of 'works on my machine' in Blender scripting.

## How to implement
1. Install Blender (LTS); record exact version in `tools/blender/VERSION`; `task doctor` checks it.
2. `tools/blender/run.py` wrapper: finds Blender, runs `blender -b --factory-startup -P <script> -- <args>`, returns exit code.
3. Taskfile entries; output folder `build/blender/` (gitignored) + export target `unity/Assets/_Project/Art/Equipment/`.
4. Smoke test: `task blender-build -- hello_cube` creates and exports a bevelled cube.
5. `.gitignore`: `build/blender/`; exports go to `unity/Assets/_Project/Art/Equipment/` (LFS or rebuilt — EXP-FND-1).

## Verification
`task blender-build -- hello_cube` produces an FBX that imports into Unity at 1 m.

## Files changed
`tools/blender/*`, `Taskfile.yml`, `.gitignore`.

## Dependencies
p0-foundation-s04.

## Common issues
Blender's Python API changes between versions — pin it.

## Sources
p0-blender-equipment README.
