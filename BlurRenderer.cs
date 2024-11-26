using FancyLighting.Utils;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;

namespace FancyLighting;

internal class BlurRenderer
{
    private static Shader _blurDownsampleShader;
    private static Shader _blurUpsampleShader;

    private static readonly BlendState _trueAdditiveBlend =
        new()
        {
            ColorBlendFunction = BlendFunction.Add,
            AlphaBlendFunction = BlendFunction.Add,
            ColorSourceBlend = Blend.One,
            ColorDestinationBlend = Blend.One,
            AlphaSourceBlend = Blend.One,
            AlphaDestinationBlend = Blend.One,
        };

    private RenderTarget2D[] _blurTargets;

    private static void InitShaders()
    {
        _blurDownsampleShader ??= EffectLoader.LoadEffect(
            "FancyLighting/Effects/Blur",
            "BlurDownsample"
        );
        _blurUpsampleShader ??= EffectLoader.LoadEffect(
            "FancyLighting/Effects/Blur",
            "BlurUpsample"
        );
    }

    public void Unload()
    {
        EffectLoader.UnloadEffect(ref _blurDownsampleShader);
        EffectLoader.UnloadEffect(ref _blurUpsampleShader);
        DisposeBlurTargets();
    }

    private void EnsureBlurTargets(int width, int height, int targetCount)
    {
        var format = TextureUtils.Format;

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

            _blurTargets[i] = new RenderTarget2D(
                Main.graphics.GraphicsDevice,
                currWidth,
                currHeight,
                false,
                format,
                DepthFormat.None,
                0,
                RenderTargetUsage.PreserveContents
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
        bool useAdditiveBlend = false
    )
    {
        var upsampleBlend = useAdditiveBlend ? _trueAdditiveBlend : BlendState.Opaque;
        var skipFinalUpsample = dst is null;

        EnsureBlurTargets(src.Width, src.Height, passCount);
        InitShaders();

        for (var i = 0; i < passCount; ++i)
        {
            var currBlurTarget = i == 0 ? src : _blurTargets[i - 1];
            var nextBlurTarget = _blurTargets[i];

            Main.graphics.GraphicsDevice.SetRenderTarget(nextBlurTarget);
            Main.spriteBatch.Begin(
                SpriteSortMode.Immediate,
                BlendState.Opaque,
                SamplerState.LinearClamp,
                DepthStencilState.None,
                RasterizerState.CullNone
            );
            _blurDownsampleShader
                .SetParameter(
                    "PixelSize",
                    new Vector2(1f / currBlurTarget.Width, 1f / currBlurTarget.Height)
                )
                .Apply();
            Main.spriteBatch.Draw(
                currBlurTarget,
                Vector2.Zero,
                null,
                Color.White,
                0f,
                Vector2.Zero,
                new Vector2(
                    (float)nextBlurTarget.Width / currBlurTarget.Width,
                    (float)nextBlurTarget.Height / currBlurTarget.Height
                ), // simulates "fullscreen" vertex shader
                SpriteEffects.None,
                0f
            );
            Main.spriteBatch.End();
        }

        var finalIndex = skipFinalUpsample ? 1 : 0;
        for (var i = passCount - 1; i >= finalIndex; --i)
        {
            var currBlurTarget = _blurTargets[i];
            var nextBlurTarget = i == 0 ? dst! : _blurTargets[i - 1];

            Main.graphics.GraphicsDevice.SetRenderTarget(nextBlurTarget);
            Main.spriteBatch.Begin(
                SpriteSortMode.Immediate,
                upsampleBlend,
                SamplerState.LinearClamp,
                DepthStencilState.None,
                RasterizerState.CullNone
            );
            _blurUpsampleShader
                .SetParameter(
                    "PixelSize",
                    new Vector2(1f / nextBlurTarget.Width, 1f / nextBlurTarget.Height)
                )
                .Apply();
            Main.spriteBatch.Draw(
                currBlurTarget,
                Vector2.Zero,
                null,
                Color.White,
                0f,
                Vector2.Zero,
                new Vector2(
                    (float)nextBlurTarget.Width / currBlurTarget.Width,
                    (float)nextBlurTarget.Height / currBlurTarget.Height
                ),
                SpriteEffects.None,
                0f
            );
            Main.spriteBatch.End();
        }

        return skipFinalUpsample ? _blurTargets[0] : dst;
    }
}
