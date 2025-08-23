using FancyLighting.Config.Enums;

namespace FancyLighting.Config;

internal record PresetOptions
{
    // Smooth Lighting

    public bool UseSmoothLighting { get; init; } = DefaultOptions.UseSmoothLighting;
    public bool UseLightMapBlurring { get; init; } = DefaultOptions.UseLightMapBlurring;
    public bool UseEnhancedBlurring { get; init; } = DefaultOptions.UseEnhancedBlurring;

    public bool SimulateNormalMaps { get; init; } = DefaultOptions.SimulateNormalMaps;

    public bool UseEnhancedGlowMaskSupport { get; init; } =
        DefaultOptions.UseEnhancedGlowMaskSupport;

    public RenderMode LightMapRenderMode { get; init; } =
        DefaultOptions.LightMapRenderMode;

    public bool HdrBloom { get; init; } = DefaultOptions.HdrBloom;

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
        UseEnhancedGlowMaskSupport = config.UseEnhancedGlowMaskSupport;
        LightMapRenderMode = config.LightMapRenderMode;
        HdrBloom = config.HdrBloom;

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

    public static readonly PresetOptions VanillaPresetOptions =
        new()
        {
            UseSmoothLighting = false,
            UseEnhancedBlurring = false,
            SimulateNormalMaps = false,
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

    public static readonly PresetOptions MinimalPresetOptions =
        new()
        {
            UseSmoothLighting = true,
            UseEnhancedBlurring = false,
            SimulateNormalMaps = false,
            UseEnhancedGlowMaskSupport = false,
            LightMapRenderMode = RenderMode.Bilinear,
            UseAmbientOcclusion = true,
            DoNonSolidAmbientOcclusion = false,
            DoTileEntityAmbientOcclusion = false,
            UseFancyLightingEngine = false,
            FancyLightingEngineMode = LightingEngineMode.Low,
            SimulateGlobalIllumination = false,
            UseFancySkyRendering = false,
            UseFancySkyColors = true,
        };

    public static readonly PresetOptions LowPresetOptions = new();

    public static readonly PresetOptions MediumPresetOptions =
        new()
        {
            UseSmoothLighting = true,
            UseEnhancedBlurring = true,
            SimulateNormalMaps = false,
            UseEnhancedGlowMaskSupport = false,
            LightMapRenderMode = RenderMode.Bicubic,
            UseAmbientOcclusion = true,
            DoNonSolidAmbientOcclusion = true,
            DoTileEntityAmbientOcclusion = true,
            UseFancyLightingEngine = true,
            FancyLightingEngineMode = LightingEngineMode.Medium,
            SimulateGlobalIllumination = true,
            UseFancySkyRendering = true,
            UseFancySkyColors = true,
        };

    public static readonly PresetOptions HighPresetOptions =
        new()
        {
            UseSmoothLighting = true,
            UseEnhancedBlurring = true,
            SimulateNormalMaps = true,
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

    public static readonly PresetOptions UltraPresetOptions =
        new()
        {
            UseSmoothLighting = true,
            UseEnhancedBlurring = true,
            SimulateNormalMaps = true,
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

    public static readonly Dictionary<PresetOptions, SettingsPreset> PresetLookup =
        new()
        {
            [VanillaPresetOptions] = SettingsPreset.VanillaPreset,
            [MinimalPresetOptions] = SettingsPreset.MinimalPreset,
            [LowPresetOptions] = SettingsPreset.LowPreset,
            [MediumPresetOptions] = SettingsPreset.MediumPreset,
            [HighPresetOptions] = SettingsPreset.HighPreset,
            [UltraPresetOptions] = SettingsPreset.UltraPreset,
        };

    public static readonly Dictionary<SettingsPreset, PresetOptions> PresetOptionsLookup =
        PresetLookup.ToDictionary(entry => entry.Value, entry => entry.Key);
}
