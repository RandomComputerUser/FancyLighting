using FancyLighting.Util;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria;
using Terraria.ModLoader;

namespace FancyLighting;

internal sealed class PostProcessing
{
    private readonly Texture2D _ditherNoise;

    private Shader _gammaToSrgbShader;

    public PostProcessing()
    {
        _ditherNoise = ModContent
            .Request<Texture2D>(
                "FancyLighting/Effects/DitherNoise",
                AssetRequestMode.ImmediateLoad
            )
            .Value;

        _gammaToSrgbShader = EffectLoader.LoadEffect(
            "FancyLighting/Effects/PostProcessing",
            "GammaToSrgb"
        );
    }

    public void Unload()
    {
        _ditherNoise?.Dispose();
        EffectLoader.UnloadEffect(ref _gammaToSrgbShader);
    }

    internal void GammaToSrgb(RenderTarget2D target, RenderTarget2D tmpTarget)
    {
        Main.graphics.GraphicsDevice.SetRenderTarget(tmpTarget);
        Main.spriteBatch.Begin(
            SpriteSortMode.Immediate,
            BlendState.Opaque,
            SamplerState.PointClamp,
            DepthStencilState.None,
            RasterizerState.CullNone
        );
        _gammaToSrgbShader
            .SetParameter(
                "DitherCoordMult",
                new Vector2(
                    (float)target.Width / _ditherNoise.Width,
                    (float)target.Height / _ditherNoise.Height
                )
            )
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
