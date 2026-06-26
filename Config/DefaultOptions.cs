using FancyLighting.Config.Enums;

namespace FancyLighting.Config;

public static class DefaultOptions
{
    public const SettingsPreset QualityPreset = SettingsPreset.MediumPreset;

    // Compatibility Settings
    public const bool DisableHdrDuringBossFights = false;
    public const bool UseHdrCompatibilityFixes = false;
    public const bool DisableFrameTimingOptimizations = false;

    // Developer Settings
    public const bool MonitorPerformance = false;
    public const bool RenderOnlyLight = false;
    public const bool ShowFancySkyColorGradients = false;

    // General Preferences
    public const int ThreadCount = -1; // Used for the DefaultValue attribute in PreferencesConfig
    public const int MinThreadCount = 1;
    public const int MaxThreadCount = 32;
    public const int MaxDefaultThreadCount = 16;
    public static int RuntimeDefaultThreadCount =>
        Math.Clamp(Environment.ProcessorCount, MinThreadCount, MaxDefaultThreadCount);

    // Color Management Preferences
    public const int Gamma = 220;
    public const bool UseSrgb = false;

    // Smooth Lighting
    public const bool UseSmoothLighting = true;
    public const RenderMode LightMapRenderMode = RenderMode.Bicubic;
    public const bool UseLightMapBlurring = true;
    public const bool UseEnhancedBlurring = true;
    public const bool SimulateNormalMaps = false;
    public const bool SimulateNonSolidNormals = false;
    public const bool SimulateTileEntityNormals = false;
    public const bool UseEnhancedGlowMaskSupport = false;
    public const bool UseTileEntitySmoothLighting = false;

    // Smooth Lighting Preferences
    public const int NormalMapsIntensity = 7;
    public const bool FineNormalMaps = false;
    public const bool UseGrayscaleLighting = false;

    // Full HDR Rendering Preferences
    public const ToneMappingPreset ToneMappingOperator = ToneMappingPreset.NeutralLms;
    public const int Exposure = 100;
    public const int VibranceBoost = 2;
    public const bool HdrBloom = true;
    public const int BloomRadius = 5;
    public const int BloomStrength = 6;

    // Ambient Occlusion
    public const bool UseAmbientOcclusion = true;
    public const bool DoNonSolidAmbientOcclusion = true;
    public const bool DoTileEntityAmbientOcclusion = false;

    // Ambient Occlusion Preferences
    public const int AmbientOcclusionRadius = 4;
    public const int AmbientOcclusionIntensity = 200;
    public const int AmbientLightProportion = 75;

    // Fancy Lighting Engine
    public const bool UseFancyLightingEngine = true;
    public const LightingEngineMode FancyLightingEngineMode = LightingEngineMode.Low;
    public const bool FancyLightingEngineUseTemporal = true;
    public const bool SimulateGlobalIllumination = true;

    // Fancy Lighting Engine Preferences
    public const int FancyLightingEngineLightLoss = 50;
    public const int FancyLightingEngineLightAbsorption = 100;
    public const int FancyLightingEngineGlobalIlluminationMult = 50;
    public const bool FancyLightingEngineNonSolidOpaque = false;

    // Fancy Sky
    public const bool UseFancySkyRendering = true;
    public const bool UseFancySkyColors = true;
    public const bool UseFancySkyLighting = true;

    // Fancy Sky Preferences
    public const int SkyBrightnessBoost = 5;
    public const SkyColorPreset FancySkyColorsPreset = SkyColorPreset.Preset1;
    public const bool DepthOfField = false;
    public const int DepthOfFieldRadius = 2;
}
