# Visual audit, 2 Nov 2026 (UTC)

Baseline for the quality overhaul. Frames are in `before_2026-11-02/` and are real Unity URP captures (final-quality bake). No new objects are added during this overhaul; the work is on the quality of what exists. Each finding is a register candidate for `visual_qa_register.json` and goes through the ChatGPT loop in `README.md`.

Reference standard: the owner's ArchVizPRO-style URP tutorial video (Unity 6, "ArchViz Vol.10"). The video itself could not be read from the build machine (only its title was reachable), so the standard used is its stated aim: believable materials, form, light and consistency, not post-processing.

## Verdict per environment

| Environment | Verdict | Why |
|---|---|---|
| PatientBay | Closest to target, still short | Lighting and ceiling are the benchmark. Walls and floor are flat colour with little surface response; a stray dark floor stain near the sink; props are clean but plain; vegetation is a low-detail plant. |
| Corridor | Below target | Walls are two flat tones with no paint or plaster definition; the floor is a smeared grey with no visible vinyl structure or seams; skirting and rail are thin flat strips; ceiling tiles show a blotchy noise pattern instead of a perforated acoustic tile; doors and frames read as flat slabs; wall frames and plaques are unlit-looking. |
| Reception | Below target | Desk is a smooth two-tone shape with no material detail (no counter edge, no screen, no objects); receptionist is hidden behind the desk; plants read as cut-out foliage; the sky behind the glass shows a bright disc. |
| Operating theatre | Furthest below target | Empty, flat, uniform grey room; props are simple primitives (cart, table, cabinet, stand); surgical light is a plain disc; no wall panels, scrub sink, monitors, pendants, gas outlets or floor detail. |
| Exterior | Does not meet target | Towers are plastic-looking blocks with repeated textured bands; trees are cartoon foliage; ground is flat paving; sky disc visible; scale and perspective are correct but surfaces are not photographic. |
| Staff characters | Below target | Lab coats look like stretched grey fabric with ballooned sleeves and no weave; hands over clipboards read poorly; coats lack the fold structure of real cloth. |

## Cross-cutting causes

1. **Materials.** Large surfaces use flat or low-variation colour. Real surfaces need albedo variation, roughness variation, a small normal map at true scale, and edge wear. Tile, vinyl, paint and plaster all need their own response.
2. **Geometry detail.** Trims, door frames, skirting, rails and light fixtures are thin extrusions with no bevels, so they catch no light. Bevelled edges (weighted normals) are the cheapest realism gain.
3. **Texel density.** Wall, floor and ceiling textures are not tied to a physical scale, so detail changes size between rooms.
4. **Lighting consistency.** PatientBay is the benchmark. Corridor and OT reuse its recipe but have no contact grounding for objects, and probe coverage for characters is thin.
5. **Exterior.** Procedural blocks cannot reach photographic quality. Facade textures need photographic sources (CC0), real glass reflections and depth; trees need real leaf cards with correct normals.
6. **Unity-side import.** Mipmaps, anisotropic level, texture compression and render scale have not been tuned per quality tier.

## Work order (highest visible gain first)

1. Wall, floor and ceiling materials for PatientBay, corridor, reception and OT (shared physical scale), with desktop and Quest texture tiers.
2. Trim and fixture geometry: bevelled skirting, rails, door frames, light housings.
3. Lab coats and clinical clothing (weave, fold structure, sleeves).
4. OT: replace primitive props with properly modelled equipment (still existing objects improved, not extra ones).
5. Exterior: facade textures, glass, trees.
6. Per-tier quality settings (Enhanced URP desktop preview vs Quest) and a measured budget.
7. Each step: capture, ChatGPT round, register, before/after pair.

## Tier plan

- Desktop preview: 2K to 4K textures, anisotropic 8x to 16x, normal maps, reflection probes, full shadow cascades.
- Quest: 1K textures, ASTC, anisotropic 2x, fewer realtime shadows, baked lighting only; every texture and mesh change gets a budget line.

Budget measurement (triangles, draw calls) on the Quest tier is still to be done; no headset performance is claimed.

## Progress log

