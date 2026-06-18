using Terraria.ModLoader.Config;

namespace FancyLighting.Config;

public sealed class CompatibilityConfig : ModConfig
{
    public override ConfigScope Mode => ConfigScope.ClientSide;

    // Handled automatically by tModLoader
    public static CompatibilityConfig Instance;
}
