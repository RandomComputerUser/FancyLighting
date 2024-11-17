using System;
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
    internal const float HiDefBrightnessScale = 0.5f;
    public static float HiDefSurfaceBrightness { get; private set; }

    private readonly Texture2D _ditherNoise;

    private Shader _gammaToGammaShader;
    private Shader _gammaToSrgbShader;
    private Shader _ditherHiDefShader;
    private Shader _toneMapShader;

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
        _ditherHiDefShader = EffectLoader.LoadEffect(
            "FancyLighting/Effects/PostProcessing",
            "DitherHiDef"
        );
        _toneMapShader = EffectLoader.LoadEffect(
            "FancyLighting/Effects/PostProcessing",
            "ToneMap"
        );
    }

    public void Unload()
    {
        _ditherNoise?.Dispose();
        EffectLoader.UnloadEffect(ref _gammaToGammaShader);
        EffectLoader.UnloadEffect(ref _gammaToSrgbShader);
        EffectLoader.UnloadEffect(ref _ditherHiDefShader);
        EffectLoader.UnloadEffect(ref _toneMapShader);
    }

    internal static void CalculateHiDefSurfaceBrightness()
    {
        HiDefSurfaceBrightness =
            1f + 0.5f * ColorUtils.Luminance(Main.ColorOfTheSkies.ToVector3());
    }

    internal void ApplyPostProcessing(
        RenderTarget2D target,
        RenderTarget2D tmpTarget,
        RenderTarget2D backgroundTarget,
        SmoothLighting smoothLightingInstance
    )
    {
        var currTarget = target;
        var nextTarget = tmpTarget;

        var hiDef = LightingConfig.Instance.HiDefFeaturesEnabled();
        var cameraMode = FancyLightingMod._inCameraMode;
        var customGamma = PreferencesConfig.Instance.UseCustomGamma();
        var srgb = PreferencesConfig.Instance.UseSrgb;

        if (LightingConfig.Instance.DrawOverbright())
        {
            smoothLightingInstance.CalculateSmoothLighting(cameraMode, cameraMode);
            if (cameraMode)
            {
                smoothLightingInstance.GetCameraModeRenderTarget(
                    FancyLightingMod._cameraModeTarget
                );
                smoothLightingInstance.DrawSmoothLightingCameraMode(
                    nextTarget,
                    currTarget,
                    false,
                    false,
                    true,
                    true
                );
                (currTarget, nextTarget) = (nextTarget, currTarget);
            }
            else
            {
                smoothLightingInstance.DrawSmoothLighting(
                    currTarget,
                    false,
                    true,
                    nextTarget
                );
            }

            Main.graphics.GraphicsDevice.SetRenderTarget(nextTarget);
            Main.graphics.GraphicsDevice.Clear(Color.Transparent);
            Main.spriteBatch.Begin(
                hiDef ? SpriteSortMode.Immediate : SpriteSortMode.Deferred,
                BlendState.AlphaBlend,
                SamplerState.PointClamp,
                DepthStencilState.None,
                RasterizerState.CullNone
            );

            if (hiDef)
            {
                var worldY = cameraMode
                    ? FancyLightingMod._cameraModeArea.Y
                        + (0.5f * FancyLightingMod._cameraModeArea.Height)
                    : (Main.screenPosition.Y + (0.5f * Main.screenHeight)) / 16f;
                var aboveSurface =
                    worldY <= (Main.worldSurface + Main.UnderworldLayer) / 2f;
                var backgroundBrightness = aboveSurface ? HiDefSurfaceBrightness : 1f;

                smoothLightingInstance.ApplyBrightenShader(
                    HiDefBrightnessScale * backgroundBrightness
                );
            }
            Main.spriteBatch.Draw(backgroundTarget, Vector2.Zero, Color.White);
            if (hiDef)
            {
                smoothLightingInstance.ApplyNoFilterShader();
            }
            Main.spriteBatch.Draw(currTarget, Vector2.Zero, Color.White);
            Main.spriteBatch.End();

            (currTarget, nextTarget) = (nextTarget, currTarget);
        }

        if (hiDef)
        {
            Main.graphics.GraphicsDevice.SetRenderTarget(nextTarget);
            Main.spriteBatch.Begin(
                SpriteSortMode.Immediate,
                BlendState.Opaque,
                SamplerState.PointClamp,
                DepthStencilState.None,
                RasterizerState.CullNone
            );
            _toneMapShader
                .SetParameter("Exposure", 1f / MathF.Pow(HiDefBrightnessScale, 2.2f))
                .Apply();
            Main.spriteBatch.Draw(currTarget, Vector2.Zero, Color.White);
            Main.spriteBatch.End();

            (currTarget, nextTarget) = (nextTarget, currTarget);
        }

        if (hiDef || customGamma || srgb)
        {
            Main.graphics.GraphicsDevice.SetRenderTarget(nextTarget);
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

            var shader =
                useSrgb ? _gammaToSrgbShader
                : customGamma ? _gammaToGammaShader
                : _ditherHiDefShader;
            shader
                .SetParameter(
                    "DitherCoordMult",
                    new Vector2(
                        (float)-currTarget.Width / _ditherNoise.Width,
                        (float)-currTarget.Height / _ditherNoise.Height
                    )
                ) // Multiply by -1 so that it's different from the dithering in bicubic filtering
                .SetParameter("GammaRatio", gammaRatio)
                .Apply();

            Main.graphics.GraphicsDevice.Textures[4] = _ditherNoise;
            Main.graphics.GraphicsDevice.SamplerStates[4] = SamplerState.PointWrap;
            Main.spriteBatch.Draw(currTarget, Vector2.Zero, Color.White);
            Main.spriteBatch.End();

            (currTarget, nextTarget) = (nextTarget, currTarget);
        }

        if (currTarget == target)
        {
            return;
        }

        Main.graphics.GraphicsDevice.SetRenderTarget(nextTarget);
        Main.spriteBatch.Begin(
            SpriteSortMode.Deferred,
            BlendState.Opaque,
            SamplerState.PointClamp,
            DepthStencilState.None,
            RasterizerState.CullNone
        );
        Main.spriteBatch.Draw(currTarget, Vector2.Zero, Color.White);
        Main.spriteBatch.End();
    }
}
