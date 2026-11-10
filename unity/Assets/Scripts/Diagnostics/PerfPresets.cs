using System.Collections.Generic;

namespace ProcSense.VR.Diagnostics
{
    /// <summary>
    /// One diagnostic configuration of the Quest benchmark (DC-13 part B, docs/qa/quest2_sannan_benchmark_guide.md). Every field is a run-time switch that leaves the scenes and assets untouched;
    /// leaving the configuration restores what the scene had. B0 is this build's production default.
    /// </summary>
    public sealed class PerfPreset
    {
        public string Id = "", Label = "";
        public bool LegacyRenderer;          // renderer 1 = the desktop renderer (intermediate texture Always), i.e. the 7 Nov build's rendering path
        public bool ShaftsOff;               // sun shafts and dust motes hidden (DesktopOnly.ShowOnAndroid = false)
        public float Foveation;              // 0 off, 0.5 medium, 1 high
        public bool RealtimeFillsOff;        // realtime "Ceiling fill" point lights and the "Soft clinical key" directional switched off
        public bool CurrentRoomOnly;         // renderers of rooms the player is not in are not drawn (scenes stay loaded; the city stays)
        public bool CityHidden;              // the "City exterior" renderers are not drawn (the photographic skybox stays)
        public bool CharactersHidden;        // skinned renderers not drawn and their animators paused (cost of people and CPU skinning)
        public bool MainShadowsOff;          // mixed directional suns cast no realtime shadow (measurement only: changes dynamic shadows)
        public bool OcclusionOff;            // camera does not use the baked occlusion data (shows what occlusion culling saves; a look-neutral setting)
        public bool OptimisedExterior;       // the Quest variant of the exterior city (QuestExterior): reduced far traffic and trees, same placement; compare against B0 (original)
        public bool RoomCullOff;             // force the room-level culling off (the Quest default is on)
        public bool CharCull;                // force the character culler on (the Quest default is on, desktop default off)
        public bool OcclusionOn;             // force the baked occlusion on (the Quest default is off, desktop default on)
        public bool RoomCull;                // room-level portal culling (RoomVisibility): rooms no door or glass can show are not drawn (replaces the baked occlusion for cross-room hiding)
        public bool OriginalExterior;        // force the untouched original city (the recoverable comparison variant); the production default on Android is the optimised one
        public bool PortalCullOff;           // with the optimised exterior: no per-chunk cone culling through windows (shows what the portal culling saves on top of the reduced meshes)
        public bool ExteriorCullOff;         // the city is always drawn (ExteriorVisibility off); shows what hiding the city behind solid walls saves
        public float ShadowDistance;         // main-light shadow distance in metres, 0 = as authored (8 m)
        public bool SoftShadowsOff;          // hard-edged realtime shadows
        public int ShadowMapSize;            // main-light shadow map resolution, 0 = as authored (2048)
        public int MaxAdditionalLights = -1; // per-object additional-light limit, -1 = as authored (4)
        public bool AdditionalLightsOff;     // no per-pixel additional lights (measurement only: the corridor fills are realtime points)
        public bool SkinOffscreenOff;        // skinned meshes are not updated while off-screen (SkinnedMeshRenderer.updateWhenOffscreen = false)
        public float EyeScale = 1f;          // XR eye-texture scale
        public int Msaa = 4;                 // MSAA samples (1 = off)
        public int ShadowCascades;           // main-light shadow cascades, 0 = as authored (1)
        public bool AdditionalLightsPerVertex; // additional lights shaded per vertex instead of per pixel (the Quest asset is authored per pixel; changes the look of the corridor fills)
        public bool DepthTextureOn;          // the pipeline renders a depth texture (authored off); look-neutral, measures the extra pass
        public bool OpaqueTextureOn;         // the pipeline copies the opaque colour (authored off); look-neutral, measures the extra copy
        public bool SrpBatcherOff;           // SRP Batcher disabled (measurement only: shows what the batcher saves)
        public bool DynamicBatchingOn;       // URP dynamic batching enabled (authored off)
        public bool HdrOn;                   // HDR colour buffer (authored off on the Quest tier)
        public bool StaticBatch;             // runtime static batching of the interiors (StaticBatchingAtLoad.Combine per loaded scene, city excluded). IRREVERSIBLE for the running process: run it in its own launch, never interleaved with other presets
        public bool InstancingAll;           // Material.enableInstancing on every material in the scenes (only meaningful with the SRP Batcher off, otherwise compatible shaders ignore it)
        public int AnisoMode;                // 0 = as authored (Medium quality level: Forced On), 1 = anisotropic filtering disabled, 2 = per texture (each texture's own level)
        public int AnisoMax;                 // 0 = no cap; otherwise the global maximum anisotropy level (Texture.SetGlobalAnisotropicFilteringLimits); 295 textures ask for 8x, 143 for 4x
        public bool DepthPrimingOn;        // depth priming forced (authored Disabled; a depth pre-pass before the opaque colour pass, usually a loss on tile-based mobile GPUs)
        public bool ReflectionProbesOff;    // reflection probes disabled (measurement only: cost of probe sampling; reflections fall back to the skybox)

