namespace FancyLighting.ModCompatibility;

internal static class SpiritReforgedCompatibility
{
    internal static bool _useSpiritReforgedFixes = false;

    internal static void Load()
    {
        if (!ModLoader.HasMod("SpiritReforged"))
        {
            return;
        }

        _useSpiritReforgedFixes = true;
    }

    internal static void Unload()
    {
        _useSpiritReforgedFixes = false;
    }
}
