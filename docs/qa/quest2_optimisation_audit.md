# Quest 2 optimisation audit: every technique, measured (final)

10 November 2026 (UTC). Statistics: `tools/unity/bench_stereo_report.py` (paired ratios against B20 per cell and round, 95% bootstrap interval, exact sign test over rounds). Raw files: `unity/Logs/bench/audit_screen.csv` (every post-fix configuration, 3 rounds), `audit_final.csv` (finalists, 6 rounds), `audit_final_q2res.csv` (finalists at the Quest 2 default eye size, 3 rounds), `audit_b1.csv` (original-city single changes, 6 rounds), pixel diffs `D:\ProcSenseWork\reports\pixeldiff\audit_pixeldiff.md`, text probe `D:\ProcSenseWork\reports\lane3\texture_probe_report.md`. Lane documents: `docs/qa/audit/lane1_urp_render.md`, `lane2_geometry_characters.md`, `lane3_textures_memory.md`. The configuration rows are in `quest2_config_matrix.md`.

## Summary

- **All numbers are desktop-only** (GTX 1660 SUPER, immediate-mode GPU, two eyes of 1832 x 1920 with 4x MSAA, or 1475 x 1546 for the Quest 2 default eye size). **They are not Quest measurements. No Quest FPS is claimed and the 72 FPS target is not claimed to be reached.** "Desktop FPS" is simply 1000 / frame ms of the desktop run.
- **Best desktop-tested configuration: B58 = the original configuration + 2 additional lights per object + hard shadows.** GPU-bound frame time **-7.0%** against B20 (headline = geometric mean over the 9 cells of the paired ratios, 0.930, 95% interval 0.918 to 0.939, faster in 6 of 6 rounds; time-weighted, mean of the 9 cell medians 19.77 ms to 18.54 ms, is -6.2%, 50.6 to 53.9 desktop FPS; aggregation and worked example in section 1); frame completion -4.6% (0.954, 0.943 to 0.965, 6 of 6). At the Quest 2 default eye size (3 rounds): -5.4% (0.946). The look is **near-preserved, not identical**: corridor ceiling light panels and hard-shadow edges differ subtly (worst view 1.04% of pixels over 60, against a run-to-run noise floor of 0.5% to 0.7%). **The owner must approve the look change.**
- **With the Quest production stack (optimised city, room culling, character culler): B56 = B29 + B58**, GPU-bound -5.6% by the geometric mean (0.944, 0.928 to 0.959, 6 of 6) but only -3.6% time-weighted (19.77 to 19.05 ms): its gain is in the small cells and absent in the bay and V4; CPU submit +2.6% (real).
- **Android-fair comparison (section 1.4, 6 rounds): the original exterior with the Android settings (B61) is 1.3% faster than the optimised one (B29), with 2.3% less CPU submit; best desktop configuration is B62 (original exterior + B58 changes) at -7.0% against B20 [0.916, 0.943], 6 of 6; the optimised-exterior equivalent B56 is -5.3%.**
- **The optimised exterior city is neutral on this desktop:** B17, B27, B28, B29 and B30 are all within +0.8% to +1.1% GPU-bound (intervals include zero, 6 rounds), although they draw 25% fewer triangles. Their **CPU submit is +3.2% to +3.5% (B27, B28, B29; real)**. The triangle cut does not buy desktop frame time; whether it helps the Quest's tile-based GPU is unknown.
- **Biggest desktop levers (all change the look; diagnostic only):** eye scale (0.8 -9.5%, 1.2 +10.3%), MSAA 1x -9.4%, main-light shadows off -9.9%, additional lights off -7.5%, city hidden -12.3% (a measurement, not an option).
- **What the desktop cannot say:** the Quest 2's tile-based Adreno 650 (binning, tile memory, bandwidth, multiview, thermals) is not the desktop GPU. Section 5.

## 1. Measured configurations

### Aggregation, stated exactly (this is what every number below means)

