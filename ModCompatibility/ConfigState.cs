using FancyLighting.Config.Enums;

namespace FancyLighting.ModCompatibility;

internal static class ConfigState
{
    public enum ConfigStateIndex
    {
        Invalid = 0,
        SmoothLightingEnabled = 1,
        SmoothLightingRenderMode = 2,
        SmoothLightingGlowRenderMode = 3,
        SmoothLightingNormalMapsEnabled = 4,
        SmoothLightingNormalMapsNonSolidTilesEnabled = 5,
        SmoothLightingNormalMapsTileEntitiesEnabled = 6,
        AmbientOcclusionEnabled = 7,
        AmbientOcclusionNonSolidTilesEnabled = 8,
        AmbientOcclusionTileEntitiesEnabled = 9,
        FancyLightingEngineEnabled = 10,
        FancyLightingEngineQuality = 11,
        FancyLightingEngineTemporalOptimizationsEnabled = 12,
        FancyLightingEngineGlobalIlluminationEnabled = 13,
        FancyAtmosphereEnabled = 14,
        FancySkyLightColorsEnabled = 15,
        FancySkyLightShadingEnabled = 16,
        SmoothLightingActiveLightMapOverride = 17,
        AmbientOcclusionActiveRenderState = 18,
    }

    public const int InvalidValue = -1;

    public enum SmoothLightingRenderMode
    {
        Disabled = 0,
        Bilinear = 1,
        Bicubic = 2,
        BasicHDR = 3,
        FullHDR = 4,
    }

    public enum SmoothLightingGlowRenderMode
    {
        Disabled = 0,
        Fast = 1,
        Accurate = 2,
    }

    public enum FancyLightingEngineQuality
    {
        Disabled = 0,
        Low = 1,
        Medium = 2,
        High = 3,
    }

    public enum SmoothLightingLightMapOverride
    {
        None = 0,
        FullBright = 1,
        NearBlack = 2,
    }

    public enum AmbientOcclusionRenderState
    {
        Inactive = 0,
        SolidTiles = 1,
        NonSolidTiles = 2,
        TileEntities = 3,
    }

    public static int[] ConfigStateInfo { get; private set; }

    public static (string Name, string Type)[] GetConfigStateInfoMetadata() =>
        [
            ("Invalid", "void"),
            ("SmoothLightingEnabled", "bool"),
            ("SmoothLightingRenderMode", "SmoothLightingRenderMode"),
            ("SmoothLightingGlowRenderMode", "SmoothLightingGlowRenderMode"),
            ("SmoothLightingNormalMapsEnabled", "bool"),
            ("SmoothLightingNormalMapsNonSolidTilesEnabled", "bool"),
            ("SmoothLightingNormalMapsTileEntitiesEnabled", "bool"),
            ("AmbientOcclusionEnabled", "bool"),
            ("AmbientOcclusionNonSolidTilesEnabled", "bool"),
            ("AmbientOcclusionTileEntitiesEnabled", "bool"),
            ("FancyLightingEngineEnabled", "bool"),
            ("FancyLightingEngineQuality", "FancyLightingEngineQuality"),
            ("FancyLightingEngineTemporalOptimizationsEnabled", "bool"),
            ("FancyLightingEngineGlobalIlluminationEnabled", "bool"),
            ("FancyAtmosphereEnabled", "bool"),
            ("FancySkyLightColorsEnabled", "bool"),
            ("FancySkyLightShadingEnabled", "bool"),
            ("SmoothLightingActiveLightMapOverride", "SmoothLightingLightMapOverride"),
            ("AmbientOcclusionActiveRenderState", "AmbientOcclusionRenderState"),
        ];

    public static Dictionary<string, string[]> GetConfigStateTypeMetadata() =>
        new()
        {
            {
                "SmoothLightingRenderMode",
                ["Disabled", "Bilinear", "Bicubic", "BasicHDR", "FullHDR"]
            },
            { "SmoothLightingGlowRenderMode", ["Disabled", "Fast", "Accurate"] },
            { "FancyLightingEngineQuality", ["Disabled", "Low", "Medium", "High"] },
            { "SmoothLightingLightMapOverride", ["None", "FullBright", "NearBlack"] },
            {
                "AmbientOcclusionRenderState",
                ["Inactive", "SolidTiles", "NonSolidTiles", "TileEntities"]
            },
        };

    internal static void Load()
    {
        ConfigStateInfo = new int[19];
        Array.Fill(ConfigStateInfo, InvalidValue);
    }

    internal static void Unload()
    {
        ConfigStateInfo = null;
    }

    internal static void SetConfigState(ConfigStateIndex index, int value)
    {
        if (
            ConfigStateInfo is null
            || (int)index < 0
            || (int)index >= ConfigStateInfo.Length
        )
        {
            return;
        }

        ConfigStateInfo[(int)index] = value;
    }