| When (UTC) | Step | Result | Status |
|---|---|---|---|
| 2 Nov 2026 | 1. PBR wall and floor surfaces (`MaterialOverhaul.Apply`, `make_pbr_surfaces.py`; CC0 Poly Haven `white_plaster_02`, `rubber_tiles`, hospital colours) on PatientBay wall paint, PatientBay grey vinyl, OT vinyl | Floors now show real tile seams and satin response; walls gain fine grain. The change is modest: walls still read mostly flat and the corridor floor tile scale is not yet tuned. Frames: `after_2026-11-02/*_step1.png`. Lightmaps are stale for the new albedo (needs the final bake). Not yet ChatGPT-reviewed. | PARTIAL, READY FOR REVIEW after bake |
| 3 Nov 2026 | 2a. Ceiling, steel, veneer, curtains (ambientCG OfficeCeiling001, Metal011, Fabric066; Poly Haven oak_veneer_01) via `pack_pbr.py` + `MaterialOverhaul` pass 2 | Corridor ceiling now shows a real acoustic tile grid with fine surface; first pass made the OT ceiling look mouldy (AO and bump too strong), reduced to bump .3 / AO .15. Door veneer reads flat brown. Frames `Logs/Rooms/Corridor_m2`. | PARTIAL |
| | 2b. Bevelled trim, door frames, light housings | Not started | OPEN |
| 3 Nov 2026 | 3. Lab coats: first fix is material only (white cotton, fine tiling, low AO). Capture `Corridor_st1` showed the coats as grey knit-looking sweaters with ballooned shoulders and short skirt flare; the garment geometry (derived from a MakeHuman blouse, `unity/Tools/MPFB/staff_outfit.py`) is NOT rebuilt yet | OPEN |
| 3 Nov 2026 | 4. OT equipment: surgical light (twin LED heads on spring arms), operating table (column, segmented pads, headrest, side rails), anaesthesia workstation (drawers, vaporisers, bellows, screen, hoses), Mayo stand (tray, telescopic column, instruments), supply cupboard (panelled doors, handles, vents). `build_ot_equipment.py` + `OtEquipmentUpgrade.cs`; same prefab ids, colliders kept. Frames `Logs/Rooms/OT_e1o`, `OT_e1c`. Clearly better than the primitives but unbaked (new meshes render dark until the final bake); scrub sink, gas panels, boom monitor, floor/wall detail still missing. Triangles: light 6.8k, table 5.8k, anaesthesia 11.9k, Mayo 9.6k, cupboard 2.6k (heavy for Quest; LODs open) | PARTIAL |
| | 5. Exterior facades, glass, trees | Not started | OPEN |
| | 6. Desktop / Quest tiers and budget | Textures for step 1 have 2K desktop and 1K ASTC Android import; no budget measured | OPEN |

## ChatGPT rounds (genuine Unity captures, `unity/Tools/QA/chatgpt_review.py`, register `visual_qa_register.md`)

| Round | Scene | Decision | Counts | What changed between |
|---|---|---|---|---|
| 1 (3 Nov) | OT | REJECTED | 8 major, 1 minor | after PBR pass 2, rebuilt equipment, final bake b1 |
| 1 | Staff | REJECTED | 1 critical, 7 major | white cotton coats, balloon sleeves, collar claims, hands/boards, face, lighting |
| 1 | PatientBay | REJECTED | 7 major, 1 minor | dark floor patches, patient anatomy, gown, ceiling, exterior, plant |
| 2 (3 Nov) | OT | REJECTED | 8 major, 1 minor | + contact-shadow quads, segmented LED lenses, workstation control panel, base geometry, final bake b2 |
| 2 | Staff | REJECTED | 1 critical, 7 major | + coats and scrubs refitted to the body (`tighten`), scrub-top bulge removed |
| 2 | PatientBay | REJECTED | 6 major, 1 minor | + stale contact-shadow quads of a hidden chair removed (the dark floor patches are gone in pb1) |

Honest reading: the reviewer re-raises the same defect classes each round (it reviews each image fresh; the register ingest adds round-2 issues as new ids, so duplicates exist and none is marked VERIFIED yet). Visible fixes that are real but not yet acknowledged by the reviewer: floor patches gone, coats no longer ballooned or grey, scrub top no longer visible through the coat, ceiling now a real acoustic tile, equipment grounded. Still genuinely open and agreed with the reviewer: anaesthesia workstation front detail, table padding/rail realism, OT room fill light on equipment, collar/hem construction and pockets on the coats, hand-to-board contact and prop detail, the male's wide-eyed expression, patient feet/hands/skin and gown weave, plants (stacked leaf cards), exterior facade quality. The two-consecutive-round gate has not been started.

## 3 Nov 2026, second half (owner requests: furnished, populated, lit, natural people)

