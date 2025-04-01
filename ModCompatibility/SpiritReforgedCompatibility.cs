using System.Reflection;
using MonoMod.RuntimeDetour;
using Terraria.Graphics;

namespace FancyLighting.ModCompatibility;

internal static class SpiritReforgedCompatibility
{
    private static Hook _hook_ModifyColor;

    internal static void Load()
    {
        if (!ModLoader.HasMod("SpiritReforged"))
        {
            return;
        }

        var modClass = ModLoader.GetMod("SpiritReforged").GetType();
        var modAssembly = modClass.Assembly;

        Type waterAlphaClass;
        try
        {
            waterAlphaClass = modAssembly.GetType(
                "SpiritReforged.Common.Visuals.WaterAlpha"
            );
        }
        catch (Exception)
        {
            // Unable to load class
            return;
        }

        if (waterAlphaClass is null)
        {
            return;
        }

        var detourMethod = waterAlphaClass.GetMethod(
            "ModifyColors",
            BindingFlags.NonPublic | BindingFlags.Static
        );
        if (detourMethod is not null)
        {
            try
            {
                _hook_ModifyColor = new(detourMethod, _ModifyColors, true);
            }
            catch (Exception)
            {
                // Unable to add the hook
            }
        }
    }

    internal static void Unload()
    {
        _hook_ModifyColor?.Dispose();
    }

    private delegate void orig_ModifyColors(
        int x,
        int y,
        ref VertexColors colors,
        bool isPartial
    );

    private static void _ModifyColors(
        orig_ModifyColors orig,
        int x,
        int y,
        ref VertexColors colors,
        bool isPartial
    )
    {
        if (LightingConfig.Instance.SmoothLightingEnabled())
        {
            return;
        }

        orig(x, y, ref colors, isPartial);
    }
}
