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
        public int Msaa = 4;                 // MSAA samples

        /// <summary>True when the configuration changes something a player would see (used to label rows "measurement only" in the HUD and the CSV).</summary>
        public bool ChangesLook => ShaftsOff || Foveation > 0 || RealtimeFillsOff || CurrentRoomOnly || CityHidden || CharactersHidden || MainShadowsOff || EyeScale != 1f || Msaa != 4 || OptimisedExterior || OriginalExterior || ShadowDistance > 0 || SoftShadowsOff || ShadowMapSize > 0 || MaxAdditionalLights >= 0 || AdditionalLightsOff;   // OcclusionOff is look-neutral by design
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
            new PerfPreset { Id = "B14", Label = "Candidate set: foveation 0.5, fills off, current room only", Foveation = 0.5f, RealtimeFillsOff = true, CurrentRoomOnly = true },
        };
    }
}
