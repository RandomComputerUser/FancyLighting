namespace FancyLighting.VFX;

internal sealed class BlurRenderer(bool alphaOnly, bool supportAdditiveBlend)
{
    private static FullscreenEffect _blurDownsampleEffect;
    private static FullscreenEffect _blurUpsampleEffect;
    private static FullscreenEffect _blurDownsampleAlphaEffect;
    private static FullscreenEffect _blurUpsampleAlphaEffect;

    private RenderTarget2D[] _blurTargets;
    private int _baseWidth;
    private int _baseHeight;

    public bool AlphaOnly { get; private init; } = alphaOnly;
    public bool SupportsAdditiveBlend { get; private init; } = supportAdditiveBlend;

    internal static void Load()
    {
        var effect = EffectLoader.Load("Blur");
        _blurDownsampleEffect = new(effect, "BlurDownsample");
        _blurUpsampleEffect = new(effect, "BlurUpsample");
        _blurDownsampleAlphaEffect = new(effect, "BlurDownsampleAlpha");
        _blurUpsampleAlphaEffect = new(effect, "BlurUpsampleAlpha");
    }

    internal static void Unload()
    {
        _blurDownsampleEffect = null;
        _blurUpsampleEffect = null;
        _blurDownsampleAlphaEffect = null;
        _blurUpsampleAlphaEffect = null;
    }

    public void Dispose()
    {
        DisposeBlurTargets();
        _blurTargets = null;
        _baseWidth = 0;
        _baseHeight = 0;
    }

    private void EnsureBlurTargets(
        int width,
        int height,
        int targetCount,
        SurfaceFormat format
    )
    {
        if (
            _blurTargets is not null
            && _blurTargets.Length >= targetCount
            && _baseWidth == width
            && _baseHeight == height
            && _blurTargets[0]?.Format == format
        )
        {
            return;
        }

        DisposeBlurTargets();

        _blurTargets = new RenderTarget2D[targetCount];
        var scale = 1f;
        for (var i = 0; i < targetCount; ++i)
        {
            scale *= 0.5f;
            var currWidth = (int)(width * scale);
            var currHeight = (int)(height * scale);

            _blurTargets[i] = new(
                Main.graphics.GraphicsDevice,
                currWidth,
                currHeight,
                false,
                format,
                DepthFormat.None,
                0,
                SupportsAdditiveBlend
                    ? RenderTargetUsage.PreserveContents
                    : RenderTargetUsage.DiscardContents
            );
        }

        _baseWidth = width;
        _baseHeight = height;
    }

    private void DisposeBlurTargets()
    {
        if (_blurTargets is null)
        {
            return;
        }

        foreach (var target in _blurTargets)
        {
            target?.Dispose();
        }
    }

    public RenderTarget2D Blur(
        RenderTarget2D src,
        RenderTarget2D dst,
        int passCount,
        bool additiveBlend = false,
        float zoom = 1f
    )
    {
        additiveBlend = additiveBlend && SupportsAdditiveBlend;
        passCount = Math.Clamp(passCount, 1, 5);

        EnsureBlurTargets(
            (int)(src.Width / zoom),
            (int)(src.Height / zoom),
            passCount,
            AlphaOnly
                ? SurfaceFormat.Color // SurfaceFormat.Alpha8 is not supported
                : TextureUtils.ScreenFormat
        );

        var upsampleBlend = additiveBlend
            ? CustomBlendStates.TrueAdditive
            : BlendState.Opaque;
        var skipFinalUpsample = dst is null;
        var downsampleEffect = AlphaOnly
            ? _blurDownsampleAlphaEffect
            : _blurDownsampleEffect;
        var upsampleEffect = AlphaOnly ? _blurUpsampleAlphaEffect : _blurUpsampleEffect;

        for (var i = 0; i < passCount; ++i)
        {
            var currBlurTarget = i == 0 ? src : _blurTargets[i - 1];
            var nextBlurTarget = _blurTargets[i];

            downsampleEffect.SetParameter(
                "PixelSize",
                new Vector2(0.5f / nextBlurTarget.Width, 0.5f / nextBlurTarget.Height)
            );
            Blitter.Blit(
                currBlurTarget,
                nextBlurTarget,
                downsampleEffect,
                samplerState: SamplerState.LinearClamp
            );
        }

        var finalIndex = skipFinalUpsample ? 1 : 0;
        for (var i = passCount - 1; i >= finalIndex; --i)
        {
            var currBlurTarget = _blurTargets[i];
            var nextBlurTarget = i == 0 ? dst! : _blurTargets[i - 1];
            var scale = i == 0 ? zoom : 1f;

            upsampleEffect.SetParameter(
                "PixelSize",
                new Vector2(scale / nextBlurTarget.Width, scale / nextBlurTarget.Height)
            );
            Blitter.Blit(
                currBlurTarget,
                nextBlurTarget,
                upsampleEffect,
                blendState: upsampleBlend,
                samplerState: SamplerState.LinearClamp
            );
        }

        return skipFinalUpsample ? _blurTargets[0] : dst;
    }
}
