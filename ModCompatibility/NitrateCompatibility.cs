using System.Reflection;
using MonoMod.RuntimeDetour;

namespace FancyLighting.ModCompatibility;

internal static class NitrateCompatibility
{
    private static Hook _hook_OnModLoad;

    internal static void Load()
    {
        if (!ModLoader.HasMod("Nitrate"))
        {
            return;
        }

        var modClass = ModLoader.GetMod("Nitrate").GetType();
        var modAssembly = modClass.Assembly;

        Type chunkSystemClass;
        try
        {
            chunkSystemClass = modAssembly.GetType(
                "TeamCatalyst.Nitrate.Optimizations.Tiles.ChunkSystem"
            );
        }
        catch (Exception)
        {
            // Unable to load class
            return;
        }

        if (chunkSystemClass is null)
        {
            return;
        }

        var detourMethod = chunkSystemClass.GetMethod(
            "OnModLoad",
            BindingFlags.Public | BindingFlags.Instance
        );
        if (detourMethod is not null)
        {
            try
            {
                _hook_OnModLoad = new(detourMethod, _OnModLoad, true);
            }
            catch (Exception)
            {
                // Unable to add the hook
            }
        }
    }

    internal static void Unload()
    {
        _hook_OnModLoad?.Dispose();
    }

    private delegate void orig_OnModLoad(ModSystem self);

    private static void _OnModLoad(orig_OnModLoad orig, ModSystem self)
    {
        // Prevent experimental tile renderer from loading
        // Needed to prevent cave backgrounds from glitching
    }
}
