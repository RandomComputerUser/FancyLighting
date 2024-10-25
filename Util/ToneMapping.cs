using System.Runtime.CompilerServices;
using Microsoft.Xna.Framework;

namespace FancyLighting.Util;

internal static class ToneMapping
{
    public const float WhitePoint = 1.25f;

    // Extended Reinhard Tone Mapping using luminance
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ToneMap(ref Vector3 color)
    {
        var luminance = (0.2126f * color.X) + (0.7152f * color.Y) + (0.0722f * color.Z);
        var mult =
            (1f + (luminance * (1f / (WhitePoint * WhitePoint)))) / (1f + luminance);
        Vector3.Multiply(ref color, mult, out color);
    }
}
