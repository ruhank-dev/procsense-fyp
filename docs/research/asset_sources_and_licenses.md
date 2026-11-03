# Asset sources and licences

Created 3 Nov 2026 (UTC). Companion files: [../assets/EXTERIOR_ASSET_SOURCES.md](../assets/EXTERIOR_ASSET_SOURCES.md) (sky/city) and [../ATTRIBUTIONS.md](../ATTRIBUTIONS.md). Rule: only CC0 or compatible-with-public-repo assets are committed.

## PBR texture sets in the project (all CC0 1.0, no attribution required; credited here)

| Local name (`Assets/Textures/Hospital/PBR/`) | Source asset | Site and URL | Creator | Used on |
|---|---|---|---|---|
| hosp_wall | White Plaster 02 (`white_plaster_02`), recoloured | polyhaven.com/a/white_plaster_02 | Poly Haven contributors | Wall paint |
| hosp_floor | Rubber Tiles (`rubber_tiles`), recoloured | polyhaven.com/a/rubber_tiles | Poly Haven contributors | Vinyl floors |
| acoustic_ceiling | OfficeCeiling001 | ambientcg.com/view?id=OfficeCeiling001 | ambientCG (Lennart Demes) | Ceiling tiles |
| satin_steel | Metal011 | ambientcg.com/view?id=Metal011 | ambientCG | Satin steel fittings |
| brushed_steel | Metal009 | ambientcg.com/view?id=Metal009 | ambientCG | Door hardware, mullions, aluminium, steel doors |
| oak_veneer | Oak Veneer 01 (`oak_veneer_01`) | polyhaven.com/a/oak_veneer_01 | Poly Haven contributors | Door veneer |
| curtain_weave | Fabric066 | ambientcg.com/view?id=Fabric066 | ambientCG | Curtains |
| twill_cotton | Fabric061 | ambientcg.com/view?id=Fabric061 | ambientCG | Lab coats, scrubs |
| cotton_white | Fabric086 | ambientcg.com/view?id=Fabric086 | ambientCG | Gown, bed linen |
| white_plastic | Plastic010 | ambientcg.com/view?id=Plastic010 | ambientCG | Light housings, trim, door frames, ivory equipment shells |
| terrazzo | Terrazzo001 | ambientcg.com/view?id=Terrazzo001 | ambientCG | Reception quartz counter |
| dark_plastic | Plastic011 | ambientcg.com/view?id=Plastic011 | ambientCG | Dark equipment housings, bezels, keyboards, door seals |
| rubber_black | Rubber004 | ambientcg.com/view?id=Rubber004 | ambientCG | Castor rubber |
| asphalt, road_marked | Asphalt031, Road007 | ambientcg.com | ambientCG | City ground, city road |
| sidewalk | Concrete Pavement 02 (`concrete_pavement_02`) | polyhaven.com/a/concrete_pavement_02 | Poly Haven contributors | City sidewalk, terrace paving |
| facade_dark_glass, facade_brick_glass, facade_curtainwall | Facade019A, Facade018A, Facade001 | ambientcg.com | ambientCG | Exterior towers (not yet assigned) |

ambientCG licence: CC0 1.0 (docs.ambientcg.com/license). Poly Haven licence: CC0 (polyhaven.com/license). Downloads were fetched from their public APIs (`api.polyhaven.com`, `ambientcg.com/get`) at 2K JPG; `unity/Tools/Environment/pack_pbr.py` repacks them. Local scans live in `build/pbr` and `build/pbr2` (not committed). Pass 3 of `MaterialOverhaul.cs` maps every flat-colour material in PatientBay, Corridor and OT to one of these sets by category (inventory: `MaterialAudit.cs`, authored colours kept in `unity/Tools/Environment/data/material_originals.txt`).

## Candidates compared and not chosen (texture search, 3 Nov)

