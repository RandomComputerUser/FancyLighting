namespace FancyLighting.VFX;

internal sealed class BlurRenderer(bool alphaOnly, bool supportAdditiveBlend)
{
    private static FullscreenEffect _blurDownsampleEffect;
    private static FullscreenEffect _blurUpsampleEffect;
    private static FullscreenEffect _blurDownsampleAlphaEffect;
    private static FullscreenEffect _blurUpsampleAlphaEffect;

    private RenderTarget2D[] _blurTargets;

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
            && _blurTargets[0]?.Width == width
            && _blurTargets[0]?.Height == height
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
                    : RenderTargetUsage.PlatformContents
            );
        }
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

        _blurTargets = null;
    }

    public RenderTarget2D RenderBlur(
        RenderTarget2D src,
        RenderTarget2D dst,
        int passCount,
        bool additiveBlend
    )
    {
        additiveBlend = additiveBlend && SupportsAdditiveBlend;
        passCount = Math.Clamp(passCount, 1, 5);

        EnsureBlurTargets(
            src.Width,
            src.Height,
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
                new Vector2(1f / currBlurTarget.Width, 1f / currBlurTarget.Height)
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

            upsampleEffect.SetParameter(
                "PixelSize",
                new Vector2(1f / nextBlurTarget.Width, 1f / nextBlurTarget.Height)
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
