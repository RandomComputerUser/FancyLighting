using System.Runtime.CompilerServices;

namespace FancyLighting.Utils.Accessors;

internal static class SpriteBatchAccessors
{
    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "customEffect")]
    public static extern ref Effect customEffect(SpriteBatch obj);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "blendState")]
    public static extern ref BlendState blendState(SpriteBatch obj);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "samplerState")]
    public static extern ref SamplerState samplerState(SpriteBatch obj);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "depthStencilState")]
    public static extern ref DepthStencilState depthStencilState(SpriteBatch obj);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "rasterizerState")]
    public static extern ref RasterizerState rasterizerState(SpriteBatch obj);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "transformMatrix")]
    public static extern ref Matrix transformMatrix(SpriteBatch obj);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "vertexBuffer")]
    public static extern ref DynamicVertexBuffer vertexBuffer(SpriteBatch obj);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "indexBuffer")]
    public static extern ref IndexBuffer indexBuffer(SpriteBatch obj);
}
