using FancyLighting.ColorGradients;
using FancyLighting.ColorGradients.SkyColor;
using FancyLighting.ColorGradients.SkyColor.Gradients;
using FancyLighting.Config.Enums;
using ReLogic.Content;

namespace FancyLighting.Core.Sky;

public sealed class FancySkyRendering
{
    private readonly Texture2D _ditherNoise;

    private readonly FullscreenEffect _skyEffect;
    private readonly FullscreenEffect _skyDitheredEffect;
    private readonly SpriteBatchEffect _sunEffect;

    private const float FadeBegin = 0.10f;
    private const float FadeHeight = 0.50f;
    private const float FadeHeightMult = 1f;

    /// <summary>
    /// Modify the colors of the sky used in Fancy Atmosphere.
    /// </summary>
    /// <param name="lowSkyColor">The color of the low part of the sky.</param>
    /// <param name="middleSkyColor">The color of the middle part of the sky.</param>
    /// <param name="highSkyColor">The color of the high part of the sky.</param>
    /// <param name="skyColorMult">A color multiplier applied to the entire sky. Typically, this changes based on the biome or weather.</param>
    public delegate void FancyAtmosphereColorsModifier(
        ref Vector3 lowSkyColor,
        ref Vector3 middleSkyColor,
        ref Vector3 highSkyColor,
        ref Vector3 skyColorMult
    );

    /// <summary>
    /// This event is invoked when the colors used for Fancy Atmosphere are calculated.
    /// </summary>
    /// <remarks>
    /// This event is invoked both while on the main menu and while in a world.
    /// </remarks>
    public static event FancyAtmosphereColorsModifier ModifyFancyAtmosphereColors;

    internal FancySkyRendering()
    {
        _ditherNoise = ModContent
            .Request<Texture2D>(
                "FancyLighting/Effects/DitherNoise",
                AssetRequestMode.ImmediateLoad
            )
            .Value;

        var effect = EffectLoader.Load("Sky");
        _skyEffect = new(effect, "Sky");
        _skyDitheredEffect = new(effect, "SkyDithered");
        _sunEffect = new(effect, "Sun", EffectFeatures.HiDef);

        AddHooks();
    }

    private void AddHooks()
    {
        On_Main.DrawStarsInBackground += _Main_DrawStarsInBackground;
    }

    internal void Unload()
    {
        ModifyFancyAtmosphereColors = null;
    }

    // Draw sky
    private void _Main_DrawStarsInBackground(
        On_Main.orig_DrawStarsInBackground orig,
        Main self,
        Main.SceneArea sceneArea,
        bool artificial
    )
    {
        if (!LightingConfig.Instance.FancySkyRenderingEnabled() || artificial)
        {
            orig(self, sceneArea, artificial);
            return;
        }

        var doOverbright = LightingConfig.Instance.DrawOverbright();
        var hiDef =
            LightingConfig.Instance.HiDefFeaturesEnabled() && MainGraphics.DoingCapture;
        var doDithering =
            !DeveloperConfig.Instance.DisableDithering
            && LightingConfig.Instance.SmoothLightingEnabled()
            && doOverbright
            && !hiDef;
        var gamma = PostProcessing.ContentGamma();

        var sbParams = Main.spriteBatch.GetParameters();
        Main.spriteBatch.End();

        var target = MainGraphics.ScreenTarget ?? Main.screenTarget;

        var hour = GameTimeUtils.CalculateCurrentHour();
        var skyColorMult =
            Main.ColorOfTheSkies.ToVector3()
            / new Color(FancySkyColors.Instance.CalculateSkyColor(hour)).ToVector3();
        skyColorMult = Vector3.Clamp(skyColorMult, Vector3.Zero, Vector3.One);

        AtmosphereColorGradientSetBase colorGradientSet = PreferencesConfig
            .Instance
            .FancySkyColorGradientsPreset switch
        {
            SkyColorGradientsPreset.Natural =>
                ModContent.GetInstance<NaturalAtmosphereColorGradientSet>(),
            SkyColorGradientsPreset.Vivid =>
                ModContent.GetInstance<VividAtmosphereColorGradientSet>(),
            _ => ModContent.GetInstance<NaturalAtmosphereColorGradientSet>(),
        };

        var atmosphereColors = colorGradientSet.GetColors(hour);

        ModifyFancyAtmosphereColors?.Invoke(
            ref atmosphereColors.LowColor,
            ref atmosphereColors.MiddleColor,
            ref atmosphereColors.HighColor,
            ref skyColorMult
        );

        atmosphereColors.LowColor *= skyColorMult;
        atmosphereColors.MiddleColor *= skyColorMult;
        atmosphereColors.HighColor *= skyColorMult;
        ColorUtils.GammaToLinear(ref atmosphereColors.LowColor);
        ColorUtils.GammaToLinear(ref atmosphereColors.MiddleColor);
        ColorUtils.GammaToLinear(ref atmosphereColors.HighColor);

        var bgTopY =
            Main.gameMenu || MainGraphics.InCameraMode
                ? sceneArea.bgTopY
                : FancyAtmosphereBgTopY();
        var highLevel = ((float)bgTopY / target.Height) + FadeBegin;
        var lowLevel = highLevel + FadeHeight;

        var midLevel = (highLevel + lowLevel) / 2f;
        lowLevel = midLevel + (FadeHeightMult * (lowLevel - midLevel));
        highLevel = midLevel + (FadeHeightMult * (highLevel - midLevel));

        if (
            !Main.gameMenu
            && !MainGraphics.InCameraMode
            && Main.BackgroundViewMatrix.TransformationMatrix.M22 < 0f
        )
        {
            (atmosphereColors.HighColor, atmosphereColors.LowColor) = (
                atmosphereColors.LowColor,
                atmosphereColors.HighColor
            );
            (highLevel, lowLevel) = (1f - lowLevel, 1f - highLevel);
        }

        var coefficients = atmosphereColors.CalculateCoefficients();

        var effect = doDithering ? _skyDitheredEffect : _skyEffect;
        effect
            .SetParameter("LowSkyLevel", lowLevel)
            .SetParameter("HighSkyLevel", highLevel)
            .SetParameter("AtmosphereColorCoefficients0", coefficients.Coefficients0)
            .SetParameter("AtmosphereColorCoefficients1", coefficients.Coefficients1)
            .SetParameter("AtmosphereColorCoefficients2", coefficients.Coefficients2)
            .SetParameter("InverseGamma", 1f / gamma);
        Blitter.Blit(
            doDithering ? _ditherNoise : null,
            null,
            effect,
            samplerState: SamplerState.PointWrap,
            setTarget: false
        );

        Main.spriteBatch.Begin(sbParams);

        var colorOfTheSkies = Main.ColorOfTheSkies;
        try
        {
            // prevent stars from appearing during sunrise/sunset
            var colorOfTheSkiesVec = colorOfTheSkies.ToVector3();
            colorOfTheSkiesVec *= 2f;
            ColorUtils.Convert(out Main.ColorOfTheSkies, colorOfTheSkiesVec);
            orig(self, sceneArea, artificial);
        }
        finally
        {
            Main.ColorOfTheSkies = colorOfTheSkies;
        }
    }

