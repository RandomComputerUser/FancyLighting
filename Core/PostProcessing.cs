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
    private readonly FullscreenEffect _combineLayersEffect;
    private readonly FullscreenEffect _overbrightCombineLayersEffect;
    private readonly FullscreenEffect _overbrightCombineLayersDitheredEffect;
    private readonly FullscreenEffect _overbrightCombineLayersGammaToLinearEffect;
    private readonly FullscreenEffect _overbrightCombineLayersGammaToLinearColorGradedEffect;
    private readonly FullscreenEffect _overbrightGammaToLinearEffect;
    private readonly FullscreenEffect _overbrightGammaToLinearColorGradedEffect;
    private readonly FullscreenEffect _gammaToGammaDitherEffect;
    private readonly FullscreenEffect _gammaToGammaNoDitherEffect;
    private readonly FullscreenEffect _gammaToSrgbDitherEffect;
    private readonly FullscreenEffect _gammaToSrgbNoDitherEffect;
    private readonly FullscreenEffect _bloomCompositeEffect;
    private readonly FullscreenEffect _toneMapNaturalEffect;
    private readonly FullscreenEffect _toneMapVividEffect;

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
        _combineLayersEffect = new(effect, "CombineLayers");
        _overbrightCombineLayersEffect = new(effect, "OverbrightCombineLayers");
        _overbrightCombineLayersDitheredEffect = new(
            effect,
            "OverbrightCombineLayersDithered"
        );
        _overbrightCombineLayersGammaToLinearEffect = new(
            effect,
            "OverbrightCombineLayersGammaToLinear"
        );
        _overbrightCombineLayersGammaToLinearColorGradedEffect = new(
            effect,
            "OverbrightCombineLayersGammaToLinearColorGraded"
        );
        _overbrightGammaToLinearEffect = new(effect, "OverbrightGammaToLinear");
        _overbrightGammaToLinearColorGradedEffect = new(
            effect,
            "OverbrightGammaToLinearColorGraded"
        );
        _gammaToGammaDitherEffect = new(effect, "GammaToGammaDither");
        _gammaToGammaNoDitherEffect = new(effect, "GammaToGammaNoDither");
        _gammaToSrgbDitherEffect = new(effect, "GammaToSrgbDither");
        _gammaToSrgbNoDitherEffect = new(effect, "GammaToSrgbNoDither");
        _bloomCompositeEffect = new(effect, "BloomComposite");
        _toneMapNaturalEffect = new(effect, "ToneMapNatural");
        _toneMapVividEffect = new(effect, "ToneMapVivid");
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

        var mainMenu = Main.gameMenu;
        var hiDef = LightingConfig.Instance.HiDefFeaturesEnabled();
        var doBloom = hiDef && PreferencesConfig.Instance.HdrBloom;
        var depthOfField = PreferencesConfig.Instance.DepthOfField;
        var hdrCompatBlending = SettingsSystem.HdrEnhancedAlphaBlendingDisabled();
        var separateBackground =
            backgroundTarget is not null && !hdrCompatBlending && !mainMenu;
        var cameraMode = MainGraphics.InCameraMode;
        var tmo = PreferencesConfig.Instance.ToneMappingOperator;
        var doColorGrading = hiDef && PreferencesConfig.Instance.VibranceBoost != 0;
        var customGamma =
            (!cameraMode && PreferencesConfig.Instance.UseCustomGamma()) || hiDef;
        var srgb = !cameraMode && PreferencesConfig.Instance.UseSrgb;
        var gamma = ContentGamma();
        var disableDither =
            DeveloperConfig.Instance.DisableDithering || tmo is ToneMappingPreset.Linear;

        var lightMapTexture = _ditherNoise; // fallback that won't brighten anything
        var lightMapTransform = Matrix.Identity;

        if (
            !mainMenu
            && LightingConfig.Instance.SmoothLightingEnabled()
            && LightingConfig.Instance.DrawOverbright()
        )
        {
            smoothLightingInstance.CalculateSmoothLighting(cameraMode);
            if (smoothLightingInstance.CanDrawSmoothLighting)
            {
                TexturePosition
                    .GetScreenPosition(currTarget)
                    .TextureToWorldTransform(out var tileToWorldTransform);
                (lightMapTexture, var lightMapPosition) =
                    smoothLightingInstance.GetLightMapTextureAndPosition();
                lightMapPosition.WorldToTextureTransform(out lightMapTransform);
                Matrix.Multiply(
                    ref tileToWorldTransform,
                    ref lightMapTransform,
                    out lightMapTransform
                );
            }

            if (separateBackground && !hiDef)
            {
                MainGraphics.ResetSavedTextures();
                MainGraphics.SetTexture(8, backgroundTarget, SamplerState.PointClamp);
                MainGraphics.SetTexture(9, lightMapTexture, SamplerState.LinearClamp);
                if (!disableDither)
                {
                    MainGraphics.SetTexture(10, _ditherNoise, SamplerState.PointClamp);
                }

                Blitter.Blit(
                    currTarget,
                    nextTarget,
                    (
                        disableDither
                            ? _overbrightCombineLayersEffect
                            : _overbrightCombineLayersDitheredEffect
                    ).SetParameter("MatrixTransform", lightMapTransform)
                );
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
                ToneMappingPreset.Vivid => 0.75f,
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
                MainGraphics.SetTexture(9, lightMapTexture, SamplerState.LinearClamp);
                Blitter.Blit(
                    currTarget,
                    nextTarget,
                    (
                        doColorGrading
                            ? SetColorGradeParameters(
                                _overbrightCombineLayersGammaToLinearColorGradedEffect,
                                vibrance
                            )
                            : _overbrightCombineLayersGammaToLinearEffect
                    )
                        .SetParameter("Exposure", exposure)
                        .SetParameter("BackgroundExposure", backgroundExposure)
                        .SetParameter("GammaRatio", gamma)
                        .SetParameter("BackgroundGamma", backgroundGamma)
                        .SetParameter("MatrixTransform", lightMapTransform)
                );
                MainGraphics.RestoreSavedTextures();
            }
            else
            {
                if (mainMenu)
                {
                    exposure *= ColorUtils.GammaToLinear(
                        CalculateHiDefBackgroundBrightness()
                    );

                    if (depthOfField)
                    {
                        gamma = 1f;
                    }
                }

                MainGraphics.ResetSavedTextures();
                MainGraphics.SetTexture(9, lightMapTexture, SamplerState.LinearClamp);
                Blitter.Blit(
                    currTarget,
                    nextTarget,
                    (
                        doColorGrading
                            ? SetColorGradeParameters(
                                _overbrightGammaToLinearColorGradedEffect,
                                vibrance
                            )
                            : _overbrightGammaToLinearEffect
                    )
                        .SetParameter("Exposure", exposure)
                        .SetParameter("GammaRatio", gamma)
                        .SetParameter("MatrixTransform", lightMapTransform)
                );
                MainGraphics.RestoreSavedTextures();
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
                ToneMappingPreset.Natural => _toneMapNaturalEffect,
                ToneMappingPreset.Vivid => _toneMapVividEffect,
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
