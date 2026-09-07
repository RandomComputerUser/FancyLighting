using System.ComponentModel;
using FancyLighting.Config.Enums;
using Terraria.ModLoader.Config;

namespace FancyLighting.Config.Configs;

public sealed class PreferencesConfig : ModConfig
{
    public override ConfigScope Mode => ConfigScope.ClientSide;

    // Handled automatically by tModLoader
    public static PreferencesConfig Instance;

    public float OutputGamma() => OutputImageGamma / 100f;

    public bool UseCustomGamma() => OutputImageGamma != DefaultOptions.OutputImageGamma;

    public float NormalMapsMultiplier() => NormalMapsIntensity / 5f;

    public float ExposureMult() => MathF.Pow(2f, Exposure / 10f);

    public float BloomLerp() => 0.005f * BloomStrength;

    public double VibranceIncrease() => VibranceBoost / 75.0;

    public float AmbientOcclusionPower() => AmbientOcclusionIntensity / 100f;

    public float AmbientOcclusionMult() => AmbientLightProportion / 100f;

    public float FancyLightingEngineExitMultiplier() =>
        1f - (FancyLightingEngineLightLoss / 100f);

    public float FancyLightingEngineAbsorptionExponent() =>
        FancyLightingEngineLightAbsorption / 100f;

    public float FancyLightingEngineGlobalIlluminationMultiplier() =>
        FancyLightingEngineIndirectBrightness / 100f;

    public float SkyBrightness() => SkyBrightnessBoost / 5f;

    public float CloudShadingMultiplier() => CloudShadingStrength / 10f * 0.7f;

    public override void OnChanged()
    {
        ModContent.GetInstance<SettingsSystem>()?.OnConfigChange();
    }

    // General

    [Header("General")]
    [Range(DefaultOptions.MinThreadCount, DefaultOptions.MaxThreadCount)]
    [Increment(1)]
    [DefaultValue(DefaultOptions.ThreadCount)]
    public int ThreadCount
    {
        get => _threadCount;
        set =>
            _threadCount =
                value is DefaultOptions.ThreadCount
                    ? DefaultOptions.RuntimeDefaultThreadCount
                    : value;
    }

    private int _threadCount;

    // Color Management

    [Header("ColorManagement")]
    [Range(100, 340)]
    [Increment(5)]
    [DefaultValue(DefaultOptions.OutputImageGamma)]
    [Slider]
    public int OutputImageGamma { get; set; }

    [DefaultValue(DefaultOptions.UseSrgb)]
    public bool UseSrgb { get; set; }

    // Smooth Lighting

    [Header("SmoothLighting")]
    [Range(1, 10)]
    [DefaultValue(DefaultOptions.NormalMapsIntensity)]
    [Slider]
    public int NormalMapsIntensity { get; set; }

    [DefaultValue(DefaultOptions.FineNormalMaps)]
    public bool FineNormalMaps { get; set; }

    [DefaultValue(DefaultOptions.UseGrayscaleLighting)]
    public bool UseGrayscaleLighting { get; set; }

    // Full HDR Rendering

    [Header("FullHdrRendering")]
    [DefaultValue(DefaultOptions.ToneMappingOperator)]
    [Dropdown]
    public ToneMappingPreset ToneMappingOperator { get; set; }

    [Range(-20, 20)]
    [DefaultValue(DefaultOptions.Exposure)]
    [Slider]
    public int Exposure { get; set; }

    [Range(0, 15)]
    [DefaultValue(DefaultOptions.VibranceBoost)]
    [Slider]
    public int VibranceBoost { get; set; }

    [DefaultValue(DefaultOptions.HdrBloom)]
    public bool HdrBloom { get; set; }

    [Range(1, 5)]
    [DefaultValue(DefaultOptions.BloomRadius)]
    [Slider]
    [DrawTicks]
    public int BloomRadius { get; set; }

    [Range(1, 15)]
    [DefaultValue(DefaultOptions.BloomStrength)]
    [Slider]
    public int BloomStrength { get; set; }

    // Ambient Occlusion

    [Header("AmbientOcclusion")]
    [Range(1, 4)]
    [DefaultValue(DefaultOptions.AmbientOcclusionRadius)]
    [Slider]
    [DrawTicks]
    public int AmbientOcclusionRadius { get; set; }

    [Range(20, 500)]
    [Increment(25)]
    [DefaultValue(DefaultOptions.AmbientOcclusionIntensity)]
    [Slider]
    public int AmbientOcclusionIntensity { get; set; }

    [Range(5, 100)]
    [Increment(5)]
    [DefaultValue(DefaultOptions.AmbientLightProportion)]
    [Slider]
    public int AmbientLightProportion { get; set; }

    // Fancy Lighting Engine

    [Header("FancyLightingEngine")]
    [Range(0, 100)]
    [Increment(5)]
    [DefaultValue(DefaultOptions.FancyLightingEngineLightLoss)]
    [Slider]
    public int FancyLightingEngineLightLoss { get; set; }

    [Range(50, 200)]
    [Increment(10)]
    [DefaultValue(DefaultOptions.FancyLightingEngineLightAbsorption)]
    [Slider]
    public int FancyLightingEngineLightAbsorption { get; set; }

    [Range(5, 95)]
    [Increment(5)]
    [DefaultValue(DefaultOptions.FancyLightingEngineGlobalIlluminationMult)]
    [Slider]
    public int FancyLightingEngineIndirectBrightness { get; set; }

    [DefaultValue(DefaultOptions.FancyLightingEngineNonSolidOpaque)]
    public bool FancyLightingEngineNonSolidOpaque { get; set; }

    // Fancy Sky

    [Header("FancySky")]
    [Range(0, 15)]
    [DefaultValue(DefaultOptions.SkyBrightnessBoost)]
    [Slider]
    public int SkyBrightnessBoost { get; set; }

    [DefaultValue(DefaultOptions.FancySkyColorsPreset)]
    [Dropdown]
    public SkyColorPreset FancySkyColorsPreset { get; set; }

    [Range(1, 10)]
    [DefaultValue(DefaultOptions.CloudShadingStrength)]
    [Slider]
    public int CloudShadingStrength { get; set; }

    // Miscellaneous

    [Header("Miscellaneous")]
    [DefaultValue(DefaultOptions.DepthOfField)]
    public bool DepthOfField { get; set; }

    [Range(1, 5)]
    [DefaultValue(DefaultOptions.DepthOfFieldRadius)]
    [Slider]
    [DrawTicks]
    public int DepthOfFieldRadius { get; set; }
}
