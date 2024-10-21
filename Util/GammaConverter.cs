using System;
using System.Runtime.CompilerServices;
using Microsoft.Xna.Framework;

namespace FancyLighting.Util;

internal static class GammaConverter
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void GammaToLinear(ref float x) => x = MathF.Pow(x, 2.2f);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void GammaToLinear(ref Vector3 color)
    {
        GammaToLinear(ref color.X);
        GammaToLinear(ref color.Y);
        GammaToLinear(ref color.Z);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float LinearToGamma(ref float x) => x = MathF.Pow(x, 1f / 2.2f);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void LinearToGamma(ref Vector3 color)
    {
        LinearToGamma(ref color.X);
        LinearToGamma(ref color.Y);
        LinearToGamma(ref color.Z);
    }
}
