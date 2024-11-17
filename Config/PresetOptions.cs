using System.Collections.Generic;
using System.Linq;
using FancyLighting.Config.Enums;

namespace FancyLighting.Config;

internal record PresetOptions
{
    public bool UseSmoothLighting { get; init; } = DefaultOptions.UseSmoothLighting;
    public bool UseLightMapBlurring { get; init; } = DefaultOptions.UseLightMapBlurring;
    public bool UseEnhancedBlurring { get; init; } = DefaultOptions.UseEnhancedBlurring;

    public bool SimulateNormalMaps { get; init; } = DefaultOptions.SimulateNormalMaps;

    public bool UseEnhancedGlowMaskSupport { get; init; } =
        DefaultOptions.UseEnhancedGlowMaskSupport;

    public RenderMode LightMapRenderMode { get; init; } =
        DefaultOptions.LightMapRenderMode;

    public bool UseAmbientOcclusion { get; init; } = DefaultOptions.UseAmbientOcclusion;

    public bool DoNonSolidAmbientOcclusion { get; init; } =
        DefaultOptions.DoNonSolidAmbientOcclusion;

    public bool DoTileEntityAmbientOcclusion { get; init; } =
        DefaultOptions.DoTileEntityAmbientOcclusion;

    public bool UseFancyLightingEngine { get; init; } =
        DefaultOptions.UseFancyLightingEngine;

    public bool FancyLightingEngineUseTemporal { get; init; } =
        DefaultOptions.FancyLightingEngineUseTemporal;

    public LightingEngineMode FancyLightingEngineMode { get; init; } =
        DefaultOptions.FancyLightingEngineMode;

    public bool SimulateGlobalIllumination { get; init; } =
        DefaultOptions.SimulateGlobalIllumination;

    public PresetOptions() { }

    public PresetOptions(LightingConfig config)
    {
        UseSmoothLighting = config.UseSmoothLighting;
        UseLightMapBlurring = config.UseLightMapBlurring;
        UseEnhancedBlurring = config.UseEnhancedBlurring;
        SimulateNormalMaps = config.SimulateNormalMaps;
        UseEnhancedGlowMaskSupport = config.UseEnhancedGlowMaskSupport;
        LightMapRenderMode = config.LightMapRenderMode;

        UseAmbientOcclusion = config.UseAmbientOcclusion;
        DoNonSolidAmbientOcclusion = config.DoNonSolidAmbientOcclusion;
        DoTileEntityAmbientOcclusion = config.DoTileEntityAmbientOcclusion;

        UseFancyLightingEngine = config.UseFancyLightingEngine;
        FancyLightingEngineUseTemporal = config.FancyLightingEngineUseTemporal;
        FancyLightingEngineMode = config.FancyLightingEngineMode;
        SimulateGlobalIllumination = config.SimulateGlobalIllumination;
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
            FancyLightingEngineMode = LightingEngineMode.One,
            SimulateGlobalIllumination = false,
        };

    public static readonly PresetOptions MinimalPresetOptions =
        new()
        {
            UseSmoothLighting = true,
            UseEnhancedBlurring = false,
            SimulateNormalMaps = false,
            UseEnhancedGlowMaskSupport = false,
            LightMapRenderMode = RenderMode.Bilinear,
            UseAmbientOcclusion = false,
            DoNonSolidAmbientOcclusion = false,
            DoTileEntityAmbientOcclusion = false,
            UseFancyLightingEngine = false,
            FancyLightingEngineMode = LightingEngineMode.One,
            SimulateGlobalIllumination = false,
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
            FancyLightingEngineMode = LightingEngineMode.Two,
            SimulateGlobalIllumination = false,
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
            FancyLightingEngineMode = LightingEngineMode.Two,
            SimulateGlobalIllumination = true,
        };

    public static readonly PresetOptions UltraPresetOptions =
        new()
        {
            UseSmoothLighting = true,
            UseEnhancedBlurring = true,
            SimulateNormalMaps = true,
            UseEnhancedGlowMaskSupport = true,
            LightMapRenderMode = RenderMode.EnhancedHdrBloom,
            UseAmbientOcclusion = true,
            DoNonSolidAmbientOcclusion = true,
            DoTileEntityAmbientOcclusion = true,
            UseFancyLightingEngine = true,
            FancyLightingEngineMode = LightingEngineMode.Four,
            SimulateGlobalIllumination = true,
        };

    public static readonly Dictionary<PresetOptions, Preset> PresetLookup =
        new()
        {
            [VanillaPresetOptions] = Preset.VanillaPreset,
            [MinimalPresetOptions] = Preset.MinimalPreset,
            [LowPresetOptions] = Preset.LowPreset,
            [MediumPresetOptions] = Preset.MediumPreset,
            [HighPresetOptions] = Preset.HighPreset,
            [UltraPresetOptions] = Preset.UltraPreset,
        };

    public static readonly Dictionary<Preset, PresetOptions> PresetOptionsLookup =
        PresetLookup.ToDictionary(entry => entry.Value, entry => entry.Key);
}