- **Data:** one value per (config, cell, round) = the harness's per-frame median over that cell's frames (`pipe_med_ms` = "GPU-bound", per-frame pipeline time; `done_med_ms` = "completion", submit plus GPU time to a read-back; `submit_med_ms` = CPU submit). **9 cells**, not 3 workloads x 9: 5 static views (V2, V3, V4, V7, V10), 3 turn cells (T-bay, T-corridor, T-theatre) and 1 walk cell (W-corridor); 6 rounds in `audit_final`, 3 in `audit_screen` and `audit_final_q2res`. Rows with `other_busy` set are dropped (none were).
- **Headline percentage ("vs B20"):** for each round, the geometric mean over the 9 cells of (config value / B20 value of the same cell and round); then the mean of those 6 round values. The 95% interval is a bootstrap over the 54 (cell, round) log-ratios (`bench_stereo_report.py`); an independent bootstrap over the 6 round values gives the same intervals within 0.002 (`verify.py`). "Rounds faster" counts rounds whose per-round geometric mean is below 1; the sign test is exact on that count.
- **Absolute ms column:** the **arithmetic mean over the 9 cells of each cell's median over rounds** (same B20 baseline in every row). "Desktop FPS" = 1000 / that mean. The "ratio of means" column is that mean divided by B20's, a time-weighted view in which the large cells (V4 about 42 ms, W-corridor about 30 ms) count more than small ones (V7, T-theatre about 10 ms); it differs from the headline geometric mean for that reason, and neither is wrong.
- **Error found and corrected (10 Nov):** the first version of this table printed the report tool's column "Median ms (all cells)", which is the median of all 54 raw values pooled. That pooled median is an unrelated number (for `audit_final` GPU-bound it equals the T-bay cell for B20, B58, B56 and B29: 16.13, 15.21, 16.16, 17.03), so dividing those values gave -5.7%, +0.2% and +5.6%, none of which is the headline. The percentages were computed correctly; the ms column was the wrong aggregate and has been replaced. Nothing was re-run.

**Worked examples from `audit_final.csv` (GPU-bound ms, median over 6 rounds per cell):**

| Cell | B20 | B58 | ratio | B56 | ratio | B29 | ratio |
|---|---|---|---|---|---|---|---|
| V2 | 22.50 | 21.13 | 0.939 | 22.45 | 0.998 | 23.31 | 1.036 |
| V3 | 18.79 | 17.62 | 0.938 | 17.56 | 0.934 | 18.54 | 0.987 |
| V4 | 42.19 | 40.45 | 0.959 | 42.54 | 1.008 | 44.38 | 1.052 |
| V7 | 11.61 | 11.06 | 0.953 | 10.36 | 0.893 | 11.12 | 0.958 |
| V10 | 12.86 | 11.17 | 0.869 | 11.13 | 0.865 | 12.79 | 0.994 |
| T-bay | 16.13 | 15.21 | 0.943 | 16.16 | 1.002 | 17.03 | 1.056 |
| T-corridor | 13.90 | 12.71 | 0.915 | 13.08 | 0.941 | 14.27 | 1.027 |
| T-theatre | 9.87 | 9.30 | 0.942 | 9.05 | 0.917 | 9.77 | 0.990 |
| W-corridor | 30.04 | 28.25 | 0.940 | 29.13 | 0.970 | 30.41 | 1.012 |
| **mean of 9 cells (ms)** | **19.77** | **18.54** | 0.938 | **19.05** | 0.964 | **20.18** | 1.021 |
| **geometric mean of the 9 ratios** | | | **0.933** | | **0.946** | | **1.012** |

(B58: the V2 ratio is 21.13 / 22.50 = 0.939, and so on; exp(mean of the nine ln ratios) = 0.933. The headline 0.930 averages the per-round geometric means instead of the per-cell medians, which is why it is 0.003 lower. B56: -3.6% time-weighted, -5.4% to -5.6% by the geometric mean, because its gain is concentrated in the small cells and absent in V2, V4 and T-bay. B29: +2.1% time-weighted, +1.0% to +1.2% by the geometric mean; it is slower than B20 in the bay cells and neutral in the theatre.)

### 1.1 Finalists (6 rounds, 1832 x 1920 per eye; culling once per frame as on a headset)

| Config | What it is | GPU-bound mean ms (9 cells) | Desktop FPS | Ratio of means | **Headline: geometric mean vs B20 [95% interval]** | Rounds faster | Completion (headline) | CPU submit (headline) | Triangles |
|---|---|---|---|---|---|---|---|---|---|
| B20 | original city, baked occlusion on (desktop default) | 19.77 | 50.6 | base | base | | base | base | 859k |
| B58 | B20 + 2 additional lights + hard shadows | 18.54 | 53.9 | -6.2% | **-7.0%** [0.918, 0.939] | 6/6 | -4.6% [0.943, 0.965] | -0.8% (ns) | 0% |
| B56 | B29 + 2 lights + hard shadows | 19.05 | 52.5 | -3.6% | **-5.6%** [0.928, 0.959] | 6/6 | -2.9% [0.959, 0.983] | +2.6% (real) | -25% |
| B24 | 2 additional lights per object only | 19.31 | 51.8 | -2.3% | -2.8% [0.961, 0.981] | 6/6 | -2.0% | -0.2% (ns) | 0% |
| B22 | hard shadows only | 19.19 | 52.1 | -2.9% | -3.3% [0.955, 0.977] | 6/6 | -2.6% | -0.9% (ns) | 0% |
| B29 | optimised city, room cull, character culler, occlusion off (Android production) | 20.18 | 49.6 | +2.1% | +1.0% [0.998, 1.022] | 1/6 | +1.4% | +3.4% (real) | -25% |
| B28 | B29 without the character culler | 20.12 | 49.7 | +1.8% | +0.8% [0.995, 1.020] | 2/6 | +1.5% | +3.2% (real) | -25% |
| B27 | optimised city, occlusion off, no room cull | 20.27 | 49.3 | +2.5% | +1.1% [0.997, 1.024] | 1/6 | +1.4% | +3.5% (real) | -24% |
| B30 | B29 with baked occlusion on | 19.92 | 50.2 | +0.8% | +0.9% [0.997, 1.019] | 1/6 | +0.2% | +1.0% (ns) | -27% |
| B17 | optimised city + occlusion on, no room/character cull | 19.96 | 50.1 | +1.0% | +0.9% [0.997, 1.019] | 2/6 | +0.8% | +1.7% (ns) | -27% |
| B8 | city hidden (measurement) | 17.81 | 56.1 | -9.9% | -12.3% [0.844, 0.906] | 6/6 | -10.7% | -10.7% | -40% |
| B55 | city hidden, occlusion off (measurement) | 18.23 | 54.9 | -7.8% | -11.6% [0.851, 0.917] | 6/6 | -8.8% | -8.5% | -39% |

