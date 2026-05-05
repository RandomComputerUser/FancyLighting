using System.ComponentModel;
using FancyLighting.Config.Enums;
using Terraria.ModLoader.Config;

namespace FancyLighting.Config;

public sealed class PreferencesConfig : ModConfig
{
    public override ConfigScope Mode => ConfigScope.ClientSide;

    // Handled automatically by tModLoader
    public static PreferencesConfig Instance;

    public float OutputGamma() => Gamma / 100f;

    public bool UseCustomGamma() => Gamma != DefaultOptions.Gamma;

    public float NormalMapsMultiplier() =>
        NormalMapsIntensity <= 5
            ? 0.5f * NormalMapsIntensity
            : (0.05f * (NormalMapsIntensity * NormalMapsIntensity)) + 1.25f;

    public float ExposureMult() => Exposure / 100f;

    public float BloomLerp() => 0.0025f * BloomStrength;

    public double VibranceIncrease() => VibranceBoost / 80.0;

    public float AmbientOcclusionPower() => AmbientOcclusionIntensity / 100f;

    public float AmbientOcclusionMult() => AmbientLightProportion / 100f;

    public float FancyLightingEngineExitMultiplier() =>
        1f - (FancyLightingEngineLightLoss / 100f);

    public float FancyLightingEngineAbsorptionExponent() =>
        FancyLightingEngineLightAbsorption / 100f;

    public float FancyLightingEngineGlobalIlluminationMultiplier() =>
        FancyLightingEngineIndirectBrightness / 100f;

    public float SkyBrightness() => SkyBrightnessBoost / 5f;

    public override void OnChanged()
    {
        ModContent.GetInstance<SettingsSystem>()?.OnConfigChange();
    }

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

    [DefaultValue(DefaultOptions.MonitorPerformance)]
    public bool MonitorPerformance { get; set; }

    // Tone Mapping

    [Header("ToneMapping")]
    [Range(140, 300)]
    [Increment(10)]
    [DefaultValue(DefaultOptions.Gamma)]
    [Slider]
    public int Gamma { get; set; }

    [DefaultValue(DefaultOptions.UseSrgb)]
    public bool UseSrgb { get; set; }

    // Smooth Lighting

    [Header("SmoothLighting")]
    [Range(1, 15)]
    [DefaultValue(DefaultOptions.NormalMapsIntensity)]
    [Slider]
    public int NormalMapsIntensity { get; set; }

    [DefaultValue(DefaultOptions.FineNormalMaps)]
    public bool FineNormalMaps { get; set; }

    [DefaultValue(DefaultOptions.DisableHdrDuringBossFights)]
    public bool DisableHdrDuringBossFights { get; set; }

    [DefaultValue(DefaultOptions.UseGrayscaleLighting)]
    public bool UseGrayscaleLighting { get; set; }

    [DefaultValue(DefaultOptions.RenderOnlyLight)]
    public bool RenderOnlyLight { get; set; }

    // Full HDR Rendering

    [Header("FullHdrRendering")]
    [Range(50, 150)]
    [Increment(5)]
    [DefaultValue(DefaultOptions.Exposure)]
    [Slider]
    public int Exposure { get; set; }

    [DefaultValue(DefaultOptions.ToneMappingOperator)]
    public ToneMappingPreset ToneMappingOperator { get; set; }

    [Range(0, 10)]
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

    [Range(1, 20)]
    [DefaultValue(DefaultOptions.BloomStrength)]
    [Slider]
    public int BloomStrength { get; set; }

    [DefaultValue(DefaultOptions.DepthOfField)]
    public bool DepthOfField { get; set; }

    [Range(1, 5)]
    [DefaultValue(DefaultOptions.DepthOfFieldRadius)]
    [Slider]
    [DrawTicks]
    public int DepthOfFieldRadius { get; set; }

    [DefaultValue(DefaultOptions.UseHdrCompatibilityFixes)]
    public bool UseHdrCompatibilityFixes { get; set; }

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
    [Range(0, 10)]
    [DefaultValue(DefaultOptions.SkyBrightnessBoost)]
    [Slider]
    public int SkyBrightnessBoost { get; set; }

    [DefaultValue(DefaultOptions.FancySkyColorsPreset)]
    public SkyColorPreset FancySkyColorsPreset { get; set; }

    [DefaultValue(DefaultOptions.ShowFancySkyColorGradients)]
    public bool ShowFancySkyColorGradients { get; set; }
}
