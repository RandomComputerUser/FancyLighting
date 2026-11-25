using System.Reflection;

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

        var modClass = ModLoader.GetMod("SpiritReforged").GetType();
        var modAssembly = modClass.Assembly;

        Type saltSkyClass;
        try
        {
            saltSkyClass = modAssembly.GetType(
                "SpiritReforged.Content.SaltFlats.Biome.SaltSky"
            );
        }
        catch (Exception)
        {
            // Unable to load class
            return;
        }

        if (saltSkyClass is null)
        {
            return;
        }

        var detourMethod = saltSkyClass.GetMethod(
            "DrawBelowSunMoon",
            BindingFlags.Public | BindingFlags.Instance
        );
        if (detourMethod is not null)
        {
            try
            {
                MonoModHooks.Add(detourMethod, _DrawBelowSunMoon);
            }
            catch (Exception)
            {
                // Unable to add the hook
            }
        }
    }

    public delegate void orig_DrawBelowSunMoon(object self, SpriteBatch spriteBatch);

    private static void _DrawBelowSunMoon(
        orig_DrawBelowSunMoon orig,
        object self,
        SpriteBatch spriteBatch
    )
    {
        if (LightingConfig.Instance?.FancySkyRenderingEnabled() is true)
        {
            return;
        }

        orig(self, spriteBatch);
    }
}