**At the Quest 2 default eye size (1475 x 1546, scale 0.805; 3 rounds; same aggregation):** GPU-bound mean ms B20 17.89 (55.9 FPS desktop), B58 17.02 (58.8), B56 17.65 (56.7), B28 18.54 (53.9), B29 18.41 (54.3). Headline vs B20: B58 -5.4% (0.946), B56 -3.5% (0.965), B28 +2.6% (1.026), B29 +2.2% (1.022); ratio of means -4.8%, -1.3%, +3.7%, +2.9%. CPU submit (headline): B58 -0.4%, B28 +4.0%, B29 +4.5%, B56 +2.9%.

**Baseline consistency:** B20 is the same configuration in every file; its mean of per-cell medians is 19.77 ms GPU-bound in `audit_final` and 19.70 in `audit_screen` (0.4% apart; completion 25.02 against 25.59, 2.3% apart), so rows from the two files are comparable but each percentage uses the B20 rows of its own file. `audit_b1` (earlier session, B20 completion 18.27 ms) is a separate run and is not compared with the others in absolute terms.

### 1.2 Single changes against B20 (`audit_screen`, 3 rounds, post-fix; GPU-bound ratio)

| Config | Change | GPU-bound vs B20 [95% interval] | Rounds faster | Verdict | Look |
|---|---|---|---|---|---|
| B31 / B32 | eye scale 0.8 / 1.2 | -9.5% [0.889, 0.920] / +10.3% [1.084, 1.123] | 3/3, 0/3 | consistent, scales with pixels | 0.8 softens text; 1.2 sharper |
| B11 / B12 | eye scale 0.9 / 1.1 | -4.6% [0.944, 0.963] / +4.5% [1.033, 1.058] | 3/3, 0/3 | consistent | 0.9 softens text |
| B33 / B13 | MSAA 1x / 2x | -9.4% [0.890, 0.920] / -0.5% [0.989, 1.002] | 3/3, 2/3 | 1x consistent; 2x not significant | 1x: heavy stair-stepping; render-target memory 54 / 107 / 215 MB for 1x / 2x / 4x |
| B34 / B35 | shadow cascades 2 / 4 | +4.1% [1.032, 1.050] / +7.7% [1.060, 1.094] | 0/3 | consistent loss | none expected at 8 m |
| B36 | shadow map 4096 | +1.2% [1.001, 1.024] | 1/3 | marginal | none |
| B37 | shadow distance 12 m | +1.2% [0.999, 1.027] | 0/3 | not significant | more far shadows |
| B38 | additional lights per vertex | -6.0% [0.922, 0.956] | 3/3 | consistent, **rejected: look changes strongly** (view V9: 61% of pixels over 60) | rejected |
| B39 | up to 8 additional lights per object | +0.5% [0.998, 1.013] | 0/3 | not significant | none |
| B40 / B41 | depth texture / opaque texture on | +2.5% [1.016, 1.033] / +3.2% [1.022, 1.044] | 0/3 | consistent loss | none |
| B42 | SRP Batcher off | +0.9% [0.998, 1.023] | 0/3 | not significant (CPU only matters on the Quest) | none |
| B43 | dynamic batching on | +0.8% [0.998, 1.020] | 1/3 | not significant | none |
| B44 | HDR on | +0.3% [0.994, 1.012] | 1/3 | not significant | none |
| B45 | reflection probes off | -3.9% [0.949, 0.974] | 3/3 | consistent (measurement only: reflections change) | reflections fall back to the sky |
| B46 | depth priming forced | +1.1% [1.003, 1.020] | 0/3 | marginal loss | none |
| B47 / B48 / B49 / B52 | anisotropic filtering presets | +0.8% / -0.2% / -0.1% / +0.1% | 1/3, 2/3, 2/3, 1/3 | none: no desktop effect, headset only | none |
| B51 | SRP Batcher off + GPU instancing on | +1.8% [1.009, 1.029] | 0/3 | loss | none |
| B7 | current room only (measurement) | -8.8% [0.874, 0.950] | 3/3 | consistent | not an option |
| B9 | characters hidden (measurement) | -5.2% [0.935, 0.962] | 3/3 | consistent | not an option |
| B15 | original city, occlusion off | +0.8% [0.993, 1.023] | 0/3 | not significant | none |
| B57 / B59 | B56 / B58 + per-vertex lights | -9.5% / -10.0% | 3/3 | look-changing, rejected with B38 | rejected |

