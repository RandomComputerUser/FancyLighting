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

    private readonly SpriteBatchEffect _brightenEffect;
    private readonly FullscreenEffect _gammaToLinearEffect;
    private readonly FullscreenEffect _gammaToLinearColorGradedEffect;
    private readonly FullscreenEffect _combineLayersGammaToLinearEffect;
    private readonly FullscreenEffect _combineLayersGammaToLinearColorGradedEffect;
    private readonly FullscreenEffect _combineLayersEffect;
    private readonly FullscreenEffect _gammaToGammaDitherEffect;
    private readonly FullscreenEffect _gammaToGammaNoDitherEffect;
    private readonly FullscreenEffect _gammaToSrgbDitherEffect;
    private readonly FullscreenEffect _gammaToSrgbNoDitherEffect;
    private readonly FullscreenEffect _bloomCompositeEffect;
    private readonly FullscreenEffect _toneMapNeutralLmsEffect;
    private readonly FullscreenEffect _toneMapNeutralOldEffect;
    private readonly FullscreenEffect _toneMapFilmicSrgbEffect;

    private readonly BlurRenderer _blurRenderer = new();

    internal PostProcessing()
    {
        _ditherNoise = ModContent
            .Request<Texture2D>(
                "FancyLighting/Effects/DitherNoise",
                AssetRequestMode.ImmediateLoad
            )
            .Value;

        var effect = EffectLoader.Load("PostProcessing");
        _brightenEffect = new(effect, "Brighten");
        _gammaToLinearEffect = new(effect, "GammaToLinear");
        _gammaToLinearColorGradedEffect = new(effect, "GammaToLinearColorGraded");
        _combineLayersGammaToLinearEffect = new(effect, "CombineLayersGammaToLinear");
        _combineLayersGammaToLinearColorGradedEffect = new(
            effect,
            "CombineLayersGammaToLinearColorGraded"
        );
        _combineLayersEffect = new(effect, "CombineLayers");
        _gammaToGammaDitherEffect = new(effect, "GammaToGammaDither");
        _gammaToGammaNoDitherEffect = new(effect, "GammaToGammaNoDither");
        _gammaToSrgbDitherEffect = new(effect, "GammaToSrgbDither");
        _gammaToSrgbNoDitherEffect = new(effect, "GammaToSrgbNoDither");
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

    internal SpriteBatchEffect GetBrightenEffect(float brightness) =>
        _brightenEffect.SetParameter("BrightnessMult", brightness);

    internal FullscreenEffect GetGammaEffect(float gamma) =>
        _gammaToGammaNoDitherEffect.SetParameter("GammaRatio", gamma);

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

    private static FullscreenEffect SetColorGradeParameters(
        FullscreenEffect effect,
        double vibranceBoost
    )
    {
        vibranceBoost *= 4.0;
        var c1 = (vibranceBoost - 1.0) / (2.0 * vibranceBoost);
        var c2 = 1.0 / (2.0 * vibranceBoost);
        var c3 = (vibranceBoost - 1.0) * (vibranceBoost - 1.0);
        var c4 = 4.0 * vibranceBoost;
        var c5 = -vibranceBoost;
        var c6 = -1.0 - (1.0 / vibranceBoost);

        return effect
            .SetParameter(
                "VibranceBoostParams1",
                new Vector4((float)c1, (float)c2, (float)c3, (float)c4)
            )
            .SetParameter("VibranceBoostParams2", new Vector2((float)c5, (float)c6));
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
        var depthOfField = PreferencesConfig.Instance.DepthOfField;
        var hdrCompatBlending = SettingsSystem.HdrEnhancedAlphaBlendingDisabled();
        var separateBackground = backgroundTarget is not null && !hdrCompatBlending;
        var cameraMode = MainGraphics.InCameraMode;
        var tmo = PreferencesConfig.Instance.ToneMappingOperator;
        var doColorGrading = hiDef && PreferencesConfig.Instance.VibranceBoost != 0;
        var customGamma =
            (!cameraMode && PreferencesConfig.Instance.UseCustomGamma()) || hiDef;
        var srgb = !cameraMode && PreferencesConfig.Instance.UseSrgb;
        var gamma = ContentGamma();
        var disableDither =
            DeveloperConfig.Instance.DisableDithering || tmo is ToneMappingPreset.Linear;

        if (
            LightingConfig.Instance.SmoothLightingEnabled()
            && LightingConfig.Instance.DrawOverbright()
        )
        {
            var switchedTargets = smoothLightingInstance.CalculateSmoothLighting(
                cameraMode
            );
            if (smoothLightingInstance.CanDrawSmoothLighting)
            {
                var inplace = hiDef && !switchedTargets;

                smoothLightingInstance.DrawSmoothLighting(
                    currTarget,
                    inplace ? null : nextTarget,
                    background: false,
                    disableNormalMaps: true,
                    doScaling: true,
                    overbrightPass: true
                );

                if (!inplace)
                {
                    (currTarget, nextTarget) = (nextTarget, currTarget);
                }
            }

            if (separateBackground && !hiDef)
            {
                MainGraphics.ResetSavedTextures();
                MainGraphics.SetTexture(8, backgroundTarget, SamplerState.PointClamp);
                Blitter.Blit(currTarget, nextTarget, _combineLayersEffect);
                MainGraphics.RestoreSavedTextures();

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

            var vibrance = Math.Clamp(
                PreferencesConfig.Instance.VibranceIncrease(),
                -0.2,
                0.2
            );

            if (separateBackground)
            {
                // The brightness of the background isn't normally affected by
                // Lighting.GlobalBrightness (which is reduced when the player
                // has the Darkness debuff), but I've decided to change that
                var backgroundExposure =
                    exposure
                    * ColorUtils.GammaToLinear(CalculateHiDefBackgroundBrightness());
                var backgroundGamma = depthOfField ? 1f : gamma;

                MainGraphics.ResetSavedTextures();
                MainGraphics.SetTexture(8, backgroundTarget, SamplerState.PointClamp);
                Blitter.Blit(
                    currTarget,
                    nextTarget,
                    (
                        doColorGrading
                            ? SetColorGradeParameters(
                                _combineLayersGammaToLinearColorGradedEffect,
                                vibrance
                            )
                            : _combineLayersGammaToLinearEffect
                    )
                        .SetParameter("Exposure", exposure)
                        .SetParameter("BackgroundExposure", backgroundExposure)
                        .SetParameter("GammaRatio", gamma)
                        .SetParameter("BackgroundGamma", backgroundGamma)
                );
                MainGraphics.RestoreSavedTextures();
            }
            else
            {
                Blitter.Blit(
                    currTarget,
                    nextTarget,
                    (
                        doColorGrading
                            ? SetColorGradeParameters(
                                _gammaToLinearColorGradedEffect,
                                vibrance
                            )
                            : _gammaToLinearEffect
                    )
                        .SetParameter("Exposure", exposure)
                        .SetParameter("GammaRatio", gamma)
                );
            }
            gamma = 1f;

            (currTarget, nextTarget) = (nextTarget, currTarget);

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
                    zoom: cameraMode ? 1f : Main.BackgroundViewMatrix.Zoom.X,
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
                    ? _gammaToSrgbNoDitherEffect
                    : _gammaToSrgbDitherEffect
                : disableDither
                    ? _gammaToGammaNoDitherEffect
                    : _gammaToGammaDitherEffect.SetParameter("OutputGamma", outputGamma);

            effect.SetParameter("GammaRatio", gamma);

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
