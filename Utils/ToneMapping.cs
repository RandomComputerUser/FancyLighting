﻿using System.Runtime.CompilerServices;

namespace FancyLighting.Utils;

internal static class ToneMapping
{
    public const float WhitePoint = 1.25f;

    // Extended Reinhard Tone Mapping using luminance
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ToneMap(ref Vector3 color)
    {
        var luminance = ColorUtils.Luma(color);
        var mult =
            (1f + (luminance * (1f / (WhitePoint * WhitePoint)))) / (1f + luminance);
        Vector3.Multiply(ref color, mult, out color);
    }
}
