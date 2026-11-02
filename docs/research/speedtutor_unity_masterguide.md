# SpeedTutor Unity master guide (ProcSense)

Reusable Unity visual-quality knowledge base. Created 3 Nov 2026 (UTC). Technique-ordered, not video-ordered. All explanations are written for this project; no transcript is reproduced.

## How this was researched, and what is NOT covered

| Item | Status |
|---|---|
| Channel inventory | **Done**: 1,032 public videos listed with `yt-dlp --flat-playlist` (public metadata only). Full list with coverage status: [speedtutor_video_inventory.csv](speedtutor_video_inventory.csv). |
| Videos whose auto-captions were read | **14**: i6m0RVHyaWQ (ArchViz Vol.10, the owner's reference), SDN22snbfZA (ArchViz Vol.8), -VR6C7m0KYA (realistic graphics, Unity 5), 9Vzr7VE7ChA (URP decals), -WoliklAr28 (glass, HDRP), 0Q18sviED6E (optimisation), 7g7DS-0cZ44 (skyboxes/HDRI), 8L6TuHxs7jc (runtime quality settings), GE3hKsb5JQ4 (auto exposure), HIY-nJsFJ4I (ambient occlusion), Oy-MEW5atyM (impostors), SeXdXvtunNY (URP post-processing), m7EAdnvStHI (URP render features), ximKTroBqeA (reflection probes). Only captions and descriptions were read; **no video was watched**, so anything shown only on screen is not captured. |
| Relevant but not yet analysed | **123** titles flagged "relevant, not yet analysed" in the CSV (lighting, shaders, VR, terrain, vegetation, post-processing). Resume by working down that list. |
| Everything else (~895) | Title only; not relevant to a hospital visual overhaul (asset-sale roundups, 2D, game-systems tutorials). |
| Channel page via WebFetch | Unreadable (only YouTube chrome returned); the inventory came from `yt-dlp`. |
| ChatGPT research | Asked on 3 Nov for free character/clothing sources: [chatgpt/characters_clothing.md](chatgpt/characters_clothing.md). Its URLs are leads to verify, not sources. |

**Resume instructions.** `build/yt/venv` (not committed) holds yt-dlp; recreate with `python -m venv build/yt/venv && build/yt/venv/Scripts/python -m pip install yt-dlp`. Fetch captions with `--skip-download --write-auto-subs --sub-langs "en.*" --sub-format vtt`. Take the next ids from the CSV rows marked "relevant, not yet analysed", add techniques below, then change their status in the CSV.

Unity version in this project: 6000.5.10f1, URP. Desktop = Enhanced URP; Quest 2 = Optimized URP (not verified on a headset).

## Techniques

Format per entry: what / why / source / compat / how / perf / ProcSense use / status / verify.

### 1. Baked GI with Progressive GPU lightmapper and Shadowmask or Baked
- **What/why:** bounce light and soft contact shadows computed offline; the single biggest realism gain indoors. The reference (ArchViz Vol.8/Vol.10) bakes at high resolution (about 1 GB of lightmaps, 4K maps, padding 4 to 5), and keeps all static furniture static so it receives the bake.
- **Source:** [How To Get REALISTIC GRAPHICS in UNITY 6 URP (ArchViz Vol.10)](https://www.youtube.com/watch?v=i6m0RVHyaWQ); [ArchViz Vol.8](https://www.youtube.com/watch?v=SDN22snbfZA). Docs: Unity Manual, "Lighting overview".
- **Compat:** URP desktop yes; Quest yes (baked only, lower lightmap resolution).
- **How:** static flags on non-moving meshes; per-renderer Scale In Lightmap (walls 1, hero props higher); lightmap UVs generated or authored in channel 2 with no overlapping islands to stop bleeding; area lights (baked only) at windows and fixtures; mixed point lights for dynamism.
- **Perf:** lightmap memory dominates; keep AA samples low.
- **ProcSense:** three rooms share one bake in `PatientBay/LightingData`; recipe in memory and `unity/Tools/Rooms`.
- **Status:** implemented (final bake 37 min); stale after any albedo change, rebake needed.
- **Verify:** `NeutralDaylightContractTests` (EditMode, needs the `-final` bake) and room captures.

### 2. Light probes for dynamic objects
- **What/why:** characters and movable props pick up baked light from probe groups; without them they look pasted-in. Unity 6 also has Adaptive Probe Volumes, the reference still uses classic probe groups.
- **Source:** Vol.10 and Vol.8 videos above.
- **Compat:** URP desktop yes; Quest yes (classic groups are cheaper).
- **ProcSense:** 348 probes baked; thin around characters.
- **Status:** implemented, coverage near staff to be improved.
- **Verify:** character shading matches the surrounding lightmap in a close-up.

### 3. Reflection probes with box projection
- **What/why:** correct reflections on glass, steel and glossy floors; box projection fits indoor rooms. Many probes (shower, kitchen, table) with equal importance, baked.
- **Source:** [Reflection Probes in Unity 5](https://www.youtube.com/watch?v=ximKTroBqeA) (concepts unchanged), Vol.8.
- **Compat:** URP yes (baked); Quest yes baked only, small cubemap.
- **ProcSense:** probes exist per room; count/coverage to check on OT and corridor.
- **Status:** partial.
- **Verify:** steel and window glass show the room, not the sky only.

### 4. HDRI sky for background and ambient
- **What/why:** a photographic HDRI gives believable sky colour and sun direction and feeds ambient light in the bake.
- **Source:** [Skyboxes & HDRI Maps in Unity](https://www.youtube.com/watch?v=7g7DS-0cZ44); Vol.10 uses an HDRI sky.
- **ProcSense:** `Skybox/Panoramic` material; Homecoming rooftop HDRI (CC0) with sun disc painted out by `make_city_skybox.py`.
- **Status:** implemented; exterior quality still below target.

### 5. URP Decal Projector
- **What/why:** projects dirt, scuffs, signage, door strips onto any surface without extra geometry or z-fighting.
- **Source:** [Decals in Unity URP](https://www.youtube.com/watch?v=9Vzr7VE7ChA); Vol.8 uses decals for door strips and mirror.
- **Compat:** URP 12+; add "Decal" Renderer Feature, Decal Projector component with a Shader Graphs/Decal material. Quest: possible but costs fill rate, use sparingly and verify on device.
- **ProcSense use:** floor scuffs and wheel marks in corridor, wall corner wear, mirror/sink splash. 
- **Status:** **not implemented** (next).
- **Verify:** decal visible in capture and no flicker at distance.

### 6. Post-processing volumes (global + local)
- **What/why:** ACES tonemapping with colour adjustments and post-exposure gives contrast; local volumes change exposure between rooms and outside; bloom only on genuine hot spots. The reference keeps vignette/film grain/chromatic aberration subtle.
- **Source:** Vol.10, Vol.8, [URP post-processing guide](https://www.youtube.com/watch?v=SeXdXvtunNY), [render features](https://www.youtube.com/watch?v=m7EAdnvStHI), [ambient occlusion at runtime](https://www.youtube.com/watch?v=HIY-nJsFJ4I), [auto exposure](https://www.youtube.com/watch?v=GE3hKsb5JQ4).
- **Compat:** URP yes. Quest: drop almost all of it (the reference does the same); tonemapping only if budget allows.
- **ProcSense rule:** no bloom/sharpening to hide poor assets (owner brief).
- **Status:** partial (neutral grade in place).

### 7. SSAO as a URP Renderer Feature
- **What/why:** adds contact darkening where baked lighting cannot (dynamic objects).
- **Compat:** URP built in; desktop only, Quest off.
- **Status:** to evaluate against baked contact shadow quad already in the project.

### 8. LOD Groups and impostors
- **What/why:** fewer triangles at distance; the references ship LODs on all furniture. Impostors replace distant props with a few quads.
- **Source:** [Amplify Impostors](https://www.youtube.com/watch?v=Oy-MEW5atyM) (paid asset; the principle is available through LOD Groups for free), Vol.8, [optimisation guide](https://www.youtube.com/watch?v=0Q18sviED6E).
- **Compat:** URP yes; Quest strongly recommended (avoid the highest LOD).
- **ProcSense:** characters and exterior buildings first.
- **Status:** not implemented.

### 9. Quest/VR tiering (from Vol.8 developer answers)
- **What/why:** Quest build = simplified shaders, lowest-LOD bias, fully baked lighting, lower texture resolution, no post-processing, merged props for fewer draw calls. PC URP differs mainly in more baked lights and richer post.
- **ProcSense:** Android texture override 1K ASTC 6x6 already used for new PBR maps; no measured budget yet.
- **Status:** partial; **no Quest claim without headset test (Sannan)**.

### 10. PBR texture authoring (albedo, normal, smoothness/metallic mask, AO)
- **What/why:** all assets in the references use a base/metallic-smoothness/normal/AO workflow so they hold up in any light.
- **How in this project:** `unity/Tools/Environment/pack_pbr.py` packs ambientCG or Poly Haven scans into `_albedo/_normal/_mask(RGBA: R metallic, A smoothness)/_ao`; `MaterialOverhaul.cs` imports them (normal-map type, 2K desktop, Android 1K ASTC, aniso 8, trilinear) and assigns `_BaseMap/_BumpMap/_MetallicGlossMap/_OcclusionMap`.
- **Status:** implemented for walls, floors, ceiling, steel, veneer, curtains, cloth; exterior pending.

### 11. Glass
- **What/why:** transparent surface with high smoothness (about 0.95), near-black tint with low alpha, reflection probes supply the look; frosted variants use distortion.
- **Source:** [Create Realistic Glass in Unity](https://www.youtube.com/watch?v=-WoliklAr28) (HDRP; the URP equivalent uses Surface Type Transparent with a smoothness and a reflection probe).
- **Status:** window glass exists; exterior facade glass not yet photographic.

### 12. Custom cloth shaders (Shader Graph) for curtains and fabric
- **What/why:** the reference uses Shader Graph fabric (sheen/back-light) with LODs. 
- **ProcSense alternative:** URP Lit with a real weave normal map and low smoothness (done); a Shader Graph with sheen is a later step. Not enabled blindly.

## Priorities from this guide for ProcSense
1. Rebake final after the new materials (lightmaps are stale).
2. Decals for floor/wall wear (technique 5).
3. LOD groups and light-probe coverage for characters (2, 8).
4. Reflection-probe audit (3).
5. Exterior photographic sources (4, 11).
