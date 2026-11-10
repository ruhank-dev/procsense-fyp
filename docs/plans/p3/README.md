# P3 — Final game integration (tier overview)

**Status:** planned 7 November 2026; owner decisions D1-D7 taken the same day (ADR-0032). Implemented so far: only the Quest performance work of D7 on branch `quest-perf/d7-optimisations` (p3-17); every other plan is still planning. P3 turns the built P0-P2 systems into one playable game: main menu, Training and Test/Examination modes, procedure selection, random valid scenarios, menu and voice interaction on one authoritative engine path, training checklist, examination hints and Submit Patient to OT, results and debrief, characters, animation, assets, UI design system, settings, sound, integration contracts, QA and release, and Quest 2 performance.

| File | What it is |
|---|---|
| [P3_FINAL_GAME_INTEGRATION_ROADMAP.md](P3_FINAL_GAME_INTEGRATION_ROADMAP.md) | Master roadmap: journey, principles, owner decisions D1-D7, milestones, order, validation |
| [p3-audit.md](p3-audit.md) | What exists, what is connected, what is missing (code-checked) |
| `p3-01-…` to `p3-19-…` | One plan per system, each with the same sixteen sections and a task table (p3-18: Quest ⇄ laptop ⇄ Ollama Cloud network runtime; p3-19: image-based Quest exterior) |
| [p3-task-index.md](p3-task-index.md) | Generated: every task in dependency waves, with owners and status |

**Conventions.** Plan files are `p3-NN-<slug>.md`, first line `# p3-NN-<slug> — <title> (<CODE>)`, sections 1-16 in a fixed order, tasks `P3-<CODE>-NN` with Owner (Ruhan, Moiz, Sannan, Team, Owner), Depends on (P3 task ids or existing P0-P2 step ids), Done when, Status (Planned, In progress, Done, Blocked). Change a task's status in its plan, then run `uv run --project tools tools/plans/p3_plans.py` to regenerate the index; CI runs it with `--check`. P3 uses task tables instead of step files so that the P0-P2 step tooling and counts (279 steps) stay unchanged.
