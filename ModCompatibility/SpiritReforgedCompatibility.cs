namespace FancyLighting.ModCompatibility;

internal static class SpiritReforgedCompatibility
{
    internal static bool _disableCustomLiquidRendering = false;

    internal static void Load()
    {
        if (!ModLoader.HasMod("SpiritReforged"))
        {
            return;
        }

        _disableCustomLiquidRendering = true;
    }

    internal static void Unload()
    {
        _disableCustomLiquidRendering = false;
    }
}