| Candidate | Why not |
|---|---|
| Poly Haven `ceiling_interior` | Rough stucco, not an acoustic tile. |
| Poly Haven `terrazzo_tiles`, `floor_tiles_06`, `large_grey_tiles` | Brown/chequered or stone look, wrong for a clinical floor. |
| Poly Haven `metal_plate`, `metal_plate_02` | Diamond/rusted industrial plate; Poly Haven has no clean stainless set. |
| ambientCG OfficeCeiling004 | Light-panel variant, needs emission handling. |

Unity Asset Store and Sketchfab were searched as discovery sources only: their licences forbid or complicate redistribution in a public repository (Asset Store EULA) or vary per model; nothing was imported from them.

## Characters and clothing

ChatGPT's independent answer ([../research/chatgpt/characters_clothing.md](chatgpt/characters_clothing.md)) agrees with the existing pipeline: **no complete, verified CC0/CC-BY set of realistic rigged hospital staff and garments exists**; the reliable route is MakeHuman/MPFB2 bodies (CC0 export), custom Blender wardrobe, rebuilt URP materials. Mixamo is excluded (not redistributable in a public repo). Quaternius and KayKit are CC0 but stylised, so wrong for realism. Current characters: see `unity/Assets/Models/Characters/MPFBPatient/README.md` (MPFB 2.0.18, MakeHuman system assets, Faceunits and Visemes packs, CC0 output).

## Sky and city

See [../assets/EXTERIOR_ASSET_SOURCES.md](../assets/EXTERIOR_ASSET_SOURCES.md).

## Medical equipment: Sketchfab CC-BY candidates found (3 Nov), NOT imported

Found with Sketchfab's public search API (`downloadable=true`, licence `by`). Sketchfab only serves the files to a logged-in account or API token, which this build machine does not have, so none was downloaded. CC-BY would need attribution and a modification note. Face counts are the uploader's; most need retopology or decimation for Quest. Verify the licence on each page before import. ChatGPT (see [chatgpt/medical_equipment.md](chatgpt/medical_equipment.md)) could not name any verified model, so these come from the API only.

| Need | Candidate (uploader, faces) | Notes |
|---|---|---|
| Operating table | "Operating Table" (shashkinv1ad, 46k) | Best size for a hero prop; "High Poly" 1.16M is too heavy. |
| Surgical lights, trolleys, misc | "Realistic Medical Equipment and Accessories Set" (daonware, 71k) | Single set, would cover several OT props if quality holds. |
| Surgical light | "_led Head Light A" (krio302, 6k) | Check it is a theatre light. |
| Patient monitor | "Patient Monitor" (Ram-je, 156k) | Needs decimation. |
| Anaesthesia machine | "Anesthesia Machine on a Medical Cart" (kkso199, 793k) / "anesthesia machine" (limj4913, 728) | One too heavy, one too crude. |
| Scrub sink | "Scrubbing Sink" (miraclejudy3, 77k) | Needs decimation. |
| IV stand | "IV Stand" (teeart, 5k) | Light enough for Quest. |
| Wheelchair | "Wheelchair" (maxencerouillet, 9.5k) | |
| Hospital bed | "Hospital Bed" (cobblegames 5.5k, larry3d 9.7k) | Existing bed is already custom and approved; low priority. |

To unblock: a Sketchfab API token (or manual downloads dropped in `build/sketchfab/`), then import with Blender and record each in the table above.

## Poly Haven 3D models in the project (CC0 1.0, no attribution required; credited here)

Fetched with `unity/Tools/Environment/fetch_ph_model.py` (glTF + 2K textures, `api.polyhaven.com/files/<id>`), converted by `convert_ph_model.py` (textures repacked to the project PBR layout, decimated to the triangle target, FBX), imported by `PolyHavenProps.cs`. Triangles are after decimation for Quest-friendly budgets.

