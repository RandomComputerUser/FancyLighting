using FancyLighting.Config.Enums;
using ReLogic.Content;

namespace FancyLighting;

public sealed class PostProcessing
{
    // Update FancyLightingMod.IL_WorldMap_UpdateLighting() if this changes
    internal const float HiDefBrightnessScale = 0.5f;

    internal const float HiDefBackgroundBrightnessMult = 1.5f;
    private const float UnderworldBackgroundBrightnessMult = 1.2f;

    internal const float DefaultGamma = 2.2f;
    private const float HiDefGamma = 2.4f;

    private readonly Texture2D _ditherNoise;

    private Shader _gammaToLinearNoAlphaShader;
    private Shader _gammaToLinearShader;
    private Shader _gammaToGammaDitherNoAlphaShader;
    private Shader _gammaToGammaDitherShader;
    private Shader _gammaToGammaNoDitherNoAlphaShader;
    private Shader _gammaToGammaNoDitherShader;
    private Shader _gammaToSrgbDitherNoAlphaShader;
    private Shader _gammaToSrgbNoDitherNoAlphaShader;
    private Shader _bloomCompositeShader;
    private Shader _toneMapNeutralLmsShader;
    private Shader _toneMapNeutralLmsVibranceBoostShader;
    private Shader _toneMapNeutralOldShader;
    private Shader _toneMapNeutralOldVibranceBoostShader;
    private Shader _toneMapFilmicSrgbShader;
    private Shader _toneMapFilmicSrgbVibranceBoostShader;
    private Shader _vibranceBoostShader;

    private readonly BlurRenderer _blurRenderer = new(false, true);

    internal PostProcessing()
    {
        _ditherNoise = ModContent
            .Request<Texture2D>(
                "FancyLighting/Effects/DitherNoise",
                AssetRequestMode.ImmediateLoad
            )
            .Value;

        _gammaToLinearNoAlphaShader = EffectLoader.LoadEffect(
            "FancyLighting/Effects/PostProcessing",
            "GammaToLinearNoAlpha"
        );
        _gammaToLinearShader = EffectLoader.LoadEffect(
            "FancyLighting/Effects/PostProcessing",
            "GammaToLinear"
        );
        _gammaToGammaDitherNoAlphaShader = EffectLoader.LoadEffect(
            "FancyLighting/Effects/PostProcessing",
            "GammaToGammaDitherNoAlpha"
        );
        _gammaToGammaDitherShader = EffectLoader.LoadEffect(
            "FancyLighting/Effects/PostProcessing",
            "GammaToGammaDither"
        );
        _gammaToGammaNoDitherNoAlphaShader = EffectLoader.LoadEffect(
            "FancyLighting/Effects/PostProcessing",
            "GammaToGammaNoDitherNoAlpha"
        );
        _gammaToGammaNoDitherShader = EffectLoader.LoadEffect(
            "FancyLighting/Effects/PostProcessing",
            "GammaToGammaNoDither"
        );
        _gammaToSrgbDitherNoAlphaShader = EffectLoader.LoadEffect(
            "FancyLighting/Effects/PostProcessing",
            "GammaToSrgbDitherNoAlpha"
        );
        _gammaToSrgbNoDitherNoAlphaShader = EffectLoader.LoadEffect(
            "FancyLighting/Effects/PostProcessing",
            "GammaToSrgbNoDitherNoAlpha"
        );
        _bloomCompositeShader = EffectLoader.LoadEffect(
            "FancyLighting/Effects/PostProcessing",
            "BloomComposite"
        );
        _toneMapNeutralLmsShader = EffectLoader.LoadEffect(
            "FancyLighting/Effects/PostProcessing",
            "ToneMapNeutralLms"
        );
        _toneMapNeutralLmsVibranceBoostShader = EffectLoader.LoadEffect(
            "FancyLighting/Effects/PostProcessing",
            "ToneMapNeutralLmsVibranceBoost"
        );
        _toneMapNeutralOldShader = EffectLoader.LoadEffect(
            "FancyLighting/Effects/PostProcessing",
            "ToneMapNeutralOld"
        );
        _toneMapNeutralOldVibranceBoostShader = EffectLoader.LoadEffect(
            "FancyLighting/Effects/PostProcessing",
            "ToneMapNeutralOldVibranceBoost"
        );
        _toneMapFilmicSrgbShader = EffectLoader.LoadEffect(
            "FancyLighting/Effects/PostProcessing",
            "ToneMapFilmicSrgb"
        );
        _toneMapFilmicSrgbVibranceBoostShader = EffectLoader.LoadEffect(
            "FancyLighting/Effects/PostProcessing",
            "ToneMapFilmicSrgbVibranceBoost"
        );
        _vibranceBoostShader = EffectLoader.LoadEffect(
            "FancyLighting/Effects/PostProcessing",
            "VibranceBoost"
        );
    }

