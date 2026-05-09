using System.Runtime.CompilerServices;

namespace FancyLighting.Utils.Accessors;

internal static class MainAccessors
{
    [UnsafeAccessor(
        UnsafeAccessorKind.StaticMethod,
        Name = "ApplyColorOfTheSkiesToTiles"
    )]
    public static extern void ApplyColorOfTheSkiesToTiles(Main canBeNull);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "RenderTiles2")]
    public static extern void RenderTiles2(Main obj);

    [UnsafeAccessor(UnsafeAccessorKind.StaticMethod, Name = "SetBackColor")]
    public static extern void SetBackColor(
        Main canBeNull,
        Main.InfoToSetBackColor info,
        out Color sunColor,
        out Color moonColor
    );

    [UnsafeAccessor(
        UnsafeAccessorKind.StaticMethod,
        Name = "UpdateAtmosphereTransparencyToSkyColor"
    )]
    public static extern void UpdateAtmosphereTransparencyToSkyColor(Main canBeNull);
}
