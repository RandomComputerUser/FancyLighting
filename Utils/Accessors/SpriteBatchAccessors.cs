using System.Runtime.CompilerServices;

namespace FancyLighting.Utils.Accessors;

internal static class SpriteBatchAccessors
{
    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "customEffect")]
    public static extern ref bool beginCalled(SpriteBatch obj);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "customEffect")]
    public static extern ref Effect customEffect(SpriteBatch obj);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "samplerState")]
    public static extern ref SamplerState samplerState(SpriteBatch obj);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "transformMatrix")]
    public static extern ref Matrix transformMatrix(SpriteBatch obj);
}
