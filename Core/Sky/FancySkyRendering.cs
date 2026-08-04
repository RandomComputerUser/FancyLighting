using FancyLighting.ColorProfiles.SkyColor;
using ReLogic.Content;

namespace FancyLighting.Core.Sky;

public sealed class FancySkyRendering
{
    private readonly Texture2D _ditherNoise;

    private readonly FullscreenEffect _skyEffect;
    private readonly FullscreenEffect _skyDitheredEffect;
    private readonly FancyEffect _sunEffect;

    private const float SkyBrightness = 1.25f;
    private const float SkyBrightnessHiDef = 1.3f;

    private const float FadeBegin = 0.12f;
    private const float FadeHeight = 0.24f;
    private const float FadeHeightMult = 15f / 8; // 3f / 2 for smoothstep

    /// <summary>
    /// Modify the colors of the sky used in Fancy Atmosphere.
    /// </summary>
    /// <param name="highSkyColor">The color of the high part of the sky.</param>
    /// <param name="lowSkyColor">The color of the low part of the sky.</param>
    /// <param name="skyColorMult">A color multiplier applied to the entire sky. Typically, this changes based on the biome.</param>
    public delegate void SkyColorModifier(
        ref Vector3 highSkyColor,
        ref Vector3 lowSkyColor,
        ref Vector3 skyColorMult
    );

    /// <summary>
    /// This event is invoked before the sky is drawn.
    /// </summary>
    /// <remarks>
    /// This event is invoked both while on the main menu and while in a world.
    /// </remarks>
    public static event SkyColorModifier PreDrawSky;

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
        PreDrawSky = null;
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
        var gamma = MainGraphics.DoingCapture
            ? PostProcessing.ContentGamma()
            : PostProcessing.DefaultGamma;

        var samplerState = SpriteBatchAccessors.samplerState(Main.spriteBatch);
        var rasterizerState = SpriteBatchAccessors.rasterizerState(Main.spriteBatch);
        var transformMatrix = SpriteBatchAccessors.transformMatrix(Main.spriteBatch);
        Main.spriteBatch.End();

        var target = MainGraphics.ScreenTarget ?? Main.screenTarget;

        var hour = GameTimeUtils.CalculateCurrentHour();
        var skyColorMult =
            Main.ColorOfTheSkies.ToVector3()
            / new Color(FancySkyColors.Instance.CalculateSkyColor(hour)).ToVector3();
        skyColorMult = Vector3.Clamp(skyColorMult, Vector3.Zero, Vector3.One);
        var skyBrightness = hiDef ? SkyBrightnessHiDef : SkyBrightness;
        skyBrightness = Math.Clamp(
            MathUtils.Lerp(1f, skyBrightness, PreferencesConfig.Instance.SkyBrightness()),
            0f,
            10f
        );

        var highSkyColor = ModContent.GetInstance<SkyColorsHigh>().GetColor(hour);
        var lowSkyColor = ModContent.GetInstance<SkyColorsLow>().GetColor(hour);

        PreDrawSky?.Invoke(ref highSkyColor, ref lowSkyColor, ref skyColorMult);

        highSkyColor *= skyColorMult;
        lowSkyColor *= skyColorMult;
        highSkyColor.X = MathF.Pow(highSkyColor.X, gamma);
        highSkyColor.Y = MathF.Pow(highSkyColor.Y, gamma);
        highSkyColor.Z = MathF.Pow(highSkyColor.Z, gamma);
        lowSkyColor.X = MathF.Pow(lowSkyColor.X, gamma);
        lowSkyColor.Y = MathF.Pow(lowSkyColor.Y, gamma);
        lowSkyColor.Z = MathF.Pow(lowSkyColor.Z, gamma);
        highSkyColor *= skyBrightness;
        lowSkyColor *= skyBrightness;

        var highLevel = (sceneArea.bgTopY + (FadeBegin * target.Width)) / target.Height;
        if (Main.gameMenu)
        {
            highLevel -= 0.04f * target.Width / target.Height;
        }
        var lowLevel = highLevel + (FadeHeight * target.Width / target.Height);

        var midLevel = (highLevel + lowLevel) / 2f;
        highLevel = midLevel + (FadeHeightMult * (highLevel - midLevel));
        lowLevel = midLevel + (FadeHeightMult * (lowLevel - midLevel));

        if (
            !Main.gameMenu
            && !MainGraphics.InCameraMode
            && Main.BackgroundViewMatrix.TransformationMatrix.M22 < 0f
        )
        {
            (highSkyColor, lowSkyColor) = (lowSkyColor, highSkyColor);
            (highLevel, lowLevel) = (1f - lowLevel, 1f - highLevel);
        }

        var effect = doDithering ? _skyDitheredEffect : _skyEffect;
        effect
            .SetParameter("HighSkyLevel", highLevel)
            .SetParameter("LowSkyLevel", lowLevel)
            .SetParameter("HighSkyColor", highSkyColor)
            .SetParameter("LowSkyColor", lowSkyColor)
            .SetParameter("InverseGamma", 1f / gamma);
        Blitter.Blit(_ditherNoise, null, effect, setTarget: false);

        Main.spriteBatch.Begin(
            SpriteSortMode.Deferred,
            BlendState.AlphaBlend,
            samplerState,
            DepthStencilState.None,
            rasterizerState,
            null,
            transformMatrix
        );

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

        var samplerState = SpriteBatchAccessors.samplerState(Main.spriteBatch);
        var rasterizerState = SpriteBatchAccessors.rasterizerState(Main.spriteBatch);
        var transform = SpriteBatchAccessors.transformMatrix(Main.spriteBatch);
        var origTransform = transform;
        Main.spriteBatch.End();

        if (!Main.gameMenu && !MainGraphics.InCameraMode)
        {
            // shift sun/moon downward
            transform.Translation += 25f * transform.Up;
        }

        if (!Main.eclipse)
        {
            var hour = GameTimeUtils.CalculateCurrentHour();
            var sunColorVec = ModContent.GetInstance<SunColors>().GetColor(hour);
            ColorUtils.Convert(out sunColor, sunColorVec);
        }

        Effect effect = null;
        if (Main.dayTime)
        {
            var gamma = MainGraphics.DoingCapture
                ? PostProcessing.ContentGamma()
                : PostProcessing.DefaultGamma;
            effect = _sunEffect
                .SetParameter("Gamma", gamma)
                .SetParameter("InverseGamma", 1f / gamma)
                .Effect;
        }
        else if (hiDef)
        {
            effect = postProcessingInstance.GetBrightenPixelOnlyEffect(1.5f).Effect;
        }

        Main.spriteBatch.Begin(
            SpriteSortMode.Deferred,
            BlendState.AlphaBlend,
            SamplerState.LinearClamp,
            DepthStencilState.None,
            rasterizerState,
            effect,
            transform
        );
        orig(self, sceneArea, moonColor, sunColor, tempMushroomInfluence);
        Main.spriteBatch.End();

        Main.spriteBatch.Begin(
            SpriteSortMode.Deferred,
            BlendState.AlphaBlend,
            samplerState,
            DepthStencilState.None,
            rasterizerState,
            null,
            origTransform
        );
    }
}
