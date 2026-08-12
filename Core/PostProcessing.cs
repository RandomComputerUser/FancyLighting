using FancyLighting.Config.Enums;
using FancyLighting.VFX;
using ReLogic.Content;

namespace FancyLighting.Core;

public sealed class PostProcessing
{
    // Update FancyLightingMod.IL_WorldMap_UpdateLighting() if this changes
    internal const float HiDefBrightnessScale = 0.5f;

    internal const float HiDefBackgroundBrightnessMult = 1.5f;
    private const float UnderworldBackgroundBrightnessMult = 1.2f;

    internal const float DefaultGamma = 2.2f;
    private const float HiDefGamma = 2.4f;

    private readonly Texture2D _ditherNoise;

    private readonly FullscreenEffect _brightenFullscreenEffect;
    private readonly SpriteBatchEffect _brightenSpriteBatchEffect;
    private readonly FullscreenEffect _gammaToLinearNoAlphaEffect;
    private readonly FullscreenEffect _gammaToLinearEffect;
    private readonly FullscreenEffect _combineLayersNoAlphaEffect;
    private readonly FullscreenEffect _combineLayersEffect;
    private readonly FullscreenEffect _combineLayersGammaToLinearNoAlphaEffect;
    private readonly FullscreenEffect _combineLayersGammaToLinearEffect;
    private readonly FullscreenEffect _gammaToGammaDitherNoAlphaEffect;
    private readonly FullscreenEffect _gammaToGammaDitherEffect;
    private readonly FullscreenEffect _gammaToGammaNoDitherNoAlphaEffect;
    private readonly FullscreenEffect _gammaToGammaNoDitherEffect;
    private readonly FullscreenEffect _gammaToSrgbDitherNoAlphaEffect;
    private readonly FullscreenEffect _gammaToSrgbNoDitherNoAlphaEffect;
    private readonly FullscreenEffect _bloomCompositeEffect;
    private readonly FullscreenEffect _vibranceBoostEffect;
    private readonly FullscreenEffect _toneMapNeutralLmsEffect;
    private readonly FullscreenEffect _toneMapNeutralOldEffect;
    private readonly FullscreenEffect _toneMapFilmicSrgbEffect;

    private readonly BlurRenderer _blurRenderer = new(false, true);

    internal PostProcessing()
    {
        _ditherNoise = ModContent
            .Request<Texture2D>(
                "FancyLighting/Effects/DitherNoise",
                AssetRequestMode.ImmediateLoad
            )
            .Value;

        var effect = EffectLoader.Load("PostProcessing");
        _brightenFullscreenEffect = new(effect, "BrightenFullscreen");
        _brightenSpriteBatchEffect = new(effect, "BrightenSpriteBatch");
        _gammaToLinearNoAlphaEffect = new(effect, "GammaToLinearNoAlpha");
        _gammaToLinearEffect = new(effect, "GammaToLinear");
        _combineLayersNoAlphaEffect = new(effect, "CombineLayersNoAlpha");
        _combineLayersEffect = new(effect, "CombineLayers");
        _combineLayersGammaToLinearNoAlphaEffect = new(
            effect,
            "CombineLayersGammaToLinearNoAlpha"
        );
        _combineLayersGammaToLinearEffect = new(effect, "CombineLayersGammaToLinear");
        _gammaToGammaDitherNoAlphaEffect = new(effect, "GammaToGammaDitherNoAlpha");
        _gammaToGammaDitherEffect = new(effect, "GammaToGammaDither");
        _gammaToGammaNoDitherNoAlphaEffect = new(effect, "GammaToGammaNoDitherNoAlpha");
        _gammaToGammaNoDitherEffect = new(effect, "GammaToGammaNoDither");
        _gammaToSrgbDitherNoAlphaEffect = new(effect, "GammaToSrgbDitherNoAlpha");
        _gammaToSrgbNoDitherNoAlphaEffect = new(effect, "GammaToSrgbNoDitherNoAlpha");
        _vibranceBoostEffect = new(effect, "VibranceBoost");
        _bloomCompositeEffect = new(effect, "BloomComposite");
        _toneMapNeutralLmsEffect = new(effect, "ToneMapNeutralLms");
        _toneMapNeutralOldEffect = new(effect, "ToneMapNeutralOld");
        _toneMapFilmicSrgbEffect = new(effect, "ToneMapFilmicSrgb");
    }

    internal void Unload()
    {
        _blurRenderer?.Dispose();
    }

    internal static float ContentGamma() =>
        LightingConfig.Instance.HiDefFeaturesEnabled() ? HiDefGamma : DefaultGamma;

    internal static float CalculateHiDefBackgroundBrightness() =>
        HiDefBrightnessScale
        * HiDefBackgroundBrightnessMult
        * (
            InUnderworld() && !MainGraphics.InCameraMode
                ? UnderworldBackgroundBrightnessMult
                : 1f
        )
        * (0.95f * Lighting.GlobalBrightness);

