namespace FancyLighting.ModCompatibility;

internal static class SpiritReforgedCompatibility
{
    internal static bool _useFixes = false;

    internal static void Load()
    {
        if (!ModLoader.HasMod("SpiritReforged"))
        {
            return;
        }

        _useFixes = true;
    }
}
