using System.Runtime.CompilerServices;
using Microsoft.Xna.Framework.Graphics.PackedVector;

namespace FancyLighting.Utils;

internal static class ColorUtils
{
    private static bool _swapRedAndBlueRgba1010102 = false;

    internal static float _gamma;
    internal static float _reciprocalGamma;
    internal static float _lightGamma;
    internal static float _lightReciprocalGamma;

    internal static void Load()
    {
        var textureRgba1010102 = new Texture2D(
            Main.graphics.GraphicsDevice,
            1,
            1,
            false,
            SurfaceFormat.Rgba1010102
        );
        var targetColor = new RenderTarget2D(
            Main.graphics.GraphicsDevice,
            1,
            1,
            false,
            SurfaceFormat.Color,
            DepthFormat.None
        );

        textureRgba1010102.SetData([new Rgba1010102(1f, 0f, 0f, 0f)]);

        Main.graphics.GraphicsDevice.SetRenderTarget(targetColor);
        Main.spriteBatch.Begin();
        Main.spriteBatch.Draw(textureRgba1010102, Vector2.Zero, Color.White);
        Main.spriteBatch.End();
        Main.graphics.GraphicsDevice.SetRenderTarget(null);

        var colors = new Color[1];
        targetColor.GetData(colors);
        // I have found this to occur on Vulkan
        _swapRedAndBlueRgba1010102 = colors[0].B >= 128;

        textureRgba1010102.Dispose();
        targetColor.Dispose();
    }

    // Provide better conversions from Vector3 to Color than XNA
    // XNA uses (byte)(x * 255f) for each component

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Assign(ref Color color, float brightness, Vector3 rgb)
    {
        color.R = (byte)((255f * MathHelper.Clamp(brightness * rgb.X, 0f, 1f)) + 0.5f);
        color.G = (byte)((255f * MathHelper.Clamp(brightness * rgb.Y, 0f, 1f)) + 0.5f);
        color.B = (byte)((255f * MathHelper.Clamp(brightness * rgb.Z, 0f, 1f)) + 0.5f);
        color.A = byte.MaxValue;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Assign(ref Rgba1010102 color, float brightness, Vector3 rgb)
    {
        var red = (uint)((1023f * MathHelper.Clamp(brightness * rgb.X, 0f, 1f)) + 0.5f);
        var green = (uint)((1023f * MathHelper.Clamp(brightness * rgb.Y, 0f, 1f)) + 0.5f);
        var blue = (uint)((1023f * MathHelper.Clamp(brightness * rgb.Z, 0f, 1f)) + 0.5f);
        const uint Alpha = 0b11;

        color.PackedValue = _swapRedAndBlueRgba1010102
            ? blue | (green << 10) | (red << 20) | (Alpha << 30)
            : red | (green << 10) | (blue << 20) | (Alpha << 30);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Assign(ref HalfVector4 color, Vector3 rgb)
    {
        color = new(rgb.X, rgb.Y, rgb.Z, 1f);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float GammaToLinear(float x) => MathF.Pow(Math.Max(x, 0f), _gamma);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void GammaToLinear(ref float x) => x = GammaToLinear(x);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void GammaToLinear(ref Vector3 color)
    {
        GammaToLinear(ref color.X);
        GammaToLinear(ref color.Y);
        GammaToLinear(ref color.Z);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float LinearToGamma(float x) =>
        MathF.Pow(Math.Max(x, 0f), _reciprocalGamma);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void LinearToGamma(ref float x) => x = LinearToGamma(x);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void LinearToGamma(ref Vector3 color)
    {
        LinearToGamma(ref color.X);
        LinearToGamma(ref color.Y);
        LinearToGamma(ref color.Z);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float LightGammaToLinear(float x) =>
        MathF.Pow(Math.Max(x, 0f), _lightGamma);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void LightGammaToLinear(ref float x) => x = LightGammaToLinear(x);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void LightGammaToLinear(ref Vector3 color)
    {
        LightGammaToLinear(ref color.X);
        LightGammaToLinear(ref color.Y);
        LightGammaToLinear(ref color.Z);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float LightLinearToGamma(float x) =>
        MathF.Pow(Math.Max(x, 0f), _lightReciprocalGamma);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void LightLinearToGamma(ref float x) => x = LightLinearToGamma(x);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void LightLinearToGamma(ref Vector3 color)
    {
        LightLinearToGamma(ref color.X);
        LightLinearToGamma(ref color.Y);
        LightLinearToGamma(ref color.Z);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float Luma(Vector3 color) =>
        (0.2126f * color.X) + (0.7152f * color.Y) + (0.0722f * color.Z);

    public static float ApproximateLightAbsorption(Vector3 absorptionColor)
    {
        const float AbsorptionDepth = 8; // arbitrarily chosen

        LightGammaToLinear(ref absorptionColor);

        absorptionColor.X = MathF.Pow(absorptionColor.X, AbsorptionDepth);
        absorptionColor.Y = MathF.Pow(absorptionColor.Y, AbsorptionDepth);
        absorptionColor.Z = MathF.Pow(absorptionColor.Z, AbsorptionDepth);

        var accumulatedAbsorption = Luma(absorptionColor);
        return MathF.Pow(LightLinearToGamma(accumulatedAbsorption), 1 / AbsorptionDepth);
    }
}
