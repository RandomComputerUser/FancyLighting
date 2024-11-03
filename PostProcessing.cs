using FancyLighting.Config;
using FancyLighting.Utils;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria;
using Terraria.ModLoader;

namespace FancyLighting;

internal sealed class PostProcessing
{
    private readonly Texture2D _ditherNoise;

    private Shader _gammaToGammaShader;
    private Shader _gammaToSrgbShader;

    public PostProcessing()
    {
        _ditherNoise = ModContent
            .Request<Texture2D>(
                "FancyLighting/Effects/DitherNoise",
                AssetRequestMode.ImmediateLoad
            )
            .Value;

        _gammaToGammaShader = EffectLoader.LoadEffect(
            "FancyLighting/Effects/PostProcessing",
            "GammaToGamma"
        );
        _gammaToSrgbShader = EffectLoader.LoadEffect(
            "FancyLighting/Effects/PostProcessing",
            "GammaToSrgb"
        );
    }

    public void Unload()
    {
        _ditherNoise?.Dispose();
        EffectLoader.UnloadEffect(ref _gammaToGammaShader);
        EffectLoader.UnloadEffect(ref _gammaToSrgbShader);
    }

    internal void ApplyingPostProcessing(RenderTarget2D target, RenderTarget2D tmpTarget)
    {
        Main.graphics.GraphicsDevice.SetRenderTarget(tmpTarget);
        Main.spriteBatch.Begin(
            SpriteSortMode.Immediate,
            BlendState.Opaque,
            SamplerState.PointClamp,
            DepthStencilState.None,
            RasterizerState.CullNone
        );

        var useSrgb = PreferencesConfig.Instance.UseSrgb;
        var gammaRatio = PreferencesConfig.Instance.GammaExponent();
        if (!useSrgb)
        {
            gammaRatio /= 2.2f;
        }

        var shader = useSrgb ? _gammaToSrgbShader : _gammaToGammaShader;
        shader
            .SetParameter(
                "DitherCoordMult",
                new Vector2(
                    (float)-target.Width / _ditherNoise.Width,
                    (float)-target.Height / _ditherNoise.Height
                )
            ) // Multiply by -1 so that it's different from the dithering in bicubic filtering
            .SetParameter("GammaRatio", gammaRatio)
            .Apply();

        Main.graphics.GraphicsDevice.Textures[4] = _ditherNoise;
        Main.graphics.GraphicsDevice.SamplerStates[4] = SamplerState.PointWrap;
        Main.spriteBatch.Draw(target, Vector2.Zero, Color.White);
        Main.spriteBatch.End();

        Main.graphics.GraphicsDevice.SetRenderTarget(target);
        Main.spriteBatch.Begin(
            SpriteSortMode.Deferred,
            BlendState.Opaque,
            SamplerState.PointClamp,
            DepthStencilState.None,
            RasterizerState.CullNone
        );
        Main.spriteBatch.Draw(tmpTarget, Vector2.Zero, Color.White);
        Main.spriteBatch.End();
    }
}
