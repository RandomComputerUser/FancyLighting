namespace FancyLighting.Common.Graphics;

internal static class Blitter
{
    private static VertexBuffer _fullscreenTriangle;
    private static FullscreenEffect _basicBlitEffect;

    internal static void Load()
    {
        _basicBlitEffect = new(EffectLoader.Load("Blit"), "Blit");
    }

    internal static void Unload()
    {
        _fullscreenTriangle?.Dispose();
        _fullscreenTriangle = null;
    }

    private static void LoadFullscreenTriangle()
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
    }

    public static void BlitOrSwap(ref RenderTarget2D src, ref RenderTarget2D dst)
    {
        if (SettingsSystem._optimizeRendering)
        {
            (src, dst) = (dst, src);
        }
        else
        {
            Blit(src, dst);
        }
    }

    public static void Blit(
        Texture2D src,
        RenderTarget2D dst,
        FullscreenEffect effect = null,
        BlendState blendState = null,
        SamplerState samplerState = null,
        Color? clearColor = null,
        bool setTarget = true
    )
    {
        var device = Main.graphics.GraphicsDevice;

        if (setTarget)
        {
            device.SetRenderTarget(dst);
        }
        if (clearColor.HasValue)
        {
            device.Clear(clearColor.Value);
        }

        if (_fullscreenTriangle is null)
        {
            LoadFullscreenTriangle();
        }

        device.BlendState = blendState ?? BlendState.Opaque;
        device.DepthStencilState = DepthStencilState.None;
        device.RasterizerState = RasterizerState.CullNone;

        if (src is not null)
        {
            device.Textures[0] = src;
            device.SamplerStates[0] = samplerState ?? SamplerState.PointClamp;
        }

        (effect ?? _basicBlitEffect).ApplyPass();
        device.SetVertexBuffer(_fullscreenTriangle);
        device.DrawPrimitives(PrimitiveType.TriangleList, 0, 1);

        if (src is RenderTarget2D)
        {
            device.Textures[0] = null;
        }
    }
}
