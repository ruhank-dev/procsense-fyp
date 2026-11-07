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
        public float EyeScale = 1f;          // XR eye-texture scale
        public int Msaa = 4;                 // MSAA samples

        /// <summary>True when the configuration changes something a player would see (used to label rows "measurement only" in the HUD and the CSV).</summary>
        public bool ChangesLook => ShaftsOff || Foveation > 0 || RealtimeFillsOff || CurrentRoomOnly || CityHidden || CharactersHidden || MainShadowsOff || EyeScale != 1f || Msaa != 4;
    }

    /// <summary>The fixed list of configurations, in the order the left thumbstick click cycles them and the automatic sweep measures them.</summary>
    public static class PerfPresets
    {
        public static readonly IReadOnlyList<PerfPreset> All = new List<PerfPreset>
        {
            new PerfPreset { Id = "B0", Label = "Production default (Quest renderer, shafts and dust on)" },
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
            new PerfPreset { Id = "B14", Label = "Candidate set: foveation 0.5, fills off, current room only", Foveation = 0.5f, RealtimeFillsOff = true, CurrentRoomOnly = true },
        };
    }
}