| Local prefab | Poly Haven id | URL | Triangles | Used for |
|---|---|---|---|---|
| potted_plant_01 | `potted_plant_01` | polyhaven.com/a/potted_plant_01 | 18,000 (176,226 in) | Floor plants, corridor and bay |
| potted_plant_02 | `potted_plant_02` | polyhaven.com/a/potted_plant_02 | 18,000 (69,806 in) | Floor plants (syngonium) |
| potted_plant_04 | `potted_plant_04` | polyhaven.com/a/potted_plant_04 | 8,038 | Tabletop succulent (not yet placed) |
| wheelchair_01 | `wheelchair_01` | polyhaven.com/a/wheelchair_01 | 14,000 (40,252 in) | Parked wheelchairs in the corridor |
| fire_alarm | `fire_alarm` | polyhaven.com/a/fire_alarm | 2,705 | Wall call points |
| korean_fire_extinguisher_01 | `korean_fire_extinguisher_01` | polyhaven.com/a/korean_fire_extinguisher_01 | 6,000 | Floor extinguishers (label is Korean) |
| hanging_picture_frame_02 | `hanging_picture_frame_02` | polyhaven.com/a/hanging_picture_frame_02 | 3,678 | Framed photograph |
| wall_clock | `wall_clock` | polyhaven.com/a/wall_clock | 3,658 | Wall clocks |

Candidates seen and rejected (rusted, vintage or not hospital-appropriate): `tool_cart`, `industrial_storage_cart`, `medical_box`, `metal_stool_*`, `metal_office_desk`, `modern_arm_chair_01`, `fancy_picture_frame_*` (gilded antique).

## Original project artwork and models (project-owned, no third-party licence)

| Asset | Tool | Note |
|---|---|---|
| Wall posters (hand hygiene, 5 moments, cough etiquette, fire safety, wristband check, wellbeing, quiet zone, visiting guide, sharps, theatre brief) | `unity/Tools/Environment/make_wall_posters.py` | Generic safety text, no clinical values; Windows system fonts are used to render but not redistributed. Heart/lung posters were drawn but not used (too crude). |
| Perforated stainless waiting bench, patient stretcher, round side table | `build_waiting_furniture.py`, `make_perforated_steel.py` | No verified free redistributable perforated-bench model exists: web search found only FurniMesh `tmov81` (licence not stated, not used), a paid CGTrader airport bench and a CC0 garden bench; ChatGPT named none. Sketchfab candidates need an authenticated session (see the equipment table above). |
| Operating-theatre equipment | `build_ot_equipment.py` | See equipment section. |
| Glass imperfection maps | `make_glass_maps.py` | Waviness normal and dust-varied smoothness. |

## Civilian characters (MakeHuman community assets via MPFB 2.0.18, CC0 output)

Eight fictional people (elderly man and woman, young woman, child, parent, two visitors, one reader) are built by `unity/Tools/MPFB/build_civilians.py` from the MakeHuman system skins, hair, clothes and shoes (casual, striped and elegant suits, CC0), posed by `civilian_poses.py` and baked by `staff_bake.py`, exactly like the staff. ChatGPT confirmed no complete free rigged medical wardrobe or civilian pack exists; Mixamo is excluded (not redistributable in a public repo). The three women share the one blouse-and-skirt mesh, recoloured per person.


## Ready-made characters and animation (2 Nov 2026 replacement of the generated people)

| Asset | Source | Creator | Licence | Used for |
|---|---|---|---|---|
| Microsoft Rocketbox avatars: `Medical_Male_01/03/04/05`, `Medical_Female_01/02`, `Business_Female_03`, `Male_Child_01`, `Female_Child_01`, `Male_Adult_03/08/13/16`, `Female_Adult_05/08/14/15` (clothed, rigged Biped, 176 facial blend shapes, hi-poly with opacity-mapped hair) | https://github.com/microsoft/Microsoft-Rocketbox (`Assets/Avatars/*/<Name>/Export/<Name>_facial.fbx` and `Textures/`) | Microsoft Corporation | MIT (copy at `unity/Assets/Characters/Rocketbox/LICENSE.md`) | Corridor waiting people, standing talkers, theatre doctors, receptionist, children |
| Universal Animation Library (Standard), clips `Idle_Loop`, `Idle_Talking_Loop`, `Sitting_Idle_Loop`, `Sitting_Talking_Loop`, `Walk_Loop`, `Walk_Formal_Loop` and others (43 clips) | https://quaternius.com (Universal Animation Library) | Quaternius | CC0 1.0 (copy at `unity/Assets/Characters/Animations/LICENSE_Quaternius_UAL_CC0.txt`) | Retargeted onto every Rocketbox avatar through Unity Humanoid |

