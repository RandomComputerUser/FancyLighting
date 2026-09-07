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
    public static BlendState MaxColor { get; private set; } =
        new()
        {
            ColorBlendFunction = BlendFunction.Max,
            ColorDestinationBlend = Blend.One,
            ColorSourceBlend = Blend.One,
            AlphaBlendFunction = BlendFunction.Add,
            AlphaDestinationBlend = Blend.Zero,
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
        MaxColor = null;
        TrueAdditive = null;
    }
}
