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

    private readonly Texture2D _ditherNoise;

    private Shader _gammaToLinearShader;
    private Shader _gammaToGammaDitherShader;
    private Shader _gammaToSrgbDitherShader;
    private Shader _toneMapShader;
    private Shader _downsampleShader;
    private Shader _downsampleKarisShader;
    private Shader _blurShader;
    private Shader _bloomCompositeShader;

    private RenderTarget2D[] _blurTargets;

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
        _downsampleShader = EffectLoader.LoadEffect(
            "FancyLighting/Effects/Blur",
            "Downsample"
        );
        _downsampleKarisShader = EffectLoader.LoadEffect(
            "FancyLighting/Effects/Blur",
            "DownsampleKaris"
        );
        _blurShader = EffectLoader.LoadEffect("FancyLighting/Effects/Blur", "Blur");
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
        EffectLoader.UnloadEffect(ref _downsampleShader);
        EffectLoader.UnloadEffect(ref _downsampleKarisShader);
        EffectLoader.UnloadEffect(ref _blurShader);
        EffectLoader.UnloadEffect(ref _bloomCompositeShader);
        DisposeBlurTargets();
    }

    private void EnsureBlurTargets(int width, int height, int targetCount)
    {
        if (
            _blurTargets is not null
            && _blurTargets.Length >= targetCount
            && _blurTargets[0]?.Width == width
            && _blurTargets[0]?.Height == height
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
                SurfaceFormat.HalfVector4,
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
                smoothLightingInstance.ApplyBrightenShader(
                    HiDefBrightnessScale * HiDefBackgroundBrightness
                );
            }
            if (backgroundTarget is not null)
            {
                Main.spriteBatch.Draw(backgroundTarget, Vector2.Zero, Color.White);
            }

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
            var exposure = 1f / HiDefBrightnessScale;
            exposure = MathF.Pow(exposure, gamma);

            Main.graphics.GraphicsDevice.SetRenderTarget(nextTarget);
            Main.spriteBatch.Begin(
                SpriteSortMode.Immediate,
                BlendState.Opaque,
                SamplerState.PointClamp,
                DepthStencilState.None,
                RasterizerState.CullNone
            );
            _gammaToLinearShader
                .SetParameter("Exposure", exposure)
                .SetParameter("GammaRatio", gamma)
                .Apply();
            Main.spriteBatch.Draw(currTarget, Vector2.Zero, Color.White);
            Main.spriteBatch.End();
            gamma = 1f;

            (currTarget, nextTarget) = (nextTarget, currTarget);

            if (doBloom)
            {
                // https://learnopengl.com/Guest-Articles/2022/Phys.-Based-Bloom

                var passCount = Math.Clamp(PreferencesConfig.Instance.BloomRadius, 1, 5);
                var bloomStrength = Math.Clamp(
                    PreferencesConfig.Instance.BloomLerp(),
                    0f,
                    1f
                );

                EnsureBlurTargets(target.Width, target.Height, passCount);

                for (var i = 0; i < passCount; ++i)
                {
                    var currBlurTarget = i == 0 ? currTarget : _blurTargets[i - 1];
                    var nextBlurTarget = _blurTargets[i];

                    var shader = i == 0 ? _downsampleKarisShader : _downsampleShader;

                    Main.graphics.GraphicsDevice.SetRenderTarget(nextBlurTarget);
                    Main.spriteBatch.Begin(
                        SpriteSortMode.Immediate,
                        BlendState.Opaque,
                        SamplerState.LinearClamp,
                        DepthStencilState.None,
                        RasterizerState.CullNone
                    );
                    shader
                        .SetParameter(
                            "FilterSize",
                            new Vector2(
                                1f / currBlurTarget.Width,
                                1f / currBlurTarget.Height
                            )
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

                for (var i = passCount - 1; i >= 1; --i)
                {
                    var currBlurTarget = _blurTargets[i];
                    var nextBlurTarget = _blurTargets[i - 1];

                    Main.graphics.GraphicsDevice.SetRenderTarget(nextBlurTarget);
                    Main.spriteBatch.Begin(
                        SpriteSortMode.Immediate,
                        BlendState.Additive,
                        SamplerState.LinearClamp,
                        DepthStencilState.None,
                        RasterizerState.CullNone
                    );
                    _blurShader
                        .SetParameter(
                            "FilterSize",
                            new Vector2(
                                0.005f
                                    * (
                                        (float)currBlurTarget.Height
                                        / currBlurTarget.Width
                                    ),
                                0.005f
                            )
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

                Main.graphics.GraphicsDevice.Textures[4] = _blurTargets[0];
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
                ) // Multiply by -1 so that it's different from the dithering in bicubic filtering
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