    internal static void SetConfigState(ConfigStateIndex index, bool value) =>
        SetConfigState(index, Convert.ToInt32(value));

    internal static void Update()
    {
        SetConfigState(ConfigStateIndex.Invalid, InvalidValue);

        if (LightingConfig.Instance is null || CompatibilityConfig.Instance is null)
        {
            Array.Fill(ConfigStateInfo, 0);
            return;
        }

        var config = LightingConfig.Instance;
        var compatConfig = CompatibilityConfig.Instance;

        var smoothLighting = config.SmoothLightingEnabled();
        var ambientOcclusion = config.AmbientOcclusionEnabled();
        var fancyLightingEngine = config.FancyLightingEngineEnabled();
        var fancyAtmosphere = config.FancySkyLightingEnabled();
        var fancySkyLightColors = config.FancySkyColorsEnabled();
        var fancySkyLightShading = config.FancySkyLightingEnabled();

        SetConfigState(ConfigStateIndex.SmoothLightingEnabled, smoothLighting);
        SetConfigState(
            ConfigStateIndex.SmoothLightingRenderMode,
            (int)(
                smoothLighting
                    ? config.LightMapRenderMode switch
                    {
                        RenderMode.Bilinear => SmoothLightingRenderMode.Bilinear,
                        RenderMode.Bicubic => SmoothLightingRenderMode.Bicubic,
                        RenderMode.BicubicOverbright => SmoothLightingRenderMode.BasicHDR,
                        RenderMode.EnhancedHdr => SmoothLightingRenderMode.FullHDR,
                        _ => (SmoothLightingRenderMode)InvalidValue,
                    }
                    : SmoothLightingRenderMode.Disabled
            )
        );
        SetConfigState(
            ConfigStateIndex.SmoothLightingGlowRenderMode,
            (int)(
                smoothLighting
                    ? compatConfig.UseAccurateGlowRendering
                        ? SmoothLightingGlowRenderMode.Accurate
                        : SmoothLightingGlowRenderMode.Fast
                    : SmoothLightingGlowRenderMode.Disabled
            )
        );
        SetConfigState(
            ConfigStateIndex.SmoothLightingNormalMapsEnabled,
            smoothLighting && config.SimulateNormalMaps
        );
        SetConfigState(
            ConfigStateIndex.SmoothLightingNormalMapsNonSolidTilesEnabled,
            smoothLighting && config.SimulateNormalMaps && config.SimulateNonSolidNormals
        );
        SetConfigState(
            ConfigStateIndex.SmoothLightingNormalMapsTileEntitiesEnabled,
            smoothLighting
                && config.SimulateNormalMaps
                && config.SimulateTileEntityNormals
        );
        SetConfigState(ConfigStateIndex.AmbientOcclusionEnabled, ambientOcclusion);
        SetConfigState(
            ConfigStateIndex.AmbientOcclusionNonSolidTilesEnabled,
            ambientOcclusion && config.DoNonSolidAmbientOcclusion
        );
        SetConfigState(
            ConfigStateIndex.AmbientOcclusionTileEntitiesEnabled,
            ambientOcclusion && config.DoTileEntityAmbientOcclusion
        );
        SetConfigState(ConfigStateIndex.FancyLightingEngineEnabled, fancyLightingEngine);
        SetConfigState(
            ConfigStateIndex.FancyLightingEngineQuality,
            (int)(
                fancyLightingEngine
                    ? config.FancyLightingEngineMode switch
                    {
                        LightingEngineMode.Low => FancyLightingEngineQuality.Low,
                        LightingEngineMode.Medium => FancyLightingEngineQuality.Medium,
                        LightingEngineMode.High => FancyLightingEngineQuality.High,
                        _ => (FancyLightingEngineQuality)InvalidValue,
                    }
                    : FancyLightingEngineQuality.Disabled
            )
        );
        SetConfigState(
            ConfigStateIndex.FancyLightingEngineTemporalOptimizationsEnabled,
            fancyLightingEngine && config.FancyLightingEngineUseTemporal
        );
        SetConfigState(
            ConfigStateIndex.FancyLightingEngineGlobalIlluminationEnabled,
            fancyLightingEngine && config.SimulateGlobalIllumination
        );
        SetConfigState(ConfigStateIndex.FancyAtmosphereEnabled, fancyAtmosphere);
        SetConfigState(ConfigStateIndex.FancySkyLightColorsEnabled, fancySkyLightColors);
        SetConfigState(
            ConfigStateIndex.FancySkyLightShadingEnabled,
            fancySkyLightShading
        );
        SetConfigState(
            ConfigStateIndex.SmoothLightingActiveLightMapOverride,
            (int)SmoothLightingLightMapOverride.None
        );
        SetConfigState(
            ConfigStateIndex.AmbientOcclusionActiveRenderState,
            (int)AmbientOcclusionRenderState.Inactive
        );
    }
}
