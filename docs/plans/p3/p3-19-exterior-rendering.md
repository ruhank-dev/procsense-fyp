# p3-19-exterior-rendering — Image-based Quest exterior that keeps the approved city look (EXT)

Status: In progress (P3, 7 November 2026 UTC; prototype built and measured on the desktop 8 November 2026; headset comparison and owner approval pending). Owner decision D7 authorises investigating an image-based, Quest-only exterior; **adopting it as the production Quest exterior needs the owner's approval of side-by-side captures** (P3-EXT-07). The desktop city and its scene configuration stay as they are and remain the reference. Investigation results: `docs/qa/quest2_performance_results.md` section 4 and `docs/qa/quest2_static_render_stats.md`.

## 1. Objective and final user-facing behaviour

From every window and glass wall the player sees the same city as today: the photographed skyline, the near towers, the streets with traffic and the trees, lit by the same sun, with correct depth in both eyes when they move their head. On Quest the distant and middle parts cost a few textured cards and boxes instead of full geometry; the parts close to the glass keep real geometry where stereo depth or reflections need it.

## 2. Current implementation status

- **The exterior is already hybrid (confirmed in code and scenes):** `CityExterior.cs` (2 Nov 2026 direction "skybox first") made the **far city a photographic HDRI skybox** (Poly Haven "Canary Wharf", CC0, processed by `Tools/Environment/make_city_skybox.py`, imported 8K on PC and **4K on Android**); the procedural **Layer C skyline impostor rings are switched off** (kept in the scene); **Layer A near towers, Layer B mid towers, the podium, streets, static traffic and trees stay as geometry** under `City exterior` in the Corridor scene. City materials carry their own daylight as emission (no scene light for the city). PatientBay's old billboard backdrop (`Window exterior backdrop`) is off.
- **Cost on Quest:** not measured. The static workload of the city per view is in `docs/qa/quest2_static_render_stats.md` (renderers and triangles of `City exterior` inside each benchmark view's frustum). DC-13 configuration B8 ("City exterior hidden") measures the upper bound of what any replacement can save.
- **Built 8 November 2026 (desktop only, `docs/qa/quest2_exterior_optimisation.md`):** the Quest exterior variant exists beside the untouched original. The inventory (P3-EXT-02, `quest2_exterior_inventory.md`) showed that the buildings are already textured boxes (about 28,000 of the city's 196,844 triangles); **traffic (90,654) and trees (64,970) are 79 %**, and all of it is submitted whole because the city is a few merged meshes. The prototype therefore did not replace the towers with images: it (1) rebuilds traffic and trees from the same seed and placement with less detail far from the hospital (`build_city.py` with `CITY_VARIANT=quest`), (2) splits traffic, trees, ground markings and mid-tower concrete into 401 spatial chunks, and (3) at run time draws only the chunks inside the view cone through each window or door (portal culling in `ExteriorVisibility`). City cost on desktop falls 60 to 92 % per view with images identical to within drifting dust and leaf-edge pixels. A switch (`QuestExterior`, default Original) shows either; diagnostic configurations B17 and B18 expose it in the headset build.
- Not built: facade impostor atlases and far-layer cards (the buildings are cheap already, and the far city is the existing photographic skybox); revisit only if DC-13 shows the remaining city cost matters.

## 3. Existing code, assets, scenes and systems to reuse

`Assets/Editor/Environment/CityExterior.cs` (Import, Build, Atmosphere, photographic skybox switch, `KeepNearCity`), the Layer C ring builder (alpha-cut skyline strips, already a parallax-free impostor technique), `Tools/Environment/make_city_skybox.py`, the QA capture views `unity/Tools/QA/views/exterior.json` (ex01 to ex08) and `qa_capture.ps1`, `compare_captures.py`, the ChatGPT visual QA loop (`unity/Tools/QA/chatgpt_review.py`, register), `QuestPerfDiagnostics` (B8) and `QuestRenderStats`.

## 4. Missing functionality

Measurement of the city's share on the headset; a classification of every exterior object by distance band and by what needs real depth; a capture tool that renders the current city into impostor textures from the hospital's viewpoints; a Quest-only exterior variant switched by the render tier; comparison captures and an approval record.

## 5. Architecture and integrations

**Distance bands (decided per object from its distance to the nearest window, not by type):**

| Band | Distance from the glass | Representation on Quest | Why |
|---|---|---|---|
| Near | under about 40 m | Original geometry (LOD0) | Stereo disparity and head-motion parallax are clearly visible; reflections in the glass |
| Middle | about 40-250 m | The original towers with a simplified LOD (box or low-poly hull) carrying a facade texture **baked from the original model** | Parallax still noticeable on head movement; a box with the right facade keeps depth correct |
| Far | over about 250 m | The existing photographic skybox plus optional depth-layered cards (the Layer C technique) baked from the current city | Stereo disparity at 250 m is far below what the Quest 2's resolution shows; a skybox is exact there |

The thresholds are first values; P3-EXT-02 fixes them from the views (the two eyes' views of a point at distance d differ by about 0.064/d radians at 64 mm interpupillary distance; at about 20 pixels per degree on Quest 2 that is 1 pixel at roughly 73 m and under 0.3 pixel beyond 250 m, so the far band shows no stereo depth a viewer could see, while the middle band still does and keeps real boxes).

**Impostor bake:** an Editor tool places a capture camera at the centre of each window band (bay window, both corridor glass walls, reception glass) and renders the original city layers with the same exposure and emission settings into textures (facade atlases per middle tower; cards per far layer), using the approved capture pipeline. Nothing is painted by hand, so colours, sun direction and haze match. The original objects stay in the scene, disabled only on the Quest tier when the variant is enabled.

**Tier switch:** a `QuestExterior` component on `City exterior` chooses Original or Optimised on Android (default Original until P3-EXT-07 approval); desktop always Original. The diagnostic build gets a configuration "B15 optimised exterior" to measure it against B0 and B8.

**What is never done:** replacing the whole city with one flat photograph; removing near geometry seen at stereo distance; changing the desktop city.

## 6. Implementation tasks

| ID | Task | Owner | Depends on | Done when | Status |
|---|---|---|---|---|---|
| P3-EXT-01 | Measure the city's cost on Quest: DC-13 part B configuration B8 (city hidden) against B0 in V3 and V8, cool and warm | Sannan | P3-PRF-01 | Numbers in `docs/testing/results/DC-13.md`; decision whether EXT continues (skip if the saving is negligible) | Planned |
| P3-EXT-02 | Exterior inventory: every `City exterior` object with its distance to the nearest window, triangles, materials and band (near/middle/far), generated by an Editor tool into `docs/qa/` | Ruhan | — | Inventory file generated and checked into docs | Done |
| P3-EXT-03 | Capture tool for facade atlases and far-layer cards: **replaced** by the geometry LOD and chunk build (`build_city.py` `CITY_VARIANT=quest`, `QuestExteriorBuild`); the buildings are already textured boxes, so no atlas is needed | Ruhan | P3-EXT-02 | Variant meshes regenerate identically from the seed (verified: original outputs unchanged) | Done |
| P3-EXT-04 | Quest exterior variant (reduced far traffic and trees, spatial chunks, portal culling in `ExteriorVisibility`), `QuestExterior` tier switch, default Original; diagnostic configurations B17 and B18 | Ruhan | P3-EXT-02 | Variant exists; original city objects untouched; desktop original captures identical; headset check of B17 against B0 pending (DC-13) | In progress |
| P3-EXT-05 | Comparison captures: original against optimised for the twelve desktop views (V1-V9 plus three street-facing views), difference masks and metrics in `docs/qa/quest2_exterior_optimisation.md`; stereo pairs (64 mm) and a head-movement sequence still to do | Ruhan | P3-EXT-04 | Comparison sheet with difference metrics (single-eye views done) | In progress |
| P3-EXT-06 | Visual QA loop on the comparison (strict review, two clean rounds) and headset check of depth and parallax (DC-13 part B, B15) | Sannan | P3-EXT-05 | Register items closed; headset notes recorded | Planned |
| P3-EXT-07 | Owner approval: use the optimised exterior on Quest, or keep the original | Owner | P3-EXT-06, P3-EXT-01 | Decision recorded; tier default set accordingly | Planned |
| P3-EXT-08 | Tests: tier switch default, desktop always original, every original object still present and recoverable | Ruhan | P3-EXT-04 | EditMode tests green | Done |

## 7. Dependencies and execution order

Measure first (EXT-01; if B8 shows little gain, stop and record it). Inventory, capture tool, variant, comparison, QA, owner approval, tests.

## 8. Frontend components

None (diagnostic configuration only).

## 9. Backend and API changes

None.

## 10. AI and voice integration

None. Image generation by AI is not used: impostors are rendered from the project's own city so they match it.

## 11. Unity objects, prefabs, animations and assets

Facade atlases, far-layer cards, simplified hulls for middle towers, `QuestExterior` component; originals untouched.

## 12. Performance

The point of this plan; budgets come from DC-13 B0 against B8.

## 13. Error handling and edge cases

Visible seams between bands (cross-fade or band overlap); sun direction baked into facades must match the baked interior sun (same `SunAzimuth`/`SunElevation`); reflections in the glass showing the card edges; window reflections probes rebaked only with the connected bake rule (project memory: three rooms bake together).

## 14. Testing

Capture comparison, visual QA loop, EditMode tests, headset check of depth.

## 15. Acceptance criteria

1. The owner approves the optimised exterior from the comparison sheet, or the original is kept.
2. No visible depth error in the headset at the windows (DC-13 notes).
3. Desktop captures identical before and after.

## 16. Definition of Done

All EXT tasks Done or closed by the measurement of P3-EXT-01; decision recorded.
