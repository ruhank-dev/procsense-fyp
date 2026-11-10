# Post-merge QA and fixes (11 November 2026)

## 1. The four Android-target test failures were a real Quest defect (fixed)

**Failing tests:** `ModeledDoorTests.One_visible_leaf_per_connection_matches_and_moves_with_the_solid_hinge`, `ClinicalPanelSceneTests.Finish_reveals_debrief_without_clinical_writes_and_session_reset_clears_it`, `ClinicalPanelSceneTests.Saved_checklist_panels_follow_the_full_golden_with_Stop_correction_and_identical_Core_log` and `EnvironmentIntegrationTests.Visible_patient_owns_band_sensors_nurse_contact_and_speech`. All four failed on the Unity log `[Assert] First two bone weights should have non-zero sum!` (3,267 occurrences when PatientBay loads).

**The earlier explanation (an Editor-only quality-level artefact) was incomplete.** The Editor only exposed it: the Android-only quality-level exclusions make the Editor use the Medium level, which is the Quest level and skins with two bones per vertex (`QualitySettings.skinWeights` = TwoBones). Forcing Medium in a Standalone Editor produces the same 3,267 asserts, so the build target is not the cause.

**Root cause (measured):** with TwoBones, `SkinnedMeshRenderer.BakeMesh` produced NaN vertices in four generated meshes, and the NaN counts equal the assert counts: `Gathered_sage_curtain_0_skinned` 972, `Gathered_sage_curtain_2_skinned` 972, `EQ_privacy_curtain_M_Curtain_1_skinned` 1,323 (sum 3,267, the load-time asserts) and `patient_gown_secondary` 426 (asserted when baked). At FourBones and Unlimited (Very High, Ultra) all four give 0 NaN vertices. `CurtainRigBuilder` and `GownRigBuilder` wrote the four `BoneWeight` entries of a vertex in an arbitrary order (bilinear grid weights, or kept weights multiplied by a factor of zero), so for those vertices entries one and two were zero and the weight sat in entries three and four. Unity reads only the first two entries in TwoBones mode, so those vertices skinned to NaN. **On the Quest this would have collapsed or scrambled those curtain and gown vertices; it is not harmless.** No imported character mesh is affected (152 meshes audited; none has an unweighted vertex).

**Fix:** `unity/Assets/Editor/Characters/SkinWeightFix.cs` rewrites the four mesh assets (same paths and GUIDs) so every vertex has its duplicate bones merged, zero weights dropped, entries sorted by weight and renormalised (215, 497 and further vertices changed their stored bone count, see the log). The two builders now emit sorted weights through `SkinWeightFix.Sorted`, so rebuilding the rigs cannot reintroduce the defect. New regression test `unity/Assets/Tests/PlayMode/SkinWeightTests.cs` loads all four scenes at the two-bone quality, bakes every skinned mesh and requires zero NaN vertices and zero asserts. `SkinWeightAudit.cs` (Editor, read-only) lists unweighted vertices per mesh.

**Verification:** the four failing tests pass under the Android build target (4 of 4) and under Standalone (4 of 4), with 0 bone-weight asserts in both logs; the regression test passes under both targets. Not verified on the desktop: the look of the curtains and gown on the headset (Quest-only check for Sannan: curtain and gown drape and motion on the Medium level).

## 2. URP asset `URP_Quest_Android.asset`: generated soft-shadow prefilter fields

**What changed in the working tree after the production APK build:** `m_PrefilterSoftShadowsQualityMedium` 0 -> 1 and `m_PrefilterSoftShadows` 1 -> 0. Nothing else in the asset changed; no other URP or renderer asset was touched (`git status`).

**Who writes them.** They are generated, not configuration. URP's build preprocessor (`ShaderBuildPreprocessor`, `UpdateShaderPrefilteringDataBeforeBuild` with `callbackOrder -100`, and `IPreprocessBuildWithReport`) gathers the shader features the renderers need, then calls `UniversalRenderPipelineAsset.UpdateShaderKeywordPrefiltering` and saves the asset (`SetDirty` + `SaveAssetIfDirty`) **before any shader is stripped in the same build**. The soft-shadow fields are `strip` flags: `m_PrefilterSoftShadowsQualityLow/Medium/High = !(that keyword is needed)` and `m_PrefilterSoftShadows = !Low || !Medium || !High`.

