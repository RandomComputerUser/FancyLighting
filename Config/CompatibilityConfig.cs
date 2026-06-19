using System.ComponentModel;
using Terraria.ModLoader.Config;

namespace FancyLighting.Config;

public sealed class CompatibilityConfig : ModConfig
{
    public override ConfigScope Mode => ConfigScope.ClientSide;

    // Handled automatically by tModLoader
    public static CompatibilityConfig Instance;

    public override void OnChanged()
    {
        ModContent.GetInstance<SettingsSystem>()?.OnConfigChange();
    }

    [Header("SmoothLighting")]
    [DefaultValue(DefaultOptions.DisableHdrDuringBossFights)]
    public bool DisableHdrDuringBossFights { get; set; }

    [Header("FullHdrRendering")]
    [DefaultValue(DefaultOptions.UseHdrCompatibilityFixes)]
    public bool UseHdrCompatibilityFixes { get; set; }

    [Header("FancyLightingEngine")]
    [DefaultValue(DefaultOptions.DisableFrameTimingOptimizations)]
    public bool DisableFrameTimingOptimizations { get; set; }
}