Original-city single changes from `audit_b1` (6 rounds, **valid**; frame completion): realtime fill lights off -5.6% [0.936, 0.952]; main-light shadows off -9.9% [0.891, 0.912] (render-target memory -8 MB); additional lights off -7.5% [0.914, 0.935]; 5 m shadow distance -0.6% (ns); 1024 shadow map -0.1% (ns, render-target memory -6 MB); shafts and dust off -0.2% (ns); fixed foveation 0.5 / 1.0 -0.3% / -0.2% (ns, as expected: a desktop GPU has no foveation); skinned meshes not updated off-screen +0.8% (ns); intermediate texture forced -0.1% (ns). **Excluded and superseded:** `audit_2a_c0`, `audit_2a_c1`, `decomp`, `fast_6`, `stereo_clean`, the chunk-size runs and the earlier +3.4% / +3.8% / +5% claims (stale-cache bug, fixed 10 Nov).

### 1.3 Pixel difference against B20 (12 views, `bench_pixeldiff.py`)

Noise floor (anisotropy presets that change nothing visible): 0.5% to 0.7% of pixels over 60 (animation and dust). B24 0.92%, B22 0.99%, **B58 1.04%** (largest connected area about 21,900 pixels, in the corridor ceiling), B56 1.04%, B34 0.89%, B35 0.99%, B36 0.88%, B37 1.07%. **B38, B57, B59 (per-vertex lights): 61% (view V9), strongly changed, rejected.** By eye B58 is near-identical, with subtle differences at the ceiling light panels and shadow edges. Clinical text crops were not re-checked against B58; the owner should confirm the text on the monitors and signs.

### 1.4 Android-fair comparison and B58 verification (`fair_full.csv`, 6 rounds, complete)

Question: the Android production stack (B29) differs from the original in three ways at once (optimised city, room culling, character culler, occlusion off). Is the optimised **city** the cost? B61 = the **original** exterior with the same Android settings (occlusion off, room culling, character culler); B62 = B61 + the B58 lights and shadows. Same 9 cells, 1832 x 1920 per eye, culling once per frame, `other_busy` 0 in all 540 rows, aggregation as in section 1 (headline = geometric mean of paired per-cell ratios; "time-weighted" = ratio of the means of the 9 cell medians). B20 is the same configuration as in section 1.1; its mean is 19.18 ms here (19.77 there; a different launch).

| Config | What it is | GPU-bound mean ms | Time-weighted vs B20 | **Headline vs B20 [95% interval]** | Rounds faster | Completion (headline) | CPU submit (headline) | Triangles / SetPass |
|---|---|---|---|---|---|---|---|---|
| B20 | original exterior, occlusion on (desktop default) | 19.18 | base | base | | base | base | 859k / 350 |
| B15 | original exterior, occlusion off, no room/char cull | 19.41 | +1.2% | +0.1% [0.991, 1.011] | 3/6 | +1.2% | +2.1% | 876k / 367 |
| **B61** | **original exterior, Android settings** | 19.44 | +1.3% | **-0.2% [0.989, 1.009]** | 4/6 | +0.9% | +1.5% | 869k / 363 |
| **B29** | **optimised exterior, Android settings (production)** | 19.59 | +2.1% | **+1.2% [0.999, 1.023]** | 0/6 | +1.9% | +3.9% (real) | 642k / 368 |
| B60 | B15 + 2 lights + hard shadows | 18.22 | -5.0% | -6.3% [0.922, 0.952] | 6/6 | -3.1% | +1.8% | |
| **B62** | **B61 + 2 lights + hard shadows** | 18.14 | -5.4% | **-7.0% [0.916, 0.943]** | 6/6 | -3.6% | +1.0% | 869k / 363 |
| **B56** | **B29 + 2 lights + hard shadows** | 18.46 | -3.8% | **-5.3% [0.933, 0.961]** | 6/6 | -2.8% | +3.2% (real) | 642k / 368 |
| B63 | B15 + 3 lights + hard shadows | | -3.8% | -5.2% [0.936, 0.960] | 6/6 | -1.5% | +1.4% | |
| B64 | B15 + 2 lights, soft shadows | | -1.7% | -2.9% [0.962, 0.981] | 6/6 | -0.5% | +1.9% | |
| B65 | B15 + hard shadows only | | -2.2% | -3.3% [0.955, 0.978] | 6/6 | -0.9% | +1.4% | |

