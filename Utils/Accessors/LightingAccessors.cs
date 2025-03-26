using System.Runtime.CompilerServices;
using Terraria;
using Terraria.Graphics.Light;

namespace FancyLighting.Utils.Accessors;

internal static class LightingAccessors
{
    [UnsafeAccessor(UnsafeAccessorKind.StaticField, Name = "_activeEngine")]
    public static extern ref ILightingEngine _activeEngine(Lighting canBeNull);
}
