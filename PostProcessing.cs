using FancyLighting.Config.Enums;

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
    private Shader _toneMap1Shader;
    private Shader _toneMap2Shader;
    private Shader _bloomCompositeShader;

    private readonly BlurRenderer _blurRenderer = new(false, true);

    public PostProcessing()
    {
        _ditherNoise = ModContent
            .Request<Texture2D>("FancyLighting/Effects/DitherNoise")
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
        _toneMap1Shader = EffectLoader.LoadEffect(
            "FancyLighting/Effects/PostProcessing",
            "ToneMap1"
        );
        _toneMap2Shader = EffectLoader.LoadEffect(
            "FancyLighting/Effects/PostProcessing",
            "ToneMap2"
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
        EffectLoader.UnloadEffect(ref _gammaToGammaNoDitherShader);
        EffectLoader.UnloadEffect(ref _gammaToSrgbDitherShader);
        EffectLoader.UnloadEffect(ref _gammaToSrgbNoDitherShader);
        EffectLoader.UnloadEffect(ref _toneMap1Shader);
        EffectLoader.UnloadEffect(ref _toneMap2Shader);
        EffectLoader.UnloadEffect(ref _bloomCompositeShader);

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
                if (tmo is ToneMappingPreset.Preset2)
                {
                    exposure *= 0.8f;
                }

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

            switch (tmo)
            {
                case ToneMappingPreset.Preset1:
                    _toneMap1Shader.Apply();
                    break;
                case ToneMappingPreset.Preset2:
                    _toneMap2Shader.Apply();
                    break;
            }

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

            if (!disableDither)
            {
                Main.graphics.GraphicsDevice.Textures[4] = _ditherNoise;
                Main.graphics.GraphicsDevice.SamplerStates[4] = SamplerState.PointWrap;
            }

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