**Direct paired comparisons (same rounds, same cells; base = the optimised stack):**

| Pair | GPU-bound headline [95% interval] | Rounds | Completion | CPU submit |
|---|---|---|---|---|
| B61 (original exterior) vs B29 (optimised) | **-1.3% [0.980, 0.995]** (time-weighted -0.8%) | 6/6 faster | -1.0% | **-2.3%** |
| B62 (original + B58) vs B56 (optimised + B58) | **-1.8% [0.976, 0.988]** (time-weighted -1.8%) | 6/6 faster | -0.8% | **-2.2%** |

**Verdict (desktop only, not Quest):**
1. With the Android settings the **original exterior is slightly faster than the optimised one on this desktop** (-1.3% to -1.8% GPU-bound, 6 of 6 rounds, intervals exclude zero; CPU submit about 2.2% lower), even though the optimised city draws 26% fewer triangles (642k against 869k) and adds 5 SetPass calls. Against B20, B61 is indistinguishable (-0.2%) and B29 is +1.2% (interval includes zero).
2. The B58 lights and shadows give **-7.0% on the original exterior (B62) and -5.3% on the optimised one (B56)**, each faster in 6 of 6 rounds. The effect is additive: hard shadows alone -3.3% (B65), 2 lights alone -2.9% (B64), both -6.3% (B60 on the B15 base). Three lights with hard shadows (B63) gives -5.2%.
3. The B58 look change (section 1.3) is the same on both exteriors; the owner must approve it.
4. **What this does not say:** whether the optimised city's 26% triangle cut helps the Quest's tile-based GPU. Both exteriors stay available for the headset A/B (B61/B62 against B29/B56, plus B20).

**Quest-default eye size:** `audit_final_q2res.csv` (B20, B58, B56, B28, B29; 3 rounds; post-fix; culling once per frame; same 9 cells) can be reused for B20, B58, B56 and B29: the same configurations and code gave matching results in the two launches (B29 +1.0% to +1.2%, B56 -5.3% to -5.6% at 1832 x 1920). The only missing Quest-size configurations are **B61 and B62**. A second launch **is essential** only if the owner wants the original-against-optimised comparison at the Quest eye size; the minimal set is B20, B56, B61, B62 (and B29 for the direct B61 vs B29 pair), 3 rounds, no pixel diffs, no captures (about 7 minutes at 5 configurations).

## 2. Every technique

Status words: **Implemented**, **Tested and rejected**, **Already optimal**, **Unsupported**, **Quest-only verification pending**, **Deferred, not tested**.

