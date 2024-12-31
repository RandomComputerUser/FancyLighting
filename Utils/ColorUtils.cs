using System;
using System.Runtime.CompilerServices;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics.PackedVector;

namespace FancyLighting.Utils;

internal static class ColorUtils
{
    internal static float _gamma;
    internal static float _reciprocalGamma;

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
        color.PackedValue = red | (green << 10) | (blue << 20) | (Alpha << 30);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Assign(ref HalfVector4 color, Vector3 rgb)
    {
        color = new HalfVector4(rgb.X, rgb.Y, rgb.Z, 1f);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void GammaToLinear(ref float x) =>
        x = MathF.Pow(Math.Max(x, 0f), _gamma);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void GammaToLinear(ref Vector3 color)
    {
        GammaToLinear(ref color.X);
        GammaToLinear(ref color.Y);
        GammaToLinear(ref color.Z);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void LinearToGamma(ref float x) =>
        x = MathF.Pow(Math.Max(x, 0f), _reciprocalGamma);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void LinearToGamma(ref Vector3 color)
    {
        LinearToGamma(ref color.X);
        LinearToGamma(ref color.Y);
        LinearToGamma(ref color.Z);
    }
}
