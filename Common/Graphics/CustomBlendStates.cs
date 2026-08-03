namespace FancyLighting.Common.Graphics;

internal static class CustomBlendStates
{
    public static BlendState MultiplyColor { get; private set; } =
        new()
        {
            ColorBlendFunction = BlendFunction.Add,
            ColorDestinationBlend = Blend.SourceColor,
            ColorSourceBlend = Blend.Zero,
            AlphaBlendFunction = BlendFunction.Add,
            AlphaDestinationBlend = Blend.One,
            AlphaSourceBlend = Blend.Zero,
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
    public static BlendState MultiplyAlpha { get; private set; } =
        new()
        {
            ColorBlendFunction = BlendFunction.Add,
            ColorDestinationBlend = Blend.Zero,
            ColorSourceBlend = Blend.Zero,
            AlphaBlendFunction = BlendFunction.Add,
            AlphaDestinationBlend = Blend.SourceAlpha,
            AlphaSourceBlend = Blend.Zero,
        };
    public static BlendState TrueAdditive { get; private set; } =
        new()
        {
            ColorBlendFunction = BlendFunction.Add,
            AlphaBlendFunction = BlendFunction.Add,
            ColorSourceBlend = Blend.One,
            ColorDestinationBlend = Blend.One,
            AlphaSourceBlend = Blend.One,
            AlphaDestinationBlend = Blend.One,
        };

    internal static void Unload()
    {
        MultiplyColor = null;
        MultiplyColorByAlpha = null;
        MultiplyAlpha = null;
        TrueAdditive = null;
    }
}
