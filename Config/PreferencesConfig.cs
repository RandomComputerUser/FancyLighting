using System.ComponentModel;
using FancyLighting.Config.Enums;
using Terraria;
using Terraria.ModLoader;
using Terraria.ModLoader.Config;

namespace FancyLighting.Config;

public sealed class PreferencesConfig : ModConfig
{
    public override ConfigScope Mode => ConfigScope.ClientSide;

    // Handled automatically by tModLoader
    public static PreferencesConfig Instance;

    internal bool NeedsColorLightMode() => UseCustomSkyColors;

    internal float GammaExponent() => Gamma / 100f;

    internal bool UseCustomGamma() => Gamma != DefaultOptions.Gamma;

    internal float NormalMapsMultiplier() =>
        NormalMapsIntensity <= 5
            ? 0.5f * NormalMapsIntensity
            : (0.05f * (NormalMapsIntensity * NormalMapsIntensity)) + 1.25f;

    internal float BloomLerp() => BloomStrength / 200f;

    internal float AmbientOcclusionPower() => AmbientOcclusionIntensity / 100f;

    internal float AmbientOcclusionMult() => AmbientLightProportion / 100f;

    internal float FancyLightingEngineExitMultiplier() =>
        1f - (FancyLightingEngineLightLoss / 100f);

    internal float FancyLightingEngineAbsorptionExponent() =>
        FancyLightingEngineLightAbsorption / 100f;

    internal float FancyLightingEngineGlobalIlluminationMultiplier() =>
        FancyLightingEngineIndirectBrightness / 100f;

    internal bool CustomSkyColorsEnabled() =>
        UseCustomSkyColors && Lighting.UsingNewLighting;

    public override void OnChanged()
    {
        ModContent.GetInstance<FancyLightingMod>()?.OnConfigChange();
        ModContent.GetInstance<FancyLightingModSystem>()?.OnConfigChange();
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
    [DrawTicks]
    public int Gamma { get; set; }

    [DefaultValue(DefaultOptions.UseSrgb)]
    public bool UseSrgb { get; set; }

    // Smooth Lighting, Normal Maps, Overbright
    [Header("SmoothLighting")]
    [Range(1, 12)]
    [Increment(1)]
    [DefaultValue(DefaultOptions.NormalMapsIntensity)]
    [Slider]
    [DrawTicks]
    public int NormalMapsIntensity { get; set; }

    [DefaultValue(DefaultOptions.FineNormalMaps)]
    public bool FineNormalMaps { get; set; }

    [Range(1, 5)]
    [Increment(1)]
    [DefaultValue(DefaultOptions.BloomRadius)]
    [Slider]
    [DrawTicks]
    public int BloomRadius { get; set; }

    [Range(1, 15)]
    [Increment(1)]
    [DefaultValue(DefaultOptions.BloomStrength)]
    [Slider]
    [DrawTicks]
    public int BloomStrength { get; set; }

    [DefaultValue(DefaultOptions.RenderOnlyLight)]
    public bool RenderOnlyLight { get; set; }

    // Ambient Occlusion
    [Header("AmbientOcclusion")]
    [Range(1, 5)]
    [Increment(1)]
    [DefaultValue(DefaultOptions.AmbientOcclusionRadius)]
    [Slider]
    [DrawTicks]
    public int AmbientOcclusionRadius { get; set; }

    [Range(20, 400)]
    [Increment(25)]
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
    [Header("LightingEngine")]
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

    [DefaultValue(DefaultOptions.FancyLightingEngineNonSolidOpaque)]
    public bool FancyLightingEngineNonSolidOpaque { get; set; }

    // Sky Color
    [Header("SkyColor")]
    [DefaultValue(DefaultOptions.UseCustomSkyColors)]
    public bool UseCustomSkyColors { get; set; }

    [DefaultValue(DefaultOptions.CustomSkyPreset)]
    [DrawTicks]
    public SkyColorPreset CustomSkyPreset { get; set; }
}
