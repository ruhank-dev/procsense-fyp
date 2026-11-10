# Lane 3, task V: does B58 keep the look? (two changes: 2 additional lights per object, hard shadows)

Written 10 November 2026 (UTC). **Status: final (section 4).** Part 1 below uses the captures that already exist; they can show where the two changes act but cannot settle monitors, posters, faces or door glass. Part 2 is the one GPU launch that is registered for lane 4 (`docs/qa/audit/benchmark_requests.md`, section b58_visual); the final verdict is written here after that launch. Desktop captures only; nothing here predicts Quest frame time, and B58's frame-time effect is lane 4's.

B58 = B20 + `MaxAdditionalLights 2` (URP asset, was 4) + `SoftShadowsOff` (URP asset, hard realtime shadows). Only the sun casts realtime shadows (additional lights are unshadowed), so the shadow change touches sunlit floor and wall patches, window shadows and contact shadows of the main light; the light-count change touches how many of the eight corridor ceiling fill lights and the room lights reach each object.

## 1. What the existing captures show

Sources used: `D:\ProcSenseWork\reports\pixeldiff` (12 benchmark views, one eye 1832 x 1920, B20 against B58, B24 = 2 lights only, B22 = hard shadows only; difference images amplified; numbers in `audit_pixeldiff.md`), the side-by-side grid `grid_B20_vs_B58.jpg`, the text-probe frames and `poses.json` of lane 3 (all B20 only: they gave the camera poses that the new capture reuses), and the `final_walk_r4` frames (B20 and B29 only: no B58, so no use for this question).

Share of pixels whose RGB sum differs by more than 60, per view (percent), with the largest connected area in pixels:

| View | B24 (2 lights) | B22 (hard shadows) | B58 (both) | Where the largest area is (B58) |
|---|---|---|---|---|
| V1 bay near wall | 0.00 | 0.02 | 0.02 | small strip at the frame edge, 783 px (not inspected) |
| V2 bay overview | 0.07 | 0.14 | 0.16 | thin vertical strip, 935 px (not inspected) |
| V3 bay window | 0.14 | 0.40 | 0.47 | window sill region, 5,289 px (not inspected; sun patch edge is the likely cause because only B22 alone gives 4,152 px at the same place) |
| V4 corridor long | 0.22 | 0.23 | 0.42 | 4,400 px near the far end of the corridor (not inspected; identical area in B24 and B58) |
| V5 corridor people | 0.92 | 0.25 | 1.04 | **ceiling light fixture, 18,843 px (the 2-light change)** |
| V6 reception | 0.65 | 0.08 | 0.76 | **ceiling light fixture, 16,100 px (the 2-light change)** |
| V7 theatre | 0.41 | 0.41 | 0.63 | staff at the left edge, 16,953 px |
| V8 corridor exterior | 0.15 | 0.30 | 0.45 | 5,005 px (not inspected; identical area in B24 and B58, so the 2-light change) |
| V9 theatre far end | 0.00 | 0.00 | 0.00 | none |
| V10 glass wall down | 0.00 | 0.13 | 0.14 | 3,990 px at the bottom left (not inspected) |
| V11 glass wall oblique | 0.00 | 0.99 | 0.99 | **plant shadow and sun patch edge, 21,886 px (the shadow change)** |
| V12 bay window down | 0.48 | 0.32 | 0.23 | 3,947 px at the bottom right (not inspected; may be a dust mote) |

Reading of the difference images and the side-by-side grid (I looked at the grid, the V5, V11 and V9 difference images, and enlarged crops of two views):

