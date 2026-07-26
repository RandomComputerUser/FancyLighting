using ReLogic.Content;

namespace FancyLighting.Common.Graphics;

internal static class EffectLoader
{
    public static Effect Load(string effectName) =>
        ModContent
            .Request<Effect>(
                $"FancyLighting/Effects/{effectName}",
                AssetRequestMode.ImmediateLoad
            )
            .Value;
}