    internal void DrawSunAndMoon(
        On_Main.orig_DrawSunAndMoon orig,
        Main self,
        Main.SceneArea sceneArea,
        Color moonColor,
        Color sunColor,
        float tempMushroomInfluence,
        PostProcessing postProcessingInstance
    )
    {
        if (_sunEffect is null)
        {
            orig(self, sceneArea, moonColor, sunColor, tempMushroomInfluence);
            return;
        }

        var hiDef =
            LightingConfig.Instance.HiDefFeaturesEnabled() && MainGraphics.DoingCapture;

        var sbParams = Main.spriteBatch.GetParameters();
        Main.spriteBatch.End();

        if (!Main.gameMenu && !MainGraphics.InCameraMode)
        {
            // shift sun/moon downward
            sceneArea.bgTopY += (int)Math.Round(25f * Main.BackgroundViewMatrix.Zoom.Y);
        }

        if (!Main.eclipse)
        {
            ITimedBasedColorGradient colorGradient = PreferencesConfig
                .Instance
                .FancySkyColorGradientsPreset switch
            {
                SkyColorGradientsPreset.Natural =>
                    ModContent.GetInstance<NaturalSunColorGradient>(),
                SkyColorGradientsPreset.Vivid =>
                    ModContent.GetInstance<VividSunColorGradient>(),
                _ => ModContent.GetInstance<NaturalSunColorGradient>(),
            };

            var hour = GameTimeUtils.CalculateCurrentHour();
            var sunColorVec = colorGradient.GetColor(hour);
            ColorUtils.Convert(out sunColor, sunColorVec);
        }

        SpriteBatchEffect effect = null;
        if (Main.dayTime)
        {
            var gamma = PostProcessing.ContentGamma();
            effect = _sunEffect
                .SetParameter("Gamma", gamma)
                .SetParameter("InverseGamma", 1f / gamma);
        }
        else if (hiDef)
        {
            effect = postProcessingInstance.GetBrightenEffect(1.5f);
        }

        SpriteBatchEffectLoader.Apply(effect);
        Main.spriteBatch.Begin(
            sbParams with
            {
                samplerState = SamplerState.LinearClamp,
                customEffect = null,
            }
        );
        orig(self, sceneArea, moonColor, sunColor, tempMushroomInfluence);
        Main.spriteBatch.End();
        SpriteBatchEffectLoader.Reset();

        Main.spriteBatch.Begin(sbParams);
    }

    private static int FancyAtmosphereBgTopY()
    {
        // This code is adapted from vanilla
        var zoom = Main.BackgroundViewMatrix.Zoom.Y;
        return (int)(
            (0f - Main.screenPosition.Y)
            / ((Main.worldSurface * 16.0) - (600.0 * zoom))
            * 200.0
            * zoom
        );
    }
}