Not available under a redistributable licence, so documented but not imported: a ready-made hospital bed gown, dedicated elderly models (the closest adults are used), Mixamo characters (Adobe terms forbid redistribution in a public repository), Unity Asset Store and BlenderKit packs (store licences). The patient in the patient bay therefore stays on the earlier generated model until a licensed replacement is verified.

## Where everything came from and how to download it again (2 Nov 2026)

| What | Where | How it got into the project |
|---|---|---|
| Rocketbox avatars (MIT) | https://github.com/microsoft/Microsoft-Rocketbox, folder `Assets/Avatars/<group>/<Name>/Export/<Name>_facial.fbx` and `Textures/*.tga` (raw files at raw.githubusercontent.com/microsoft/Microsoft-Rocketbox/master/) | `python -I unity/Tools/Characters/fetch_rocketbox.py [Name ...]` (needs `build/rocketbox_tree.json`, the GitHub tree listing); then Unity menu method `ProcSense.EditorTools.RocketboxSetup.Import`, `BuildPrefabs`, `RocketboxPeople.Apply` |
| Universal Animation Library (CC0) | https://quaternius.com (Universal Animation Library, Standard pack, `Unity/UAL1_Standard.fbx`) | Unzipped to `build/ual/x/`, copied by `RocketboxSetup.Import` |
| PBR textures (CC0) | ambientCG https://ambientcg.com and Poly Haven https://polyhaven.com/textures | `unity/Tools/Environment/pack_pbr.py`, `make_pbr_surfaces.py`; the sets used are listed at the top of this file |
| 3D props, plants, clocks (CC0) | Poly Haven models https://polyhaven.com/models | `fetch_ph_model.py`, `convert_ph_model.py`, `PolyHavenProps.cs` |
| Sky and city backdrop | Poly Haven HDRIs (`sky_partly_cloudy_4k`, `city_skyline_8k`), see `docs/assets/EXTERIOR_ASSET_SOURCES.md` | `CityExterior.cs` |
| Medical equipment candidates (Sketchfab CC-BY) | Listed above, NOT imported: need a signed-in Sketchfab session or API token | Open (VO-02) |
| Mixamo (https://www.mixamo.com), Unity Asset Store, BlenderKit | Researched, excluded: Adobe and store licences do not allow redistributing the files in a public repository | Not used |
| SpeedTutor Unity channel research | YouTube channel inventory and master guide | `docs/research/speedtutor_unity_masterguide.md`, `speedtutor_video_inventory.csv` |
| Reference photos from the owner | Doctor and nurse clothing photos, corridor lighting reference image | Used as visual targets only, not shipped |

Commands to rebuild the lighting after any change to doors, walls or lights: `powershell -File unity/Tools/Rooms/light_cycle.ps1 -Tag <name>` (preview bake about 11 minutes; add `-Final` for the roughly 45 minute final bake; `-SkipDoors` when the Blender doors did not change). Captures land in `unity/Logs/Rooms/` (git-ignored).

## Patient character (3 Nov 2026): licensing waived by the owner

The generated MPFB patient could not be given correct clothes, so the owner asked for an existing, free, ready-made clothed character and said licensing does not matter for this asset ("forget licensing, it should be free"). Current state: a clothed Microsoft Rocketbox avatar (MIT, `Male_Adult_08`) lies on the bed; the MPFB rig stays live but invisible so the mounted sensors and idle hooks keep working. Candidates found for something more realistic, none downloaded yet because each needs a site login: Meshy community "Photorealistic 3D human, 25-year-old male patient" (light-blue gown, arms at sides, legs straight; Meshy's FAQ states its community patient assets are CC0), Meshy "Patient in Hospital Gown", Sketchfab and CGTrader free human sets. When a file is supplied it is recorded here with its source URL and the licence the page states.
