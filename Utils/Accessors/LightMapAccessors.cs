using System.Runtime.CompilerServices;
using Microsoft.Xna.Framework;
using Terraria.Graphics.Light;

namespace FancyLighting.Utils.Accessors;

internal static class LightMapAccessors
{
    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_colors")]
    public static extern ref Vector3[] _colors(LightMap obj);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_mask")]
    public static extern ref LightMaskMode[] _mask(LightMap obj);
}