        /// <summary>True when the configuration changes something a player would see (used to label rows "measurement only" in the HUD and the CSV).</summary>
        public bool ChangesLook => ShaftsOff || Foveation > 0 || RealtimeFillsOff || CurrentRoomOnly || CityHidden || CharactersHidden || MainShadowsOff || EyeScale != 1f || Msaa != 4 || OptimisedExterior || OriginalExterior || ShadowDistance > 0 || SoftShadowsOff || ShadowMapSize > 0 || MaxAdditionalLights >= 0 || AdditionalLightsOff || ShadowCascades > 0 || AnisoMode > 0 || AnisoMax > 0 || AdditionalLightsPerVertex || HdrOn || ReflectionProbesOff;   // OcclusionOff, DepthTextureOn, OpaqueTextureOn, SrpBatcherOff and DynamicBatchingOn are look-neutral; OcclusionOff is look-neutral by design
    }

    /// <summary>The fixed list of configurations, in the order the left thumbstick click cycles them and the automatic sweep measures them.</summary>
    public static class PerfPresets
    {
        public static readonly IReadOnlyList<PerfPreset> All = new List<PerfPreset>
        {
            new PerfPreset { Id = "B0", Label = "Production default (Android: optimised exterior, room and city portal culling, character culler, no baked occlusion; desktop: original exterior, no culling)" },
            new PerfPreset { Id = "B1", Label = "Legacy: 7 Nov rendering (intermediate texture Always, shafts off)", LegacyRenderer = true, ShaftsOff = true },
            new PerfPreset { Id = "B2", Label = "Legacy renderer only (intermediate texture Always)", LegacyRenderer = true },
            new PerfPreset { Id = "B3", Label = "Shafts and dust off", ShaftsOff = true },
            new PerfPreset { Id = "B4", Label = "Foveation medium (0.5)", Foveation = 0.5f },
            new PerfPreset { Id = "B5", Label = "Foveation high (1.0)", Foveation = 1f },
            new PerfPreset { Id = "B6", Label = "Realtime fill and key lights off", RealtimeFillsOff = true },
            new PerfPreset { Id = "B7", Label = "Only the current room drawn", CurrentRoomOnly = true },
            new PerfPreset { Id = "B8", Label = "City exterior hidden", CityHidden = true },
            new PerfPreset { Id = "B9", Label = "Characters hidden", CharactersHidden = true },
            new PerfPreset { Id = "B10", Label = "Main-light realtime shadows off", MainShadowsOff = true },
            new PerfPreset { Id = "B11", Label = "Eye scale 0.9", EyeScale = 0.9f },
            new PerfPreset { Id = "B12", Label = "Eye scale 1.1", EyeScale = 1.1f },
            new PerfPreset { Id = "B13", Label = "MSAA 2x", Msaa = 2 },
            new PerfPreset { Id = "B15", Label = "Occlusion culling off (shows what the baked occlusion saves)", OcclusionOff = true },
            new PerfPreset { Id = "B16", Label = "City always drawn (exterior culling off)", ExteriorCullOff = true },
            new PerfPreset { Id = "B17", Label = "Optimised exterior city (Quest variant, P3-EXT)", OptimisedExterior = true },
            new PerfPreset { Id = "B18", Label = "Optimised exterior without portal culling (reduced meshes only)", OptimisedExterior = true, PortalCullOff = true },
            new PerfPreset { Id = "B19", Label = "Before the 8 Nov optimisations (occlusion off, city always drawn, original exterior)", OcclusionOff = true, ExteriorCullOff = true },
            new PerfPreset { Id = "B20", Label = "Original exterior city (recoverable comparison variant; the Quest default is the optimised one)", OriginalExterior = true },
            new PerfPreset { Id = "B27", Label = "Optimised exterior, baked occlusion off (candidate: removes the light-selection side effect of occlusion)", OptimisedExterior = true, OcclusionOff = true },
            new PerfPreset { Id = "B28", Label = "Candidate: optimised exterior + room portal culling, baked occlusion off", OptimisedExterior = true, OcclusionOff = true, RoomCull = true },
            new PerfPreset { Id = "B29", Label = "Candidate B28 + character culler", OptimisedExterior = true, OcclusionOff = true, RoomCull = true, CharCull = true },
            new PerfPreset { Id = "B30", Label = "Quest candidate with baked occlusion forced on (shows what the occlusion adds to B29)", OptimisedExterior = true, RoomCull = true, CharCull = true, OcclusionOn = true },
            new PerfPreset { Id = "B21", Label = "Shadow distance 5 m", ShadowDistance = 5f },
            new PerfPreset { Id = "B22", Label = "Hard realtime shadows", SoftShadowsOff = true },
            new PerfPreset { Id = "B23", Label = "Shadow map 1024", ShadowMapSize = 1024 },
            new PerfPreset { Id = "B24", Label = "At most 2 additional lights per object", MaxAdditionalLights = 2 },
            new PerfPreset { Id = "B25", Label = "Additional lights off (measurement only)", AdditionalLightsOff = true },
            new PerfPreset { Id = "B26", Label = "Skinned meshes not updated off-screen", SkinOffscreenOff = true },
            // Round 4 audit presets (lane 1): every URP and rendering setting measured one at a time against B28/B29 by the two-eye benchmark.
            new PerfPreset { Id = "B31", Label = "Eye scale 0.8", EyeScale = 0.8f },
            new PerfPreset { Id = "B32", Label = "Eye scale 1.2", EyeScale = 1.2f },
            new PerfPreset { Id = "B33", Label = "MSAA off (1x)", Msaa = 1 },
            new PerfPreset { Id = "B34", Label = "Main-light shadow cascades 2", ShadowCascades = 2 },
            new PerfPreset { Id = "B35", Label = "Main-light shadow cascades 4", ShadowCascades = 4 },
            new PerfPreset { Id = "B36", Label = "Shadow map 4096", ShadowMapSize = 4096 },
            new PerfPreset { Id = "B37", Label = "Shadow distance 12 m", ShadowDistance = 12f },
            new PerfPreset { Id = "B38", Label = "Additional lights per vertex (authored per pixel)", AdditionalLightsPerVertex = true },
            new PerfPreset { Id = "B39", Label = "Up to 8 additional lights per object (authored 4)", MaxAdditionalLights = 8 },
            new PerfPreset { Id = "B40", Label = "Depth texture on (authored off)", DepthTextureOn = true },
            new PerfPreset { Id = "B41", Label = "Opaque texture on (authored off)", OpaqueTextureOn = true },
            new PerfPreset { Id = "B42", Label = "SRP Batcher off (measurement only)", SrpBatcherOff = true },
            new PerfPreset { Id = "B43", Label = "Dynamic batching on (authored off)", DynamicBatchingOn = true },
            new PerfPreset { Id = "B44", Label = "HDR colour buffer on (authored off)", HdrOn = true },
            new PerfPreset { Id = "B45", Label = "Reflection probes off (measurement only)", ReflectionProbesOff = true },
            new PerfPreset { Id = "B46", Label = "Depth priming forced (authored disabled; look-neutral, measures the extra depth pre-pass)", DepthPrimingOn = true },
            new PerfPreset { Id = "B47", Label = "Anisotropic filtering off (authored: Forced On at the Medium level; shows the texture-sampling cost; floors and walls at grazing angles blur)", AnisoMode = 1 },
            new PerfPreset { Id = "B48", Label = "Anisotropic filtering per texture (each texture's own level instead of Forced On)", AnisoMode = 2 },
            new PerfPreset { Id = "B49", Label = "Anisotropic filtering capped at 4x (authored: uncapped, up to 8x per texture)", AnisoMax = 4 },
            new PerfPreset { Id = "B52", Label = "Anisotropic filtering capped at 2x", AnisoMax = 2 },
            new PerfPreset { Id = "B50", Label = "Runtime static batching of the interiors (irreversible: own launch only; look-neutral, measures draw calls, SetPass, CPU submit and memory)", StaticBatch = true },
            new PerfPreset { Id = "B51", Label = "SRP Batcher off + GPU instancing on every material (compare with B42 alone; measurement only)", SrpBatcherOff = true, InstancingAll = true },
            // Look-preserving candidates (lane 4, round 4 audit): 2 additional lights per object + hard shadows, optionally per-vertex additional lights; each needs a pixel diff against B20 before it is recommended
            new PerfPreset { Id = "B58", Label = "B20 + 2 additional lights per object + hard shadows (look-preserving candidate)", MaxAdditionalLights = 2, SoftShadowsOff = true },
            new PerfPreset { Id = "B59", Label = "B58 + additional lights per vertex (candidate; look must be diffed)", MaxAdditionalLights = 2, SoftShadowsOff = true, AdditionalLightsPerVertex = true },
            new PerfPreset { Id = "B56", Label = "B29 + 2 additional lights per object + hard shadows (best look-preserving desktop candidate)", OptimisedExterior = true, OcclusionOff = true, RoomCull = true, CharCull = true, MaxAdditionalLights = 2, SoftShadowsOff = true },
            new PerfPreset { Id = "B57", Label = "B56 + additional lights per vertex (candidate; look must be diffed)", OptimisedExterior = true, OcclusionOff = true, RoomCull = true, CharCull = true, MaxAdditionalLights = 2, SoftShadowsOff = true, AdditionalLightsPerVertex = true },
            new PerfPreset { Id = "B55",Label = "City hidden, baked occlusion off (no-city base for the occlusion-off pair B15/B27)", CityHidden = true, OcclusionOff = true },
            // Android-fairness comparison set (round 4): every preset below has baked occlusion OFF as the Android player runs it (B20 had it ON). Android runs the optimised exterior, room portal culling and the character culler; with the original exterior only the exterior variant changes.
            new PerfPreset { Id = "B60", Label = "B15 (original exterior, baked occlusion off, no room or character culling) + B58 lights and shadows (2 additional lights per object, hard shadows)", OcclusionOff = true, MaxAdditionalLights = 2, SoftShadowsOff = true },
            new PerfPreset { Id = "B61", Label = "ORIGINAL exterior with the real Android settings: baked occlusion off, room portal culling, character culler (B29 with the original city)", OriginalExterior = true, OcclusionOff = true, RoomCull = true, CharCull = true },
            new PerfPreset { Id = "B62", Label = "B61 + B58 lights and shadows (2 additional lights per object, hard shadows)", OriginalExterior = true, OcclusionOff = true, RoomCull = true, CharCull = true, MaxAdditionalLights = 2, SoftShadowsOff = true },
            new PerfPreset { Id = "B63", Label = "B15 + 3 additional lights per object + hard shadows (selective alternative)", OcclusionOff = true, MaxAdditionalLights = 3, SoftShadowsOff = true },
            new PerfPreset { Id = "B64", Label = "B15 + 2 additional lights per object, soft shadows (B24 on the B15 base)", OcclusionOff = true, MaxAdditionalLights = 2 },
            new PerfPreset { Id = "B65", Label = "B15 + hard shadows only (B22 on the B15 base)", OcclusionOff = true, SoftShadowsOff = true },
            new PerfPreset { Id = "B14", Label = "Candidate set: foveation 0.5, fills off, current room only", Foveation = 0.5f, RealtimeFillsOff = true, CurrentRoomOnly = true },
        };
    }
}
