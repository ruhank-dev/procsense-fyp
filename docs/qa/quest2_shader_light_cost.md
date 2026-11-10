# Quest 2 shader, light and transparency cost (static analysis, lane D, 9 November 2026 UTC)

Static analysis of the scene files, material files, shaders and the `URP_Quest_Android` assets. No Quest measurement exists; desktop figures come from `quest2_optimisation_round2.md`.

## 1. Transparent overdraw: sun shafts, dust, glass

| Item | Count in the scenes | Cost reading |
|---|---|---|
| Shaft meshes | PatientBay 1, Corridor 3 (`Shafts z0`, `z37`, `z71`, `z98`) | 6 prisms each, 4 walls per prism, 24 quads (48 triangles) per mesh; geometry is negligible, cost is fill-rate |
| Shaft blending | `Blend One One`, `ZWrite Off`, `Cull Off` | both faces of every wall draw: a ray through one prism crosses 2 walls, through the overlapping prisms of one window up to 12 fragments per pixel |
| Shaft fragment | no texture, 2 value-noise lookups of 8 hashes each (16 hashes) plus lerps: roughly 200 to 250 scalar ALU | per pixel after the early-out (the near-eye and silhouette pixels cost about 40 ALU) |
| Dust systems | PatientBay 2, Corridor 6, max 150 motes each, 5 to 20 mm billboards | one texture sample, about 6 ALU per pixel, tiny screen coverage: negligible next to the shafts |
| Window / vision / monitor glass | 6 transparent materials (`Surface 1`, back-face culled, `ZWrite Off`, queue 3000) | none reads the opaque or depth texture (both off in `URP_Quest_Android`); single layer, no double-sided cost |

Reading: the shafts (not the dust, not the glass) carry the measured ~14% desktop cost (B3). The cost scales with overdraw times 16 hashes, so the Quest's tile GPU pays it per fragment in both eyes. Nothing here forces an extra render pass (no opaque or depth copy).

## 2. Lights and shadows (`URP_Quest_Android`)

* Additional lights: per-pixel mode (1), per-object limit 4. Counts of enabled lights per scene: PatientBay 6 (1 directional mixed with soft shadow), Corridor 46 (8 realtime fills, no shadows; the rest baked), OT 17. All additional lights are baked or unshadowed realtime, so the realtime additional-light cost is the 8 corridor point fills (limit 4 per object).
* Shadows: main light only, 1 cascade, distance 8 m, 2048 map (the main-light shadow pass is one 2048 target); additional-light shadow atlas 256 (no additional light casts realtime shadows). Soft shadows quality 2. These were measured in B21 to B26 (round 2): not changed because each reduction moves shadow quality.
* Transparent shadow receive is on in the renderer asset (`m_ShadowTransparentReceive: 1`): glass and shafts do not sample the shadow map (shafts `receiveShadows = false`); left.

## 3. Changes made

| File | Old | New | Reason |
|---|---|---|---|
| `Assets/Shaders/SunShaft.shader` | `normalize(i.normalWS)` per pixel; `pow(x, 1.5)` per pixel | normal normalised once in the vertex shader (every vertex of a quad has the same normal, so the interpolated value is unchanged); `x * sqrt(x)` replaces `pow(x, 1.5)` (mathematically identical) | removes one rsqrt chain and an exp2/log2 pair per fragment, at every overdraw layer; no look change |

## 4. Rejected (changes the look or adds cost)

| Idea | Why rejected |
|---|---|
| Noise baked to a 3D or 2D texture | changes the noise pattern (visible), adds texture bandwidth on the tile GPU, and the drift is a function of world position and time |
| Half-resolution transparent pass | needs an extra render target and a composite pass; on a tiled GPU with 4x MSAA this costs more than it saves |
| Fewer prisms or one-sided walls | changes the shaft density and edges (look change) |
| Dropping `n2` octave | visible loss of the shaft breaks |
| Opaque / depth texture, HDR | already off, no change needed |
| Shadow distance / cascades / atlas reduction | measured B21 to B26 earlier, rejected for quality (owner look) |

## 5. Shader variants

`QuestShaderStripping` already strips 22,604 of 45,208 Lit variants (Lit 213 MB to 110 MB in the player). The two custom shaders have only `multi_compile_instancing` (2 variants each). No further stripping applied; no change made to the stripper.

## 6. Verification

Old (HEAD) and new `SunShaft.shader` rendered through `QuestWalkthrough.Walkthrough` (preset B0, bay segment, first 40 frames, 960x540, JPEG, run by lane D on 9 Nov 2026 UTC; frames in `D:\ProcSenseWorkrames\shaft_old` and `shaft_new`). Both runs passed.

* Worst frame: 0.10% of pixels differ by more than 60 (sum over RGB); up to 0.73% differ by more than 12 (JPEG noise plus drift).
* The largest connected area differing by more than 60 is 15 pixels, scattered: these are the drifting dust motes and the patient's breathing (the two runs are different wall-clock times), the same pattern as the first shaft check. No light-cone region changes.
* Verdict: no visible look change; the edit is kept.
