namespace FancyLighting.VFX;

internal sealed class BlurRenderer
{
    private static FullscreenEffect _blurDownsampleEffect;
    private static FullscreenEffect _blurUpsampleEffect;
    private static FullscreenEffect _blurDownsampleRedEffect;
    private static FullscreenEffect _blurUpsampleRedEffect;

    private RenderTarget2D[] _blurTargets;
    private int _baseWidth;
    private int _baseHeight;

    internal static void Load()
    {
        var effect = EffectLoader.Load("Blur");
        _blurDownsampleEffect = new(effect, "BlurDownsample");
        _blurUpsampleEffect = new(effect, "BlurUpsample");
        _blurDownsampleRedEffect = new(effect, "BlurDownsampleRed");
        _blurUpsampleRedEffect = new(effect, "BlurUpsampleRed");
    }

    internal static void Unload()
    {
        _blurDownsampleEffect = null;
        _blurUpsampleEffect = null;
        _blurDownsampleRedEffect = null;
        _blurUpsampleRedEffect = null;
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
        SurfaceFormat format,
        RenderTargetUsage usage
    )
    {
        if (
            _blurTargets is not null
            && _blurTargets.Length >= targetCount
            && _baseWidth == width
            && _baseHeight == height
            && _blurTargets[0]?.Format == format
            && _blurTargets[0]?.RenderTargetUsage == usage
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
            var currWidth = Math.Max((int)(width * scale), 1);
            var currHeight = Math.Max((int)(height * scale), 1);

            _blurTargets[i] = new(
                Main.graphics.GraphicsDevice,
                currWidth,
                currHeight,
                false,
                format,
                DepthFormat.None,
                0,
                usage
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
        float zoom = 1f,
        bool redOnly = false,
        bool additiveBlend = false,
        int additiveBlendMinLevel = 0,
        SurfaceFormat? format = null,
        SamplerState samplerState = null
    )
    {
        passCount = Math.Clamp(passCount, 1, 5);
        format ??= (redOnly ? SurfaceFormat.HalfSingle : TextureUtils.ScreenFormat);
        samplerState ??= SamplerState.LinearClamp;

        EnsureBlurTargets(
            (int)(src.Width / zoom),
            (int)(src.Height / zoom),
            passCount,
            format.Value,
            additiveBlend
                ? RenderTargetUsage.PreserveContents
                : RenderTargetUsage.DiscardContents
        );

        var upsampleBlend = additiveBlend
            ? CustomBlendStates.TrueAdditive
            : BlendState.Opaque;
        var skipFinalUpsample = dst is null;
        var downsampleEffect = redOnly ? _blurDownsampleRedEffect : _blurDownsampleEffect;
        var upsampleEffect = redOnly ? _blurUpsampleRedEffect : _blurUpsampleEffect;

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
                samplerState: samplerState
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
                blendState: i < additiveBlendMinLevel ? BlendState.Opaque : upsampleBlend,
                samplerState: samplerState
            );
        }

        return skipFinalUpsample ? _blurTargets[0] : dst;
    }
}