**Why they flipped.** The owner-approved B58 setting `m_SoftShadowsSupported: 0` means no renderer needs soft shadows, so all three quality keywords are stripped: Low/Medium/High = 1 and `m_PrefilterSoftShadows` = 0. The committed file still held the pre-B58 values (Medium = 0 because Medium soft shadows were needed; `m_PrefilterSoftShadows` = 1). The values now on disk are exactly what URP derives from `m_SoftShadowsSupported: 0` and `m_SoftShadowQuality: 2`; `m_AdditionalLightsPerObjectLimit` is 2 as intended.

**Does the APK match B58?** Yes. The preprocessor updates the in-memory asset before shader stripping, so the APK was built with the new values whether or not they were committed. A clean checkout of `main` (stale prefilter fields) therefore builds the same shader set; the only difference is that Unity dirties the file again at build time. The build is reproducible; the repository was simply out of step with what the build writes.

**Decision.** The working-tree file is the correct, consistent content. Commit it on the fix branch so a clean build leaves `git status` clean. No other generated field is stale.

## 3. Why the production APK is 325 MB, not 380 MB (lane QA-3, no Unity run)

Compared: `apk/final/ProcSense-final-main-de1376c9.apk` (325,404,377 B, merged `main`, B58 lighting) against `apk/r3/ProcSense-demo.apk` (380,061,204 B, our branch before the merge and before B58). `apk/ProcSense-demo.apk` (named "round 2") is also 379.8 MB, so it is not an independent 363 MB baseline. Build reports: `artifacts/unity/build-demo.log` (new) and `build-diag.log` (r3 diagnostic build, same content as the r3 production build apart from diagnostics).

| Where | Before | After | Change |
|---|---|---|---|
| APK, compressed | 379.8 MB | 325.1 MB | -54.6 MB |
| `sharedassets0.assets` (holds the shaders), compressed | 98.5 MB | 44.7 MB | -53.8 MB (every other category is unchanged to 0.1 MB: textures/meshes `sharedassets4` 112.2, `sharedassets2` 81.2, `sharedassets5` 22.0, native libraries 27.5, dex 6.4) |
| Build report, Shaders (uncompressed) | 109.6 MB | 48.7 MB | -60.9 MB |
| Build report, Textures / Meshes | 260.5 / 240.2 MB | 260.5 / 240.3 MB | unchanged |
| `Universal Render Pipeline/Lit` | about 110 MB | about 52 MB | -58.3 MB (SimpleLit -1.4, terrain shaders -1.2) |
| Lit variants in (after URP prefilter) / kept | 45,208 / 22,604 (round-2 report) | 22,680 / 11,340 (`shader_stripping.txt`) | halved |

**Cause: the B58 lighting change, intentional.** Turning soft shadows off in `URP_Quest_Android.asset` makes URP's build prefilter drop every `_SHADOWS_SOFT*` variant, which halves the Lit variant count; the Android stripper then removes what is left that cannot occur (`LOD_FADE_CROSSFADE`, 11,340 variants). No texture, mesh, animation, native library or script size changed. The prefilter fields that Unity rewrote (section 2) are what produced the strip, so a clean checkout must carry those values to reproduce the size.

**Nothing required was excluded.** 2,770 used assets before, 2,787 now: 0 removed, 17 added, all Sannan's (tympanic thermometer, clipboard chart, wristband and allergy band models, three prefabs, three materials, `NurseWristbandController`, `ContactActionEmitter`). The scene list is identical (Boot, Sys_Core, MainMenu, PatientBay, Corridor, OT, Recovery, DevOnly) and the APK has level0 to level7 in both. `patient_folder.prefab` is not listed separately because it is instanced inside `PatientBay.unity` (the scene carries it; its mesh `patient_folder.fbx` is in the build). The door scripts (`SwingDoor`) and `PatientIdleView` are present. Not verified without a Unity run: the shader variants themselves (counted from the strip log, not inspected in the player).
