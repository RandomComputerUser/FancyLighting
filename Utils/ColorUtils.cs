using System;
using System.Runtime.CompilerServices;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics.PackedVector;

namespace FancyLighting.Utils;

public static class ColorUtils
{
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
    public static void Assign(ref HalfVector4 color, Vector3 rgb)
    {
        color = new HalfVector4(rgb.X, rgb.Y, rgb.Z, 1f);
    }

    // Input must be in gamma space;
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float Luminance(Vector3 color) =>
        (0.2126f * MathF.Pow(color.X, 2.2f))
        + (0.7152f * MathF.Pow(color.Y, 2.2f))
        + (0.0722f * MathF.Pow(color.Z, 2.2f));
}
