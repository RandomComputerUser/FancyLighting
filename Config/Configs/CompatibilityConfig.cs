using System.ComponentModel;
using Terraria.ModLoader.Config;

namespace FancyLighting.Config.Configs;

public sealed class CompatibilityConfig : ModConfig
{
    public override ConfigScope Mode => ConfigScope.ClientSide;

    // Handled automatically by tModLoader
    public static CompatibilityConfig Instance;

    public override void OnChanged()
    {
        ModContent.GetInstance<SettingsSystem>()?.OnConfigChange();
    }

    [Header("General")]
    [DefaultValue(DefaultOptions.DisableRenderingOptimizations)]
    public bool DisableRenderingOptimizations { get; set; }

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
}
