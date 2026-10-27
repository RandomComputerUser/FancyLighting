using System.ComponentModel;
using Terraria.ModLoader.Config;

namespace FancyLighting.Config.Configs;

public sealed class CompatibilityConfig : ModConfig
{
    public override ConfigScope Mode => ConfigScope.ClientSide;

    // Handled automatically by tModLoader
    public static CompatibilityConfig Instance;

    public bool RenderingOptimizationsDisabled() =>
        _forceDisableRenderingOptimizations || DisableRenderingOptimizations;

    public bool MainMenuCaptureDisabled() =>
        _forceDisableMainMenuCapture || DisableMainMenuCapture;

    public bool SmoothLightingDuringBossFightsDisabled() =>
        _forceDisableSmoothLightingDuringBossFights
        || DisableSmoothLightingDuringBossFights;

    public bool AccurateGlowRenderingEnabled() =>
        _forceUseAccurateGlowRendering || UseAccurateGlowRendering;

    public bool HdrEnhancedAlphaBlendingDisabled() =>
        _forceDisableHdrEnhancedAlphaBlending || DisableHdrEnhancedAlphaBlending;

    public bool HdrLightingSyncDisabled() =>
        _forceDisableHdrLightingSync || DisableHdrLightingSync;

    public bool FrameTimingOptimizationsDisabled() =>
        _forceDisableFrameTimingOptimizations || DisableFrameTimingOptimizations;

    public bool SunAndMoonShadersDisabled() =>
        _forceDisableSunAndMoonShaders || DisableSunAndMoonShaders;

    private bool _forceDisableRenderingOptimizations;
    private bool _forceDisableMainMenuCapture;
    private bool _forceDisableSmoothLightingDuringBossFights;
    private bool _forceUseAccurateGlowRendering;
    private bool _forceDisableHdrEnhancedAlphaBlending;
    private bool _forceDisableHdrLightingSync;
    private bool _forceDisableFrameTimingOptimizations;
    private bool _forceDisableSunAndMoonShaders;

    /// <summary>
    /// Force a particular compatibility setting to be enabled.
    /// </summary>
    /// <param name="settingName">The name of the setting to force enable.</param>
    /// <returns>Whether <paramref name="settingName" /> is a valid setting and was thus force enabled.</returns>
    /// <remarks>This should be called during `Load()`, or else there may not be an effect.</remarks>
    public bool ForceEnableSetting(string settingName)
    {
        switch (settingName)
        {
            case "DisableRenderingOptimizations":
                _forceDisableRenderingOptimizations = true;
                return true;
            case "DisableMainMenuCapture":
                _forceDisableMainMenuCapture = true;
                return true;
            case "DisableSmoothLightingDuringBossFights":
                _forceDisableSmoothLightingDuringBossFights = true;
                return true;
            case "UseAccurateGlowRendering":
                _forceUseAccurateGlowRendering = true;
                return true;
            case "DisableHdrEnhancedAlphaBlending":
                _forceDisableHdrEnhancedAlphaBlending = true;
                return true;
            case "DisableHdrLightingSync":
                _forceDisableHdrLightingSync = true;
                return true;
            case "DisableFrameTimingOptimizations":
                _forceDisableFrameTimingOptimizations = true;
                return true;
            case "DisableSunAndMoonShaders":
                _forceDisableSunAndMoonShaders = true;
                return true;
            default:
                return false;
        }
    }

    public override void OnChanged()
    {
        ModContent.GetInstance<SettingsSystem>()?.OnConfigChange();
    }

    [Header("General")]
    [DefaultValue(DefaultOptions.DisableRenderingOptimizations)]
    public bool DisableRenderingOptimizations { get; set; }

    [DefaultValue(DefaultOptions.DisableMainMenuCapture)]
    [ReloadRequired]
    public bool DisableMainMenuCapture { get; set; }

    [Header("SmoothLighting")]
    [DefaultValue(DefaultOptions.DisableSmoothLightingDuringBossFights)]
    public bool DisableSmoothLightingDuringBossFights { get; set; }

    [DefaultValue(DefaultOptions.UseAccurateGlowRendering)]
    public bool UseAccurateGlowRendering { get; set; }

    [Header("FullHdrRendering")]
    [DefaultValue(DefaultOptions.DisableHdrEnhancedAlphaBlending)]
    public bool DisableHdrEnhancedAlphaBlending { get; set; }

    [DefaultValue(DefaultOptions.DisableHdrLightingSync)]
    public bool DisableHdrLightingSync { get; set; }

    [Header("FancyLightingEngine")]
    [DefaultValue(DefaultOptions.DisableFrameTimingOptimizations)]
    public bool DisableFrameTimingOptimizations { get; set; }

    [Header("FancySky")]
    [DefaultValue(DefaultOptions.DisableSunAndMoonShaders)]
    public bool DisableSunAndMoonShaders { get; set; }
}
