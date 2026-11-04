# ADR-0031 — Use ready-made licensed characters, and light the corridor with baked sun that enters only through real door glass

**Date:** 2 November 2026 · **Status:** Accepted by the owner's instructions of 2 November 2026 (visual review by ChatGPT still REJECTED, see `docs/qa/visual_audit_2026-11-02.md`) · **Related:** `docs/research/asset_sources_and_licenses.md`, `docs/deferred-tasks.md` VO-01 to VO-16

## Context
Characters generated in Blender from MPFB (clothing built by hand) kept being rejected by the owner and by the ChatGPT review: poses, hands, cloth. The owner then ordered that no character is created from scratch any more: replace them with complete, clothed, rigged, professionally made characters, document source, creator and licence, and verify each replacement before the old model is removed. The owner also supplied a lighting reference: low-angle sun entering through the glass of the doors, clear patches on the floor and the opposite wall, long frame and bench shadows, neutral 4000 K lamps, no blown ceiling panels, about -0.3 to -0.6 EV exposure, Quest settings kept separate.

## Decision
1. **Characters:** Microsoft Rocketbox (MIT, GitHub) for the people and Quaternius Universal Animation Library (CC0) for the movement, retargeted through Unity Humanoid. The Rocketbox files are fetched by `unity/Tools/Characters/fetch_rocketbox.py`, imported by `RocketboxSetup.cs` and placed by `RocketboxPeople.cs`. Mixamo, Asset Store and BlenderKit characters are not used: their terms do not allow redistribution in a public repository. The generated MPFB people stay in the project (prefabs `Staff_*`) until the owner approves the new ones; the patient in the patient bay stays MPFB because no licensed bed-gown or elderly model was found.
2. **Sun:** `CorridorSun.cs` adds two baked directional lights (17 degrees and a grazing 7 degrees, yaw 108, about 5800 K). The west corridor wall is one solid slab with the doors mounted in front of it, so the slab stops casting in the bake and static shadow-only blockers rebuild it (and each door leaf) with a hole exactly behind each door's vision glass; the glass casts nothing. Vision panes were widened to 0.52 x 1.05 m (`build_doors.py`). Everything is baked, so Quest has no extra realtime cost.
3. **Lamps and exposure:** neutral 4000 to 4300 K panels, realtime ceiling fills cut from 0.85 to 0.1, sconces become an upward and a downward baked soft cone (corridor, patient bay, theatre), ceiling diffuser emission 0.8 to 0.30, post exposure -0.4 EV on `Assets/DefaultVolumeProfile.asset`.

## Alternatives
- Keep generating characters and tune joints: rejected by the owner after repeated failures.
- Mixamo or purchased packs: better variety, but the licence does not allow the files in this public repository.
- One sun matching the patient bay window: its angle (36 degrees) gives floor streaks only, no opposite-wall patches, and changing it would alter the approved patient bay look.
- Cut real openings in the wall mesh: would change the wall UV mapping; blockers keep the visible wall unchanged.

## Consequences
Rocketbox is a stylised-realistic game avatar set (9,000 triangle bodies, no dedicated elderly or gown models). The lightmaps must be re-baked after any change to doors, walls or lights (about 11 minutes preview, about 45 minutes final). The rooms behind the west doors are open void in the bake, not modelled rooms; the patient bay's own walls block the doors next to it. Post exposure is shared by every room. No headset measurement exists.

## Revisit if
The owner rejects the Rocketbox look in the headset, a redistributable elderly or gown model is found, a Quest frame-time measurement fails, or the final-quality bake changes the patches materially.
