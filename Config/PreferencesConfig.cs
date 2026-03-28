using System.ComponentModel;
using FancyLighting.Config.Enums;
using Terraria.ModLoader.Config;

namespace FancyLighting.Config;

public sealed class PreferencesConfig : ModConfig
{
    public override ConfigScope Mode => ConfigScope.ClientSide;

    // Handled automatically by tModLoader
    public static PreferencesConfig Instance;

    internal float GammaExponent() =>
        (
            LightingConfig.Instance?.HiDefFeaturesEnabled() is true
                ? PostProcessing.HiDefGammaMult
                : 1f
        ) * (Gamma / 100f);

    internal bool UseCustomGamma() => Gamma != DefaultOptions.Gamma;

    internal float NormalMapsMultiplier() =>
        NormalMapsIntensity <= 5
            ? 0.5f * NormalMapsIntensity
            : (0.05f * (NormalMapsIntensity * NormalMapsIntensity)) + 1.25f;

    internal float ExposureMult() => Exposure / 100f;

    internal float BloomLerp() => 0.0025f * BloomStrength;

    internal float AmbientOcclusionPower() => AmbientOcclusionIntensity / 100f;

    internal float AmbientOcclusionMult() => AmbientLightProportion / 100f;

    internal float FancyLightingEngineExitMultiplier() =>
        1f - (FancyLightingEngineLightLoss / 100f);

    internal float FancyLightingEngineAbsorptionExponent() =>
        FancyLightingEngineLightAbsorption / 100f;

    internal float FancyLightingEngineGlobalIlluminationMultiplier() =>
        FancyLightingEngineIndirectBrightness / 100f;

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

    // Tone Mapping

    [Header("ToneMapping")]
    [Range(160, 280)]
    [Increment(10)]
    [DefaultValue(DefaultOptions.Gamma)]
    [Slider]
    public int Gamma { get; set; }

    [DefaultValue(DefaultOptions.UseSrgb)]
    public bool UseSrgb { get; set; }

    // Smooth Lighting

    [Header("SmoothLighting")]
    [Range(1, 15)]
    [Increment(1)]
    [DefaultValue(DefaultOptions.NormalMapsIntensity)]
    [Slider]
    public int NormalMapsIntensity { get; set; }

    [DefaultValue(DefaultOptions.FineNormalMaps)]
    public bool FineNormalMaps { get; set; }

    [Range(50, 150)]
    [Increment(5)]
    [DefaultValue(DefaultOptions.Exposure)]
    [Slider]
    public int Exposure { get; set; }

    [Range(1, 5)]
    [Increment(1)]
    [DefaultValue(DefaultOptions.BloomRadius)]
    [Slider]
    [DrawTicks]
    public int BloomRadius { get; set; }

    [Range(1, 20)]
    [Increment(1)]
    [DefaultValue(DefaultOptions.BloomStrength)]
    [Slider]
    public int BloomStrength { get; set; }

    [DefaultValue(DefaultOptions.ToneMappingOperator)]
    public ToneMappingPreset ToneMappingOperator { get; set; }

    [DefaultValue(DefaultOptions.UseHdrCompatibilityFixes)]
    public bool UseHdrCompatibilityFixes { get; set; }

    [DefaultValue(DefaultOptions.DisableHdrDuringBossFights)]
    public bool DisableHdrDuringBossFights { get; set; }

    [DefaultValue(DefaultOptions.UseGrayscaleLighting)]
    public bool UseGrayscaleLighting { get; set; }

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

    [DefaultValue(DefaultOptions.TrackFancyLightingEnginePerf)]
    public bool TrackFancyLightingEnginePerf { get; set; }

    // Fancy Sky

    [Header("FancySky")]
    [DefaultValue(DefaultOptions.FancySkyColorsPreset)]
    public SkyColorPreset FancySkyColorsPreset { get; set; }

    [DefaultValue(DefaultOptions.ShowFancySkyColorGradients)]
    public bool ShowFancySkyColorGradients { get; set; }
}
