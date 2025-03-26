using System.Runtime.CompilerServices;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace FancyLighting.Utils.Accessors;

internal static class SpriteBatchAccessors
{
    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "samplerState")]
    public static extern ref SamplerState samplerState(SpriteBatch obj);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "transformMatrix")]
    public static extern ref Matrix transformMatrix(SpriteBatch obj);
}
