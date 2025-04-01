using System.Runtime.CompilerServices;
using Terraria.Graphics.Light;

namespace FancyLighting.Utils.Accessors;

internal static class LightingEngineAccessors
{
    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_activeLightMap")]
    public static extern ref LightMap _activeLightMap(LightingEngine obj);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_workingProcessedArea")]
    public static extern ref Rectangle _workingProcessedArea(LightingEngine obj);
}
