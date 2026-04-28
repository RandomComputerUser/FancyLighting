namespace FancyLighting.ModCompatibility;

internal static class ModCalls
{
    public static object Call(object[] args)
    {
        if (args is null || args.Length == 0)
        {
            return null;
        }

        {
            if (
                args.Length == 3
                && args[0] is "AddHook"
                && args[1] is "PostUpdateLightMap"
                && args[2].IsDelegate(out SmoothLighting.LightMapUpdateHandler hook)
            )
            {
                SmoothLighting.PostUpdateLightMap += hook;
                return () => SmoothLighting.PostUpdateLightMap -= hook;
            }
        }

        {
            if (
                args.Length == 3
                && args[0] is "AddHook"
                && args[1] is "PreDrawSky"
                && args[2].IsDelegate(out FancySkyRendering.SkyColorModifier hook)
            )
            {
                FancySkyRendering.PreDrawSky += hook;
                return () => FancySkyRendering.PreDrawSky -= hook;
            }
        }

        {
            if (
                args.Length == 3
                && args[0] is "AddCustomTileLighting"
                && args[1] is int tileType
                && args[2]
                    .IsDelegate(out SmoothLighting.TileLightModifier tileLightModifier)
            )
            {
                return SmoothLighting.SetCustomTileLighting(tileType, tileLightModifier);
            }
        }

        {
            if (
                args.Length == 3
                && args[0] is "AddCustomTileLighting"
                && args[1] is ushort tileType
                && args[2]
                    .IsDelegate(out SmoothLighting.TileLightModifier tileLightModifier)
            )
            {
                return SmoothLighting.SetCustomTileLighting(tileType, tileLightModifier);
            }
        }

        {
            if (
                args.Length == 2
                && args[0] is "RemoveCustomTileLighting"
                && args[1] is int tileType
            )
            {
                return SmoothLighting.SetCustomTileLighting(tileType, null);
            }
        }

        {
            if (
                args.Length == 2
                && args[0] is "RemoveCustomTileLighting"
                && args[1] is ushort tileType
            )
            {
                return SmoothLighting.SetCustomTileLighting(tileType, null);
            }
        }

        return null;
    }
}
