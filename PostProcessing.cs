using FancyLighting.Config.Enums;
using ReLogic.Content;

namespace FancyLighting;

internal sealed class PostProcessing
{
    // Update FancyLightingMod._WorldMap_UpdateLighting() if this changes
    internal const float HiDefBrightnessScale = 0.5f;
    internal const float HiDefBackgroundBrightnessMult = 1.5f;

    internal const float DefaultGamma = 2.2f;
    private const float HiDefGamma = 2.4f;

    private readonly Texture2D _ditherNoise;

    private Shader _gammaToLinearShader;
    private Shader _gammaToGammaDitherShader;
    private Shader _gammaToGammaNoDitherShader;
    private Shader _gammaToSrgbDitherShader;
    private Shader _gammaToSrgbNoDitherShader;
    private Shader _bloomCompositeShader;
    private Shader _toneMap1Shader;
    private Shader _toneMap1VibranceBoostShader;
    private Shader _toneMap2Shader;
    private Shader _toneMap2VibranceBoostShader;
    private Shader _vibranceBoostShader;

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
        _gammaToGammaNoDitherShader = EffectLoader.LoadEffect(
            "FancyLighting/Effects/PostProcessing",
            "GammaToGammaNoDither"
        );
        _gammaToSrgbDitherShader = EffectLoader.LoadEffect(
            "FancyLighting/Effects/PostProcessing",
            "GammaToSrgbDither"
        );
        _gammaToSrgbNoDitherShader = EffectLoader.LoadEffect(
            "FancyLighting/Effects/PostProcessing",
            "GammaToSrgbNoDither"
        );
        _bloomCompositeShader = EffectLoader.LoadEffect(
            "FancyLighting/Effects/PostProcessing",
            "BloomComposite"
        );
        _toneMap1Shader = EffectLoader.LoadEffect(
            "FancyLighting/Effects/PostProcessing",
            "ToneMap1"
        );
        _toneMap1VibranceBoostShader = EffectLoader.LoadEffect(
            "FancyLighting/Effects/PostProcessing",
            "ToneMap1VibranceBoost"
        );
        _toneMap2Shader = EffectLoader.LoadEffect(
            "FancyLighting/Effects/PostProcessing",
            "ToneMap2"
        );
        _toneMap2VibranceBoostShader = EffectLoader.LoadEffect(
            "FancyLighting/Effects/PostProcessing",
            "ToneMap2VibranceBoost"
        );
        _vibranceBoostShader = EffectLoader.LoadEffect(
            "FancyLighting/Effects/PostProcessing",
            "VibranceBoost"
        );
    }

    public void Unload()
    {
        _ditherNoise?.Dispose();
        EffectLoader.UnloadEffect(ref _gammaToLinearShader);
        EffectLoader.UnloadEffect(ref _gammaToGammaDitherShader);
        EffectLoader.UnloadEffect(ref _gammaToGammaNoDitherShader);
        EffectLoader.UnloadEffect(ref _gammaToSrgbDitherShader);
        EffectLoader.UnloadEffect(ref _gammaToSrgbNoDitherShader);
        EffectLoader.UnloadEffect(ref _bloomCompositeShader);
        EffectLoader.UnloadEffect(ref _toneMap1Shader);
        EffectLoader.UnloadEffect(ref _toneMap1VibranceBoostShader);
        EffectLoader.UnloadEffect(ref _toneMap2Shader);
        EffectLoader.UnloadEffect(ref _toneMap2VibranceBoostShader);
        EffectLoader.UnloadEffect(ref _vibranceBoostShader);

        _blurRenderer.Unload();
    }

    internal static float ContentGamma() =>
        LightingConfig.Instance?.HiDefFeaturesEnabled() is true
            ? HiDefGamma
            : DefaultGamma;

    internal static float CalculateHiDefBackgroundBrightness() =>
        HiDefBrightnessScale
        * HiDefBackgroundBrightnessMult
        * (0.9f * Lighting.GlobalBrightness);

    private static (Vector4, Vector2) CalculateVibranceBoostParameters(double boost)
    {
        boost *= 4.0;
        var c1 = (boost - 1.0) / (2.0 * boost);
        var c2 = 1.0 / (2.0 * boost);
        var c3 = (boost - 1.0) * (boost - 1.0);
        var c4 = 4.0 * boost;
        var c5 = -boost;
        var c6 = -1.0 - (1.0 / boost);
        return (
            new((float)c1, (float)c2, (float)c3, (float)c4),
            new((float)c5, (float)c6)
        );
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
        var doBloom = hiDef && PreferencesConfig.Instance.HdrBloom;
        var doDepthOfField = hiDef && PreferencesConfig.Instance.DepthOfFieldEnabled();
        var hdrCompat = SettingsSystem.HdrCompatibilityEnabled();
        var separateBackground = backgroundTarget is not null && !hdrCompat;
        var cameraMode = FancyLightingMod._inCameraMode;
        var gameCameraMode = FancyLightingMod._isGameInCameraMode;
        var customGamma =
            (!gameCameraMode && PreferencesConfig.Instance.UseCustomGamma()) || hiDef;
        var srgb = !gameCameraMode && PreferencesConfig.Instance.UseSrgb;
        var gamma = ContentGamma();

        var tmo = PreferencesConfig.Instance.ToneMappingOperator;
        var disableDither = tmo is ToneMappingPreset.Linear;

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
                    currTarget,
                    nextTarget,
                    false,
                    false,
                    true,
                    true
                );
            }
            else
            {
                smoothLightingInstance.DrawSmoothLighting(
                    currTarget,
                    nextTarget,
                    false,
                    true,
                    true
                );
            }
            (currTarget, nextTarget) = (nextTarget, currTarget);

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
                exposure *= tmo switch
                {
                    ToneMappingPreset.Preset2 => 0.8f,
                    _ => 1f,
                };

                if (separateBackground)
                {
                    // The brightness of the background isn't normally affected by
                    // Lighting.GlobalBrightness (which is reduced when the player
                    // has the Darkness debuff), but I've decided to change that
                    var backgroundBrightness = ColorUtils.GammaToLinear(
                        CalculateHiDefBackgroundBrightness()
                    );
                    _gammaToLinearShader
                        .SetParameter("Exposure", exposure * backgroundBrightness)
                        .SetParameter("GammaRatio", gamma)
                        .Apply();
                    Main.spriteBatch.Draw(backgroundTarget, Vector2.Zero, Color.White);

                    if (doDepthOfField)
                    {
                        Main.spriteBatch.End();

                        var passCount = Math.Clamp(
                            PreferencesConfig.Instance.DepthOfFieldRadius,
                            1,
                            5
                        );
                        _blurRenderer.RenderBlur(
                            nextTarget,
                            nextTarget,
                            passCount,
                            false
                        );

                        Main.spriteBatch.Begin(
                            SpriteSortMode.Immediate,
                            BlendState.AlphaBlend,
                            SamplerState.PointClamp,
                            DepthStencilState.None,
                            RasterizerState.CullNone
                        );
                    }
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
            else if (separateBackground)
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

                var bloomTarget = _blurRenderer.RenderBlur(
                    currTarget,
                    null,
                    passCount,
                    true
                );

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

                MainGraphics.ResetSavedTextures();
                MainGraphics.SetTexture(4, bloomTarget, SamplerState.LinearClamp);

                Main.spriteBatch.Draw(currTarget, Vector2.Zero, Color.White);
                Main.spriteBatch.End();
                MainGraphics.RestoreSavedTextures();

                (currTarget, nextTarget) = (nextTarget, currTarget);
            }

            Shader toneMappingShader;
            if (PreferencesConfig.Instance.VibranceBoost == 0)
            {
                toneMappingShader = tmo switch
                {
                    ToneMappingPreset.Preset1 => _toneMap1Shader,
                    ToneMappingPreset.Preset2 => _toneMap2Shader,
                    _ => null,
                };
            }
            else
            {
                toneMappingShader = tmo switch
                {
                    ToneMappingPreset.Preset1 => _toneMap1VibranceBoostShader,
                    ToneMappingPreset.Preset2 => _toneMap2VibranceBoostShader,
                    _ => _vibranceBoostShader,
                };

                var (params1, params2) = CalculateVibranceBoostParameters(
                    Math.Clamp(
                        PreferencesConfig.Instance.VibranceIncrease(),
                        -0.249,
                        0.249
                    )
                );

                toneMappingShader
                    .SetParameter("VibranceBoostParams1", params1)
                    .SetParameter("VibranceBoostParams2", params2);
            }

            if (toneMappingShader is not null)
            {
                Main.graphics.GraphicsDevice.SetRenderTarget(nextTarget);
                Main.spriteBatch.Begin(
                    SpriteSortMode.Immediate,
                    BlendState.Opaque,
                    SamplerState.PointClamp,
                    DepthStencilState.None,
                    RasterizerState.CullNone
                );
                toneMappingShader.Apply();
                Main.spriteBatch.Draw(currTarget, Vector2.Zero, Color.White);
                Main.spriteBatch.End();

                (currTarget, nextTarget) = (nextTarget, currTarget);
            }
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

            var outputGamma = gameCameraMode
                ? DefaultGamma
                : PreferencesConfig.Instance.OutputGamma();
            if (!srgb)
            {
                gamma /= outputGamma;
            }

            var shader = srgb
                ? disableDither
                    ? _gammaToSrgbNoDitherShader
                    : _gammaToSrgbDitherShader
                : disableDither
                    ? _gammaToGammaNoDitherShader
                    : _gammaToGammaDitherShader;
            shader
                .SetParameter(
                    "DitherCoordMult",
                    new Vector2(
                        (float)-currTarget.Width / _ditherNoise.Width,
                        (float)-currTarget.Height / _ditherNoise.Height
                    )
                ) // Multiply by -1 so that it's different from the dithering in smooth lighting
                .SetParameter("GammaRatio", gamma)
                .SetParameter("OutputGamma", outputGamma)
                .Apply();

            MainGraphics.ResetSavedTextures();
            if (!disableDither)
            {
                MainGraphics.SetTexture(4, _ditherNoise, SamplerState.PointWrap);
            }

            Main.spriteBatch.Draw(currTarget, Vector2.Zero, Color.White);
            Main.spriteBatch.End();
            MainGraphics.RestoreSavedTextures();

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
