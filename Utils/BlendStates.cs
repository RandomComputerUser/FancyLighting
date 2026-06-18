namespace FancyLighting.Utils;

internal static class BlendStates
{
    public static BlendState Multiply { get; private set; } =
        new()
        {
            ColorBlendFunction = BlendFunction.Add,
            ColorDestinationBlend = Blend.SourceColor,
            ColorSourceBlend = Blend.Zero,
            AlphaBlendFunction = BlendFunction.Add,
            AlphaDestinationBlend = Blend.SourceAlpha,
            AlphaSourceBlend = Blend.Zero,
        };
    public static BlendState MultiplyColor { get; private set; } =
        new()
        {
            ColorBlendFunction = BlendFunction.Add,
            ColorDestinationBlend = Blend.SourceColor,
            ColorSourceBlend = Blend.Zero,
            AlphaBlendFunction = BlendFunction.Add,
            AlphaDestinationBlend = Blend.Zero,
            AlphaSourceBlend = Blend.One,
        };
    public static BlendState MultiplyColorByAlpha { get; private set; } =
        new()
        {
            ColorBlendFunction = BlendFunction.Add,
            ColorDestinationBlend = Blend.SourceAlpha,
            ColorSourceBlend = Blend.Zero,
            AlphaBlendFunction = BlendFunction.Add,
            AlphaDestinationBlend = Blend.One,
            AlphaSourceBlend = Blend.Zero,
        };
}