| # | Technique | What it does | Original | Alternatives tested | Status | Measured effect (desktop, GPU-bound unless stated) | Visual difference | Final choice and why |
|---|---|---|---|---|---|---|---|---|
| 1 | MSAA | Samples per pixel; resolved in tile memory on Quest | 4x | 2x (B13), 1x (B33) | Already optimal for the look; Quest-only verification pending | 2x -0.5% (ns); 1x -9.4% [0.890, 0.920]; render targets 215 / 107 / 54 MB (4x / 2x / 1x) | 2x and 1x show stair-stepping on thin edges | Keep 4x; on a tile GPU the resolve is cheap, so the desktop gain does not carry over; headset A/B if the GPU is the limit |
| 2 | Fixed foveated rendering | Lower shading rate in the periphery | level 0 | 0.5 (B4), 1.0 (B5) | Quest-only verification pending | -0.3% / -0.2% (ns): a desktop GPU has no foveation | periphery blur | Keep 0; headset A/B with the text check |
| 3 | Dynamic (eye-tracked) foveation | Moves foveation with gaze | n/a | none | Unsupported (no eye tracking on Quest 2) | n/a | n/a | n/a |
| 4 | Resolution (eye) scale | XR eye texture scale | 1.0 | 0.8, 0.9, 1.1, 1.2 | Tested; Quest-only verification pending | 0.8 -9.5%, 0.9 -4.6%, 1.1 +4.5%, 1.2 +10.3% (cost follows pixel count) | 0.9 and below soften clinical text | Keep 1.0; headset A/B 1.0 / 0.9 |
| 5 | Shadow distance | Main-light shadow range | 8 m | 5 m (B21), 12 m (B37) | Tested and rejected | 5 m -0.6% (ns); 12 m +1.2% (ns) | 5 m drops far contact shadows | Keep 8 m |
| 6 | Shadow cascades | Splits the shadow map | 1 | 2, 4 | Already optimal | 2 +4.1%, 4 +7.7% | none expected | Keep 1 |
| 7 | Shadow map resolution | Texels of the shadow map | 2048 | 1024 (B23), 4096 (B36) | Tested and rejected | 1024 -0.1% (ns, -6 MB); 4096 +1.2% (marginal) | 1024 softens contact edges | Keep 2048 |
| 8 | Soft shadows | Filtered shadow edges | on (Medium) | hard (B22) | Implemented as part of B58, **pending owner approval of the look** | -3.3% [0.955, 0.977], 6/6 rounds | harder, aliased shadow edges; pixel diff 0.99% (noise floor 0.5% to 0.7%) | Candidate: part of the best configuration B58 |
| 9 | Main-light realtime shadows | Dynamic shadows from people and carts | on | off (B10) | Tested and rejected (look) | -9.9% [0.891, 0.912] (6 rounds, original city); render targets -8 MB | no dynamic shadows | Keep; diagnostic only |
| 10 | Additional-light mode | Per-pixel vs per-vertex fills | per pixel | per vertex (B38), lights off (B25) | Tested and rejected | per vertex -6.0% [0.922, 0.956]; off -7.5% [0.914, 0.935] | per vertex: 61% of pixels in view V9, rejected; off changes the look | Keep per pixel |
| 11 | Per-object additional-light limit | Lights each object samples | 4 | 2 (B24), 8 (B39) | Implemented as part of B58, **pending owner approval of the look** | 2: -2.8% [0.961, 0.981], 6/6; 8: +0.5% (ns) | pixel diff 0.92% (noise floor 0.5% to 0.7%); ceiling fills where 3 or 4 overlap | Candidate: part of B58 |
| 12 | Realtime vs baked lighting | Which lights are baked | baked + 8 realtime corridor fills + realtime key | fills off (B6) | Already optimal as authored | fills off -5.6% [0.936, 0.952] | the look changes | Keep; baking the fills is an owner decision |
| 13 | Depth texture | Depth copy | off | on (B40) | Already optimal | +2.5% [1.016, 1.033] | none | Keep off |
| 14 | Opaque texture | Colour copy | off | on (B41) | Already optimal | +3.2% [1.022, 1.044] | none | Keep off |
| 15 | Intermediate texture | Offscreen RT then blit | Auto | forced (B2) | Already optimal | -0.1% (ns) | none | Keep Auto |
| 16 | Depth priming | Depth pre-pass | Disabled | forced (B46) | Already optimal | +1.1% [1.003, 1.020] | none | Keep disabled |
| 17 | SRP Batcher | Cuts CPU state setup | on | off (B42) | Already optimal | +0.9% (ns) on this desktop; the CPU benefit shows on the Quest | none | Keep on |
| 18 | Dynamic batching | CPU merges small meshes | off | on (B43) | Already optimal | +0.8% (ns) | none | Keep off |
| 19 | GPU instancing | One draw for repeated meshes | per-material flags | B51 (SRP Batcher off + instancing) | Tested and rejected | +1.8% [1.009, 1.029] | none | Keep as is |
| 20 | HDR | Float colour buffer | off | on (B44) | Already optimal | +0.3% (ns) | none | Keep off |
| 21 | Post-processing | Bloom and grading | none | no toggle needed | Already optimal | no pass exists | none | none |
| 22 | Light layers | Per-light layer masks | on | off | Tested and rejected | n/a | the skyline would be lit | Keep on |
| 23 | Reflection probes | Reflection sampling | as authored | off (B45) | Tested and rejected (look) | -3.9% [0.949, 0.974] (measurement) | reflections fall back to the sky | Keep |
| 24 | Camera stacking, Render Graph, forward | extra passes; path | none / the only path / forward | n/a | Already optimal | no extra passes | none | none |
| 25 | Static batching (runtime) | One draw for many static meshes | partly authored | B50 | **Deferred, not tested** | not measured: only 10% to 25% of interior meshes are readable; making the rest readable doubles their memory; low expected benefit | none | Not adopted |
| 26 | Mesh combining | Fewer draws from merged meshes | chunked city (320 m, 44 renderers) | 2000 m cell (20 renderers); interior combining not built | Tested and rejected by analysis | the city variants measured post-fix are neutral (rows 31, 32); no gain from merging further was established | none | Keep 320 m |
| 27 | LODs | Simpler meshes at distance | no LOD groups | not built | Tested and rejected by analysis | no interior mesh is above 17k triangles | would change distant meshes | Not built |
| 28 | Baked occlusion | Hides objects behind walls | on (desktop) | off (B15), B30 against B29, light-padding experiment | Tested and rejected on Quest (off) | B15 +0.8% (ns); B30 vs B29 -0.1% (ns); triangles -3% | corridor ceiling shading differs at 9 of 1,080 poses (worst 0.176%) | Off on Quest: no frame-time gain, lighting shift |
| 29 | Room portal culling | Hides rooms no door or glass shows | all drawn | B28 against B27 | Implemented, Quest-only verification pending | B28 vs B27 -0.3% (ns); CPU 0.14 ms per camera | none | Kept: its triangle saving is a tile-GPU effect the desktop can't show |
| 30 | Character culler | Stops off-screen skinning and animators | all update | B29 against B28; B26 | Implemented, Quest-only verification pending | B29 vs B28 +0.2% (ns); B26 +0.8% (ns) | none (12 tests) | Kept: cost 0.003 ms per frame |
| 31 | Optimised exterior city | Simplified far traffic, trees, mid towers; cone culling | original city | B17, B27 to B30 | Implemented, **neutral on this desktop** | GPU-bound +0.8% to +1.1% (ns, 6 rounds); CPU submit +3.2% to +3.5% (real); triangles -25%, SetPass +4% | 0.047% of pixels at worst against the original | Keep as the Quest default only if the headset A/B (B20 vs B29) agrees; desktop does not support a gain |
| 32 | City chunk size | Renderer count vs cull granularity | 320 m (44 renderers) | 80, 160, 2000 m measured pre-fix (excluded) | Quest-only verification pending | no valid post-fix comparison of chunk sizes exists | none | 320 m stays; a post-fix chunk-size run is not done |
| 33 | GPU skinning | Skinning on the GPU | CPU | `diag-gpuskin` APK | Quest-only verification pending | ratio 1.00 on this desktop (one-eye run) | none | CPU; headset A/B |
| 34 | Shader variants | Fewer compiled variants | 45,208 Lit variants | stripping (Android only) | Implemented | Lit shader 213 to 110 MB, APK 471 to 363 MB; no frame effect expected | none | Kept |
| 35 | Sun shafts and dust | Additive shaft prisms and dust motes | full cost | B3 (off); shader edit | Implemented | off -0.2% (ns, two eyes); the earlier "about 14%" was a one-eye artefact; shader edit worst frame 0.10% of pixels over 60 | none | Kept on |
| 36 | ASTC compression | Compressed GPU texture formats | 446 of 635 textures already ASTC | monitor override; text probe (uncompressed full size, today, ASTC 6x6 and 4x4 at full size, one Quest eye, 12 surfaces tried, 7 measured) | Implemented (monitor); text check **conclusive for 5 posters, the TV screen and the monitor UI** (not for 4 posters, monitor labels, reception screen, notice board: no view shows them) | about 12 MB saved (five shipped monitor textures 16.3 MB to 3.9 MB); posters today (6x6 at 256 x 512) 37.6 to 39.7 dB, sharpness 0.89 to 0.93; 6x6 at 1024 51 to 53 dB; 4x4 at 1024 61 dB; monitor UI 4x4 54.1 dB but 6x6 only 34.0 dB | the compression format is fine; poster text is limited by the Android maximum size of 512, not by 6x6 | Keep 6x6 for text, keep 4x4 for the monitor UI. Recommend poster maximum size 512 to 1024 (+0.55 MB each, about +5 MB), needs the owner's yes. See docs/qa/audit/lane3_textures_memory.md section 4 |
| 37 | Mipmaps, streaming mipmaps | Distance filtering; streaming | on for 277 of 309 textures; streaming inactive | n/a | Already optimal; streaming Unsupported as configured | none | none | Keep |
| 38 | Texture atlasing | Fewer materials via merged textures | none | counted per room | Tested and rejected | n/a | tiling materials cannot be atlased without a look change | Rejected |
| 39 | Symmetric Projection | Symmetric eye frusta (Vulkan, Quest) | off | none (no desktop proxy) | Quest-only verification pending | n/a | none expected | Off; headset A/B `m_symmetricProjection` 0 to 1 |
| 40 | Multiview Render Regions | Per-eye render regions for the final pass | off | none (no desktop proxy) | Quest-only verification pending | n/a | none expected | Off; headset A/B `m_multiviewRenderRegionsOptimizationMode` 0 to 1 or 2 |
| 41 | Runtime performance levels | CPU/GPU level hints | no hint | feature enabled, SustainedHigh hint | Quest-only verification pending | 3 editor tests pass; behaviour only on the device | none | Kept enabled; headset check for thermals |
| 42 | Anisotropic filtering | Sharper surfaces at grazing angles; texture bandwidth | forced on in the Quest level; 206 textures at 8 | presets B47, B48, B49, B52 | Quest-only verification pending | within -0.2% to +0.8% (ns): no desktop effect | none today | headset A/B |

