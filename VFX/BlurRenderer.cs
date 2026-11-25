namespace FancyLighting.VFX;

internal sealed class BlurRenderer
{
    private static FullscreenEffect _blurDownsampleEffect;
    private static FullscreenEffect _blurUpsampleEffect;
    private static FullscreenEffect _blurDownsampleRedEffect;
    private static FullscreenEffect _blurUpsampleRedEffect;
    private static FullscreenEffect _blurLineEffect;

    private RenderTarget2D[] _blurTargets;
    private int _baseWidth;
    private int _baseHeight;

    private RenderTarget2D _starburstTarget0;
    private RenderTarget2D _starburstTarget1;
    private RenderTarget2D _starburstTarget2;

    internal static void Load()
    {
        var effect = EffectLoader.Load("Blur");
        _blurDownsampleEffect = new(effect, "BlurDownsample");
        _blurUpsampleEffect = new(effect, "BlurUpsample");
        _blurDownsampleRedEffect = new(effect, "BlurDownsampleRed");
        _blurUpsampleRedEffect = new(effect, "BlurUpsampleRed");
        _blurLineEffect = new(effect, "BlurLine");
    }

    internal static void Unload()
    {
        _blurDownsampleEffect = null;
        _blurUpsampleEffect = null;
        _blurDownsampleRedEffect = null;
        _blurUpsampleRedEffect = null;
        _blurLineEffect = null;
    }

    public void Dispose()
    {
        DisposeBlurTargets();
        _blurTargets = null;
        _starburstTarget0 = null;
        _starburstTarget1 = null;
        _starburstTarget2 = null;
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

    private bool EnsureStarburstTargets(int pass)
    {
        if (_blurTargets is null || _blurTargets.Length < pass + 1)
        {
            return false;
        }

        var target = _blurTargets[pass];
        TextureUtils.MatchSizeAndFormat(ref _starburstTarget0, target);
        TextureUtils.MatchSizeAndFormat(ref _starburstTarget1, target);
        TextureUtils.MatchSizeAndFormat(ref _starburstTarget2, target);
        return true;
    }

    private void DisposeBlurTargets()
    {
        _starburstTarget0?.Dispose();
        _starburstTarget1?.Dispose();
        _starburstTarget2?.Dispose();

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
        SamplerState samplerState = null,
        int starburstCount = 0,
        int starburstPassCount = 0,
        float starburstStrength = 0f
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

        starburstPassCount = Math.Clamp(starburstPassCount, 0, 5);
        var starburstPass = 0;
        var doStarbursts =
            starburstCount > 0
            && starburstPassCount > 0
            && starburstStrength > 0f
            && additiveBlend
            && EnsureStarburstTargets(starburstPass);

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

        if (doStarbursts)
        {
            var target = _blurTargets[starburstPass];
            Blitter.Blit(target, _starburstTarget0);
            RenderStarbursts(
                _starburstTarget0,
                target,
                _starburstTarget1,
                _starburstTarget2,
                starburstCount,
                starburstPassCount,
                starburstStrength
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

    private void RenderStarbursts(
        RenderTarget2D src,
        RenderTarget2D dst,
        RenderTarget2D tmp1,
        RenderTarget2D tmp2,
        int count,
        int passCount,
        float strength
    )
    {
        var decay = (float)Math.Pow(1e-8, 1.0 / Math.Pow(4.0, passCount));
        var angleMult = (float)(Math.Tau / count);

        for (var spike = 0; spike < count; ++spike)
        {
            var angle = angleMult * spike;
            var offset = new Vector2(
                MathF.Cos(angle) / dst.Width,
                MathF.Sin(angle) / dst.Height
            );
            var mult = decay;
            for (var pass = 0; pass < passCount; ++pass)
            {
                var isLastPass = pass + 1 == passCount;

                var mult0 = isLastPass ? strength : 1f;
                mult *= mult0;
                var mult2 = mult * mult;
                var mult3 = mult2 * mult;

                _blurLineEffect
                    .SetParameter("PixelSize", offset)
                    .SetParameter("PixelSize2", 2f * offset)
                    .SetParameter("PixelSize3", 3f * offset)
                    .SetParameter("Mult0", mult0)
                    .SetParameter("Mult1", mult)
                    .SetParameter("Mult2", mult2)
                    .SetParameter("Mult3", mult3);

                Blitter.Blit(
                    pass == 0 ? src : tmp1,
                    isLastPass ? dst : tmp2,
                    samplerState: SamplerState.LinearClamp,
                    blendState: isLastPass
                        ? CustomBlendStates.TrueAdditive
                        : BlendState.Opaque,
                    effect: _blurLineEffect
                );
                (tmp1, tmp2) = (tmp2, tmp1);

                offset *= 4f;
                mult *= mult3;
            }
        }
    }
}
