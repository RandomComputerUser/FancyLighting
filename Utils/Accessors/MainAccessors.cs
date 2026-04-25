using System.Runtime.CompilerServices;

namespace FancyLighting.Utils.Accessors;

internal static class MainAccessors
{
    [UnsafeAccessor(UnsafeAccessorKind.StaticField, Name = "shimmerShine")]
    public static extern ref Vector3 shimmerShine(Main canBeNull);
}
