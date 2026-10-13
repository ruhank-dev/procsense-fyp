# Working model — who does what (the developer vs team vs tools)

## Where the developer is strong
- **Pure C# engine** (Core): write, test, replay, profile — fully verifiable from the terminal.
- **Backend** (FastAPI, PostgreSQL on Supabase, tests, Docker).
- **Medical research and data**: guidelines, cross-checks, datasets, licences, synthetic data, source register.
- **Unity C# scripts**: MonoBehaviours, editor scripts, Play Mode/Edit Mode tests, build scripts, shaders as text.
- **Blender Python (`bpy`) scripts** for all equipment and props — and the developer can run Blender **headless**, render
  preview images, and look at them to check its own work before you see it.
- **Content pipelines, validators, CI, docs.**

## Where the developer is weak — and how we compensate

| Area | Why it's hard for the developer | How we handle it |
|---|---|---|
| **Unity scene work** (placement, lighting, prefab wiring) | Can't see or click the Editor | Logic stays in Core; the developer writes **editor scripts** that build/wire/validate prefabs; you do layout by hand and send **screenshots**. Optional Unity MCP bridge (community tools, e.g. CoplayDev `unity-mcp`) — **UNVERIFIED, tested in EXP-VR-4 first**. |
| **Headset testing** | Can't wear a Quest | XR Device Simulator + Meta XR Simulator + automated Play Mode tests; you do device passes and report notes/videos. Session logs replay headless. |
| **"Feels right" in VR** (scale, reach, comfort, readability) | Needs a body in the headset | Team judges; the developer converts notes into fixes + checklist items. |
| **3D visual judgement** | Can render and view images, but not judge "in-headset feel" | Render previews (several angles, with a 1.7 m human scale reference) for every asset → team reviews in Blender and headset → the developer revises the script. |
| **Characters (humans)** | Procedural human modelling in `bpy` is weak | Open human generators/sets as candidates (MPFB/MakeHuman, Microsoft Rocketbox — UNVERIFIED, team checks first) + Mixamo animations; the developer scripts clinical-sign materials and blend shapes. |

## 3D approach (decided)

| Layer | Source |
|---|---|
| Rooms (ward, ED, theatre…) | **Supervisor's base hospital room models** (empty shells) |
| All equipment, machinery, props | **Procedural Blender Python scripts** written by the developer (one script per asset, shared helper library) — detail in `p0/p0-blender-equipment/` |
| Free online models (Sketchfab etc.) | Listed in `p0/p0-blender-equipment/free-model-candidates.md` as **visual references only** — never imported |
| AI text-to-3D | **Not used** |
| Characters | Candidate open sets (team approves) + our clinical-sign materials/blend shapes |

## VR approach (decided)
- Unity is a **view + input layer**; Core holds the truth.
- Every VR interaction emits a logged `SimAction` → any headset session replays headless.
- Greybox first (rooms + simple stand-ins), then swap in the Blender assets as they are ready.

## Backend approach (decided)
- The developer leads; INSTRUCTIONS §4b/§5/§5b. Hosted service on PostgreSQL (Supabase) with accounts and organisations (ADR-0009); the engine runs on the headset, so play works without a connection and results upload when one is available.

## Feedback loop

| You send | The developer does |
|---|---|
| Screenshot / short video of a scene | Reviews against step spec + player-view table; lists fixes |
| Blender/headset notes on an asset | Edits the asset script, re-renders previews |
| Unity Console log / profiler capture | Diagnoses and fixes |
| Session JSONL from the Quest | Replays headless, reproduces the bug |
