using FancyLighting.Config.Enums;

namespace FancyLighting.Config;

public static class DefaultOptions
{
    public const SettingsPreset QualityPreset = SettingsPreset.LowPreset;

    // General Preferences
    public const int ThreadCount = -1; // Used for the DefaultValue attribute in PreferencesConfig
    public const int MinThreadCount = 1;
    public const int MaxThreadCount = 32;
    public const int MaxDefaultThreadCount = 16;
    public static int RuntimeDefaultThreadCount =>
        Math.Clamp(Environment.ProcessorCount, MinThreadCount, MaxDefaultThreadCount);

    // Tone Mapping Preferences
    public const int Gamma = 220;
    public const bool UseSrgb = false;

    // Smooth Lighting
    public const bool UseSmoothLighting = true;
    public const bool UseLightMapBlurring = true;
    public const bool UseEnhancedBlurring = false;
    public const bool SimulateNormalMaps = false;
    public const bool UseEnhancedGlowMaskSupport = false;
    public const RenderMode LightMapRenderMode = RenderMode.Bilinear;
    public const bool HdrBloom = true;

    // Smooth Lighting Preferences
    public const int NormalMapsIntensity = 4;
    public const bool FineNormalMaps = false;
    public const int Exposure = 100;
    public const int BloomRadius = 5;
    public const int BloomStrength = 6;
    public const bool UseHdrCompatibilityFixes = false;
    public const bool DisableHdrDuringBossFights = false;
    public const bool UseGrayscaleLighting = false;
    public const bool RenderOnlyLight = false;

    // Ambient Occlusion
    public const bool UseAmbientOcclusion = true;
    public const bool DoNonSolidAmbientOcclusion = true;
    public const bool DoTileEntityAmbientOcclusion = false;

    // Ambient Occlusion Preferences
    public const int AmbientOcclusionRadius = 4;
    public const int AmbientOcclusionIntensity = 100;
    public const int AmbientLightProportion = 70;

    // Fancy Lighting Engine
    public const bool UseFancyLightingEngine = true;
    public const bool FancyLightingEngineUseTemporal = true;
    public const LightingEngineMode FancyLightingEngineMode = LightingEngineMode.Low;
    public const bool SimulateGlobalIllumination = false;

    // Fancy Lighting Engine Preferences
    public const int FancyLightingEngineLightLoss = 50;
    public const int FancyLightingEngineLightAbsorption = 100;
    public const int FancyLightingEngineGlobalIlluminationMult = 50;
    public const bool FancyLightingEngineNonSolidOpaque = false;

    // Fancy Sky
    public const bool UseFancySkyColors = true;

    // Fancy Sky Preferences
    public const SkyColorPreset FancySkyColorsPreset = SkyColorPreset.Preset1;
}
