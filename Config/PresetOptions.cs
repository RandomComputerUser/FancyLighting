using FancyLighting.Config.Enums;

namespace FancyLighting.Config;

internal record PresetOptions
{
    // Smooth Lighting

    public bool UseSmoothLighting { get; init; } = DefaultOptions.UseSmoothLighting;
    public bool UseLightMapBlurring { get; init; } = DefaultOptions.UseLightMapBlurring;
    public bool UseEnhancedBlurring { get; init; } = DefaultOptions.UseEnhancedBlurring;

    public bool SimulateNormalMaps { get; init; } = DefaultOptions.SimulateNormalMaps;

    public bool SimulateNonSolidNormals { get; init; } =
        DefaultOptions.SimulateNonSolidNormals;

    public bool SimulateTileEntityNormals { get; init; } =
        DefaultOptions.SimulateTileEntityNormals;

    public bool UseEnhancedGlowMaskSupport { get; init; } =
        DefaultOptions.UseEnhancedGlowMaskSupport;

    public RenderMode LightMapRenderMode { get; init; } =
        DefaultOptions.LightMapRenderMode;

    // Ambient Occlusion

    public bool UseAmbientOcclusion { get; init; } = DefaultOptions.UseAmbientOcclusion;

    public bool DoNonSolidAmbientOcclusion { get; init; } =
        DefaultOptions.DoNonSolidAmbientOcclusion;

    public bool DoTileEntityAmbientOcclusion { get; init; } =
        DefaultOptions.DoTileEntityAmbientOcclusion;

    // Fancy Lighting Engine

    public bool UseFancyLightingEngine { get; init; } =
        DefaultOptions.UseFancyLightingEngine;

    public bool FancyLightingEngineUseTemporal { get; init; } =
        DefaultOptions.FancyLightingEngineUseTemporal;

    public LightingEngineMode FancyLightingEngineMode { get; init; } =
        DefaultOptions.FancyLightingEngineMode;

    public bool SimulateGlobalIllumination { get; init; } =
        DefaultOptions.SimulateGlobalIllumination;

    // Fancy Sky

    public bool UseFancySkyRendering { get; init; } = DefaultOptions.UseFancySkyRendering;

    public bool UseFancySkyColors { get; init; } = DefaultOptions.UseFancySkyColors;

    public PresetOptions() { }

    public PresetOptions(LightingConfig config)
    {
        UseSmoothLighting = config.UseSmoothLighting;
        UseLightMapBlurring = config.UseLightMapBlurring;
        UseEnhancedBlurring = config.UseEnhancedBlurring;
        SimulateNormalMaps = config.SimulateNormalMaps;
        SimulateNonSolidNormals = config.SimulateNonSolidNormals;
        SimulateTileEntityNormals = config.SimulateTileEntityNormals;
        UseEnhancedGlowMaskSupport = config.UseEnhancedGlowMaskSupport;
        LightMapRenderMode = config.LightMapRenderMode;

        UseAmbientOcclusion = config.UseAmbientOcclusion;
        DoNonSolidAmbientOcclusion = config.DoNonSolidAmbientOcclusion;
        DoTileEntityAmbientOcclusion = config.DoTileEntityAmbientOcclusion;

        UseFancyLightingEngine = config.UseFancyLightingEngine;
        FancyLightingEngineUseTemporal = config.FancyLightingEngineUseTemporal;
        FancyLightingEngineMode = config.FancyLightingEngineMode;
        SimulateGlobalIllumination = config.SimulateGlobalIllumination;

        UseFancySkyRendering = config.UseFancySkyRendering;
        UseFancySkyColors = config.UseFancySkyColors;
    }

    public static PresetOptions VanillaPresetOptions =>
        new()
        {
            UseSmoothLighting = false,
            UseEnhancedBlurring = false,
            SimulateNormalMaps = false,
            SimulateNonSolidNormals = false,
            SimulateTileEntityNormals = false,
            UseEnhancedGlowMaskSupport = false,
            LightMapRenderMode = RenderMode.Bilinear,
            UseAmbientOcclusion = false,
            DoNonSolidAmbientOcclusion = false,
            DoTileEntityAmbientOcclusion = false,
            UseFancyLightingEngine = false,
            FancyLightingEngineMode = LightingEngineMode.Low,
            SimulateGlobalIllumination = false,
            UseFancySkyRendering = false,
            UseFancySkyColors = false,
        };

    public static PresetOptions LowPresetOptions =>
        new()
        {
            UseSmoothLighting = true,
            UseEnhancedBlurring = false,
            SimulateNormalMaps = false,
            SimulateNonSolidNormals = false,
            SimulateTileEntityNormals = false,
            UseEnhancedGlowMaskSupport = false,
            LightMapRenderMode = RenderMode.Bilinear,
            UseAmbientOcclusion = true,
            DoNonSolidAmbientOcclusion = false,
            DoTileEntityAmbientOcclusion = false,
            UseFancyLightingEngine = false,
            FancyLightingEngineMode = LightingEngineMode.Low,
            SimulateGlobalIllumination = false,
            UseFancySkyRendering = true,
            UseFancySkyColors = true,
        };

    public static PresetOptions MediumPresetOptions => new();

    public static PresetOptions HighPresetOptions =>
        new()
        {
            UseSmoothLighting = true,
            UseEnhancedBlurring = true,
            SimulateNormalMaps = true,
            SimulateNonSolidNormals = true,
            SimulateTileEntityNormals = true,
            UseEnhancedGlowMaskSupport = true,
            LightMapRenderMode = RenderMode.BicubicOverbright,
            UseAmbientOcclusion = true,
            DoNonSolidAmbientOcclusion = true,
            DoTileEntityAmbientOcclusion = true,
            UseFancyLightingEngine = true,
            FancyLightingEngineMode = LightingEngineMode.Medium,
            SimulateGlobalIllumination = true,
            UseFancySkyRendering = true,
            UseFancySkyColors = true,
        };

    public static PresetOptions UltraPresetOptions =>
        new()
        {
            UseSmoothLighting = true,
            UseEnhancedBlurring = true,
            SimulateNormalMaps = true,
            SimulateNonSolidNormals = true,
            SimulateTileEntityNormals = true,
            UseEnhancedGlowMaskSupport = true,
            LightMapRenderMode = RenderMode.EnhancedHdr,
            UseAmbientOcclusion = true,
            DoNonSolidAmbientOcclusion = true,
            DoTileEntityAmbientOcclusion = true,
            UseFancyLightingEngine = true,
            FancyLightingEngineMode = LightingEngineMode.High,
            SimulateGlobalIllumination = true,
            UseFancySkyRendering = true,
            UseFancySkyColors = true,
        };

    public static Dictionary<PresetOptions, SettingsPreset> PresetLookup
    {
        get;
        private set;
    } =
        new()
        {
            [VanillaPresetOptions] = SettingsPreset.VanillaPreset,
            [LowPresetOptions] = SettingsPreset.LowPreset,
            [MediumPresetOptions] = SettingsPreset.MediumPreset,
            [HighPresetOptions] = SettingsPreset.HighPreset,
            [UltraPresetOptions] = SettingsPreset.UltraPreset,
        };

    public static Dictionary<SettingsPreset, PresetOptions> PresetOptionsLookup
    {
        get;
        private set;
    } = PresetLookup.ToDictionary(entry => entry.Value, entry => entry.Key);

    internal static void Unload()
    {
        PresetLookup = null;
        PresetOptionsLookup = null;
    }
}
