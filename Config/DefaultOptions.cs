using System;
using FancyLighting.Config.Enums;

namespace FancyLighting.Config;

public static class DefaultOptions
{
    public const Preset QualityPreset = Preset.LowPreset;

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

    // Smooth Lighting Preferences
    public const int NormalMapsIntensity = 5;
    public const bool FineNormalMaps = false;
    public const int BloomRadius = 4;
    public const int BloomStrength = 5;
    public const bool RenderOnlyLight = false;

    // Ambient Occlusion
    public const bool UseAmbientOcclusion = true;
    public const bool DoNonSolidAmbientOcclusion = true;
    public const bool DoTileEntityAmbientOcclusion = false;

    // Ambient Occlusion Preferences
    public const int AmbientOcclusionRadius = 2;
    public const int AmbientOcclusionIntensity = 200;
    public const int AmbientLightProportion = 50;

    // Fancy Lighting Engine
    public const bool UseFancyLightingEngine = true;
    public const bool FancyLightingEngineUseTemporal = true;
    public const LightingEngineMode FancyLightingEngineMode = LightingEngineMode.One;
    public const bool SimulateGlobalIllumination = false;

    // Fancy Lighting Engine Preferences
    public const int FancyLightingEngineLightLoss = 50;
    public const int FancyLightingEngineLightAbsorption = 100;
    public const int FancyLightingEngineGlobalIlluminationMult = 50;
    public const bool FancyLightingEngineNonSolidOpaque = false;

    // Fancy Sky Colors
    public const bool UseCustomSkyColors = true;
    public const SkyColorPreset CustomSkyPreset = SkyColorPreset.Profile4;
}