* **Attribution is clean.** The two changes act in different places. The 2-light change (B24) is almost the whole difference at the corridor ceiling fixtures (V5, V6) and does nothing in V9, V10, V11. The hard-shadow change (B22) is the whole difference on sunlit floor, window and plant shadow edges (V11, V3, V8, V10). B58 is roughly their union, with no new region beyond the theatre-left strip.
* **Ceiling fill lights (2-light change).** Enlarged B20 against B58 in the bay view shows the ceiling panels and tile grid unchanged to the eye; the soft glow ring around the round downlights is slightly weaker with two lights. Subtle, only visible side by side.
* **Hard shadows: a visible artefact.** In V11 (enlarged 4x) the plant's cast shadow on the window sill has a **stair-stepped, jagged edge** with hard shadows, where the soft shadow edge is smooth. That is shadow-map aliasing becoming visible once the softening filter is gone. It is a real quality loss on sun-lit edges; whether it shows in a headset (it will shimmer when the head moves) is exactly what the registered capture must show at Quest size.
* **People (V5, V6, V7):** the difference images show white outlines of the standing and seated staff and a wave of dots (dust motes) in every candidate, including the ones that do not touch the lights. Outlines with no filled interior mean the character was in a slightly different pose between captures, and the dots are moving dust: these captures are **not time-deterministic**, so the percentages above contain motion, and the 16,953 px in V7 (larger than B22 plus B24 alone) is staff motion, not shading. The registered capture freezes time and seeds the dust so this stops.
* **Reflections and equipment:** V9 (theatre far end: anaesthesia machine, instrument tray, cabinet) has 0.00% of pixels over the threshold in B58 (largest per-pixel difference 4 of 765). The metallic equipment in the other views shows no difference outside the regions above.
* **Not covered at all by existing captures:** patient monitor UI and labels, posters and signs at reading distance, door glass, faces. Nothing can be said about them from B58 data.

## 2. Interim verdict (existing captures only)

| Category | Verdict | Evidence | Cause |
|---|---|---|---|
| Monitors (UI, labels) | **unverified** | no B58 capture holds the monitor at reading distance | none known; the monitor's own light is emissive and unshadowed, so no change is expected |
| Labels, signs, posters | **unverified** | wall posters show only faint edge differences in V5/V6 (inside the amplified noise); no reading-distance capture | none known |
| Doors and door glass | **unverified** | only the door edges at the frame border appear in V6/V8 (thin lines) | would be the shadow change if anything |
| Window glass | **preserved / subtly different** | V10 0.14%, V12 0.23%; the city and glass look the same, sun patch edges differ | hard shadows (edge position and hardness only) |
| People (faces, bodies) | **unverified** | difference is capture motion, not shading; no close-up capture | none attributable |
| Lighting and shadows | **subtly different, with one artefact to check** | ceiling fixtures differ (2 lights), glow ring around downlights weaker; sun-shadow edges jagged (hard shadows) | lights: 4 to 2; jagged edges: soft to hard |
| Reflections, metal | **preserved** | V9 0.00%, no difference on equipment elsewhere | none |
| Operating theatre | **preserved** in V9; V7 inconclusive (staff motion) | V9 identical | none |
| Patient bay | **subtly different** | V1 0.02%, V2 0.16%, V3 0.47% (window sill sun patch edge), V12 0.23% | hard shadows |

## 3. Proposed selective configurations (to be benchmarked and judged after the capture)

The registered capture includes them. In the order I would try them:

1. **B24 / B64: 2 lights, soft shadows kept.** Removes the jagged-edge risk entirely; the cost is only the weaker ceiling glow. If the capture shows the ceiling change is invisible, this is the safe half of B58.
2. **B63: 3 lights, hard shadows (on the B15 base).** Tests whether most of the ceiling difference disappears with one extra light.
3. **B65: hard shadows only on the B15 base.** Isolates the artefact on the base that actually ships (original exterior B61/B62 and B29/B56 pairs cover the optimised and original cities).
4. **Keep soft shadows, cheaper otherwise** (not a preset yet): if jagged edges are visible, the remedy is not hard shadows but a smaller soft-shadow cost (shadow resolution or cascade distance), which lane 1 would have to define; do not enable B58's hard shadows until the capture shows the edges are acceptable at Quest size.
5. **Per-light selectivity** (4 lights only for the lights near monitors, per-light shadow softness) is not available as a project setting today: `maxAdditionalLightsCount` is one URP asset value and soft shadows are one asset flag. It would need a code change (a light-priority script that disables the farthest fill lights per room), so propose it only if the capture shows the ceiling change matters near monitors.

## 4. Final verdict (captures of 10 November 2026: 14 presets, 37 poses each, 1440 x 1584, deterministic)

Method: `B58VisualCapture` (all poses identical for every preset, time frozen), then `tools/unity/b58_visual_report.py`; I looked at the difference images and enlarged crops myself (monitor UI, TV sign, corridor ceiling, plant and window-frame shadows, a seated person). Files: `D:/ProcSenseWork/reports/b58_visual` (`rep_a.md` B20 and B58 against B20, `rep_b.md` B62 against B61 and B56 against B29, `rep_c.md` B24 and B22, `sheets/`).

