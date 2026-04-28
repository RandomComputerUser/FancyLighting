using System.Runtime.CompilerServices;

namespace FancyLighting.Utils.Accessors;

internal static class MainAccessors
{
    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "RenderTiles2")]
    public static extern void RenderTiles2(Main obj);
}
