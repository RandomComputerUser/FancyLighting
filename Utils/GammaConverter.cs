using System;
using System.Runtime.CompilerServices;
using Microsoft.Xna.Framework;

namespace FancyLighting.Utils;

internal static class GammaConverter
{
    internal static float _gamma;
    internal static float _reciprocalGamma;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void GammaToLinear(ref float x) =>
        x = x < 0f ? 0f : MathF.Pow(x, _gamma);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void GammaToLinear(ref Vector3 color)
    {
        GammaToLinear(ref color.X);
        GammaToLinear(ref color.Y);
        GammaToLinear(ref color.Z);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void LinearToGamma(ref float x) => x = MathF.Pow(x, _reciprocalGamma);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void LinearToGamma(ref Vector3 color)
    {
        LinearToGamma(ref color.X);
        LinearToGamma(ref color.Y);
        LinearToGamma(ref color.Z);
    }
}
