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
    public static float HiDefBackgroundBrightness { get; private set; }

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

    private readonly Texture2D _ditherNoise;

    private Shader _gammaToLinearShader;
    private Shader _gammaToGammaDitherShader;
    private Shader _gammaToSrgbDitherShader;
    private Shader _toneMapShader;
    private Shader _bloomCompositeShader;

    private readonly BlurRenderer _blurRenderer = new(false, true);

    public PostProcessing()
    {
        _ditherNoise = ModContent
            .Request<Texture2D>(
                "FancyLighting/Effects/DitherNoise",
                AssetRequestMode.ImmediateLoad
            )
            .Value;

        _gammaToLinearShader = EffectLoader.LoadEffect(
            "FancyLighting/Effects/PostProcessing",
            "GammaToLinear"
        );
        _gammaToGammaDitherShader = EffectLoader.LoadEffect(
            "FancyLighting/Effects/PostProcessing",
            "GammaToGammaDither"
        );
        _gammaToSrgbDitherShader = EffectLoader.LoadEffect(
            "FancyLighting/Effects/PostProcessing",
            "GammaToSrgbDither"
        );
        _toneMapShader = EffectLoader.LoadEffect(
            "FancyLighting/Effects/PostProcessing",
            "ToneMap"
        );
        _bloomCompositeShader = EffectLoader.LoadEffect(
            "FancyLighting/Effects/PostProcessing",
            "BloomComposite"
        );
    }

    public void Unload()
    {
        _ditherNoise?.Dispose();
        EffectLoader.UnloadEffect(ref _gammaToLinearShader);
        EffectLoader.UnloadEffect(ref _gammaToGammaDitherShader);
        EffectLoader.UnloadEffect(ref _gammaToSrgbDitherShader);
        EffectLoader.UnloadEffect(ref _toneMapShader);
        EffectLoader.UnloadEffect(ref _bloomCompositeShader);

        _blurRenderer.Unload();
    }

    internal static void CalculateHiDefSurfaceBrightness()
    {
        HiDefBackgroundBrightness = 1.5f;
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
        var doBloom = LightingConfig.Instance.BloomEnabled();
        var cameraMode = FancyLightingMod._inCameraMode;
        var customGamma = PreferencesConfig.Instance.UseCustomGamma() || hiDef;
        var srgb = PreferencesConfig.Instance.UseSrgb;
        var gamma = PreferencesConfig.Instance.GammaExponent();

        if (
            LightingConfig.Instance.SmoothLightingEnabled()
            && LightingConfig.Instance.DrawOverbright()
        )
        {
            smoothLightingInstance.CalculateSmoothLighting(cameraMode);
            if (cameraMode)
            {
                Main.graphics.GraphicsDevice.SetRenderTarget(nextTarget);
                Main.graphics.GraphicsDevice.Clear(Color.Transparent);

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

            if (hiDef)
            {
                Main.graphics.GraphicsDevice.SetRenderTarget(nextTarget);
                Main.graphics.GraphicsDevice.Clear(Color.Transparent);
                Main.spriteBatch.Begin(
                    SpriteSortMode.Immediate,
                    BlendState.AlphaBlend,
                    SamplerState.PointClamp,
                    DepthStencilState.None,
                    RasterizerState.CullNone
                );

                var exposure = 1f / HiDefBrightnessScale;
                exposure = MathF.Pow(exposure, gamma);
                exposure *= Math.Max(0f, PreferencesConfig.Instance.ExposureMult());

                if (backgroundTarget is not null)
                {
                    // The brightness of the background isn't normally affected by
                    // Lighting.GlobalBrightness (which is reduced when the player
                    // has the Darkness debuff), but I've decided to change that
                    var backgroundBrightness = ColorUtils.GammaToLinear(
                        HiDefBrightnessScale
                            * HiDefBackgroundBrightness
                            * (0.9f * Lighting.GlobalBrightness)
                    );
                    _gammaToLinearShader
                        .SetParameter("Exposure", exposure * backgroundBrightness)
                        .SetParameter("GammaRatio", gamma)
                        .Apply();
                    Main.spriteBatch.Draw(backgroundTarget, Vector2.Zero, Color.White);
                }

                _gammaToLinearShader
                    .SetParameter("Exposure", exposure)
                    .SetParameter("GammaRatio", gamma)
                    .Apply();
                Main.spriteBatch.Draw(currTarget, Vector2.Zero, Color.White);
                Main.spriteBatch.End();
                gamma = 1f;

                (currTarget, nextTarget) = (nextTarget, currTarget);
            }
            else if (backgroundTarget is not null)
            {
                Main.graphics.GraphicsDevice.SetRenderTarget(nextTarget);
                Main.graphics.GraphicsDevice.Clear(Color.Transparent);
                Main.spriteBatch.Begin(
                    SpriteSortMode.Deferred,
                    BlendState.AlphaBlend,
                    SamplerState.PointClamp,
                    DepthStencilState.None,
                    RasterizerState.CullNone
                );
                Main.spriteBatch.Draw(backgroundTarget, Vector2.Zero, Color.White);
                Main.spriteBatch.Draw(currTarget, Vector2.Zero, Color.White);
                Main.spriteBatch.End();

                (currTarget, nextTarget) = (nextTarget, currTarget);
            }
        }

        if (hiDef)
        {
            if (doBloom)
            {
                // https://learnopengl.com/Guest-Articles/2022/Phys.-Based-Bloom

                var passCount = Math.Clamp(PreferencesConfig.Instance.BloomRadius, 1, 5);
                var bloomStrength = Math.Clamp(
                    PreferencesConfig.Instance.BloomLerp(),
                    0f,
                    1f
                );

                var bloomTarget = _blurRenderer.RenderBlur(currTarget, null, passCount);

                Main.graphics.GraphicsDevice.SetRenderTarget(nextTarget);
                Main.spriteBatch.Begin(
                    SpriteSortMode.Immediate,
                    BlendState.Opaque,
                    SamplerState.PointClamp,
                    DepthStencilState.None,
                    RasterizerState.CullNone
                );
                _bloomCompositeShader
                    .SetParameter("BloomStrength", bloomStrength)
                    .Apply();

                Main.graphics.GraphicsDevice.Textures[4] = bloomTarget;
                Main.graphics.GraphicsDevice.SamplerStates[4] = SamplerState.LinearClamp;

                Main.spriteBatch.Draw(currTarget, Vector2.Zero, Color.White);
                Main.spriteBatch.End();

                (currTarget, nextTarget) = (nextTarget, currTarget);
            }

            Main.graphics.GraphicsDevice.SetRenderTarget(nextTarget);
            Main.spriteBatch.Begin(
                SpriteSortMode.Immediate,
                BlendState.Opaque,
                SamplerState.PointClamp,
                DepthStencilState.None,
                RasterizerState.CullNone
            );
            _toneMapShader.Apply();
            Main.spriteBatch.Draw(currTarget, Vector2.Zero, Color.White);
            Main.spriteBatch.End();

            (currTarget, nextTarget) = (nextTarget, currTarget);
        }

        if (customGamma || srgb)
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
            if (!useSrgb)
            {
                gamma /= 2.2f;
            }

            var shader = useSrgb ? _gammaToSrgbDitherShader : _gammaToGammaDitherShader;
            shader
                .SetParameter(
                    "DitherCoordMult",
                    new Vector2(
                        (float)-currTarget.Width / _ditherNoise.Width,
                        (float)-currTarget.Height / _ditherNoise.Height
                    )
                ) // Multiply by -1 so that it's different from the dithering in smooth lighting
                .SetParameter("GammaRatio", gamma)
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