| Area | What was done | Status |
|---|---|---|
| Materials | Pass 3 maps every flat-colour material in PatientBay, Corridor and OT to a CC0 PBR set (steel, plastics, painted plaster, veneer, fabric, rubber, quartz, road, sidewalk); authored colours recorded in `Tools/Environment/data/material_originals.txt` | Done for the three rooms; exterior facades and plant leaves still generated art |
| Benches and furniture | Original perforated stainless 3-seat bench (alpha-cut holes, chrome arms and legs, teal foot caps), patient stretcher, round side tables, placed in facing groups along both corridor walls with a 2.2 m walking lane; wheelchairs from Poly Haven (CC0) | Placed; stretcher not yet seen in a capture |
| Wall decoration | 10 original posters in framed prints, Poly Haven clocks, framed photograph, fire alarm call points and extinguishers, plants; placement tests every candidate against all existing renderers | Placed, art visible after fixing a depth bug |
| Lights | 15 oval wall sconces with baked warm point lights; the sun was raised from a barely visible 0.35 to a real beam (pitch 36, yaw 70) so window-frame shadows and floor patches show; the NeutralDaylight contract ranges were widened for this on purpose (intensity up to 3.5, penumbra floor 1.5 degrees) | First pass overexposed and too warm; retuned (sun 1.7, sconce 0.75, panels 1.9) and rebaked |
| Sun shafts | Soft additive ribbons along the sun direction behind windows and glass walls, desktop only (`DesktopOnly`) | Visible at reception and PatientBay windows |
| Glass | Premultiplied-alpha panes so Fresnel reflection stays visible while the pane stays clear, plus a waviness normal and dust-varied smoothness | Applied; look verified only in passing |
| People | 8 civilians (MPFB, CC0) with natural poses: upright sitting against the backrest with hands on thighs or in the lap, two-handed phone, two-handed paperwork, coughing into the elbow, conversing; nurses moved to the theatre holding clipboards at chest/waist; idle behaviours per role | Placed; walking people, a wheelchair pusher and staff pushing equipment NOT done (no locomotion animation) |
| Contact shadows | Soft footprints and foot/castor spots for benches, tables, wheelchairs, stretcher, plants and every person (corridor) and the OT equipment | Applied |

Honest limitations: the reviewer (ChatGPT) has rejected every round so far; the corridor round-1 critical findings (bench legs hidden, door with mirrored text and overlapping wood) are only partly fixed. Door mirrored lettering ("RESTRICTED AREA" seen backwards on the Patient Bay door) is still open. No headset or Quest measurement was made.


## 2 Nov 2026: lighting and shadow rework to the owner's corridor reference

| Area | What was done | Status |
|---|---|---|
| Sun through door glass | `CorridorSun` (Assets/Editor/Environment): two baked directionals (17 degree sun for floor streaks, 7 degree grazing sun for wall patches, shared yaw 108, colour about 5800 K). The west wall was one solid slab with the doors mounted proud of it, so it stops casting in the bake and shadow-only static blockers rebuild it with a hole behind each door's vision glass; the door leaves are rebuilt the same way, so the glass is the only way in. Vision panes widened to 0.52 x 1.05 m (`build_doors.py`) | Patches on floor and opposite wall, long bench and frame shadows (preview bake) |
| Contrast | Realtime ceiling fills 0.85 to 0.1, room light 1.0 to 0.22, baked panels 1.9 to 0.85, darker trilight ambient, sun balance 6.2 / 2.8 | Round 20 said patches were blown white; sun lowered from 10 to 6.2 |
| Lamps | Sconce point lights replaced by an upward and a downward soft baked cone (about 4300 K) in Corridor, PatientBay (1) and OT (5): no hot spot on the wall | Applied |
| Ceiling | Diffuser emission 0.8 to 0.30, base 0.86 | Hot blobs reduced, still flagged |
| Exposure | Post exposure -0.4 EV on `Assets/DefaultVolumeProfile.asset` | Applied (shared by all rooms) |
| Quest | Everything is baked, no new realtime lights or shadows; the up/down cones add lightmap work only | Not measured on a headset |

Open: ChatGPT round 20 REJECTED (6 major, 1 minor: ceiling hot spots, floor contact shadows, sun patches clipping to white, bench shimmer, repetitive city backdrop, plant cards). A final-quality bake (`light_cycle.ps1 -Final`) has not been run on the rebalanced values, and the rooms behind the west doors are open void for the bake (the patient bay's own walls block the doors beside it); no modelled rooms exist there.

## 2 Nov 2026: characters replaced by ready-made avatars (ADR-0031)

| Area | What was done | Status |
|---|---|---|
| Source | 17 Microsoft Rocketbox avatars (MIT) fetched, imported as valid Humanoid rigs; 43 Quaternius UAL clips (CC0) | Done |
| Corridor | 5 seated (bench heights aligned by `SeatedPerson` using the sitting clip), 1 reader at the notice board; the talking pair was blocked by a physics check | Partly done |
| Theatre | Medical_Female_02 and Medical_Male_03 in white coats with stethoscopes, talking gesture clip | Looks right in the captured view |
| Known defects | White patch on one woman's hair, no held props, no walking, receptionist and children not replaced, patient bay unchanged | Open (VO-13) |
| ChatGPT | Not yet run on the new characters | Pending |