    // This code is adapted from vanilla (Main.DrawUnderworldBackground())
    private static bool InUnderworld() =>
        Main.screenPosition.Y + Main.screenHeight >= (Main.maxTilesY - 220) * 16f;

    internal FullscreenEffect GetBrightenFullscreenEffect(float brightness) =>
        _brightenFullscreenEffect.SetParameter("BrightnessMult", brightness);

    internal SpriteBatchEffect GetBrightenSpriteBatchEffect(float brightness) =>
        _brightenSpriteBatchEffect.SetParameter("BrightnessMult", brightness);

    internal FullscreenEffect GetGammaNoAlphaEffect(float exposure, float gamma) =>
        _gammaToLinearNoAlphaEffect
            .SetParameter("Exposure", exposure)
            .SetParameter("GammaRatio", gamma);

    internal FullscreenEffect GetGammaEffect(float exposure, float gamma) =>
        _gammaToLinearEffect
            .SetParameter("Exposure", exposure)
            .SetParameter("GammaRatio", gamma);

    internal RenderTarget2D Blur(RenderTarget2D src, RenderTarget2D dst, int radius) =>
        _blurRenderer.Blur(src, dst, radius);

    internal void BlitTwo(
        RenderTarget2D foreground,
        RenderTarget2D background,
        RenderTarget2D dst
    )
    {
        MainGraphics.ResetSavedTextures();
        MainGraphics.SetTexture(8, background, SamplerState.PointClamp);
        Blitter.Blit(foreground, dst, _combineLayersEffect);
        MainGraphics.RestoreSavedTextures();
    }

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
        ref RenderTarget2D screenTarget,
        ref RenderTarget2D screenTargetSwap,
        RenderTarget2D backgroundTarget,
        SmoothLighting smoothLightingInstance
    )
    {
        var currTarget = screenTarget;
        var nextTarget = screenTargetSwap;

        var hiDef = LightingConfig.Instance.HiDefFeaturesEnabled();
        var doBloom = hiDef && PreferencesConfig.Instance.HdrBloom;
        var doDepthOfField = hiDef && PreferencesConfig.Instance.DepthOfField;
        var hdrCompatBlending = SettingsSystem.HdrEnhancedAlphaBlendingDisabled();
        var separateBackground = backgroundTarget is not null && !hdrCompatBlending;
        var cameraMode = MainGraphics.InCameraMode;
        var customGamma =
            (!cameraMode && PreferencesConfig.Instance.UseCustomGamma()) || hiDef;
        var srgb = !cameraMode && PreferencesConfig.Instance.UseSrgb;
        var gamma = ContentGamma();
        var tmo = PreferencesConfig.Instance.ToneMappingOperator;
        var disableDither =
            DeveloperConfig.Instance.DisableDithering || tmo is ToneMappingPreset.Linear;

        if (
            LightingConfig.Instance.SmoothLightingEnabled()
            && LightingConfig.Instance.DrawOverbright()
        )
        {
            smoothLightingInstance.CalculateSmoothLighting(cameraMode);
            if (smoothLightingInstance.CanDrawSmoothLighting)
            {
                smoothLightingInstance.DrawSmoothLighting(
                    currTarget,
                    hiDef ? null : nextTarget,
                    background: false,
                    disableNormalMaps: true,
                    doScaling: true,
                    overbrightPass: true
                );

                if (!hiDef)
                {
                    (currTarget, nextTarget) = (nextTarget, currTarget);
                }
            }

            if (hiDef)
            {
                var exposure = 1f / HiDefBrightnessScale;
                exposure = MathF.Pow(exposure, gamma);
                exposure *= Math.Max(0f, PreferencesConfig.Instance.ExposureMult());
                exposure *= tmo switch
                {
                    ToneMappingPreset.FilmicSrgb => 0.75f,
                    _ => 1f,
                };

                if (separateBackground)
                {
                    // The brightness of the background isn't normally affected by
                    // Lighting.GlobalBrightness (which is reduced when the player
                    // has the Darkness debuff), but I've decided to change that
                    var backgroundExposure =
                        exposure
                        * ColorUtils.GammaToLinear(CalculateHiDefBackgroundBrightness());

                    if (doDepthOfField)
                    {
                        Blitter.Blit(
                            backgroundTarget,
                            nextTarget,
                            (
                                cameraMode
                                    ? _gammaToLinearEffect
                                    : _gammaToLinearNoAlphaEffect
                            )
                                .SetParameter("Exposure", backgroundExposure)
                                .SetParameter("GammaRatio", gamma)
                        );

                        _blurRenderer.Blur(
                            nextTarget,
                            nextTarget,
                            PreferencesConfig.Instance.DepthOfFieldRadius
                        );

                        Blitter.Blit(
                            currTarget,
                            nextTarget,
                            _gammaToLinearEffect
                                .SetParameter("Exposure", exposure)
                                .SetParameter("GammaRatio", gamma),
                            blendState: BlendState.AlphaBlend,
                            setTarget: false
                        );
                    }
                    else
                    {
                        MainGraphics.ResetSavedTextures();
                        MainGraphics.SetTexture(
                            8,
                            backgroundTarget,
                            SamplerState.PointClamp
                        );
                        Blitter.Blit(
                            currTarget,
                            nextTarget,
                            (
                                cameraMode
                                    ? _combineLayersGammaToLinearEffect
                                    : _combineLayersGammaToLinearNoAlphaEffect
                            )
                                .SetParameter("Exposure", exposure)
                                .SetParameter("BackgroundExposure", backgroundExposure)
                                .SetParameter("GammaRatio", gamma)
                        );
                        MainGraphics.RestoreSavedTextures();
                    }
                }
                else
                {
                    Blitter.Blit(
                        currTarget,
                        nextTarget,
                        (cameraMode ? _gammaToLinearEffect : _gammaToLinearNoAlphaEffect)
                            .SetParameter("Exposure", exposure)
                            .SetParameter("GammaRatio", gamma)
                    );
                }
                gamma = 1f;

                (currTarget, nextTarget) = (nextTarget, currTarget);
            }
            else if (separateBackground)
            {
                MainGraphics.ResetSavedTextures();
                MainGraphics.SetTexture(8, backgroundTarget, SamplerState.PointClamp);
                Blitter.Blit(
                    currTarget,
                    nextTarget,
                    cameraMode ? _combineLayersEffect : _combineLayersNoAlphaEffect
                );
                MainGraphics.RestoreSavedTextures();

                (currTarget, nextTarget) = (nextTarget, currTarget);
            }
        }

        if (hiDef)
        {
            if (PreferencesConfig.Instance.VibranceBoost != 0)
            {
                var (params1, params2) = CalculateVibranceBoostParameters(
                    Math.Clamp(PreferencesConfig.Instance.VibranceIncrease(), -0.2, 0.2)
                );

                _vibranceBoostEffect
                    .SetParameter("VibranceBoostParams1", params1)
                    .SetParameter("VibranceBoostParams2", params2);
                Blitter.Blit(currTarget, nextTarget, _vibranceBoostEffect);

                (currTarget, nextTarget) = (nextTarget, currTarget);
            }

            if (doBloom)
            {
                // https://learnopengl.com/Guest-Articles/2022/Phys.-Based-Bloom

                var bloomStrength = Math.Clamp(
                    PreferencesConfig.Instance.BloomLerp(),
                    0f,
                    1f
                );

                var bloomTarget = _blurRenderer.Blur(
                    currTarget,
                    null,
                    PreferencesConfig.Instance.BloomRadius,
                    additiveBlend: true
                );

                _bloomCompositeEffect.SetParameter("BloomStrength", bloomStrength);
                MainGraphics.ResetSavedTextures();
                MainGraphics.SetTexture(8, bloomTarget, SamplerState.LinearClamp);
                Blitter.Blit(currTarget, nextTarget, _bloomCompositeEffect);
                MainGraphics.RestoreSavedTextures();

                (currTarget, nextTarget) = (nextTarget, currTarget);
            }

            var toneMappingEffect = tmo switch
            {
                ToneMappingPreset.NeutralLms => _toneMapNeutralLmsEffect,
                ToneMappingPreset.NeutralOld => _toneMapNeutralOldEffect,
                ToneMappingPreset.FilmicSrgb => _toneMapFilmicSrgbEffect,
                _ => null,
            };

            if (toneMappingEffect is not null)
            {
                Blitter.Blit(currTarget, nextTarget, toneMappingEffect);

                (currTarget, nextTarget) = (nextTarget, currTarget);
            }
        }

        if (customGamma || srgb)
        {
            var outputGamma = cameraMode
                ? DefaultGamma
                : PreferencesConfig.Instance.OutputGamma();
            if (!srgb)
            {
                gamma /= outputGamma;
            }

            // in camera mode, the background can be transparent, so we can't use the no-alpha effect
            // otherwise using the no-alpha effect makes things simpler
            var effect = srgb
                ? disableDither
                    ? _gammaToSrgbNoDitherNoAlphaEffect
                    : _gammaToSrgbDitherNoAlphaEffect
                : disableDither
                    ? cameraMode
                        ? _gammaToGammaNoDitherEffect
                        : _gammaToGammaNoDitherNoAlphaEffect
                    : cameraMode
                        ? _gammaToGammaDitherEffect
                        : _gammaToGammaDitherNoAlphaEffect;
            effect
                .SetParameter("GammaRatio", gamma)
                .SetParameter("OutputGamma", outputGamma);

            MainGraphics.ResetSavedTextures();
            if (!disableDither)
            {
                MainGraphics.SetTexture(8, _ditherNoise, SamplerState.PointWrap);
            }
            Blitter.Blit(currTarget, nextTarget, effect);
            MainGraphics.RestoreSavedTextures();

            (currTarget, nextTarget) = (nextTarget, currTarget);
        }

        if (ReferenceEquals(currTarget, screenTargetSwap))
        {
            Blitter.BlitOrSwap(ref screenTargetSwap, ref screenTarget);
            MainGraphics.AssignScreenTargets();
        }
    }
}
