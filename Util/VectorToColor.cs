using System.Runtime.CompilerServices;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics.PackedVector;

namespace FancyLighting.Util;

public static class VectorToColor
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
    public static void Assign(ref Rgba64 color, float brightness, Vector3 rgb)
    {
        var r = (ulong)((65535f * MathHelper.Clamp(brightness * rgb.X, 0f, 1f)) + 0.5f);
        var g = (ulong)((65535f * MathHelper.Clamp(brightness * rgb.Y, 0f, 1f)) + 0.5f);
        var b = (ulong)((65535f * MathHelper.Clamp(brightness * rgb.Z, 0f, 1f)) + 0.5f);

        color.PackedValue = r | (g << 16) | (b << 32) | ((ulong)ushort.MaxValue << 48);
    }
}