    internal void Unload()
    {
        _ditherNoise?.Dispose();
        EffectLoader.UnloadEffect(ref _gammaToLinearNoAlphaShader);
        EffectLoader.UnloadEffect(ref _gammaToLinearShader);
        EffectLoader.UnloadEffect(ref _gammaToGammaDitherNoAlphaShader);
        EffectLoader.UnloadEffect(ref _gammaToGammaDitherShader);
        EffectLoader.UnloadEffect(ref _gammaToGammaNoDitherNoAlphaShader);
        EffectLoader.UnloadEffect(ref _gammaToGammaNoDitherShader);
        EffectLoader.UnloadEffect(ref _gammaToSrgbDitherNoAlphaShader);
        EffectLoader.UnloadEffect(ref _gammaToSrgbNoDitherNoAlphaShader);
        EffectLoader.UnloadEffect(ref _bloomCompositeShader);
        EffectLoader.UnloadEffect(ref _toneMapNeutralLmsShader);
        EffectLoader.UnloadEffect(ref _toneMapNeutralLmsVibranceBoostShader);
        EffectLoader.UnloadEffect(ref _toneMapNeutralOldShader);
        EffectLoader.UnloadEffect(ref _toneMapNeutralOldVibranceBoostShader);
        EffectLoader.UnloadEffect(ref _toneMapFilmicSrgbShader);
        EffectLoader.UnloadEffect(ref _toneMapFilmicSrgbVibranceBoostShader);
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
        * (
            InUnderworld() && !FancyLightingMod._inCameraMode
                ? UnderworldBackgroundBrightnessMult
                : 1f
        )
        * (0.9f * Lighting.GlobalBrightness);

    // This code is adapted from vanilla (Main.DrawUnderworldBackground())
    private static bool InUnderworld() =>
        Main.screenPosition.Y + Main.screenHeight >= (Main.maxTilesY - 220) * 16f;

    internal void ApplyGammaNoAlphaShader(float exposure, float gamma) =>
        _gammaToLinearNoAlphaShader
            .SetParameter("Exposure", exposure)
            .SetParameter("GammaRatio", gamma)
            .Apply();

    internal void ApplyGammaShader(float exposure, float gamma) =>
        _gammaToLinearShader
            .SetParameter("Exposure", exposure)
            .SetParameter("GammaRatio", gamma)
            .Apply();

    internal RenderTarget2D Blur(RenderTarget2D src, RenderTarget2D dst, int radius) =>
        _blurRenderer.RenderBlur(src, dst, radius, false);

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
        var doDepthOfField = hiDef && PreferencesConfig.Instance.DepthOfField;
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
                    ToneMappingPreset.FilmicSrgb => 0.8f,
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
                    (gameCameraMode ? _gammaToLinearShader : _gammaToLinearNoAlphaShader)
                        .SetParameter("Exposure", exposure * backgroundBrightness)
                        .SetParameter("GammaRatio", gamma)
                        .Apply();
                    Main.spriteBatch.Draw(backgroundTarget, Vector2.Zero, Color.White);

                    if (doDepthOfField)
                    {
                        Main.spriteBatch.End();

                        _blurRenderer.RenderBlur(
                            nextTarget,
                            nextTarget,
                            PreferencesConfig.Instance.DepthOfFieldRadius,
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

                (
                    separateBackground || gameCameraMode
                        ? _gammaToLinearShader
                        : _gammaToLinearNoAlphaShader
                )
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

                var bloomStrength = Math.Clamp(
                    PreferencesConfig.Instance.BloomLerp(),
                    0f,
                    1f
                );

                var bloomTarget = _blurRenderer.RenderBlur(
                    currTarget,
                    null,
                    PreferencesConfig.Instance.BloomRadius,
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
                    ToneMappingPreset.NeutralLms => _toneMapNeutralLmsShader,
                    ToneMappingPreset.NeutralOld => _toneMapNeutralOldShader,
                    ToneMappingPreset.FilmicSrgb => _toneMapFilmicSrgbShader,
                    _ => null,
                };
            }
            else
            {
                toneMappingShader = tmo switch
                {
                    ToneMappingPreset.NeutralLms => _toneMapNeutralLmsVibranceBoostShader,
                    ToneMappingPreset.NeutralOld => _toneMapNeutralOldVibranceBoostShader,
                    ToneMappingPreset.FilmicSrgb => _toneMapFilmicSrgbVibranceBoostShader,
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

            // in camera mode, the background can be transparent, so we can't use the no-alpha shader
            // otherwise using the no-alpha shader makes things simpler
            var shader = srgb
                ? disableDither
                    ? _gammaToSrgbNoDitherNoAlphaShader
                    : _gammaToSrgbDitherNoAlphaShader
                : disableDither
                    ? gameCameraMode
                        ? _gammaToGammaNoDitherShader
                        : _gammaToGammaNoDitherNoAlphaShader
                    : gameCameraMode
                        ? _gammaToGammaDitherShader
                        : _gammaToGammaDitherNoAlphaShader;
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
