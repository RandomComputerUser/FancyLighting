namespace FancyLighting.Utils;

internal static class SpriteBatchExtensions
{
    public static SpriteBatchParameters GetParameters(this SpriteBatch spriteBatch) =>
        new(spriteBatch);

    public static void Begin(
        this SpriteBatch spriteBatch,
        SpriteBatchParameters parameters
    ) => parameters.BeginSpriteBatch(spriteBatch);
}
