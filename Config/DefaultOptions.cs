using System;

namespace FancyLighting.Config;

public static class DefaultOptions
{
    public const Preset QualityPreset = Preset.LowPreset;

    // General
    public const bool UseHiDefFeatures = false;

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
    public const bool UseLightMapToneMapping = false;
    public const bool SimulateNormalMaps = false;
    public const GlowMaskMode GlowMaskSupport = GlowMaskMode.Basic;
    public const RenderMode LightMapRenderMode = RenderMode.Bilinear;
    public const bool OverbrightWaterfalls = false;
    public const bool OverbrightNPCsAndPlayer = false;
    public const bool OverbrightProjectiles = false;
    public const bool OverbrightDustAndGore = false;
    public const bool OverbrightItems = false;
    public const bool OverbrightRain = false;

    // Smooth Lighting Preferences
    public const int NormalMapsStrength = 150;
    public const bool FineNormalMaps = false;
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
    public const int FancyLightingEngineGlobalIlluminationMult = 40;
    public const bool FancyLightingEngineVinesOpaque = false;

    // Fancy Sky Colors
    public const bool UseCustomSkyColors = true;
    public const SkyColorPreset CustomSkyPreset = SkyColorPreset.Profile1;
}
