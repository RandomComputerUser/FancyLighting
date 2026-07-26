namespace FancyLighting.Common.Graphics;

internal static class Blitter
{
    private static VertexBuffer _fullscreenTriangle;
    private static FullscreenEffect _basicBlitEffect;

    internal static void Load()
    {
        _fullscreenTriangle = new(
            Main.graphics.GraphicsDevice,
            VertexPositionTexture.VertexDeclaration,
            3,
            BufferUsage.WriteOnly
        );
        _fullscreenTriangle.SetData(
            (VertexPositionTexture[])
                [
                    new(new(-1f, 1f, 0f), new(0f, 0f)),
                    new(new(-1f, -3f, 0f), new(0f, 2f)),
                    new(new(3f, 1f, 0f), new(2f, 0f)),
                ]
        );

        _basicBlitEffect = new(EffectLoader.Load("Blit"), "Blit");
    }

    internal static void Unload()
    {
        _fullscreenTriangle?.Dispose();
        _fullscreenTriangle = null;
    }

    public static void BlitOrSwap(ref RenderTarget2D src, ref RenderTarget2D dst)
    {
        if (CompatibilityConfig.Instance.DisableRenderingOptimizations)
        {
            Blit(src, dst);
        }
        else
        {
            (src, dst) = (dst, src);
        }
    }

    public static void Blit(
        Texture2D src,
        RenderTarget2D dst,
        FullscreenEffect effect = null,
        BlendState blendState = null,
        SamplerState samplerState = null,
        Color? clearColor = null
    )
    {
        var device = Main.graphics.GraphicsDevice;

        device.SetRenderTarget(dst);
        if (clearColor.HasValue)
        {
            device.Clear(clearColor.Value);
        }

        device.BlendState = blendState ?? BlendState.Opaque;
        device.DepthStencilState = DepthStencilState.None;
        device.RasterizerState = RasterizerState.CullNone;

        device.Textures[0] = src;
        device.SamplerStates[0] = samplerState ?? SamplerState.PointClamp;

        (effect ?? _basicBlitEffect).ApplyPass();
        device.SetVertexBuffer(_fullscreenTriangle);
        device.DrawPrimitives(PrimitiveType.TriangleList, 0, 1);
    }
}
