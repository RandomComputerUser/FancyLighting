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
                && args[2] is Action<Texture2D, Matrix, Rectangle, bool> hook
            )
            {
                var handler = new SmoothLighting.LightMapUpdateHandler(hook);
                SmoothLighting.PostUpdateLightMap += handler;
                return () => SmoothLighting.PostUpdateLightMap -= handler;
            }
        }

        {
            if (
                args.Length == 3
                && args[0] is "AddHook"
                && args[1] is "PreDrawSky"
                && args[2]
                    is Func<Vector3, Vector3, Vector3, (Vector3, Vector3, Vector3)> hook
            )
            {
                var handler = new FancySkyRendering.SkyColorModifier(
                    (
                        ref Vector3 highSkyColor,
                        ref Vector3 lowSkyColor,
                        ref Vector3 skyColorMult
                    ) =>
                    {
                        (highSkyColor, lowSkyColor, skyColorMult) = hook(
                            highSkyColor,
                            lowSkyColor,
                            skyColorMult
                        );
                    }
                );
                FancySkyRendering.PreDrawSky += handler;
                return () => FancySkyRendering.PreDrawSky -= handler;
            }
        }

        return null;
    }
}
