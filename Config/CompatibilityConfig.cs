using System.ComponentModel;
using Terraria.ModLoader.Config;

namespace FancyLighting.Config;

public sealed class CompatibilityConfig : ModConfig
{
    public override ConfigScope Mode => ConfigScope.ClientSide;

    // Handled automatically by tModLoader
    public static CompatibilityConfig Instance;

    [DefaultValue(DefaultOptions.GlowEffectCompatibilityFixes)]
    public bool GlowEffectCompatibilityFixes { get; set; }
}