## 3. Other configuration rows (all in `quest2_config_matrix.md`)

| Item | Status | Final setting |
|---|---|---|
| Vulkan only; OpenXR + Meta Quest; Single Pass Instanced (multiview); Game Activity; ARM64, API 29 | Already optimal | unchanged |
| Optimize Buffer Discards | Already optimal (value 1; the gain is Quest-only) | on |
| Meta XR Subsampled Layout | Tested and rejected (only useful with foveation above 0) | off |
| Dynamic resolution | Tested and rejected (blurs clinical text unpredictably) | off |
| Quest 2 refresh rate | Unsupported as an explicit request (no API in this Unity/OpenXR); the 72 Hz default is read back and logged | default |
| Graphics Jobs; fullscreen copies | Quest-only verification pending | unchanged |
| Adaptive Performance flag | Already optimal (no provider installed) | unchanged |
| LOD cross-fade flag; skin weights 2; reflection probe atlas | Already optimal | unchanged |
| Android quality levels (only level 2 built) | Implemented | Android-only exclusion |
| Door vision glass aperture, doorway reset, head-motion pad, theatre east-end clip (DT-288 fixed at the source) | Implemented | in the project |
| Image-based buildings / impostors | Tested and rejected (buildings are 8% of the city's triangles) | not built |
| Texture bandwidth; transparent overdraw | Quest-only verification pending | n/a |
| Sky HDR resize, shadowmask lightmap compression, staff mesh Read/Write off, XR sample textures | candidates, **not applied** (lane 3 section 3) | owner decisions |
| Android emulator | Unsupported (no XR runtime or Adreno GPU) | not used |

## 4. Best desktop-tested configuration, exact settings

**B62** (desktop recommendation, evidence in section 1.4): the **original exterior** with the Android settings (baked occlusion off, room portal culling, character culler) plus the B58 changes: `maxAdditionalLightsCount` 4 to 2 and soft shadows off. GPU-bound **-7.0% [0.916, 0.943] against B20, 6 of 6 rounds** (time-weighted -5.4%); completion -3.6%; CPU submit +1.0% (the culling scripts' cost). It is also 1.8% faster than the optimised-exterior equivalent B56 (6 of 6 rounds). Look: B58 changes are near-preserved, not identical (worst view 1.04% of pixels over 60, noise floor 0.5% to 0.7%: corridor ceiling light panels and shadow edges; text on monitors and signs still to be confirmed); **the owner must approve them**.
**B56** (the same two changes on the optimised exterior, i.e. the current Android production stack): GPU-bound -5.3% [0.933, 0.961], 6 of 6; CPU submit +3.2% (real).
**Exterior choice:** on this desktop the original exterior is slightly ahead of the optimised one; the Quest decision needs the headset A/B (both exteriors are in the diagnostic APK: B61/B62 against B29/B56).
Not recommended: per-vertex lights (B38, B57, B59: the look changes strongly), shadows off, lights off, eye scale below 1.0, MSAA below 4x (diagnostic only).

## 5. What the desktop can and cannot predict for a Quest 2

- **Can show:** relative cost of shadows, lights and per-pass work, scaling with pixel count, CPU submit cost of culling, triangle and SetPass counts, correctness of culling (pixel diffs), memory sizes.
- **Cannot show:** the Quest 2 uses a tile-based Adreno 650 (binning, tile memory, MSAA resolve in tile memory, bandwidth, multiview, thermal throttling). The desktop is immediate-mode with a faster fill rate. Triangle cuts and room culling may help the Quest where they do not here; light and shadow savings may help it less or more; foveation, Symmetric Projection and Multiview Render Regions only exist on the device.
- The reported 20 to 30 FPS on the headset is unverified. **No Quest FPS is estimated and the 72 FPS target is not claimed.**

## 6. Headset A/B for Sannan

Pointers: `docs/qa/audit/lane1_urp_render.md` section 3 (runtime-switchable presets: foveation, eye scale, MSAA, lights, shadows; build-time fields for Symmetric Projection and Multiview Render Regions), `lane2_geometry_characters.md` section 5 (B20 vs B29, B27 vs B28, B28 vs B29, B29 vs B30, CPU vs GPU skinning), `lane3_textures_memory.md` section 5 (anisotropy, HDR sky), and `quest2_sannan_benchmark_guide.md`. Priority order: B20 vs B58 (look and GPU), B20 vs B29 (optimised city and culling), eye scale 1.0 vs 0.9, foveation 0 / 0.5 / 1, Symmetric Projection, Multiview Render Regions.

## 7. Method

Interleaved rounds (configurations measured back to back, order rotated), warm-up, per-preset render targets (`rt_active_mb` proves each setting applied), culling once per frame in the post-fix runs (`BENCH_CULL_ONCE`), `other_busy` filter (0 in all rows used), results of the stale-cache run excluded. Digest outputs: `D:\ProcSenseWork\analysis`.

## 8. Adopted on Android (10 November 2026, owner decision)

The owner approved the B58 lighting change for the Quest build, accepting the corridor-light and hard-shadow trade-offs (section 1.4 and `audit/lane3_visual_b58.md`). Changed in `unity/Assets/Settings/URP_Quest_Android.asset` only (desktop URP assets untouched): `m_AdditionalLightsPerObjectLimit` 4 -> 2 and `m_SoftShadowsSupported` 1 -> 0. **To revert the original lighting, set both fields back (4 and 1).** The diagnostic presets B0 (production default) and B20 to B30 remain available, so Sannan can A/B the old and new lighting and both exteriors on the headset.
