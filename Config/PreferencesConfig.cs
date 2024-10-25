using System;
using System.ComponentModel;
using Terraria;
using Terraria.ModLoader;
using Terraria.ModLoader.Config;

namespace FancyLighting.Config;

public sealed class PreferencesConfig : ModConfig
{
    public override ConfigScope Mode => ConfigScope.ClientSide;

    // Handled automatically by tModLoader
    public static PreferencesConfig Instance;

    internal bool UseNormalMaps() => NormalMapsStrength != 0;

    internal float NormalMapsMultiplier() => NormalMapsStrength / 100f;

    internal float AmbientOcclusionPower() => AmbientOcclusionIntensity / 100f;

    internal float AmbientOcclusionMult() => AmbientLightProportion / 100f;

    internal float FancyLightingEngineExitMultiplier() =>
        1f - FancyLightingEngineLightLoss / 100f;

    internal float FancyLightingEngineAbsorptionExponent() =>
        FancyLightingEngineLightAbsorption / 100f;

    internal float FancyLightingEngineGlobalIlluminationMultiplier() =>
        FancyLightingEngineIndirectBrightness / 100f;

    internal bool CustomSkyColorsEnabled() =>
        UseCustomSkyColors && Lighting.UsingNewLighting;

    public override void OnChanged() =>
        ModContent.GetInstance<FancyLightingMod>()?.OnConfigChange();

    [DefaultValue(DefaultOptions.UseSrgb)]
    public bool UseSrgb { get; set; }

    [Range(DefaultOptions.MinThreadCount, DefaultOptions.MaxThreadCount)]
    [Increment(1)]
    [DefaultValue(DefaultOptions.ThreadCount)]
    public int ThreadCount
    {
        get => _threadCount;
        set
        {
            _threadCount =
                value is DefaultOptions.ThreadCount
                    ? DefaultOptions.RuntimeDefaultThreadCount
                    : value;
        }
    }
    private int _threadCount;

    // Smooth Lighting, Normal Maps, Overbright
    [Header("SmoothLighting")]
    [Range(25, 400)]
    [Increment(25)]
    [DefaultValue(DefaultOptions.NormalMapsStrength)]
    [Slider]
    [DrawTicks]
    public int NormalMapsStrength { get; set; }

    [DefaultValue(DefaultOptions.FineNormalMaps)]
    public bool FineNormalMaps { get; set; }

    [DefaultValue(DefaultOptions.RenderOnlyLight)]
    public bool RenderOnlyLight { get; set; }

    // Ambient Occlusion
    [Header("AmbientOcclusion")]
    [Range(1, 4)]
    [Increment(1)]
    [DefaultValue(DefaultOptions.AmbientOcclusionRadius)]
    [Slider]
    [DrawTicks]
    public int AmbientOcclusionRadius { get; set; }

    [Range(20, 400)]
    [Increment(20)]
    [DefaultValue(DefaultOptions.AmbientOcclusionIntensity)]
    [Slider]
    [DrawTicks]
    public int AmbientOcclusionIntensity { get; set; }

    [Range(5, 100)]
    [Increment(5)]
    [DefaultValue(DefaultOptions.AmbientLightProportion)]
    [Slider]
    [DrawTicks]
    public int AmbientLightProportion { get; set; }

    // Fancy Lighting Engine
    [Range(0, 100)]
    [Increment(5)]
    [DefaultValue(DefaultOptions.FancyLightingEngineLightLoss)]
    [Slider]
    [DrawTicks]
    public int FancyLightingEngineLightLoss { get; set; }

    [Range(50, 200)]
    [Increment(10)]
    [DefaultValue(DefaultOptions.FancyLightingEngineLightAbsorption)]
    [Slider]
    [DrawTicks]
    public int FancyLightingEngineLightAbsorption { get; set; }

    [Range(5, 95)]
    [Increment(5)]
    [DefaultValue(DefaultOptions.FancyLightingEngineGlobalIlluminationMult)]
    [Slider]
    [DrawTicks]
    public int FancyLightingEngineIndirectBrightness { get; set; }

    [Header("LightingEngine")]
    [DefaultValue(DefaultOptions.FancyLightingEngineVinesOpaque)]
    public bool FancyLightingEngineVinesOpaque { get; set; }

    // Sky Color
    [Header("SkyColor")]
    [DefaultValue(DefaultOptions.UseCustomSkyColors)]
    public bool UseCustomSkyColors { get; set; }

    [DefaultValue(DefaultOptions.CustomSkyPreset)]
    [DrawTicks]
    public SkyColorPreset CustomSkyPreset { get; set; }
}