**Noise floor** (B20 against a second capture of B20, worst pose per category, % of pixels over 60): monitors 0.037, signs 0.000, doors 0.001, windows 0.103, people 0.256, lighting 0.018, reflections 0.001, theatre 0.049, bay 0.012. It is dust motes and character micro-motion; the whole-frame PSNR is 42 to 99 dB. A difference at or under these values is not a rendering difference.

**B58 against B20** (B62 against B61 and B56 against B29 give the same numbers to two decimals: the two changes behave identically on the original and the optimised exterior, so the verdict below holds for all three pairs):

| Category | Verdict | Evidence | Cause |
|---|---|---|---|
| Monitors (UI, labels) | **preserved** | worst 0.033% over 60 (noise 0.037); edge ratio, gradient correlation and stroke-mask SSIM all 1.000; enlarged crop identical to the eye, text and numbers equally sharp | none |
| Labels, signs, posters | **preserved (text); TV glass reflection differs** | all posters 0.000%; TV "Welcome / ProcSense University Hospital": edge ratio 0.992, correlation 0.996, mask SSIM 0.964 (text equally legible); the only change on the TV is the specular dot of a ceiling light on its glass, which disappears with two lights, plus the shadow on the window sill under it (0.83% of the frame) | lights (the dot) and shadows (the sill) |
| Doors and door glass | **preserved** | 0.074% over 60, thin lines at the door edges (noise 0.001) | shadows, imperceptible |
| Window glass | **subtly different, shadow edge quality degraded** | 0.60% over 60 (noise 0.10). Sun shadows of the window frames and plant on the sill and floor have **crisp, stair-stepped edges** with hard shadows where the original has soft smooth edges (clear at 3x zoom, `B22` and `B58` alike, absent in `B24`); with head motion in the headset stair-stepped edges will crawl | hard shadows |
| People (faces, bodies) | **preserved** | seated woman: difference is faint contour lines only (0.05% over 60 inside the figure); worst body 0.70% (noise 0.26) | mostly lights (B24 0.56%), faces shadows; below visibility |
| Lighting: corridor ceiling | **degraded** | looking up the corridor 6.92% of the frame differs (mean 13.9 levels, a connected area of 89,881 px, PSNR 26.7 dB): the ceiling light panel loses its glowing gradient and turns flat grey, and the glow patches on the tiles change (compare the left and middle images in `sheets`) | **two additional lights**: B24 6.914%, B64 6.913%, B58 6.920%; **three lights (B63) 0.007%**, hard shadows alone (B22/B65) 0.01 to 0.03% |
| Lighting: floor and bay | **subtly different** | contact shadows and sun patches 0.11 to 0.22% of the frame | hard shadows |
| Reflections, metal | **subtly different** | bench 0.42% (metal sparkle under the fill lights), theatre equipment 0.000% | lights (B24 0.31%) |
| Operating theatre | **preserved** | 0.20% (noise 0.05), equipment view 0.000% | both, tiny |
| Patient bay | **subtly different** | 0.36 to 0.64% (window and reception views) | hard shadows |

**Which change causes what.** The two-light cap causes the one clear degradation (the corridor ceiling luminaires) and the TV reflection dot. Hard shadows cause the stair-stepped shadow edges and nearly all of the remaining small differences. Monitors, signs, posters, doors and faces are unaffected by either change.

**Recommendation: B58 = candidate requiring visual approval; keep current production lighting.** Reasons: the ceiling change is large and visible when looking up, and the hard-shadow edges are visibly stair-stepped; neither is a "look-preserving" change. Among the selective alternatives:
* **B24 / B64 (2 lights, soft shadows): no.** Fixes the jaggies, keeps the ceiling degradation.
* **B63 (3 lights, hard shadows): the ceiling and bodies are preserved (0.007% and 0.00%), but the stair-stepped shadow edges remain.** Acceptable only if the owner approves hard shadows after looking at `sheets/B22_vs_B20_windows_*` and the headset.
* **Best candidate to benchmark next, not defined yet: three additional lights, soft shadows kept** (the B15 base with `MaxAdditionalLights = 3`; call it B66). B63 shows that three lights reproduce the ceiling and the bodies, and soft shadows are today's look. It needs one more capture (pairs with B15 and B20) and lane 4's frame time to see whether going from four to three lights saves anything.
* Per-light selectivity (four lights only near monitors) is not needed: monitors are not affected at all by the cap.
